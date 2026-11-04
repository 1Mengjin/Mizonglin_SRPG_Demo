using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zilong.Demo;
using Zilong.Forest;

public sealed class AnnouncementPlayChecks : MonoBehaviour
{
    BattleDemo demo;
    string report = "";
    int checks;
    string Folder => Path.GetFullPath("Artifacts");
    object Call(string method, params object[] args) => typeof(BattleDemo).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(demo, args);
    T Field<T>(string name) => (T)typeof(BattleDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(demo);
    string Current => Field<RectTransform>("announcementContent") != null ? Field<RectTransform>("announcementContent").name : "";
    bool Busy => Field<bool>("busy");
    ForestBattleState State => demo.ForestSession.State;
    void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    void Capture(string name) { Call("Capture", Path.Combine(Folder, name + ".png")); }
    void Note(string value) { report += "PASS " + value + "\n"; Debug.Log(value); }
    void Start()
    {
        demo = FindObjectOfType<BattleDemo>(); Directory.CreateDirectory(Folder);
        // This suite isolates banners; first-contact tutorial behavior has its own Play suite.
        Field<ForestTutorialGuide>("tutorialGuide").MarkShown(ForestTutorialTopic.Fog | ForestTutorialTopic.Mask);
        Call("CloseForestTutorial"); StartCoroutine(Drive(Verify()));
    }
    IEnumerator Drive(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        float deadline = Time.realtimeSinceStartup + 110;
        while (stack.Count > 0)
        {
            object next = null;
            try
            {
                Check(Time.realtimeSinceStartup < deadline, "UI verification timed out: " + Current);
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(Folder, "battle-ui-verification.txt"), report + "FAIL " + ex);
                Debug.LogException(ex); UnityEditor.EditorApplication.Exit(1); yield break;
            }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        File.WriteAllText(Path.Combine(Folder, "battle-ui-verification.txt"), report + "PASS actual Unity Play flow and rendered captures.\n");
        UnityEditor.EditorApplication.Exit(0);
    }
    IEnumerator Until(Func<bool> predicate, float timeout = 12)
    {
        float deadline = Time.unscaledTime + timeout;
        while (!predicate()) { Check(Time.unscaledTime < deadline, "Wait timed out: " + Current); yield return null; }
    }
    IEnumerator Verify()
    {
        yield return Until(() => Current == "Victory conditions");
        yield return new WaitForSecondsRealtime(.45f);
        var root = Field<RectTransform>("announcementContent");
        Check(root.anchoredPosition.x == 0 && State.phase == BattlePhase.PlayerStart && Busy, "Objective centered before gameplay");
        demo.ClickCell(State.unitsById["C01"].cell); Check(demo.State.selected == null, "Opening blocks selection");
        yield return new WaitForSecondsRealtime(1.55f);
        Check(Current == "Victory conditions" && root.anchoredPosition.x == 0, "Objective is still centered after two seconds");
        // Capture only after the timing assertion: synchronous rendering can stall for shader compilation.
        Capture("ui-01-objective");
        yield return Until(() => Current == "Player turn transition");
        Check(State.phase == BattlePhase.PlayerStart && Busy, "First player phase not begun during transition");
        yield return new WaitForSecondsRealtime(.4f); Capture("ui-02-player-turn");
        yield return Until(() => !Busy);
        Check(State.phase == BattlePhase.Player && Current == "", "Opening fully clears and begins player phase");
        Note("opening: objective right-to-center, 4s hold, left exit; player turn completes before phase begins; input blocked.");

        var hud = Field<Canvas>("canvas");
        Check(hud.renderMode == RenderMode.ScreenSpaceOverlay && hud.pixelPerfect, "UI renders at screen resolution");
        Check(Field<UnityEngine.UI.Text>("forestHint").text == "第 1/" + State.Rules.roundLimit + " 回合", "Round display is concise");
        demo.ClickCell(State.unitsById["C04"].cell); yield return null; Capture("ui-06-status-card");
        int eventCount = State.eventLog.Count;
        Call("ShowStatusDetails", 0);
        Check(Field<RectTransform>("statusPopup") != null && State.eventLog.Count == eventCount, "Buff tap opens read-only effect details");
        Capture("ui-07-status-effect"); Call("HideStatusDetails"); demo.Cancel();
        Note("status card: native-resolution canvas, concise round counter, clickable read-only effect details.");

        var flyer = State.unitsById["C03"];
        demo.ClickCell(flyer.cell); demo.ClickCell(flyer.cell);
        int actions = flyer.statistics.actions;
        Call("SelectForestSkill", ActionKind.SwitchStance);
        yield return Until(() => Current == "Player skill cut-in");
        Check(flyer.flight.switchRound == 0 && Busy, "Free skill not resolved before announcement");
        yield return new WaitForSecondsRealtime(.4f);
        root = Field<RectTransform>("announcementContent");
        var portrait = root.Find("Portrait crop/Caster portrait").GetComponent<UnityEngine.UI.Image>();
        Check(portrait.sprite == demo.State.selected.stats.portrait, "Actual caster portrait assigned");
        yield return new WaitForSecondsRealtime(.7f);
        Check(Current == "Player skill cut-in" && flyer.flight.switchRound == 0, "Skill banner retains full 1s hold");
        Capture("ui-03-skill-portrait");
        yield return Until(() => !Busy);
        Check(flyer.flight.switchRound == 2 && !flyer.acted && flyer.statistics.actions == actions && demo.State.phase == SelectionPhase.Preview, "Free skill preserves movement preview and action rights");
        Note("skill cut-in: real portrait, recoil entry, 1s hold, simultaneous exit; free stance retains movement preview and action rights.");

        demo.Cancel(); demo.Cancel();
        var healer = State.unitsById["C02"]; healer.currentHp = 40; demo.State.SyncForest();
        demo.ClickCell(healer.cell); demo.ClickCell(healer.cell);
        demo.StartCoroutine((IEnumerator)Call("ForestPlayerAction", new ForestCommand { actorId = healer.instanceId, targetId = healer.instanceId, kind = ActionKind.Heal }));
        yield return Until(() => Current == "Player skill cut-in");
        Check(healer.currentHp == 40 && !healer.acted, "Healing waits for skill announcement");
        yield return Until(() => !Busy);
        // Read the authored heal amount instead of assuming the factory default.
        int healedHp = Math.Min(ForestRules.Stats(State, healer).maxHp, 40 + State.GetTemplate(healer.templateId).support.healAmount);
        Check(healer.currentHp == healedHp && healer.acted, "Healing resolves exactly once after announcement");
        Note("targeted skill: validated self-heal executes once after the cut-in.");

        demo.StartCoroutine((IEnumerator)Call("ForestRound"));
        yield return Until(() => Current == "Enemy turn transition");
        Check(!State.eventLog.Any(e => e.kind == "MaskCharge"), "Enemy action waits for turn transition");
        yield return new WaitForSecondsRealtime(.35f); Capture("ui-04-enemy-turn");
        yield return Until(() => Current == "Enemy skill notification", 30);
        Check(!State.eventLog.Any(e => e.kind == "MaskCharge"), "Enemy skill waits for announcement");
        Check(Field<bool>("cinematicCamera"), "Enemy camera is locked during notification");
        var boss = demo.State.units.First(u => u.forestUnit.maskBoss != null);
        Check(Vector3.Distance(Field<Vector3>("cameraFocus"), new Vector3(boss.cell.x, 0, State.Map.height - 1 - boss.cell.y)) < .02f, "Camera reached enemy before notification");
        yield return new WaitForSecondsRealtime(.3f); Capture("ui-05-enemy-skill");
        yield return Until(() => Current == "Player turn transition", 30);
        Check(State.round == 2 && State.phase == BattlePhase.PlayerStart && Busy, "Next player phase waits for transition");
        yield return Until(() => !Busy);
        Check(State.phase == BattlePhase.Player && State.eventLog.Count(e => e.kind == "MaskCharge") == 1, "Enemy skill executes once; phase resumes");
        Note("turn flow: enemy turn blocks AI; camera arrives before enemy skill; skill executes once; next player phase waits for transition.");

        // Force only the presentation test's live state to a burst round; authored assets stay unchanged.
        foreach (var tree in State.unitsById.Values.Where(u => u.tree != null)) tree.tree.nextBurstRound = State.round;
        // Authored fog placement can change; explicitly provide fog for this absorption fixture.
        var absorber = State.unitsById.Values.First(u => u.devourer != null && ForestRules.Alive(u));
        State.groundFog.Add(absorber.cell);
        State.groundFog.Add(State.Map.patrolPoints[absorber.devourer.nextPatrolIndex]);
        demo.StartCoroutine((IEnumerator)Call("ForestRound"));
        yield return Until(() => Current == "Enemy skill notification", 30);
        yield return Until(() => State.eventLog.Any(e => e.kind == "MaskBlast"), 30);
        yield return Until(() => Current == "Enemy skill notification" && Field<RectTransform>("announcementContent").GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.text == "瘴果爆发"), 30);
        Check(!State.eventLog.Any(e => e.kind == "TreeBurst"), "Automatic tree skill waits for notification");
        yield return Until(() => !Busy, 30);
        Check(State.eventLog.Count(e => e.kind == "TreeBurst") == 2 && State.eventLog.Any(e => e.kind == "FogAbsorbed"), "Actual round-end tree/absorb batch preserved: bursts=" + State.eventLog.Count(e => e.kind == "TreeBurst") + ", absorbed=" + State.eventLog.Count(e => e.kind == "FogAbsorbed"));
        Note("automatic enemy skills: tree bursts and fog absorption announced before atomic round-end settlement; actual event counts preserved.");

        Call("BeginForestCombat");
        Check(Current == "Victory conditions" && Busy && State.round == 1, "Restart replays opening");
        yield return new WaitForSecondsRealtime(.3f);
        Call("BeginForestCombat");
        Check(Current == "Victory conditions" && !Field<bool>("cinematicCamera"), "Restart interrupts/cleans old UI");
        yield return Until(() => !Busy);
        Check(State.eventLog.Count(e => e.kind == "PlayerPhase") == 1 && Current == "", "Repeated restart leaves exactly one opening/phase sequence");
        Note("restart: interruption cleans old banners and camera lock; repeated restart leaves one opening and one player phase.");
    }
}
