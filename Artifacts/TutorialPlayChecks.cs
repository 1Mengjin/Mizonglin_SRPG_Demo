using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Mizonglin.Demo;
using Mizonglin.Forest;

public sealed class TutorialPlayChecks : MonoBehaviour
{
    BattleDemo demo; string report = "";
    object Call(string method, params object[] args) => typeof(BattleDemo).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(demo, args);
    T Field<T>(string name) => (T)typeof(BattleDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(demo);
    void Set(string name, object value) => typeof(BattleDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(demo, value);
    RectTransform Popup => Field<RectTransform>("tutorialOverlay");
    bool Busy => Field<bool>("busy");
    void Check(bool ok, string why) { if (!ok) throw new Exception(why); report += "PASS " + why + "\n"; }
    void Close(string caption = "×") => Popup.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<Text>().text == caption).onClick.Invoke();
    void Capture(string name) => Call("Capture", Path.GetFullPath("Artifacts/" + name + ".png"));
    void CheckTextFits()
    {
        Canvas.ForceUpdateCanvases();
        var text = Popup.GetComponentsInChildren<Text>().Single(t => t.name == "Tutorial body");
        Check(text.preferredHeight <= ((RectTransform)text.transform.parent).rect.height, "Tutorial text fits the reference layout without scrolling (" + text.preferredHeight + "/" + ((RectTransform)text.transform.parent).rect.height + ")");
    }
    void Start() { demo = FindObjectOfType<BattleDemo>(); Directory.CreateDirectory("Artifacts"); StartCoroutine(Drive(Verify())); }
    IEnumerator Drive(IEnumerator test)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(test); float deadline = Time.realtimeSinceStartup + 90;
        while (stack.Count > 0)
        {
            object next = null;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("Tutorial Play timed out");
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
            }
            catch (Exception ex)
            {
                File.WriteAllText("Artifacts/tutorial-ui-verification.txt", report + "FAIL " + ex);
                Debug.LogException(ex); UnityEditor.EditorApplication.Exit(1); yield break;
            }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        File.WriteAllText("Artifacts/tutorial-ui-verification.txt", report + "PASS actual Unity Play tutorial flow and rendered captures.\n");
        UnityEditor.EditorApplication.Exit(0);
    }
    IEnumerator Until(Func<bool> predicate)
    {
        float deadline = Time.unscaledTime + 12;
        while (!predicate()) { if (Time.unscaledTime > deadline) throw new Exception("Tutorial wait timed out"); yield return null; }
    }
    IEnumerator Verify()
    {
        yield return null;
        float openingDeadline = Time.unscaledTime + 12;
        while (Busy)
        {
            if (Popup != null) Close();
            if (Time.unscaledTime > openingDeadline) throw new Exception("Opening did not complete");
            yield return null;
        }
        var s = demo.ForestSession.State; s.groundFog.Clear(); foreach (var u in s.unitsById.Values) u.poisonStacks = 0;
        Call("ResetForestTutorials"); demo.State.SyncForest(); Call("Refresh");
        var actor = s.unitsById["C01"]; var origin = actor.cell;
        var destination = ForestPathfinder.Reachable(s, actor).costs.Keys.First(p => ForestRules.Distance(p, origin) == 1);
        demo.ClickCell(origin); s.groundFog.Add(destination); demo.State.SyncForest(); Call("Refresh");
        int events = s.eventLog.Count; demo.ClickCell(destination);
        yield return Until(() => Popup != null);
        Check(Busy && actor.cell == origin && s.eventLog.Count == events, "Fog path opens tutorial before committing movement or resources");
        Check(Popup.GetComponentsInChildren<Text>().Any(t => t.text == "瘴气与瘴毒"), "Correct fog tutorial title");
        var position = Field<Vector3>("cameraFocus"); yield return new WaitForSecondsRealtime(.2f);
        Check(Field<Vector3>("cameraFocus") == position && s.eventLog.Count == events, "Tutorial pauses camera and battle progression");
        CheckTextFits(); Capture("ui-11-fog-tutorial"); Close(); yield return Until(() => !Busy);
        Check(demo.State.phase == SelectionPhase.Preview && actor.cell == origin, "Close button resumes the same uncommitted movement preview");
        demo.Cancel(); demo.ClickCell(destination); yield return null;
        Check(Popup == null && !Busy, "Repeated fog contact does not reopen tutorial"); demo.Cancel(); demo.Cancel();

        var boss = s.unitsById.Values.First(u => u.maskBoss != null);
        demo.ClickCell(boss.cell); yield return Until(() => Popup != null);
        Check(Popup.GetComponentsInChildren<Text>().Any(t => t.text == "千面与元神"), "Inspecting boss opens its tutorial");
        Check(Popup.GetComponentsInChildren<Text>().Any(t => t.name == "Tutorial body" && t.text.Contains("携带") && t.text.Contains("蚀痕")), "Boss tutorial includes current spirit controls and purification costs");
        CheckTextFits(); Capture("ui-12-mask-tutorial"); Close("我知道了"); yield return Until(() => !Busy);
        Check(Field<ForestTutorialGuide>("tutorialGuide").Shown == (ForestTutorialTopic.Fog | ForestTutorialTopic.Mask), "Both topics remembered for this run");
        demo.Cancel();

        Call("ResetForestTutorials"); Set("busy", true);
        var bossView = demo.State.units.First(u => u.id == boss.instanceId);
        demo.StartCoroutine((IEnumerator)Call("ShowEnemySkill", bossView, "千面震击"));
        yield return Until(() => Popup != null);
        Check(Field<RectTransform>("announcementContent") == null && Busy, "Enemy skill notification waits for encounter tutorial");
        Close(); yield return Until(() => Field<RectTransform>("announcementContent") != null);
        Check(Field<RectTransform>("announcementContent").name == "Enemy skill notification", "Closing encounter tutorial resumes enemy notification");
        yield return Until(() => Field<RectTransform>("announcementContent") == null); Set("busy", false);

        Call("ResetForestTutorials"); demo.StartCoroutine((IEnumerator)Call("PresentForestTutorials", ForestTutorialTopic.Fog | ForestTutorialTopic.Mask));
        yield return Until(() => Popup != null); Close();
        yield return Until(() => Popup != null);
        Check(Busy && Popup.GetComponentsInChildren<Text>().Any(t => t.text == "千面与元神"), "Simultaneous contacts queue two tutorials while retaining input lock");
        Close(); yield return Until(() => !Busy);
        Call("ResetForestTutorials"); demo.StartCoroutine((IEnumerator)Call("PresentForestTutorials", ForestTutorialTopic.Fog));
        yield return Until(() => Popup != null); Call("BeginForestCombat");
        Check(Popup == null && Field<ForestTutorialGuide>("tutorialGuide").Shown == ForestTutorialTopic.None, "Restart interrupts tutorial and resets per-run progress");
        while (Busy) { if (Popup != null) Close(); yield return null; }
        Check(demo.ForestSession.State.phase == BattlePhase.Player && Popup == null, "Restart returns to playable battle without a stuck overlay");
    }
}
