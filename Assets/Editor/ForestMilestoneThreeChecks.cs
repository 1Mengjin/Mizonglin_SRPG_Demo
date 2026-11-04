using System.IO;
using UnityEditor;
using UnityEngine;

public static class ForestMilestoneThreeChecks
{
    [MenuItem("迷踪林战棋 Demo/迷踪林/执行里程碑3验收")]
    public static void Run()
    {
        string report = ForestResourceChecks.Run();
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/forest-m3-unity-verification.txt", report);
        Debug.Log("FOREST_M3_PASS\n" + report);
    }
}
