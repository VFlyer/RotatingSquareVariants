using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class FaultyCruelFlashingRotatingSquaresScript : RotatingSquaresSpinoffCore {
    public MeshRenderer[] btnRenderers;
    public TextMesh[] cbBtnTexts;
    public KMColorblindMode colorblindMode;
    static int modIDCnt;
    static Color[] activeColors = new[] { Color.red, Color.yellow, Color.green, Color.blue };
    List<int> idxFlashingPos, idxColor, rememberedIdxColorsLast;
    static string[] colorNames = new[] { "red", "yellow", "green", "blue" },
        colorNamesAbbrev = new[] { "R", "Y", "G", "B" };
    bool colorblindDetected, isFlashingWhite;

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
        idxFlashingPos = new List<int>();
        idxColor = new List<int>();
        pressedIDxes = new List<int>();
        rememberedIdxColorsLast = new List<int>();
        moduleID = ++modIDCnt;
        for (var x = 0; x < btnSelectables.Length; x++)
        {
            var y = x;
            btnSelectables[x].OnInteract += delegate {
                if (needyActive)
                    HandleIdxPress(y);
                return false;
            };
        }
        QuickLog("All actions and Dead Squares are logged as if the plate was not rotated at all.");
        needyHandler.OnNeedyActivation += HandleNeedyActivation;
        needyHandler.OnNeedyDeactivation += HandleDeactivation;
        needyHandler.OnTimerExpired += HandleTimerExpire;
        for (var x = 0; x < btnRenderers.Length; x++)
        {
            btnRenderers[x].material.color = Color.black;
            if (btnRenderers[x].material.HasProperty("_UnlitColor"))
                btnRenderers[x].material.SetColor("_UnlitColor", Color.white);
            if (btnRenderers[x].material.HasProperty("_MainColor"))
                btnRenderers[x].material.SetColor("_MainColor", Color.black);
        }
        for (var x = 0; x < cbBtnTexts.Length; x++)
            cbBtnTexts[x].text = "";
        for (var x = 0; x < TPArrowRenders.Length; x++)
            TPArrowRenders.ElementAtOrDefault(x).enabled = false;
    }

    protected override void HandleNeedyActivation()
    {
        needyActive = true;
        
        var remainingBtns = Enumerable.Range(0, 16).Except(pressedIDxes).ToArray();
        var invalid = true;
        do
        {
            invalid = false;
            idxFlashingPos.Clear();
            for (var x = 0; x < 3; x++)
                idxFlashingPos.Add(Random.Range(0, 16));
            idxColor.Clear();
            idxColor.AddRange(Enumerable.Range(0, 4).ToArray().Shuffle().Take(3));
            invalid |= idxFlashingPos.Select(a => a / 4).Distinct().Count() < 3 ||
                idxFlashingPos.Select(a => a % 4).Distinct().Count() < 3 ||
                !remainingBtns.Any(a => !InvalidPressIdx(a));
        }
        while (invalid);
        
        for (int n = 0; n < idxFlashingPos.Count; n++)
        {
            int idxPos = idxFlashingPos[n];
            var curColor = colorNames[idxColor[n]];
            QuickLog("The {0} Dead Square is square #{1} in reading order.", curColor, idxPos + 1);
        }
        QuickLog("Valid squares to press: {0}", remainingBtns.Where(a => !InvalidPressIdx(a)).Select(a => a + 1).Join(", "));
        StartCoroutine(HandleFlashingColors());
        if (TwitchPlaysActive)
            HandleNeedyStartTP();
    }

    protected override void HandleDeactivation()
    {
        needyActive = false;
        for (var x = 0; x < cbBtnTexts.Length; x++)
            cbBtnTexts[x].text = "";
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
        var rowIdxPressed = idx / 4;
        var colIdxPressed = idx % 4;
        var idxRelevantColors = new List<int>();
        for (var x = 0; x < idxFlashingPos.Count; x++)
        {
            var curIdxColor = idxColor[x];
            var curIdxPos = idxFlashingPos[x];
            if (curIdxPos % 4 == colIdxPressed || curIdxPos / 4 == rowIdxPressed)
                idxRelevantColors.Add(curIdxColor);
        }
        return pressedIDxes.Contains(idx) || !idxRelevantColors.Any() || idxRelevantColors.Any(a => rememberedIdxColorsLast.Contains(a));
    }
    protected override void HandleIdxPress(int idx)
    {
        btnSelectables[idx].AddInteractionPunch(0.5f);
        mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, btnSelectables[idx].transform);
        var rowIdxPressed = idx / 4;
        var colIdxPressed = idx % 4;
        var idxRelevantColors = new List<int>();
        for (var x = 0; x < idxFlashingPos.Count; x++)
        {
            var curIdxColor = idxColor[x];
            var curIdxPos = idxFlashingPos[x];
            if (curIdxPos % 4 == colIdxPressed || curIdxPos / 4 == rowIdxPressed)
                idxRelevantColors.Add(curIdxColor);
        }
        if (InvalidPressIdx(idx))
        {
            QuickLog("You pressed square #{0} in reading order.{1}{2}", idx + 1,
                pressedIDxes.Contains(idx) ? " You pressed this square before." : "",
                !idxRelevantColors.Any() ? " The square pressed is not in the same column/row as any Dead Squares." :
                idxRelevantColors.Any(a => rememberedIdxColorsLast.Contains(a)) ?
                string.Format(" The square pressed is in the same row/column as the {0} Dead Square(s). At least one of these colors was used before.", idxRelevantColors.Select(a => colorNames[a]).Join(", ")) : "");
            needyHandler.HandleStrike();
            return;
        }
        pressedIDxes.Add(idx);
        rememberedIdxColorsLast.Clear();
        rememberedIdxColorsLast.AddRange(idxRelevantColors);
        QuickLog("You pressed square #{0} in reading order. The Dead Square{1} sharing the row/column of the pressed square {2} {3}", idx + 1,
            idxRelevantColors.Count > 1 ? "s" : "",
            idxRelevantColors.Count > 1 ? "are" : "is",
            idxRelevantColors.Select(a => colorNames[a]).Join(", "));
        HandleDeactivation();
        needyHandler.HandlePass();
        var remainingPosIdxes = Enumerable.Range(0, 16).Except(pressedIDxes);
        if (!remainingPosIdxes.Any())
        {
            pressedIDxes.Clear();
            StartCoroutine(HandleFlashingWhite());
            QuickLog("All 16 squares have been pressed at this point from their unset states. Squares pressed from before can be pressed again.");
        }
        var rotateAmount = Random.Range(1, 841) * (Random.value < 0.5f ? -1 : 1);
        while (TwitchPlaysActive && Mathf.Abs((plateTransform.localRotation * Quaternion.Euler(0, rotateAmount, 0)).eulerAngles.y) % 90 == 45)
            rotateAmount = Random.Range(1, 841) * (Random.value < 0.5f ? -1 : 1);
        QuickLog("The plate has rotated {0} degrees {1}.", Mathf.Abs(rotateAmount), rotateAmount < 0 ? "CCW" : "CW");
        StartCoroutine(HandleRotateRandomly(rotateAmount));
        
    }
    IEnumerator HandleFlashingWhite()
    {
        bool BlendNot0All;
        do
        {
            BlendNot0All = false;
            for (var x = 0; x < btnRenderers.Length; x++)
            {
                var relevantMaterial = btnRenderers[x].material;
                if (relevantMaterial.HasProperty("_Blend"))
                {
                    var blendVal = relevantMaterial.GetFloat("_Blend");
                    if (blendVal > 0f)
                        BlendNot0All = true;
                }
            }
            yield return null;
        }
        while (BlendNot0All);
        isFlashingWhite = true;
        for (var n = 0; n < 5 && !needyActive; n++)
        {
            for (var x = 0; x < btnRenderers.Length; x++)
            {
                var relevantMaterial = btnRenderers[x].material;
                if (relevantMaterial.HasProperty("_UnlitColor"))
                    relevantMaterial.SetColor("_UnlitColor", Color.white);
            }
            for (float t = 0; t < 1f && !needyActive; t += Time.deltaTime * moduleSpeed)
            {
                var curProg = Easing.InOutSine(t, 0, 1, 0.5f);
                for (var x = 0; x < btnRenderers.Length; x++)
                    if (btnRenderers[x].material.HasProperty("_Blend"))
                        btnRenderers[x].material.SetFloat("_Blend", curProg);
                yield return null;
            }
        }
        isFlashingWhite = false;
    }
    IEnumerator HandleFlashingColors()
    {
        var flashingOptions = new List<List<int>>();
        for (var x = 0; x < 16; x++)
        {
            var newOptions = Enumerable.Range(0, 4).ToList();

            if (idxFlashingPos.Contains(x))
            {
                var y = x;
                var idxFound = idxFlashingPos.IndexOf(a => a == y);
                newOptions.Remove(idxColor[idxFound]);
            }
            newOptions.Shuffle();
            flashingOptions.Add(newOptions);
        }
        var lcdTick = 12;
        var curTick = 0;
        while (needyActive)
        {
            
            for (var x = 0; x < btnRenderers.Length; x++)
            {
                var colorIdx = flashingOptions[x][curTick % flashingOptions[x].Count];
                var relevantMaterial = btnRenderers[x].material;
                if (relevantMaterial.HasProperty("_UnlitColor"))
                    relevantMaterial.SetColor("_UnlitColor", activeColors[colorIdx]);
            }
            for (float t = 0; t < 1f && needyActive; t += Time.deltaTime * moduleSpeed)
            {
                var curProg = Easing.InOutSine(t, 0, 1, 0.5f);
                for (var x = 0; x < btnRenderers.Length; x++)
                {
                    var colorIdx = flashingOptions[x][curTick % flashingOptions[x].Count];
                    if (btnRenderers[x].material.HasProperty("_Blend"))
                    {
                        var curBlend = btnRenderers[x].material.GetFloat("_Blend");
                        cbBtnTexts[x].text = colorblindDetected && curBlend >= 0.4f ? colorNamesAbbrev[colorIdx] : "";
                        cbBtnTexts[x].color = colorIdx == 1 ? Color.black : Color.white;
                        btnRenderers[x].material.SetFloat("_Blend", curProg);
                    }
                }
                yield return null;
            }
            curTick = (curTick + 1) % lcdTick;
        }
    }
    protected override IEnumerator HandleRotateRandomly(float degrees)
    {
        var lastRotation = plateTransform.localRotation;
        var pickedVector = Vector3.up * degrees;

        var endingRotation = lastRotation * Quaternion.Euler(pickedVector);
        var speed = new[] { 60, 120, 180, 240, 300, 360 }.PickRandom();
        for (float t = 0; t < 1f; t += Time.deltaTime * speed / Mathf.Abs(degrees))
        {
            var curProg = Easing.InOutSine(t, 0, 1, 1);
            var requiredRotation = Quaternion.Euler(pickedVector * curProg);
            plateTransform.localRotation = lastRotation * requiredRotation;
            yield return null;
        }
        plateTransform.localRotation = endingRotation;
        for (var x = 0; x < cbBtnTexts.Length; x++)
            cbBtnTexts[x].transform.localRotation *= Quaternion.Euler(Vector3.forward * degrees);
        yield break;
    }
    void Update()
    {
        if (!(needyActive || isFlashingWhite))
            for (var x = 0; x < btnRenderers.Length; x++)
            {
                if (btnRenderers[x].material.HasProperty("_Blend"))
                {
                    var curBlend = btnRenderers[x].material.GetFloat("_Blend");
                    var curModifier = Time.deltaTime * moduleSpeed;
                    btnRenderers[x].material.SetFloat("_Blend", Mathf.Clamp(curBlend - curModifier, 0, 1));
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
