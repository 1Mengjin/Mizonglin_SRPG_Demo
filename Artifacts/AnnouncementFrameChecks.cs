using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Mizonglin.Demo;

public static class AnnouncementFrameChecks
{
    static BattleDemo demo;
    static string report = "";
    static object Call(string method, params object[] args) => typeof(BattleDemo).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(demo, args);
    static T Field<T>(string name) => (T)typeof(BattleDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(demo);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); report += "PASS " + message + "\n"; }
    static void Capture(string name) => Call("Capture", Path.GetFullPath("Artifacts/" + name + ".png"));
    static void Panel(string name)
    {
        var panel = Field<RectTransform>("announcementContent").Find(name);
        Check(panel != null && panel.GetComponent<Image>() != null, name + " uses a native Image background");
        var fill = panel.GetComponent<Image>().color;
        Check(fill.r == 0 && fill.g == 0 && fill.b == 0 && Mathf.Approximately(fill.a, .72f), name + " has 72% translucent black fill");
        Check(panel.GetComponentsInChildren<Image>().Count(i => i.name.EndsWith(" border")) == 4, name + " has four straight Image borders");
        Check(Field<Image>("announcementShade").color.a == 0, name + " leaves the scene outside undimmed");
    }
    public static void Run()
    {
        Directory.CreateDirectory("Artifacts");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            demo = new GameObject("Frame verification").AddComponent<BattleDemo>();
            demo.forestScenario = AssetDatabase.LoadAssetAtPath<Mizonglin.Forest.ForestScenario>("Assets/Data/ForestScenario.asset");
            demo.scenario = AssetDatabase.LoadAssetAtPath<BattleScenario>("Assets/Data/BattleScenario.asset");
            demo.RebuildEditorPreview();
            Field<Mizonglin.Forest.ForestTutorialGuide>("tutorialGuide").MarkShown(Mizonglin.Forest.ForestTutorialTopic.Fog | Mizonglin.Forest.ForestTutorialTopic.Mask);
            var hud = Field<Canvas>("canvas"); hud.gameObject.SetActive(true);
            ((IEnumerator)Call("ShowVictoryConditions")).MoveNext(); Panel("Objective ribbon"); Capture("ui-08-objective-frame");
            var actor = demo.State.units.First(u => !u.enemy && u.stats?.portrait != null);
            ((IEnumerator)Call("ShowSkillCutIn", actor, "技能释放")).MoveNext(); Panel("Skill ribbon"); Panel("Skill name frame"); Capture("ui-09-skill-frame");
            // Focus is a separate awaited step; advance it before constructing the enemy panel.
            var enemy = demo.State.units.First(u => u.enemy);
            Call("ClearAnnouncements");
            var routine = (IEnumerator)Call("ShowEnemySkill", enemy, "千面震击");
            while (routine.MoveNext() && Field<RectTransform>("announcementContent") == null) { }
            Panel("Enemy skill frame"); Capture("ui-10-enemy-frame");
            ((IEnumerator)Call("ShowTurnTransition", false, 1)).MoveNext();
            Check(Mathf.Approximately(Field<Image>("announcementShade").color.a, .58f), "turn transition keeps its full-screen shade");
            Call("ClearAnnouncements");
            Call("SaveForestRun", "胜利界面布局检查");
            Call("FinishBattle", true, "雾源已除，地面瘴气与全部携雾已清空");
            Capture("ui-19-victory-title");
            File.WriteAllText("Artifacts/announcement-frames-verification.txt", report + "Native Unity UI construction and frame rendering completed; animation timing not exercised by this suite.\n");
        }
        catch (Exception ex)
        {
            File.WriteAllText("Artifacts/announcement-frames-verification.txt", report + "FAIL " + ex);
            Debug.LogException(ex); EditorApplication.Exit(1);
        }
    }
}
