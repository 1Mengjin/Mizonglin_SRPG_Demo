using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zilong.Demo;

[InitializeOnLoad]
public static class AnnouncementIntegration
{
    const string Key = "AnnouncementIntegration.Active";
    static AnnouncementIntegration() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BattleDemo.unity");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var demo = UnityEngine.Object.FindObjectOfType<BattleDemo>();
        if (demo?.ForestSession == null) return;
        SessionState.SetBool(Key, false);
        new GameObject("Announcement play checks").AddComponent<AnnouncementPlayChecks>();
    }
}
