using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class RotatingSquaresSpinoffCore : MonoBehaviour {

	public KMNeedyModule needyHandler;
	public KMAudio mAudio;
	public KMSelectable[] btnSelectables;
	public Transform plateTransform;
	public MeshRenderer[] TPArrowRenders;
	protected List<int> pressedIDxes;
	protected bool needyActive, TwitchPlaysActive;
	protected int moduleID;
	[SerializeField, Range(1f, 10f)]
	protected float moduleSpeed = 5f;
	protected virtual void QuickLogDebug(string toLog = "", params object[] args)
	{
		Debug.LogFormat("<{0} #{1}> {2}", needyHandler.ModuleDisplayName, moduleID, string.Format(toLog, args));
	}
	protected virtual void QuickLog(string toLog = "", params object[] args)
	{
		Debug.LogFormat("[{0} #{1}] {2}", needyHandler.ModuleDisplayName, moduleID, string.Format(toLog, args));
	}
	protected virtual void Start()
	{
		pressedIDxes = new List<int>();
		for (int x = 0; x < btnSelectables.Length; x++)
		{
			var y = x;
			btnSelectables[x].OnInteract += delegate {
				if (needyActive)
					HandleIdxPress(y);
				return false;
			};
		}
		needyHandler.OnTimerExpired += HandleTimerExpire;
		needyHandler.OnNeedyDeactivation += delegate { needyActive = false; };
		needyHandler.OnNeedyActivation += HandleNeedyActivation;
	}
	protected virtual void HandleNeedyActivation()
	{
		needyActive = true;
		if (TwitchPlaysActive)
			HandleNeedyStartTP();
	}
	protected virtual void HandleTimerExpire()
	{
		needyActive = false;
		needyHandler.HandleStrike();
	}
	protected virtual void HandleDeactivation()
    {
		needyActive = false;
    }
	protected virtual void HandleIdxPress(int idx)
	{
		if (InvalidPressIdx(idx))
			needyHandler.HandleStrike();
		else
        {
			needyActive = false;
			needyHandler.HandlePass();
			pressedIDxes.Add(idx);
			if (pressedIDxes.Count > 15)
				pressedIDxes.Clear();
			StartCoroutine(HandleRotateRandomly(Random.Range(1, 5) * 90));
        }
	}
	/// <summary>
	/// Conditional method to determine if a button would be not a valid button to press.
	/// </summary>
	/// <param name="idx">The index of the button associated with the module</param>
	/// <returns>"False" if a button is valid to press, "True" otherwise</returns>
	protected virtual bool InvalidPressIdx(int idx)
    {
		return pressedIDxes.Contains(idx);
	}
	protected virtual IEnumerator HandleRotateRandomly(float degrees)
    {
		var lastRotation = plateTransform.localRotation;
		var pickedVector = (Random.value < 0.5f ? Vector3.up : Vector3.down) * degrees;

		var endingRotation = lastRotation * Quaternion.Euler(pickedVector);
		var speed = new[] { 60f, 120f, 180f, 240f, 300f, 360f }.PickRandom();
        for (float t = 0; t < 1f; t += Time.deltaTime * speed / Mathf.Abs(degrees))
        {
			var curProg = Easing.InOutSine(t, 0, 1, 1);
			var requiredRotation = Quaternion.Euler(pickedVector * curProg);
			plateTransform.localRotation = lastRotation * requiredRotation;
			yield return null;
        }
		plateTransform.localRotation = endingRotation;
		yield break;
    }
	#region TwitchPlays
	protected virtual void HandleNeedyEndTP()
    {
		for (var x = 0; x < TPArrowRenders.Length; x++)
			TPArrowRenders.ElementAtOrDefault(x).enabled = false;
	}
	protected virtual void HandleNeedyStartTP()
    {
		for (var x = 0; x < TPArrowRenders.Length; x++)
			TPArrowRenders.ElementAtOrDefault(x).enabled = false;
		if (TPArrowRenders != null && TPArrowRenders.Length == 4)
			TPArrowRenders.ElementAtOrDefault(DetectTPIdxFromPlate()).enabled = true;
	}
	protected int DetectTPIdxFromPlate()
    {
		var usedEularAngleY = plateTransform.localEulerAngles.y;
		var relevantAngles = new[] { 0, 90, 180, 270, 360 };
		if (usedEularAngleY < 0)
			usedEularAngleY += 360;
		var differences = relevantAngles.Select(a => Mathf.Abs(usedEularAngleY - a));
		Debug.Log(differences.Join());
		return Enumerable.Range(0, 5).FirstOrDefault(a => differences.ElementAt(a) <= differences.Min()) % 4;
    }
	protected int[][] pressIdxesAffected = new int[][] {
		Enumerable.Range(0, 16).ToArray(),
		new[] { 12, 8, 4, 0, 13, 9, 5, 1, 14, 10, 6, 2, 15, 11, 7, 3 },
		Enumerable.Range(0, 16).Reverse().ToArray(),
		new[] { 3, 7, 11, 15, 2, 6, 10, 14, 1, 5, 9, 13, 0, 4, 8, 12 },
	};
	protected string TwitchHelpMessage = "\"!{0} press 12\" [Presses the 12th square in reading order, with respect to the rotated plate, \"press\" is optional.] | \"!{0} colorblind/cb/colourblind\" | On Twitch Plays, the 1st square in reading order is marked with a white triangle pointing down.";
	protected virtual IEnumerator ProcessTwitchCommand(string cmd)
    {
        var intCmd = cmd;
		var rgxPress = Regex.Match(cmd, @"^press\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (rgxPress.Success)
			intCmd = intCmd.Skip(rgxPress.Value.Length).Join("");
		int btnNum;
		if (!int.TryParse(intCmd, out btnNum) || btnNum < 1 || btnNum> 16)
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
	protected virtual void TwitchHandleForcedSolve()
    {
		StartCoroutine(HandleAutosolve());
    }
	protected virtual IEnumerator HandleAutosolve()
    {
		yield return null;
		while (enabled)
		{
			if (needyActive)
			{
				var possibleBtns = Enumerable.Range(0, 16).Where(a => !InvalidPressIdx(a)).ToArray();
				btnSelectables[possibleBtns.PickRandom()].OnInteract();
				yield return null;
			}
			yield return new WaitForSeconds(1f);
		}
	}
    #endregion
}
