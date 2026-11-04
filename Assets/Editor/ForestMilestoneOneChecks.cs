using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mizonglin.Demo;
using Mizonglin.Forest;

public static class ForestMilestoneOneChecks
{
    const string RequestPath = "Artifacts/forest-m1-check-request.txt";
    const string ReportPath = "Artifacts/forest-m1-unity-verification.txt";

    [MenuItem("迷踪林战棋 Demo/迷踪林/执行里程碑1验收")]
    public static void Run()
    {
        var scenario = AssetDatabase.LoadAssetAtPath<ForestScenario>(ForestDataWindow.AssetPath);
        RunFor(scenario);
    }

    public static string RunFor(ForestScenario scenario)
    {
        try
        {
            if (scenario == null) throw new InvalidOperationException("正式配置资源不存在");
            var report = ForestDataChecks.Run(scenario.definition);
            var serialized = JsonUtility.ToJson(scenario.definition);
            var roundTrip = JsonUtility.FromJson<ScenarioDefinition>(serialized);
            ForestDataChecks.Run(roundTrip);
            if (serialized != JsonUtility.ToJson(roundTrip)) throw new Exception("正式配置JSON往返不一致");
            var legacy = AssetDatabase.LoadAssetAtPath<BattleScenario>(DemoSetup.ScenarioPath);
            if (legacy == null || legacy.Validate() != null) throw new Exception("现有旧配置资源无法读取");
            var legacyCopy = JsonUtility.FromJson<LevelMap>(JsonUtility.ToJson(legacy.map));
            if (legacyCopy.Validate() != null) throw new Exception("旧地图JSON无法读取");
            var legacyState = new BattleState(); legacyState.Reset(legacyCopy); legacy.CopyFighters().Apply(legacyState, legacyCopy);
            foreach (var map in new[] { LevelMap.Default(), LevelMap.Forest() })
            {
                legacyState.Reset(map); FighterBook.Defaults().Apply(legacyState, map);
                if (legacyState.units.Count != map.spawns.Count) throw new Exception("旧工厂地图初始化失败");
            }
            report += "\nPASS Unity: actual .asset load, formal configuration JSON roundtrip, existing legacy .asset/JSON load and initialization."
                + "\nNo scene, default gameplay binding or build output was modified by these checks.";
            Directory.CreateDirectory("Artifacts"); File.WriteAllText(ReportPath, report);
            Debug.Log("FOREST_M1_PASS\n" + report);
            return report;
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory("Artifacts"); File.WriteAllText(ReportPath, "FAIL\n" + ex);
            throw;
        }
    }

    // Lets an already-open Editor run the same checks after a script reload, without touching the scene.
    [InitializeOnLoadMethod]
    static void RunRequestedChecks()
    {
        EditorApplication.delayCall += () => {
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            try { Run(); } catch (Exception ex) { Debug.LogException(ex); }
        };
    }
}
