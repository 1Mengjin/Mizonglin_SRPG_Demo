using UnityEditor;
using UnityEngine;
using Zilong.Demo;

[CustomEditor(typeof(BattleDemo))]
public sealed class BattleDemoInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("在 Unity 关卡设计窗口修改地块、出生点和单位数值。点击 Play 直接进入战斗。", MessageType.Info);
        if (GUILayout.Button("打开关卡设计窗口")) LevelDesigner.Open();
        if (GUILayout.Button("编辑技能与状态说明")) BattleTextEditor.Open();
        if (GUILayout.Button("刷新并保存场景预览") && !EditorApplication.isPlaying)
            DemoSetup.RefreshPreview();
        if (GUILayout.Button("在 Project 中定位主场景"))
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoSetup.ScenePath));
    }
}
