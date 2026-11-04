using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zilong.Demo;
using Zilong.Forest;

[InitializeOnLoad]
public static class ForestM2Integration
{
    const string Key = "ForestM2Integration.Active";
    const string StepKey = "ForestM2Integration.Step";
    static string report = "";
    static ForestM2Integration() { EditorApplication.update += Tick; }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static Button Button(BattleDemo demo, string text) => UnityEngine.Object.FindObjectsOfType<Button>()
        .Single(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text == text);

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Artifacts");
            ForestMilestoneOneChecks.Run(); ForestMilestoneTwoChecks.Run();
            var preset = DemoSetup.GetForestScenario();
            var custom = ScriptableObject.CreateInstance<ForestScenario>(); custom.definition = preset.definition.Copy();
            custom.definition.unitTemplates[0].attack = 101; custom.definition.unitTemplates[0].move = 5;
            Check(ForestAuthoring.Place(custom.definition, "C01", new Vector2Int(1, 10), "C01") == null, "Custom placement");
            const string assetPath = "Assets/Editor/PreviewAssets/ForestM2Roundtrip.asset";
            try
            {
                AssetDatabase.CreateAsset(custom, assetPath); AssetDatabase.SaveAssets(); Resources.UnloadAsset(custom);
                custom = AssetDatabase.LoadAssetAtPath<ForestScenario>(assetPath);
                var session = custom.CreateSession(); ForestRules.BeginPlayerPhase(session.State);
                var view = new BattleState(); view.BindForest(session.State, custom.definition);
                Check(view.units.Single(u => u.id == "C01").stats.attack == 101 && view.units.Single(u => u.id == "C01").move == 5
                    && view.units.Single(u => u.id == "C01").cell == new Vector2Int(1, 10), "Saved formal edits reach live view");
            }
            finally { AssetDatabase.DeleteAsset(assetPath); }
            var designer = EditorWindow.GetWindow<LevelDesigner>(); designer.position = new Rect(20, 20, 1080, 900); designer.Show();
            designer.SendEvent(new Event { type = EventType.Layout }); designer.Repaint();
            Set(designer, "tab", 1); Set(designer, "factionFilter", 2);
            foreach (var template in preset.definition.unitTemplates)
            {
                Set(designer, "selectedTemplate", template.templateId);
                designer.SendEvent(new Event { type = EventType.Layout }); designer.SendEvent(new Event { type = EventType.Repaint });
            }
            Set(designer, "tab", 2); designer.SendEvent(new Event { type = EventType.Layout }); designer.SendEvent(new Event { type = EventType.Repaint });
            designer.Close();
            EditorSceneManager.OpenScene(DemoSetup.ScenePath);
            DemoSetup.RefreshPreview(); DemoSetup.VerifySavedPreview();
            // VerifySavedPreview reopens the serialized scene; rebuild its nonserialized model before inspecting it.
            DemoSetup.RefreshPreview();
            var demo = UnityEngine.Object.FindObjectOfType<BattleDemo>();
            Check(demo.forestScenario == preset && demo.State.Forest != null && demo.State.units.Count == 11, "Scene formal binding");
            Check(demo.State.units.All(u => u.forestUnit != null && u.stats.id == u.forestUnit.templateId), "All live units use corresponding formal templates");
            SessionState.SetString("ForestM2Integration.EditReport", "PASS native Editor: actual formal asset save/reload, edited values and placement reach live templates; all 8 template editors and rules GUI render; formal scene preview saved/rendered; all 11 live view bindings.\n");
            SessionState.SetString("ForestM2Integration.Started", DateTime.UtcNow.Ticks.ToString());
            SessionState.SetInt(StepKey, 0); SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception ex) { Fail(ex); }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            if (long.TryParse(SessionState.GetString("ForestM2Integration.Started", ""), out var start)
                && new TimeSpan(DateTime.UtcNow.Ticks - start).TotalSeconds > 90) throw new Exception("Play integration timed out at step " + SessionState.GetInt(StepKey, -1));
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var demo = UnityEngine.Object.FindObjectOfType<BattleDemo>(); if (demo?.ForestSession == null) return;
            var s = demo.ForestSession.State;
            switch (SessionState.GetInt(StepKey, 0))
            {
                case 0:
                    if (s.phase != BattlePhase.Player) return;
                    Check(!UnityEngine.Object.FindObjectsOfType<Button>().Any(b => b.gameObject.activeInHierarchy
                        && (b.GetComponentInChildren<Text>()?.text == "飞行" || b.GetComponentInChildren<Text>()?.text == "落地")),
                        "No phase-start stance popup");
                    demo.ClickCell(s.unitsById["C03"].cell); demo.ClickCell(s.unitsById["C03"].cell);
                    Check(demo.State.CanCancelMovementPreview && Button(demo, "取消").gameObject.activeInHierarchy,
                        "Actual cancel button visible for self-cell preview");
                    Button(demo, "取消").onClick.Invoke();
                    Check(demo.State.phase == SelectionPhase.Selected, "Actual self-cell cancel returns to selection");
                    demo.ClickCell(new Vector2Int(1, 11));
                    Button(demo, "展翼\n下回合飞行").onClick.Invoke();
                    Check(s.unitsById["C03"].cell == new Vector2Int(2, 11) && !s.unitsById["C03"].acted
                        && s.unitsById["C03"].statistics.actions == 0 && s.unitsById["C03"].flight.switchRound == 2
                        && demo.State.phase == SelectionPhase.Preview && demo.State.preview == new Vector2Int(1, 11),
                        "Free skill keeps pending movement and action rights");
                    Button(demo, "取消").onClick.Invoke();
                    Check(s.unitsById["C03"].flight.switchRound == 2, "Movement cancel retains scheduled free skill");
                    Button(demo, "待机").onClick.Invoke();
                    s.unitsById["C02"].currentHp = 50; s.unitsById["C02"].poisonStacks = 2; demo.State.SyncForest();
                    demo.ClickCell(s.unitsById["C02"].cell); Button(demo, "治疗\n恢复30").onClick.Invoke();
                    demo.ClickCell(s.unitsById["C02"].cell);
                    SessionState.SetInt(StepKey, 1); break;
                case 1:
                    if (!s.unitsById["C02"].acted) return;
                    Check(s.unitsById["C02"].currentHp == 80 && s.unitsById["C02"].poisonStacks == 2, "Actual heal skill button/target/action");
                    int before = s.eventLog.Count;
                    demo.ClickCell(s.unitsById["C01"].cell); demo.ClickCell(new Vector2Int(3, 10)); demo.Cancel();
                    Check(s.unitsById["C01"].cell == new Vector2Int(3, 11) && s.eventLog.Count == before, "Actual movement preview cancel");
                    for (int y = 9; y <= 11; y++) for (int x = 4; x <= 6; x++) if (s.Map.Walkable(new Vector2Int(x, y))) s.groundFog.Add(new Vector2Int(x, y));
                    demo.State.SyncForest(); Call(demo, "Refresh");
                    Button(demo, "符箓·镇\n剩余 1").onClick.Invoke(); demo.Cancel();
                    Check(ForestRules.Ability(s.unitsById["C01"], "talisman").remainingUses == 1, "Actual targeting cancel keeps charge");
                    Button(demo, "符箓·镇\n剩余 1").onClick.Invoke(); demo.ClickCell(new Vector2Int(5, 10));
                    SessionState.SetInt(StepKey, 2); break;
                case 2:
                    if (!s.unitsById["C01"].acted) return;
                    Check(ForestRules.Ability(s.unitsById["C01"], "talisman").remainingUses == 0
                        && !s.groundFog.Contains(new Vector2Int(5, 10)), "Actual talisman after movement cancel completes and clears fog");
                    Button(demo, "结束回合").onClick.Invoke(); Button(demo, "确认").onClick.Invoke(); SessionState.SetInt(StepKey, 3); break;
                case 3:
                    if (s.round != 2 || s.phase != BattlePhase.Player) return;
                    Check(s.unitsById["C03"].flight.stance == FlightStance.Flying, "Scheduled flight applies next round");
                    demo.ClickCell(s.unitsById["C03"].cell);
                    Button(demo, "收翼\n下回合落地").onClick.Invoke();
                    Check(!s.unitsById["C03"].acted && s.unitsById["C03"].flight.stance == FlightStance.Flying
                        && s.unitsById["C03"].flight.switchRound == 3, "Free landing skill while flying");
                    demo.Cancel(); Button(demo, "结束回合").onClick.Invoke(); Button(demo, "确认").onClick.Invoke();
                    SessionState.SetInt(StepKey, 4); break;
                case 4:
                    if (s.round != 3 || s.phase != BattlePhase.Player) return;
                    Check(s.unitsById["C03"].flight.stance == FlightStance.Grounded, "Scheduled landing applies next round");
                    Check(ForestRules.Stats(s, s.unitsById["C04"]).attack == 90 && ForestRules.Stats(s, s.unitsById["C04"]).defense == 0, "Actual turn3 automatic form");
                    demo.ClickCell(s.unitsById["C04"].cell); Call(demo, "Capture", Path.GetFullPath("Artifacts/forest-m2-play.png"));
                    Button(demo, "结束回合").onClick.Invoke(); Button(demo, "确认").onClick.Invoke(); SessionState.SetInt(StepKey, 5); break;
                case 5:
                    if (s.round != 4 || s.phase != BattlePhase.Player) return;
                    Check(s.unitsById["C04"].currentHp == 133, "Actual skipped attack-form wait self damage");
                    Button(demo, "重 开").onClick.Invoke(); Button(demo, "确认").onClick.Invoke();
                    s = demo.ForestSession.State; Check(s.round == 1 && s.unitsById["C03"].flight.switchRound == 0 && s.unitsById["C03"].flight.stance == FlightStance.Grounded && s.unitsById["C04"].currentHp == 140
                        && ForestRules.Ability(s.unitsById["C01"], "talisman").remainingUses == 1, "Actual restart restores snapshot");
                    report = SessionState.GetString("ForestM2Integration.EditReport", "")
                        + "PASS actual Play: formal startup without stance popup; free stance skill and next-round application; self-cell/move cancellation buttons; self-heal; target cancellation; talisman; manual end and phase advance; turn3 form; skipped self-damage; restart.\n"
                        + File.ReadAllText("Artifacts/forest-m2-unity-verification.txt");
                    File.WriteAllText("Artifacts/forest-m2-integration-verification.txt", report);
                    SessionState.SetBool(Key, false); Debug.Log("FOREST_M2_INTEGRATION_PASS\n" + report); EditorApplication.Exit(0); break;
            }
        }
        catch (Exception ex) { Fail(ex); }
    }
    static void Fail(Exception ex)
    {
        SessionState.SetBool(Key, false); Directory.CreateDirectory("Artifacts");
        File.WriteAllText("Artifacts/forest-m2-integration-verification.txt", "FAIL\n" + ex);
        Debug.LogException(ex); EditorApplication.Exit(1);
    }
}
