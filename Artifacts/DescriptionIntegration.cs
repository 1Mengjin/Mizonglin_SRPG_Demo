using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mizonglin.Demo;

[InitializeOnLoad]
public static class DescriptionIntegration
{
    const string Key = "DescriptionIntegration.Active";
    static DescriptionIntegration() { EditorApplication.update += Tick; }
    public static void Run()
    {
        var data = AssetDatabase.LoadAssetAtPath<BattleTextCatalog>(BattleTextEditor.AssetPath);
        if (data == null || data.Validate() != null) throw new Exception("Description asset did not import correctly");
        var window = EditorWindow.GetWindow<BattleTextEditor>(); window.position = new Rect(20, 20, 1000, 700); window.Show();
        window.SendEvent(new Event { type = EventType.Layout }); window.Repaint(); window.Close();
        EditorSceneManager.OpenScene("Assets/Scenes/BattleDemo.unity");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var demo = UnityEngine.Object.FindObjectOfType<BattleDemo>(); if (demo?.ForestSession == null) return;
        SessionState.SetBool(Key, false); new GameObject("Description play checks").AddComponent<DescriptionPlayChecks>();
    }
}
