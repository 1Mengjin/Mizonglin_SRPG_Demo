using UnityEditor;
using UnityEngine;

public static class ForestUndoCheckMenu
{
    [MenuItem("紫龙 Demo/迷踪林/执行悔棋逻辑验收")]
    public static void Run()
    {
        string report = ForestUndoChecks.Run();
        Debug.Log(report);
    }
}
