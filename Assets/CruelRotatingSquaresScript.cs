using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class CruelRotatingSquaresScript : RotatingSquaresSpinoffCore {
    public MeshRenderer[] btnRenderers;
    public TextMesh[] cbBtnTexts, cbBtnTextsDiffuse;
    public KMColorblindMode colorblindMode;
    int[] btnColorIdxes;
    static int modIDCnt;
    int idxLastColor = -1;
    static Color[] activeColors = new[] { Color.red, Color.yellow, Color.green, Color.blue },
        inactiveColors = new[] {
            new Color(0.5f, 0, 0),
            new Color(0.5f, 0.5f, 0),
            new Color(0, 0.5f, 0),
            new Color(0, 0, 0.5f), };

    static string[] colorNames = new[] { "red", "yellow", "green", "blue" },
        colorNamesAbbrev = new[] { "R", "Y", "G", "B" };
    bool colorblindDetected;
    List<int> idxToLight;
    protected override void Start()
    {
        try
        {
            colorblindDetected = colorblindMode.ColorblindModeActive;
        }
        catch
        {
            colorblindDetected = false;
        }
        pressedIDxes = new List<int>();
        idxToLight = new List<int>();
        moduleID = ++modIDCnt;
        btnColorIdxes = Enumerable.Range(0, 16).Select(a => a / 4).ToArray().Shuffle();
        for (var x = 0; x < btnSelectables.Length; x++)
        {
            var y = x;
            btnSelectables[x].OnInteract += delegate {
                if (needyActive)
                    HandleIdxPress(y);
                return false;
            };
        }
        QuickLog("All actions and square colors are logged as if the plate was not rotated at all.");
        QuickLog("Initial square colors:");
        for (var x = 0; x < 4; x++)
            QuickLog(btnColorIdxes.Skip(4 * x).Take(4).Select(a => colorNamesAbbrev[a]).Join());
        needyHandler.OnNeedyActivation += HandleNeedyActivation;
        needyHandler.OnNeedyDeactivation += HandleDeactivation;
        needyHandler.OnTimerExpired += HandleTimerExpire;
        for (var x = 0; x < btnRenderers.Length; x++)
        {
            var colorIdx = btnColorIdxes[x];
            btnRenderers[x].material.color = inactiveColors[colorIdx];
            if (btnRenderers[x].material.HasProperty("_UnlitColor"))
                btnRenderers[x].material.SetColor("_UnlitColor", activeColors[colorIdx]);
            if (btnRenderers[x].material.HasProperty("_MainColor"))
                btnRenderers[x].material.SetColor("_MainColor", inactiveColors[colorIdx]);
        }
        for (var x = 0; x < cbBtnTexts.Length; x++)
            cbBtnTexts[x].text = "";
        for (var x = 0; x < cbBtnTextsDiffuse.Length; x++)
        {
            cbBtnTextsDiffuse[x].color = btnColorIdxes[x] == 1 ? Color.black : Color.white;
            cbBtnTextsDiffuse[x].text = colorblindDetected ? colorNamesAbbrev[btnColorIdxes[x]] : "";
        }
    }

    protected override void HandleNeedyActivation()
    {
        needyActive = true;
        idxToLight.AddRange(Enumerable.Range(0, 16));
        for (var x = 0; x < cbBtnTexts.Length; x++)
        {
            cbBtnTexts[x].color = btnColorIdxes[x] == 1 ? Color.black : Color.white;
            cbBtnTexts[x].text = colorblindDetected ? colorNamesAbbrev[btnColorIdxes[x]] : "";
        }
        for (var x = 0; x < cbBtnTextsDiffuse.Length; x++)
            cbBtnTextsDiffuse[x].text = "";
        if (TwitchPlaysActive)
            HandleNeedyStartTP();
    }
    protected override void HandleDeactivation()
    {
        needyActive = false;
        for (var x = 0; x < cbBtnTexts.Length; x++)
            cbBtnTexts[x].text = "";
        for (var x = 0; x < cbBtnTextsDiffuse.Length; x++)
        {
            cbBtnTextsDiffuse[x].color = btnColorIdxes[x] == 1 ? Color.black : Color.white;
            cbBtnTextsDiffuse[x].text = colorblindDetected ? colorNamesAbbrev[btnColorIdxes[x]] : "";
        }
        if (TwitchPlaysActive)
            HandleNeedyEndTP();
    }

    protected override void HandleTimerExpire()
    {
        QuickLog("Letting the needy timer run out is not a good idea.");
        needyHandler.HandleStrike();
        HandleDeactivation();
    }
    protected override bool InvalidPressIdx(int idx)
    {
        var colorIdxPressed = btnColorIdxes[idx];
        return pressedIDxes.Contains(idx) || colorIdxPressed == idxLastColor;
    }

    protected override void HandleIdxPress(int idx)
    {
        btnSelectables[idx].AddInteractionPunch(0.5f);
        mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, btnSelectables[idx].transform);
        idxToLight.Remove(idx);
        var colorIdxPressed = btnColorIdxes[idx];
        if (InvalidPressIdx(idx))
        {
            cbBtnTexts[idx].text = "";
            cbBtnTextsDiffuse[idx].color = btnColorIdxes[idx] == 1 ? Color.black : Color.white;
            cbBtnTextsDiffuse[idx].text = colorblindDetected ? colorNamesAbbrev[btnColorIdxes[idx]] : "";
            QuickLog("You pressed square #{0} in reading order.{1}{2}", idx + 1, pressedIDxes.Contains(idx) ? " You pressed this square before." : "", colorIdxPressed == idxLastColor ? string.Format(" Its color is {0}, which you pressed from the previous activation.", colorNames[colorIdxPressed]) : "");
            needyHandler.HandleStrike();
            return;
        }
        pressedIDxes.Add(idx);
        idxLastColor = colorIdxPressed;
        QuickLog("You pressed square #{0} in reading order. Its color is {1}.", idx + 1, colorNames[colorIdxPressed]);
        needyActive = false;
        needyHandler.HandlePass();
        var remainingPosIdxes = Enumerable.Range(0, 16).Except(pressedIDxes);
        if (!remainingPosIdxes.Any())
        {
            pressedIDxes.Clear();
            QuickLog("All 16 squares have been pressed at this point from their unset states. Squares pressed from before can be pressed again.");
            QuickLog("The last color remembered can be forgetten for the next activation.");
            remainingPosIdxes = Enumerable.Range(0, 16);
            idxLastColor = -1;
        }
        var lastBtnColors = btnColorIdxes.ToArray();
        var invalid = true;
        do
        {
            btnColorIdxes.Shuffle();
            foreach (var btnIdx in remainingPosIdxes)
                invalid &= InvalidPressIdx(btnIdx);
        }
        while (invalid);
        for (var x = 0; x < cbBtnTexts.Length; x++)
            cbBtnTexts[x].text = "";
        StartCoroutine(HandleChangingColors(lastBtnColors, btnColorIdxes.ToArray()));
        QuickLog("Square colors from resulting press:");
        for (var x = 0; x < 4; x++)
            QuickLog(btnColorIdxes.Skip(4 * x).Take(4).Select(a => colorNamesAbbrev[a]).Join());
        if (idxLastColor != -1)
            QuickLog("Valid presses from current squares: {0}", remainingPosIdxes.Where(a => btnColorIdxes[a] != idxLastColor).Select(a => a + 1).Join(", "));
        var rotateAmount = Random.Range(1, 73) * 10 * (Random.value < 0.5f ? -1 : 1);
        QuickLog("The plate has rotated {0} degrees {1}.", Mathf.Abs(rotateAmount), rotateAmount < 0 ? "CCW" : "CW");
        StartCoroutine(HandleRotateRandomly(rotateAmount));
        if (TwitchPlaysActive)
            HandleNeedyEndTP();
    }
    IEnumerator HandleChangingColors(int[] lastIdxColors, int[] newIdxColors)
    {
        for (var x = 0; x < cbBtnTextsDiffuse.Length; x++)
        {
            cbBtnTextsDiffuse[x].color = lastIdxColors[x] == 1 ? Color.black : Color.white;
            cbBtnTextsDiffuse[x].text = colorblindDetected ? colorNamesAbbrev[lastIdxColors[x]] : "";
        }
        bool BlendNot0All;
        do
        {
            BlendNot0All = false;
            //var debugMatsNot0 = new List<int>();
            for (var x = 0; x < btnRenderers.Length; x++)
            {
                var relevantMaterial = btnRenderers[x].material;
                if (relevantMaterial.HasProperty("_Blend"))
                {
                    var blendVal = relevantMaterial.GetFloat("_Blend");
                    if (blendVal > 0f)
                    {
                        BlendNot0All = true;
                        //debugMatsNot0.Add(x);
                    }
                }
            }
            yield return null;
            //Debug.Log(debugMatsNot0.Join());
        }
        while (BlendNot0All);
        for (var x = 0; x < btnRenderers.Length; x++)
        {
            var colorIdx = newIdxColors[x];
            var relevantMaterial = btnRenderers[x].material;
            if (relevantMaterial.HasProperty("_UnlitColor"))
                relevantMaterial.SetColor("_UnlitColor", activeColors[colorIdx]);
        }
        for (var x = 0; x < cbBtnTextsDiffuse.Length; x++)
            if (lastIdxColors[x] != newIdxColors[x])
                cbBtnTextsDiffuse[x].text = "";
        for (float t = 0; t < 1f; t += Time.deltaTime * moduleSpeed)
        {
            for (var x = 0; x < btnRenderers.Length; x++)
            {
                var relevantMaterial = btnRenderers[x].material;
                var startColorIdx = lastIdxColors[x];
                var endColorIdx = newIdxColors[x];
                var lerpingColor = Color.Lerp(inactiveColors[startColorIdx], inactiveColors[endColorIdx], t);
                relevantMaterial.color = lerpingColor;
                if (relevantMaterial.HasProperty("_MainColor"))
                    relevantMaterial.SetColor("_MainColor", lerpingColor);
            }
            yield return null;
        }
        for (var x = 0; x < btnRenderers.Length; x++)
        {
            var relevantMaterial = btnRenderers[x].material;
            var endColorIdx = newIdxColors[x];
            relevantMaterial.color = inactiveColors[endColorIdx];
            if (relevantMaterial.HasProperty("_MainColor"))
                relevantMaterial.SetColor("_MainColor", inactiveColors[endColorIdx]);
        }
        for (var x = 0; x < cbBtnTextsDiffuse.Length; x++)
        {
            cbBtnTextsDiffuse[x].color = newIdxColors[x] == 1 ? Color.black : Color.white;
            cbBtnTextsDiffuse[x].text = colorblindDetected ? colorNamesAbbrev[newIdxColors[x]] : "";
        }
    }
    protected override IEnumerator HandleRotateRandomly(float degrees)
    {
        var lastRotationPlate = plateTransform.localRotation;
        var lastRotationCBDiffuseTxts = cbBtnTextsDiffuse.Select(a => a.transform.localRotation).ToArray();
        var pickedVector = Vector3.up * degrees;
        for (var x = 0; x < cbBtnTexts.Length; x++)
            cbBtnTexts[x].transform.localRotation *= Quaternion.Euler(Vector3.forward * degrees);
        var endingRotationPlate = lastRotationPlate * Quaternion.Euler(pickedVector);
        var speed = new[] { 60, 120, 180, 240, 300, 360 }.PickRandom();
        for (float t = 0; t < 1f; t += Time.deltaTime * speed / Mathf.Abs(degrees))
        {
            var curProg = Easing.InOutSine(t, 0, 1, 1);
            var requiredRotation = Quaternion.Euler(pickedVector * curProg);
            plateTransform.localRotation = lastRotationPlate * requiredRotation;
            for (var x = 0; x < cbBtnTextsDiffuse.Length; x++)
                cbBtnTextsDiffuse[x].transform.localRotation = lastRotationCBDiffuseTxts[x] * Quaternion.Euler(Vector3.forward * degrees * curProg);
            yield return null;
        }
        plateTransform.localRotation = endingRotationPlate;
        for (var x = 0; x < cbBtnTextsDiffuse.Length; x++)
            cbBtnTextsDiffuse[x].transform.localRotation = lastRotationCBDiffuseTxts[x] * Quaternion.Euler(Vector3.forward * degrees);
        yield break;
    }
    void Update()
    {
        for (var x = 0; x < btnRenderers.Length; x++)
        {
            if (btnRenderers[x].material.HasProperty("_Blend"))
            {
                var curBlend = btnRenderers[x].material.GetFloat("_Blend");
                var curModifier = (needyActive && idxToLight.Contains(x) ? 1 : -1) * Time.deltaTime * moduleSpeed;
                btnRenderers[x].material.SetFloat("_Blend", Mathf.Clamp(curBlend + curModifier, 0, 1));
            }
        }
    }
    void HandleColorblindTP()
    {
        if (needyActive)
        {
            for (var x = 0; x < cbBtnTexts.Length; x++)
            {
                cbBtnTexts[x].color = btnColorIdxes[x] == 1 ? Color.black : Color.white;
                cbBtnTexts[x].text = colorblindDetected ? colorNamesAbbrev[btnColorIdxes[x]] : "";
            }
        }
        else
        {
            for (var x = 0; x < cbBtnTextsDiffuse.Length; x++)
            {
                cbBtnTextsDiffuse[x].color = btnColorIdxes[x] == 1 ? Color.black : Color.white;
                cbBtnTextsDiffuse[x].text = colorblindDetected ? colorNamesAbbrev[btnColorIdxes[x]] : "";
            }
        }
    }

    protected override IEnumerator ProcessTwitchCommand(string cmd)
    {
        var rgxColorblind = Regex.Match(cmd, @"^(colou?rblind|cb)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (rgxColorblind.Success)
        {
            yield return null;
            colorblindDetected ^= true;
            HandleColorblindTP();
            yield break;
        }
        var intCmd = cmd;
        var rgxPress = Regex.Match(cmd, @"^press\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (rgxPress.Success)
            intCmd = intCmd.Skip(rgxPress.Value.Length).Join("");
        int btnNum;
        if (!int.TryParse(intCmd, out btnNum) || btnNum < 1 || btnNum > 16)
        {
            yield return string.Format("sendtochaterror \"{0}\" does not correspond to a valid button on the module!", intCmd);
            yield break;
        }

        if (!needyActive)
        {
            yield return "sendtochat {0}, #{1} is not active right now.";
            yield break;
        }
        yield return null;
        var obtainedPressIdxes = pressIdxesAffected[DetectTPIdxFromPlate()];
        btnSelectables[obtainedPressIdxes[btnNum - 1]].OnInteract();
    }
}
