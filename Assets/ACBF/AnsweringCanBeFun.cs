using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    private string message;
    private int msgIndex = 0;

    // Aesthetic info
    private static int acbfSolves = 0;
    private static int acbfStrikes = 0;
    private static bool phaseTwo = false;
    private static bool playedDoubleFault = false;

    private int moduleStrikes = 0;

    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;

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

        // Generates the area code (first 3 numbers)
        if (isUnicorn == true)
            newAreaCode = 123;

        else
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
            oldAreaCode = UnityEngine.Random.Range(100, 1000);
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

            if (isGenerated == false) {
                GenerateNumber();

                if (phaseTwo == true)
                    SetRedLEDs();
            }

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
                lastTimerDigit = (int) Math.Floor(Bomb.GetTime()) % 10;

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
        Debug.LogFormat("[Answering Can Be Fun #{0}] You dialed {1}.", moduleId, submitNumber);
        yield return new WaitForSeconds(0.5f);
        Audio.PlaySoundAtTransform("ACBF_Ring", transform);
        yield return new WaitForSeconds(5.0f);

        // If the submitted number is the same as the old number
        if (submitNumber == oldNumber.ToString()) {
            Audio.PlaySoundAtTransform("ACBF_Str_Oldnumber", transform);
            yield return new WaitForSeconds(7.0f);
            Debug.LogFormat("[Answering Can Be Fun #{0}] Strike! You dialed the old number!", moduleId);
            StartCoroutine(Strike(false));
        }

        // If the unicorn applies and the number is incorrect
        else if (isUnicorn == true && submitNumber != "1234567890") {
            Debug.LogFormat("[Answering Can Be Fun #{0}] Strike! For this case, you actually disobey the manual and actually dial 1234567890.", moduleId);
            StartCoroutine(Strike(true));
        }

        // If the unicorn applies and the number is correct
        else if (isUnicorn == true && submitNumber == "1234567890")
            StartCoroutine(Solve());

        // If the first nine digits are correct but the time is wrong
        else if ((submitNumber.Substring(0, 9) == newNumber.Substring(0, 9)) && isGoodTime == false) {
            Audio.PlaySoundAtTransform("ACBF_Str_Badtime", transform);
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
            case 1: Audio.PlaySoundAtTransform("ACBF_Str_Norm2", transform); break;
            case 2: Audio.PlaySoundAtTransform("ACBF_Str_Norm3", transform); break;
            case 3: Audio.PlaySoundAtTransform("ACBF_Str_Norm4", transform); break;
            case 4: Audio.PlaySoundAtTransform("ACBF_Str_Norm6", transform); break;
            case 5: Audio.PlaySoundAtTransform("ACBF_Str_Norm7", transform); break;
            case 6: Audio.PlaySoundAtTransform("ACBF_Str_Norm1", transform); break;
            case 7: Audio.PlaySoundAtTransform("ACBF_Str_Norm5", transform); break;
            default: Audio.PlaySoundAtTransform("ACBF_Str_Norm8", transform); break;
            }
        }
    }

    // Module solves
    private IEnumerator Solve() {
        GetComponent<KMBombModule>().HandlePass();
        acbfSolves++;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, gameObject.transform);
        yield return new WaitForSeconds(0.5f);

        // If this is the second Answering Can Be Fun module solved with a strike on it
        if (moduleStrikes > 0 && acbfSolves > 1 && moduleStrikes != acbfStrikes && playedDoubleFault == false && phaseTwo == false) {
            Debug.LogFormat("[Answering Can Be Fun #{0}] Solve! Your friend knew that you struck on two of these modules now, though.", moduleId);
            Audio.PlaySoundAtTransform("ACBF_Sol_Doublefault", transform);
            playedDoubleFault = true;
        }

        // If there was a strike on this module
        else if (moduleStrikes > 0) {
            Debug.LogFormat("[Answering Can Be Fun #{0}] Solve! Your friend knew that you struck, though.", moduleId);
            int index;

            if (playedDoubleFault == true)
                index = UnityEngine.Random.Range(0, 3);

            else
                index = UnityEngine.Random.Range(0, 2);

            if (index == 0)
                Audio.PlaySoundAtTransform("ACBF_Sol_Fault1", transform);

            else if (index == 0)
                Audio.PlaySoundAtTransform("ACBF_Sol_Fault2", transform);

            else
                Audio.PlaySoundAtTransform("ACBF_Sol_Doublefault", transform);
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
                Audio.PlaySoundAtTransform("ACBF_Sol_Good3", transform);

            else if (index == 1)
                Audio.PlaySoundAtTransform("ACBF_Sol_Good2", transform);

            else
                Audio.PlaySoundAtTransform("ACBF_Sol_Good1", transform);
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

        if (isUnicorn == true) {
            newNumber = "1234567890";
            Debug.LogFormat("[Answering Can Be Fun #{0}] The number to dial is {1}. You got lucky.", moduleId, newNumber);
        }

        else {
            for (int i = 0; i < serialNumber.Length; i++) {
                if (Char.IsNumber(serialNumber, i) == true)
                    newNumber += serialNumber[i] * digitCount % 10; // serialNumber[i]'s value is 2 lower than it should be, I incorporated that bug into the manual
                
                else
                    newNumber += (message.Count(x => x == serialNumber[i]) + 3 + i) % 10;
            }

            Debug.LogFormat("[Answering Can Be Fun #{0}] The number to dial is {1}#, where # is the last number based on the timer.", moduleId, newNumber);
        }

        isGenerated = true;
    }


    // Chooses the message
    private void ChooseMessage() {
        if (phaseTwo == false)
            msgIndex = UnityEngine.Random.Range(1, 22);

        else
            msgIndex = UnityEngine.Random.Range(1, 43);

        if (isUnicorn == true)
            msgIndex = 43;

        switch (msgIndex) {
        case 1: message = "EVERYBODYSTHESAMECOLORWITHTHELIGHTSOFF"; break;
        case 2: message = "TOCONTINUEWITHTHISMODULEPLEASEPRESSONE"; break;
        case 3: message = "THISVIDEOISNTCOOLYOUKNOWWHATSCOOLAMILLIONDOLLARS"; break;
        case 4: message = "ANDNOWFORSOMETHINGCOMPLETELYDIFFERENT"; break;
        case 5: message = "ITSFREEREALESTATE"; break;
        case 6: message = "ARENTYOUTHATYOUTUBEGUITARIST"; break;
        case 7: message = "INTERIORCROCODILEALLIGATORIDRIVEACHEVROLETMOVIETHEATER"; break;
        case 8: message = "IMGOINGTOJAIL"; break;
        case 9: message = "WHATAREYOUWEARINGJAKEFROMSTATEFARM"; break;
        case 10: message = "EVERYSIXTYSECONDSINAFRICAAMINUTEPASSES"; break;
        case 11: message = "GOODBYEAMERICAHELLONEWYORK"; break;
        case 12: message = "YOUGETTOUNDERSTANDTHATIAMANUMBER"; break;
        case 13: message = "REQUESTINGIMMEDIATEASSISTANCEOVERANDOUT"; break;
        case 14: message = "YOUVEJUSTBEENPWNED"; break;
        case 15: message = "THISISAROBBERY"; break;
        case 16: message = "WHATDOYOUMEANYOUREATSOUP"; break;
        case 17: message = "IAMGOINGTOBESWITZERLAND"; break;
        case 18: message = "IKNOWWHATYOURETHINKING"; break;
        case 19: message = "CONGRATULATIONSYOUVELAUNCHEDTHEPINKSLIPVIRUS"; break;
        case 20: message = "BUTLETSSEENOWWEHAVEONOURTEAMWEHAVEWHOSONFIRST"; break;
        case 21: message = "SHENEVEREVENKNEWHERSHIRTHADAWHOLELOTOFSTYLE"; break;
        case 22: message = "OHYOUALMOSTHADITYOUGOTTOBEQUICKERTHANTHAT"; break;
        case 23: message = "AMNESIADUSTYOUTHROWAPINCHTHEKIDFORGETSEVERYTHINGTHATHAPPENEDFORTHELASTFEWSECONDS"; break;
        case 24: message = "IAUTOTUNEDTHEHOTENDSUSINGTHEBUILTINAUTOTUNER"; break;
        case 25: message = "BABYBABYBABYOH"; break;
        case 26: message = "RINGRINGRINGRINGRINGRINGRINGBANANAPHONE"; break;
        case 27: message = "CANISLEEPINYOURBEDTONIGHT"; break;
        case 28: message = "NINETYNINEBOTTLESOFBEERONTHEWALLHOWMANYTIMESDOESSIXGOINTONINETYNINEANDIDONTEVENDRINKBEER"; break;
        case 29: message = "GIVEMEBACKTHATFILLETOFISHGIVEMETHATFISH"; break;
        case 30: message = "FUDGEFACTORYATONEHUNDREDPERCENTAWESOMENESSPOWERINGUPTHEFISHINGRODEPICBASSDROPACTIVATE"; break;
        case 31: message = "SOMEBODYMUGSYOUWITHAGUNJUSTEATTHEGUN"; break;
        case 32: message = "HELLOISITMEYOURELOOKINGFOR"; break;
        case 33: message = "LIFEGIVESYOULEMONSILLMAKEBEEFSTEW"; break;
        case 34: message = "LETMESEEYOUMOVESOMETHING"; break;
        case 35: message = "IEATTOOUCHIDRINKTOOMUCHIWANTTOOMUCH"; break;
        case 36: message = "ITSHIGHNOON"; break;
        case 37: message = "NOPEITSJUSTCHUCKTESTA"; break;
        case 38: message = "PSYCHTHATSTHEWRONGNUMBER"; break;
        case 39: message = "WHATAREYOUSINKINGABOUT"; break;
        case 40: message = "IJUSTWASTEDTENSECONDSOFYOURLIFE"; break;
        case 41: message = "WHETHERYOUREYOUNGWHETHERYOUREOLDWHETHERYOUREREALLYOLDWHETHERYOUREGREATWHETHERYOUSUCKWHETHERYOUREALLYSUCK"; break;
        case 42: message = "YEET"; break;
        case 43: message = "THEREAREFOURBATTERIESINTWOHOLDERSANDALITBOBINDICATORPLEASEDIALONETWOTHREEFOURFIVESIXSEVENEIGHTNINETEN"; break;
        default: message = "TESTTESTONETWOTHREE"; break;
        }

        Debug.LogFormat("[Answering Can Be Fun #{0}] The message string is \"{1}\"", moduleId, message);
    }

    // Plays the message
    private IEnumerator PlayMessage() {
        canListen = false;
        yield return new WaitForSeconds(0.5f);

        switch (msgIndex) {
        case 1: Audio.PlaySoundAtTransform("ACBF_Msg_Color", transform); break;
        case 2: Audio.PlaySoundAtTransform("ACBF_Msg_Continue", transform); break;
        case 3: Audio.PlaySoundAtTransform("ACBF_Msg_Cool", transform); break;
        case 4: Audio.PlaySoundAtTransform("ACBF_Msg_Different", transform); break;
        case 5: Audio.PlaySoundAtTransform("ACBF_Msg_Estate", transform); break;
        case 6: Audio.PlaySoundAtTransform("ACBF_Msg_Guitar", transform); break;
        case 7: Audio.PlaySoundAtTransform("ACBF_Msg_Interior", transform); break;
        case 8: Audio.PlaySoundAtTransform("ACBF_Msg_Jail", transform); break;
        case 9: Audio.PlaySoundAtTransform("ACBF_Msg_Jake", transform); break;
        case 10: Audio.PlaySoundAtTransform("ACBF_Msg_Minute", transform); break;
        case 11: Audio.PlaySoundAtTransform("ACBF_Msg_Newyork", transform); break;
        case 12: Audio.PlaySoundAtTransform("ACBF_Msg_Number", transform); break;
        case 13: Audio.PlaySoundAtTransform("ACBF_Msg_Out", transform); break;
        case 14: Audio.PlaySoundAtTransform("ACBF_Msg_Pwned", transform); break;
        case 15: Audio.PlaySoundAtTransform("ACBF_Msg_Robbery", transform); break;
        case 16: Audio.PlaySoundAtTransform("ACBF_Msg_Soup", transform); break;
        case 17: Audio.PlaySoundAtTransform("ACBF_Msg_Switzerland", transform); break;
        case 18: Audio.PlaySoundAtTransform("ACBF_Msg_Thinking", transform); break;
        case 19: Audio.PlaySoundAtTransform("ACBF_Msg_Virus", transform); break;
        case 20: Audio.PlaySoundAtTransform("ACBF_Msg_Who", transform); break;
        case 21: Audio.PlaySoundAtTransform("ACBF_Msg_Whole", transform); break;
        case 22: Audio.PlaySoundAtTransform("ACBF_Msg_Almost", transform); break;
        case 23: Audio.PlaySoundAtTransform("ACBF_Msg_Amnesia", transform); break;
        case 24: Audio.PlaySoundAtTransform("ACBF_Msg_Autotune", transform); break;
        case 25: Audio.PlaySoundAtTransform("ACBF_Msg_Baby", transform); break;
        case 26: Audio.PlaySoundAtTransform("ACBF_Msg_Banana", transform); break;
        case 27: Audio.PlaySoundAtTransform("ACBF_Msg_Bed", transform); break;
        case 28: Audio.PlaySoundAtTransform("ACBF_Msg_Beer", transform); break;
        case 29: Audio.PlaySoundAtTransform("ACBF_Msg_Fish", transform); break;
        case 30: Audio.PlaySoundAtTransform("ACBF_Msg_Fudge", transform); break;
        case 31: Audio.PlaySoundAtTransform("ACBF_Msg_Gun", transform); break;
        case 32: Audio.PlaySoundAtTransform("ACBF_Msg_Hello", transform); break;
        case 33: Audio.PlaySoundAtTransform("ACBF_Msg_Lemons", transform); break;
        case 34: Audio.PlaySoundAtTransform("ACBF_Msg_Move", transform); break;
        case 35: Audio.PlaySoundAtTransform("ACBF_Msg_Much", transform); break;
        case 36: Audio.PlaySoundAtTransform("ACBF_Msg_Noon", transform); break;
        case 37: Audio.PlaySoundAtTransform("ACBF_Msg_Nope", transform); break;
        case 38: Audio.PlaySoundAtTransform("ACBF_Msg_Psych", transform); break;
        case 39: Audio.PlaySoundAtTransform("ACBF_Msg_Sinking", transform); break;
        case 40: Audio.PlaySoundAtTransform("ACBF_Msg_Wasted", transform); break;
        case 41: Audio.PlaySoundAtTransform("ACBF_Msg_Whether", transform); break;
        case 42: Audio.PlaySoundAtTransform("ACBF_Msg_Yeet", transform); break;
        case 43: Audio.PlaySoundAtTransform("ACBF_Msg_Unicorn", transform); break;
        default: Audio.PlaySoundAtTransform("ACBF_Msg_Test", transform); break;
        }

        yield return new WaitForSeconds(10.0f);

        canListen = true;
    }


    // Makes the LEDs red for Phase 2
    public void SetRedLEDs() {
        for (int i = 0; i < LEDs.Length; i++)
            LEDs[i].material = LEDColors[1];
    }
}