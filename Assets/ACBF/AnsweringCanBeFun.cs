using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class AnsweringCanBeFun : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;

    public KMSelectable[] Keys;
    public KMSelectable PlayButton;
    public TextMesh Screen;

    public Renderer[] LEDs;
    public Material[] LEDColors;

    public Transform SpeakerPos;

    // Logging info
    private static int moduleIdCounter = 1;
    private int moduleId;

    // Solving info
    private long oldNumber;
    private string newNumber;

    private string submitNumber = "";
    private int keysPressed = 0;

    private long oldAreaCode = 888;
    private long newAreaCode = 888;

    private string serialNumber;
    private int firstDigit;
    private int lastDigit;
    private int sumOfDigits;
    private int digitCount;

    private int lastTimerDigit = 0;

    private bool isUnicorn = false;
    private bool isGenerated = false;
    private bool isGoodTime = true;
    private bool inputMode = true;
    private bool canListen = true;
    private bool animating = false;

    private string message;
    private int msgIndex = 0;

    // Aesthetic info
    private static int acbfSolves = 0;
    private static int acbfStrikes = 0;
    private static bool phaseTwo = false;
    private static bool playedDoubleFault = false;

    private int moduleStrikes = 0;

    private ACBFSettings Settings;

    sealed class ACBFSettings {
        public bool disableTaunts = false;
    }

    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;

        // Module Settings
        var modConfig = new ModConfig<ACBFSettings>("Answering Can Be Fun");
        Settings = modConfig.Settings;
        modConfig.Settings = Settings;

        // Delegation
        for (int i = 0; i < Keys.Length; i++) {
            int j = i;
            Keys[i].OnInteract += delegate () {
                PressKey(j);
                return false;
            };
        }

        PlayButton.OnInteract += delegate () { PressListener(); return false; };
    }

    // Gets information
    private void Start() {
        serialNumber = Bomb.GetSerialNumber();

        firstDigit = Bomb.GetSerialNumberNumbers().First();
        lastDigit = Bomb.GetSerialNumberNumbers().Last();
        sumOfDigits = Bomb.GetSerialNumberNumbers().Sum();
        digitCount = Bomb.GetSerialNumberNumbers().Count();

        // Gets unicorn condition - same as Laundry
        if (Bomb.GetBatteryCount() == 4 && Bomb.GetBatteryHolderCount() == 2 && Bomb.IsIndicatorOn("BOB"))
            isUnicorn = true;

        // Gets the area code
        newAreaCode = GetAreaCode();

        // Sets the area code to the number, prepends zeros when needed
        if (newAreaCode.ToString().Length == 1)
            newNumber += "00" + newAreaCode.ToString();

        else if (newAreaCode.ToString().Length == 2)
            newNumber += "0" + newAreaCode.ToString();

        else
            newNumber += newAreaCode.ToString();


        // Generates a decoy area code that doesn't match with the correct one
        do
            oldAreaCode = UnityEngine.Random.Range(124, 1000);
        while (oldAreaCode == newAreaCode);

        oldNumber = oldAreaCode * 10000000 + UnityEngine.Random.Range(0, 10000000);

        // Displays the old number to the screen
        DisplayToScreen(oldNumber.ToString());

        if (phaseTwo == true)
            SetRedLEDs();
    }


    // Listen button is pressed
    private void PressListener() {
        PlayButton.AddInteractionPunch(0.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, gameObject.transform);

        if (canListen == true) {

            if (isGenerated == false)
                GenerateNumber();

            StartCoroutine(PlayMessage());
        }
    }

    // Key is pressed
    private void PressKey(int i) {
        Keys[i].AddInteractionPunch(0.25f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, gameObject.transform);

        if (inputMode == true) {
            // Adds the number to the screen
            submitNumber += i.ToString();
            keysPressed++;
            DisplayToScreen(submitNumber);

            // Ten digits have been entered
            if (keysPressed >= 10) {
                // Checks timer rule for last digit
                lastTimerDigit = (int)Math.Floor(Bomb.GetTime()) % 10;

                if (lastTimerDigit == i)
                    isGoodTime = true;

                else
                    isGoodTime = false;

                inputMode = false;
                keysPressed = 0;

                if (isGenerated == false)
                    GenerateNumber();

                StartCoroutine(CallNumber());
            }
        }
    }


    // Module submitting
    private IEnumerator CallNumber() {
        animating = true;
        Debug.LogFormat("[Answering Can Be Fun #{0}] You dialed {1}.", moduleId, submitNumber);
        yield return new WaitForSeconds(0.5f);
        Audio.PlaySoundAtTransform("ACBF_Ring", SpeakerPos);
        yield return new WaitForSeconds(5.0f);

        // If the submitted number is the same as the old number
        if (submitNumber == oldNumber.ToString()) {
            Audio.PlaySoundAtTransform("ACBF_Str_Oldnumber", SpeakerPos);
            yield return new WaitForSeconds(7.0f);
            Debug.LogFormat("[Answering Can Be Fun #{0}] Strike! You dialed the old number!", moduleId);
            StartCoroutine(Strike(false));
        }

        // If the unicorn applies and the number is correct
        else if (isUnicorn == true && submitNumber == "1234567890")
            StartCoroutine(Solve());

        // If the first nine digits are correct but the time is wrong
        else if ((submitNumber.Substring(0, 9) == newNumber.Substring(0, 9)) && isGoodTime == false) {
            Audio.PlaySoundAtTransform("ACBF_Str_Badtime", SpeakerPos);
            yield return new WaitForSeconds(5.0f);
            Debug.LogFormat("[Answering Can Be Fun #{0}] Strike! The first nine digits were correct, but you failed to dial the last digit at the right time!", moduleId);
            StartCoroutine(Strike(false));
        }

        // If the number is correct
        else if ((submitNumber.Substring(0, 9) == newNumber.Substring(0, 9)) && isGoodTime == true)
            StartCoroutine(Solve());

        // If the number is flat-out wrong
        else {
            Debug.LogFormat("[Answering Can Be Fun #{0}] Strike! That's the wrong number!", moduleId);
            StartCoroutine(Strike(true));
        }

        inputMode = true;
        submitNumber = "";
        animating = false;
    }

    // Module strikes
    private IEnumerator Strike(bool playSound) {
        GetComponent<KMBombModule>().HandleStrike();
        acbfStrikes++;
        moduleStrikes++;
        yield return new WaitForSeconds(0.5f);

        int index;

        if (playSound == true) {
            if (phaseTwo == false)
                index = UnityEngine.Random.Range(0, 6);

            else
                index = UnityEngine.Random.Range(0, 8);

            switch (index) {
            case 1: Audio.PlaySoundAtTransform("ACBF_Str_Norm2", SpeakerPos); break;
            case 2: Audio.PlaySoundAtTransform("ACBF_Str_Norm3", SpeakerPos); break;
            case 3: Audio.PlaySoundAtTransform("ACBF_Str_Norm4", SpeakerPos); break;
            case 4: Audio.PlaySoundAtTransform("ACBF_Str_Norm6", SpeakerPos); break;
            case 5: Audio.PlaySoundAtTransform("ACBF_Str_Norm7", SpeakerPos); break;
            case 6: Audio.PlaySoundAtTransform("ACBF_Str_Norm1", SpeakerPos); break;
            case 7: Audio.PlaySoundAtTransform("ACBF_Str_Norm5", SpeakerPos); break;
            default: Audio.PlaySoundAtTransform("ACBF_Str_Norm8", SpeakerPos); break;
            }
        }
    }

    // Module solves
    private IEnumerator Solve() {
        GetComponent<KMBombModule>().HandlePass();
        acbfSolves++;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, gameObject.transform);
        yield return new WaitForSeconds(0.5f);

        /* This if statement is supposed to work when this the second Answering Can Be Fun module that solved that a strike also occured on, 
         * from the last time the game has been restarted, and then never to activate until a restart and that condition is met again.*/

        // If this is the second Answering Can Be Fun module solved with a strike on it (currently bugged)
        /*if (moduleStrikes > 0 && acbfSolves > 1 && moduleStrikes != acbfStrikes && playedDoubleFault == false && Settings.disableTaunts == false) {
            Debug.LogFormat("[Answering Can Be Fun #{0}] Solve! Your friend knew that you struck on two of these modules now, though.", moduleId);
            Audio.PlaySoundAtTransform("ACBF_Sol_Doublefault", SpeakerPos);
            playedDoubleFault = true;
        }*/

        // If there was a strike on this module
        if (moduleStrikes > 0 && Settings.disableTaunts == false) {
            Debug.LogFormat("[Answering Can Be Fun #{0}] Solve! Your friend knew that you struck, though.", moduleId);
            int index;

            if (playedDoubleFault == true)
                index = UnityEngine.Random.Range(0, 3);

            else
                index = UnityEngine.Random.Range(0, 2);

            if (index == 0)
                Audio.PlaySoundAtTransform("ACBF_Sol_Fault1", SpeakerPos);

            else if (index == 1)
                Audio.PlaySoundAtTransform("ACBF_Sol_Fault2", SpeakerPos);

            else
                Audio.PlaySoundAtTransform("ACBF_Sol_Doublefault", SpeakerPos);
        }

        // If there were no strikes on the module
        else {
            Debug.LogFormat("[Answering Can Be Fun #{0}] Solve! Good job keeping contact with your friend!", moduleId);
            int index;

            if (phaseTwo == false)
                index = UnityEngine.Random.Range(0, 2);

            else
                index = UnityEngine.Random.Range(0, 3);

            if (index == 0)
                Audio.PlaySoundAtTransform("ACBF_Sol_Good3", SpeakerPos);

            else if (index == 1)
                Audio.PlaySoundAtTransform("ACBF_Sol_Good2", SpeakerPos);

            else
                Audio.PlaySoundAtTransform("ACBF_Sol_Good1", SpeakerPos);
        }

        phaseTwo = true;
    }

    // Generates the correct area code
    private long GetAreaCode() {
        long code = 0;
        int middleDigit = sumOfDigits % 10;

        code += firstDigit * 100 + middleDigit * 10 + lastDigit;
        return code;
    }


    // Displays a number to the screen and formats it
    private void DisplayToScreen(string str) {
        if (str.Length <= 3)
            Screen.text = str;

        else if (str.Length <= 6) {
            string newStr = str.Substring(0, 3) + " " + str.Substring(3, str.Length - 3);

            Screen.text = newStr;
        }

        else {
            string newStr = str.Substring(0, 3) + " " + str.Substring(3, 3) + " " + str.Substring(6, str.Length - 6);

            Screen.text = newStr;
        }
    }


    // Generates the number
    private void GenerateNumber() {
        ChooseMessage();

        if (phaseTwo == true)
            SetRedLEDs();

        if (isUnicorn == true) {
            newNumber = "1234567890";
            Debug.LogFormat("[Answering Can Be Fun #{0}] The number to dial is {1}. You got lucky.", moduleId, newNumber);
        }

        else {
            for (int i = 0; i < serialNumber.Length; i++) {
                if (Char.IsNumber(serialNumber, i) == true)
                    newNumber += serialNumber[i] * digitCount % 10; // serialNumber[i] results in a number 2 lower than what's displayed on the serial number, when used within this context

                else
                    newNumber += (message.Count(x => x == serialNumber[i]) + 3 + i) % 10;
            }

            Debug.LogFormat("[Answering Can Be Fun #{0}] The number to dial is {1}#, where # is the last number based on the timer.", moduleId, newNumber);
        }

        isGenerated = true;
    }


    // Chooses the message
    private void ChooseMessage() {
        int[] stageOneMessages = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 27, 28, 29, 31, 35, 36, 39 };

        if (phaseTwo == false)
            msgIndex = stageOneMessages[UnityEngine.Random.Range(0, stageOneMessages.Length)];

        else
            msgIndex = UnityEngine.Random.Range(1, 40);

        if (isUnicorn == true)
            message = "THEREAREFOURBATTERIESINTWOHOLDERSANDALITBOBINDICATORPLEASEDIALONETWOTHREEFOURFIVESIXSEVENEIGHTNINEZERO";

        else {
            switch (msgIndex) {
            case 1: message = "EVERYBODYSTHESAMECOLORWITHTHELIGHTSOFF"; break;
            case 2: message = "TOCONTINUEWITHTHISMODULEPLEASEPRESSONE"; break;
            case 3: message = "THISVIDEOISNTCOOLYOUKNOWWHATSCOOLABILLIONDOLLARS"; break;
            case 4: message = "ANDNOWFORSOMETHINGCOMPLETELYDIFFERENT"; break;
            case 5: message = "ITSFREEREALESTATE"; break;
            case 6: message = "ARENTYOUTHATYOUTUBEGUITARIST"; break;
            case 7: message = "IMGOINGTOJAIL"; break;
            case 8: message = "WHATAREYOUWEARINGJAKEFROMSTATEFARM"; break;
            case 9: message = "EVERYSIXTYSECONDSINAFRICAAMINUTEPASSES"; break;
            case 10: message = "GOODBYEAMERICAHELLONEWYORK"; break;
            case 11: message = "YOUGETTOUNDERSTANDTHATIAMANUMBER"; break;
            case 12: message = "REQUESTINGIMMEDIATEASSISTANCEOVERANDOUT"; break;
            case 13: message = "YOUVEJUSTBEENPWNED"; break;
            case 14: message = "THISISAROBBERY"; break;
            case 15: message = "WHATDOYOUMEANYOUREATSOUP"; break;
            case 16: message = "IAMGOINGTOBESWITZERLAND"; break;
            case 17: message = "IKNOWWHATYOURETHINKING"; break;
            case 18: message = "CONGRATULATIONSYOUVELAUNCHEDTHEPINKSLIPVIRUS"; break;
            case 19: message = "WELLLETSSEENOWWEHAVEONOURTEAMWEHAVEWHOSONFIRST"; break;
            case 20: message = "SHENEVEREVENKNEWHERSHIRTHADAWHOLELOTOFSTYLE"; break;
            case 21: message = "OHYOUALMOSTHADITYOUGOTTOBEQUICKERTHANTHAT"; break;
            case 22: message = "AMNESIADUSTYOUTHROWAPINCHTHEKIDFORGETSEVERYTHINGTHATHAPPENEDFORTHELASTFEWSECONDS"; break;
            case 23: message = "IAUTOTUNEDTHEHOTENDSUSINGTHEBUILTINAUTOTUNER"; break;
            case 24: message = "CANISLEEPINYOURBEDTONIGHT"; break;
            case 25: message = "NINETYNINEBOTTLESOFBEERONTHEWALLHOWMANYTIMESDOESSIXGOINTONINETYNINEANDIDONTEVENDRINKBEER"; break;
            case 26: message = "FUDGEFACTORYATONEHUNDREDPERCENTAWESOMENESSPOWERINGUPTHEFISHINGRODEPICBASSDROPACTIVATE"; break;
            case 27: message = "SOMEBODYMUGSYOUWITHAGUNJUSTEATTHEGUN"; break;
            case 28: message = "LIFEGIVESYOULEMONSILLMAKEBEEFSTEW"; break;
            case 29: message = "ITSHIGHNOON"; break;
            case 30: message = "NOPEITSJUSTCHUCKTESTA"; break;
            case 31: message = "PSYCHTHATSTHEWRONGNUMBER"; break;
            case 32: message = "WHATAREYOUSINKINGABOUT"; break;
            case 33: message = "WHETHERYOUREYOUNGWHETHERYOUREOLDWHETHERYOUREREALLYOLDWHETHERYOUREGREATWHETHERYOUSUCKWHETHERYOUREALLYSUCK"; break;
            case 34: message = "YEET"; break;
            case 35: message = "HOWDIDTHISHAPPEN"; break;
            case 36: message = "WHENSTHEFOURTHOFJULY"; break;
            case 37: message = "GOTAQUESTIONFORYOUWHATSHEAVIERAKILOGRAMOFSTEELORAKILOGRAMOFFEATHERS"; break;
            case 38: message = "IFEELTHATIGETMORENERVOUSONSTAGESTHATDONTHAVEPOLESONTHEM"; break;
            case 39: message = "SOISITANYWONDERPEOPLEAREAFRAIDOFTECHNOLOGY"; break;
            default: message = "TESTTESTONETWOTHREE"; break;
            }
        }

        Debug.LogFormat("[Answering Can Be Fun #{0}] The message string is \"{1}\"", moduleId, message);
    }

    // Plays the message
    private IEnumerator PlayMessage() {
        canListen = false;
        yield return new WaitForSeconds(0.5f);

        if (isUnicorn == true)
            Audio.PlaySoundAtTransform("ACBF_Msg_Unicorn", SpeakerPos);

        else {
            switch (msgIndex) {
            case 1: Audio.PlaySoundAtTransform("ACBF_Msg_Color", SpeakerPos); break;
            case 2: Audio.PlaySoundAtTransform("ACBF_Msg_Continue", SpeakerPos); break;
            case 3: Audio.PlaySoundAtTransform("ACBF_Msg_Cool", SpeakerPos); break;
            case 4: Audio.PlaySoundAtTransform("ACBF_Msg_Different", SpeakerPos); break;
            case 5: Audio.PlaySoundAtTransform("ACBF_Msg_Estate", SpeakerPos); break;
            case 6: Audio.PlaySoundAtTransform("ACBF_Msg_Guitar", SpeakerPos); break;
            case 7: Audio.PlaySoundAtTransform("ACBF_Msg_Jail", SpeakerPos); break;
            case 8: Audio.PlaySoundAtTransform("ACBF_Msg_Jake", SpeakerPos); break;
            case 9: Audio.PlaySoundAtTransform("ACBF_Msg_Minute", SpeakerPos); break;
            case 10: Audio.PlaySoundAtTransform("ACBF_Msg_Newyork", SpeakerPos); break;
            case 11: Audio.PlaySoundAtTransform("ACBF_Msg_Number", SpeakerPos); break;
            case 12: Audio.PlaySoundAtTransform("ACBF_Msg_Out", SpeakerPos); break;
            case 13: Audio.PlaySoundAtTransform("ACBF_Msg_Pwned", SpeakerPos); break;
            case 14: Audio.PlaySoundAtTransform("ACBF_Msg_Robbery", SpeakerPos); break;
            case 15: Audio.PlaySoundAtTransform("ACBF_Msg_Soup", SpeakerPos); break;
            case 16: Audio.PlaySoundAtTransform("ACBF_Msg_Switzerland", SpeakerPos); break;
            case 17: Audio.PlaySoundAtTransform("ACBF_Msg_Thinking", SpeakerPos); break;
            case 18: Audio.PlaySoundAtTransform("ACBF_Msg_Virus", SpeakerPos); break;
            case 19: Audio.PlaySoundAtTransform("ACBF_Msg_Who", SpeakerPos); break;
            case 20: Audio.PlaySoundAtTransform("ACBF_Msg_Whole", SpeakerPos); break;
            case 21: Audio.PlaySoundAtTransform("ACBF_Msg_Almost", SpeakerPos); break;
            case 22: Audio.PlaySoundAtTransform("ACBF_Msg_Amnesia", SpeakerPos); break;
            case 23: Audio.PlaySoundAtTransform("ACBF_Msg_Autotune", SpeakerPos); break;
            case 24: Audio.PlaySoundAtTransform("ACBF_Msg_Bed", SpeakerPos); break;
            case 25: Audio.PlaySoundAtTransform("ACBF_Msg_Beer", SpeakerPos); break;
            case 26: Audio.PlaySoundAtTransform("ACBF_Msg_Fudge", SpeakerPos); break;
            case 27: Audio.PlaySoundAtTransform("ACBF_Msg_Gun", SpeakerPos); break;
            case 28: Audio.PlaySoundAtTransform("ACBF_Msg_Lemons", SpeakerPos); break;
            case 29: Audio.PlaySoundAtTransform("ACBF_Msg_Noon", SpeakerPos); break;
            case 30: Audio.PlaySoundAtTransform("ACBF_Msg_Nope", SpeakerPos); break;
            case 31: Audio.PlaySoundAtTransform("ACBF_Msg_Psych", SpeakerPos); break;
            case 32: Audio.PlaySoundAtTransform("ACBF_Msg_Sinking", SpeakerPos); break;
            case 33: Audio.PlaySoundAtTransform("ACBF_Msg_Whether", SpeakerPos); break;
            case 34: Audio.PlaySoundAtTransform("ACBF_Msg_Yeet", SpeakerPos); break;
            case 35: Audio.PlaySoundAtTransform("ACBF_Msg_How", SpeakerPos); break;
            case 36: Audio.PlaySoundAtTransform("ACBF_Msg_Fourth", SpeakerPos); break;
            case 37: Audio.PlaySoundAtTransform("ACBF_Msg_Kilogram", SpeakerPos); break;
            case 38: Audio.PlaySoundAtTransform("ACBF_Msg_Poles", SpeakerPos); break;
            case 39: Audio.PlaySoundAtTransform("ACBF_Msg_Technology", SpeakerPos); break;
            default: Audio.PlaySoundAtTransform("ACBF_Msg_Test", SpeakerPos); break;
            }
        }

        yield return new WaitForSeconds(10.0f);

        canListen = true;
    }


    // Makes the LEDs red for Phase 2
    public void SetRedLEDs() {
        for (int i = 0; i < LEDs.Length; i++)
            LEDs[i].material = LEDColors[1];
    }


    // Twitch Plays support - made by eXish


    //twitch plays
    private bool paramsValid(string prms) {
        char[] valids = { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' };
        if (prms.Length == 10) {
            for (int i = 0; i < 10; i++) {
                if (!valids.Contains(prms.ElementAt(i))) {
                    return false;
                }
            }
        }
        else if (prms.Length == 12) {
            for (int i = 0; i < 12; i++) {
                if (i == 3 || i == 7) {
                    if (!prms.ElementAt(i).Equals('-')) {
                        return false;
                    }
                }
                else if (!valids.Contains(prms.ElementAt(i))) {
                    return false;
                }
            }
        }
        else {
            return false;
        }
        return true;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} listen [Listens to your friend's last recorded message] | !{0} dial <phone#> [Dials the specified phone number] | Valid phone numbers have 10 digits | The 10th digit will automatically be submitted at the correct time";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command) {
        if (Regex.IsMatch(command, @"^\s*listen\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
            yield return null;
            if (!inputMode) {
                yield return "sendtochaterror The recorded message cannot be played while one is being processed!";
            }
            else if (canListen) {
                PlayButton.OnInteract();
            }
            else {
                yield return "sendtochaterror The recorded message cannot be played since the 10 second cooldown is still in effect!";
            }
            yield break;
        }
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*dial\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || Regex.IsMatch(parameters[0], @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
            if (parameters.Length == 1) {
                yield return "sendtochaterror Please specify which phone number you would like to dial!";
            }
            else if (parameters.Length == 2) {
                if (!inputMode) {
                    yield return "sendtochaterror A phone number cannot be dialed while one is being processed!";
                }
                else if (paramsValid(parameters[1])) {
                    yield return null;
                    if (parameters[1].Length == 12) {
                        parameters[1] = parameters[1].Replace("-", "");
                    }
                    for (int i = 0; i < 10; i++) {
                        if (i == 9) {
                            int temp = 0;
                            int.TryParse(parameters[1].Substring(9), out temp);
                            while ((int)Bomb.GetTime() % 60 % 10 != temp) {
                                yield return new WaitForSeconds(0.1f);
                            }
                        }
                        if (parameters[1].ElementAt(i).Equals('0')) {
                            Keys[0].OnInteract();
                        }
                        else if (parameters[1].ElementAt(i).Equals('1')) {
                            Keys[1].OnInteract();
                        }
                        else if (parameters[1].ElementAt(i).Equals('2')) {
                            Keys[2].OnInteract();
                        }
                        else if (parameters[1].ElementAt(i).Equals('3')) {
                            Keys[3].OnInteract();
                        }
                        else if (parameters[1].ElementAt(i).Equals('4')) {
                            Keys[4].OnInteract();
                        }
                        else if (parameters[1].ElementAt(i).Equals('5')) {
                            Keys[5].OnInteract();
                        }
                        else if (parameters[1].ElementAt(i).Equals('6')) {
                            Keys[6].OnInteract();
                        }
                        else if (parameters[1].ElementAt(i).Equals('7')) {
                            Keys[7].OnInteract();
                        }
                        else if (parameters[1].ElementAt(i).Equals('8')) {
                            Keys[8].OnInteract();
                        }
                        else if (parameters[1].ElementAt(i).Equals('9')) {
                            Keys[9].OnInteract();
                        }
                        yield return new WaitForSeconds(0.1f);
                    }
                    if ((isUnicorn == true && submitNumber == "1234567890") || ((submitNumber.Substring(0, 9) == newNumber.Substring(0, 9)) && isGoodTime == true))
                    {
                        yield return "solve";
                    }
                    else
                    {
                        yield return "strike";
                    }
                }
                else {
                    yield return "sendtochaterror '" + parameters[1] + "' is an invalid phone number! Make sure to use digits 0-9 and if you tried to use -'s make sure they are in the right place!";
                }
            }
            else if (parameters.Length > 2) {
                yield return "sendtochaterror Phone numbers do not have spaces in them!";
            }
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve() {
        if (isGenerated == false)
            GenerateNumber();

        keysPressed = 0;
        submitNumber = "";
        
        for (int i = 0; i < 10; i++) {
            if (i == 9) {
                if ((int)Bomb.GetTime() % 60 % 10 == 0) {
                    Keys[0].OnInteract();
                }
                else if ((int)Bomb.GetTime() % 60 % 10 == 1) {
                    Keys[1].OnInteract();
                }
                else if ((int)Bomb.GetTime() % 60 % 10 == 2) {
                    Keys[2].OnInteract();
                }
                else if ((int)Bomb.GetTime() % 60 % 10 == 3) {
                    Keys[3].OnInteract();
                }
                else if ((int)Bomb.GetTime() % 60 % 10 == 4) {
                    Keys[4].OnInteract();
                }
                else if ((int)Bomb.GetTime() % 60 % 10 == 5) {
                    Keys[5].OnInteract();
                }
                else if ((int)Bomb.GetTime() % 60 % 10 == 6) {
                    Keys[6].OnInteract();
                }
                else if ((int)Bomb.GetTime() % 60 % 10 == 7) {
                    Keys[7].OnInteract();
                }
                else if ((int)Bomb.GetTime() % 60 % 10 == 8) {
                    Keys[8].OnInteract();
                }
                else if ((int)Bomb.GetTime() % 60 % 10 == 9) {
                    Keys[9].OnInteract();
                }
                break;
            }
            if (newNumber.ElementAt(i).Equals('0')) {
                Keys[0].OnInteract();
            }
            else if (newNumber.ElementAt(i).Equals('1')) {
                Keys[1].OnInteract();
            }
            else if (newNumber.ElementAt(i).Equals('2')) {
                Keys[2].OnInteract();
            }
            else if (newNumber.ElementAt(i).Equals('3')) {
                Keys[3].OnInteract();
            }
            else if (newNumber.ElementAt(i).Equals('4')) {
                Keys[4].OnInteract();
            }
            else if (newNumber.ElementAt(i).Equals('5')) {
                Keys[5].OnInteract();
            }
            else if (newNumber.ElementAt(i).Equals('6')) {
                Keys[6].OnInteract();
            }
            else if (newNumber.ElementAt(i).Equals('7')) {
                Keys[7].OnInteract();
            }
            else if (newNumber.ElementAt(i).Equals('8')) {
                Keys[8].OnInteract();
            }
            else if (newNumber.ElementAt(i).Equals('9')) {
                Keys[9].OnInteract();
            }
            yield return new WaitForSeconds(0.1f);
        }
        while (animating) { yield return true; }
    }
}