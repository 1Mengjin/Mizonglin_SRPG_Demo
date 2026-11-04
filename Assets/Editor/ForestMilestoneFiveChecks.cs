using System.IO;
using UnityEditor;
using UnityEngine;

public static class ForestMilestoneFiveChecks
{
    [MenuItem("迷踪林战棋 Demo/迷踪林/执行里程碑5逻辑验收")]
    public static void Run()
    {
        string report = ForestFeedbackChecks.Run();
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/forest-m5-unity-verification.txt", report);
        Debug.Log("FOREST_M5_ENGINEERING_PASS\n" + report);
    }
}
