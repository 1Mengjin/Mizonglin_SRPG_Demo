using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mizonglin.Demo;

[InitializeOnLoad]
public static class TutorialIntegration
{
    const string Key = "TutorialIntegration.Active";
    static TutorialIntegration() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BattleDemo.unity");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var demo = Object.FindObjectOfType<BattleDemo>(); if (demo?.ForestSession == null) return;
        SessionState.SetBool(Key, false); new GameObject("Tutorial play checks").AddComponent<TutorialPlayChecks>();
    }
}
