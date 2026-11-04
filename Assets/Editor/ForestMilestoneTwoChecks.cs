using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mizonglin.Forest;

public static class ForestMilestoneTwoChecks
{
    [MenuItem("迷踪林战棋 Demo/迷踪林/执行里程碑2验收")]
    public static void Run()
    {
        var scenario = DemoSetup.GetForestScenario();
        string error = scenario.Validate(); if (error != null) throw new Exception(error);
        string report = ForestCombatChecks.Run();
        var json = JsonUtility.ToJson(scenario.definition);
        var restored = JsonUtility.FromJson<ScenarioDefinition>(json); restored.ValidateOrThrow();
        if (JsonUtility.ToJson(restored) != json) throw new Exception("正式配置序列化往返不一致");
        report += "\nPASS Unity native formal configuration load and JSON roundtrip.";
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/forest-m2-unity-verification.txt", report);
        Debug.Log("FOREST_M2_PASS\n" + report);
    }
}
