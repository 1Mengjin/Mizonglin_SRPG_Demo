using System.IO;
using UnityEditor;
using UnityEngine;

public static class ForestMilestoneFourChecks
{
    [MenuItem("迷踪林战棋 Demo/迷踪林/执行里程碑4验收")]
    public static void Run()
    {
        string report = ForestEnemyChecks.Run();
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/forest-m4-unity-verification.txt", report);
        Debug.Log("FOREST_M4_PASS\n" + report);
    }
}
