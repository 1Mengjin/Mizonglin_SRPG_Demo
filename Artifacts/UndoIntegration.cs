using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Mizonglin.Demo;
using Mizonglin.Forest;

// Verifies the in-game undo button in an actual Play session.
[InitializeOnLoad]
public static class UndoIntegration
{
    const string Key = "UndoIntegration.Active";
    static int step;
    static double start;
    static int logStart;
    static string actor = "C01";

    static UndoIntegration() { EditorApplication.update += Tick; }
    static object Call(object target, string name, params object[] arguments) =>
        target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, arguments);
    static object Get(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Artifacts");
            EditorSceneManager.OpenScene(DemoSetup.ScenePath);
            DemoSetup.RefreshPreview();
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Fail(error); }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (start == 0) start = EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup - start < 120, "timeout at step " + step);
            var demo = UnityEngine.Object.FindObjectOfType<BattleDemo>();
            if (demo?.ForestSession == null) return;
            var state = demo.ForestSession.State;
            bool busy = (bool)Get(demo, "busy");
            if (step == 0)
            {
                Call(demo, "CloseForestTutorial");
                if (state.phase != BattlePhase.Player || busy) return;
                var undo = (Button)Get(demo, "undoButton");
                var ledger = (Button)Get(demo, "ledgerButton");
                var clock = (Text)Get(demo, "runClock");
                Check(undo != null && undo.GetComponentInChildren<Text>().text == "悔棋", "undo button exists with its caption");
                var undoRect = (RectTransform)undo.transform;
                Check(undoRect.anchoredPosition == new Vector2(-28, -148) && undoRect.sizeDelta == new Vector2(130, 48),
                    "undo button sits below the battle ledger button");
                Check(undo.GetComponent<Image>().color == ledger.GetComponent<Image>().color, "undo button shares the ledger style");
                Check(((RectTransform)clock.transform).anchoredPosition == new Vector2(-28, -202), "run clock moved below the undo button");
                Check(!undo.interactable, "undo disabled while the history is empty");
                Set(demo, "busy", true); step = 1;
            }
            else if (step == 1)
            {
                Call(demo, "TickForestFeedback");
                Check(!((Button)Get(demo, "undoButton")).interactable, "undo disabled while the battle is busy");
                Set(demo, "busy", false); step = 2;
            }
            else if (step == 2)
            {
                if (busy) return;
                logStart = state.eventLog.Count;
                var command = new ForestCommand { actorId = actor, kind = ActionKind.Wait };
                Check(ForestRules.ValidateCommand(state, command) == null, "wait command is legal");
                demo.StartCoroutine((IEnumerator)Call(demo, "ForestPlayerAction", command));
                step = 3;
            }
            else if (step == 3)
            {
                if (busy || demo.ForestSession.UndoCount != 1) return;
                // The button state is refreshed from Update; force it so the editor tick cannot read a stale frame.
                Call(demo, "TickForestFeedback");
                Check(((Button)Get(demo, "undoButton")).interactable, "undo enabled after a character operation");
                Check(state.unitsById[actor].acted, "the character action committed before the undo");
                ((Button)Get(demo, "undoButton")).onClick.Invoke();
                step = 4;
            }
            else if (step == 4)
            {
                if (busy) return;
                Check(demo.ForestSession.UndoCount == 0, "clicking undo consumes the step");
                Check(!state.unitsById[actor].acted, "undo reverts the character's whole round");
                Check(state.eventLog.Count == logStart, "undo truncates the battle log");
                Check(demo.State.selected != null && demo.State.selected.id == actor, "the restored character is selected again");
                Check(((Text)Get(demo, "toast")).text.Contains("已撤回"), "undo reports what was reverted");
                Call(demo, "TickForestFeedback");
                Check(!((Button)Get(demo, "undoButton")).interactable, "undo disabled again after the last step");
                Call(demo, "Capture", Path.GetFullPath("Artifacts/undo-ui.png"));
                File.WriteAllText("Artifacts/undo-ui-verification.txt",
                    "PASS Unity Play: undo button sits below 战斗记录 and above 本局用时 with the same ink style; "
                    + "disabled while busy or without history; a real Wait operation enables it; one click reverts the "
                    + "character's whole round, truncates the log, reselects the character and disables the button.");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error) { Fail(error); }
    }

    static void Fail(Exception error)
    {
        SessionState.SetBool(Key, false);
        try { Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/undo-ui-verification.txt", "FAIL: " + error); }
        finally { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
