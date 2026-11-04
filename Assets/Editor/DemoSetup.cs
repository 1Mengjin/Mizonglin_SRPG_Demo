using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mizonglin.Demo;
using Mizonglin.Forest;

[InitializeOnLoad]
public static class DemoSetup
{
    public const string ScenePath = "Assets/Scenes/BattleDemo.unity";
    public const string ScenarioPath = "Assets/Data/BattleScenario.asset";
    const string MaterialPath = "Assets/Editor/PreviewAssets/ScenePreview.mat";
    const string FontPath = "Assets/Editor/PreviewAssets/ChinesePreview.fontsettings";
    static Font livePreviewFont;

    static DemoSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += InitializeCurrentScene;
        EditorApplication.playModeStateChanged += ValidateBeforePlay;
    }

    public static BattleScenario GetScenario()
    {
        var data = AssetDatabase.LoadAssetAtPath<BattleScenario>(ScenarioPath);
        if (data != null) return data;
        Directory.CreateDirectory("Assets/Data");
        data = ScriptableObject.CreateInstance<BattleScenario>();
        AssetDatabase.CreateAsset(data, ScenarioPath);
        AssetDatabase.SaveAssets();
        return data;
    }

    public static ForestScenario GetForestScenario()
    {
        var data = AssetDatabase.LoadAssetAtPath<ForestScenario>(ForestDataWindow.AssetPath);
        if (data != null) return data;
        data = ScriptableObject.CreateInstance<ForestScenario>(); data.definition = ForestDefaults.Create();
        AssetDatabase.CreateAsset(data, ForestDataWindow.AssetPath); AssetDatabase.SaveAssets(); return data;
    }

    static void ValidateBeforePlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode || SceneManager.GetActiveScene().path != ScenePath) return;
        var error = GetForestScenario().Validate();
        if (error != null)
        {
            EditorApplication.isPlaying = false;
            Debug.LogError("关卡配置校验失败，无法开始战斗。" + error);
            return;
        }
        AssetDatabase.SaveAssets();
    }

    static void InitializeCurrentScene()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.path == ScenePath) EnsurePreview(scene);
        else if (!scene.isDirty && (scene.path.EndsWith("/StageThree.unity") || scene.path.EndsWith("/StageTwo.unity") || scene.path.EndsWith("/StageOne.unity") || scene.path.EndsWith("/SampleScene.unity")))
            Open();
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == ScenePath) EditorApplication.delayCall += () => EnsurePreview(scene);
    }

    static BattleDemo FindDemo(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var demo = root.GetComponent<BattleDemo>();
            if (demo != null) return demo;
        }
        return null;
    }

    static void EnsurePreview(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var demo = FindDemo(scene);
        if (demo != null)
        {
            bool current=false, legacyHud=false, currentHud=false, formal=false;
            var formalScenario = GetForestScenario();
            if (formalScenario.Validate() != null) return;
            var map=formalScenario.definition.map;
            foreach(var child in demo.GetComponentsInChildren<Transform>(true))
            {
                if(child.name==$"{map.width} x {map.height} board") current=true;
                if(child.name=="Objective") legacyHud=true;
                if(child.name=="Selected character card") currentHud=true;
                if(child.name==BattleDemo.ForestPreviewMarker) formal=true;
            }
            if(!current || !formal || legacyHud || !currentHud || demo.forestScenario==null || demo.GetComponentsInChildren<Camera>(true).Length==0
                || demo.GetComponentInChildren<Camera>(true).name!="Tactical camera") RefreshPreview();
        }
        BindPreviewFonts(scene);
    }

    static void BindPreviewFonts(Scene scene)
    {
        if (livePreviewFont == null)
        {
            livePreviewFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
            livePreviewFont.hideFlags = HideFlags.HideAndDontSave;
        }
        var demo = FindDemo(scene);
        if (demo == null) return;
        foreach (var text in demo.GetComponentsInChildren<Text>(true))
        {
            text.font = livePreviewFont;
            livePreviewFont.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
        }
        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
    }

    [MenuItem("迷踪林战棋 Demo/打开主场景")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("迷踪林战棋 Demo/刷新并保存场景预览")]
    public static void RefreshPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        var demo = FindDemo(scene);
        if (demo == null) return;
        demo.scenario = GetScenario();
        var error = GetForestScenario().Validate(); if (error != null) { Debug.LogWarning("正式配置校验未通过，不刷新预览：" + error); return; }
        demo.forestScenario = GetForestScenario();
        var preview = demo.RebuildEditorPreview();
        Directory.CreateDirectory("Assets/Editor/PreviewAssets");
        var assetMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (assetMaterial == null)
        {
            assetMaterial = new Material(Resources.Load<Shader>("Whitebox"));
            AssetDatabase.CreateAsset(assetMaterial, MaterialPath);
        }
        var palette = new Dictionary<Color, Material>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(MaterialPath))
            if (asset is Material material) palette[material.color] = material;
        foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
        {
            var color = renderer.sharedMaterial.color;
            if (!palette.TryGetValue(color, out var material))
            {
                material = new Material(renderer.sharedMaterial) { name = "Preview " + ColorUtility.ToHtmlStringRGB(color) };
                AssetDatabase.AddObjectToAsset(material, assetMaterial);
                palette[color] = material;
            }
            renderer.sharedMaterial = material;
        }
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
            AssetDatabase.CreateAsset(font, FontPath);
        }
        foreach (var text in preview.GetComponentsInChildren<Text>(true)) text.font = font;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        BindPreviewFonts(scene);
        Debug.Log("DEMO_PREVIEW_SAVED");
    }

    public static void BuildAndVerify()
    {
        var validation = GetForestScenario().Validate();
        if (validation != null) throw new Exception("关卡配置校验失败。" + validation);
        DemoChecks.VerifyModel(); DemoChecks.VerifyMap(); DemoChecks.CheckRules();
        EditorSceneManager.OpenScene(ScenePath);
        RefreshPreview();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Directory.CreateDirectory("Build/Demo");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64,
            locationPathName = "Build/Demo/MizonglinDemo.exe", options = BuildOptions.Development
        });
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Demo build failed");
        Debug.Log("DEMO_BUILD_PASS");
    }

    public const string WindowsBuildPath = "Build/Demo/MizonglinDemo.exe";

    [MenuItem("迷踪林战棋 Demo/构建 Windows 版（Release）", false, 100)]
    public static void BuildWindowsRelease()
    {
        var validation = GetForestScenario().Validate();
        if (validation != null)
        {
            EditorUtility.DisplayDialog("无法构建", "关卡配置校验失败。" + validation, "知道了");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("构建已取消：存在未保存的场景改动。");
            return;
        }
        if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        RefreshPreview();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Directory.CreateDirectory("Build/Demo");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64,
            locationPathName = WindowsBuildPath, options = BuildOptions.None
        });
        var summary = report.summary;
        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError("DEMO_WINDOWS_BUILD_FAILED " + summary.result + " errors=" + summary.totalErrors);
            EditorUtility.DisplayDialog("构建失败", "结果：" + summary.result + "，错误数：" + summary.totalErrors + "。详情见 Console。", "知道了");
            return;
        }
        Debug.Log("DEMO_WINDOWS_BUILD_PASS size=" + summary.totalSize + " time=" + summary.totalTime);
        EditorUtility.RevealInFinder(WindowsBuildPath);
    }

    public static void VerifySavedPreview()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        BindPreviewFonts(scene);
        var demo = FindDemo(scene);
        if (demo == null || demo.transform.childCount != 1) throw new Exception("Missing saved preview");
        var preview = demo.transform.GetChild(0).gameObject;
        if (!preview.CompareTag("EditorOnly")) throw new Exception("Preview must be excluded from builds");
        int tiles = 0;
        foreach (var item in preview.GetComponentsInChildren<Transform>(true))
            if (item.name.StartsWith("Tile ")) tiles++;
        if (tiles != GetForestScenario().definition.map.width*GetForestScenario().definition.map.height) throw new Exception("Preview tile count does not match map");
        var cameras = preview.GetComponentsInChildren<Camera>(true);
        if (cameras.Length != 1 || preview.GetComponentsInChildren<Canvas>(true).Length != 1)
            throw new Exception("Preview camera or Canvas missing");
        foreach (var text in preview.GetComponentsInChildren<Text>(true))
            if (text.font == null) throw new Exception("Preview text font missing after reload");
        var camera = cameras[0];
        var hud = preview.GetComponentInChildren<Canvas>(true);
        var previousMode = hud.renderMode; var previousCamera = hud.worldCamera; float previousDistance = hud.planeDistance;
        hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = camera; hud.planeDistance = 1;
        var texture = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Artifacts");
        File.WriteAllBytes("Artifacts/editor-scene-preview.png", image.EncodeToPNG());
        camera.targetTexture = null;
        hud.renderMode = previousMode; hud.worldCamera = previousCamera; hud.planeDistance = previousDistance;
        RenderTexture.active = previous;
        texture.Release();
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(image);
        Debug.Log("DEMO_SAVED_PREVIEW_PASS");
    }

    public static void VerifyDesignWorkflow()
    {
        const string testPath = "Assets/Editor/PreviewAssets/DesignRoundtrip.asset";
        var source = GetScenario();
        var authored = UnityEngine.Object.Instantiate(source);
        authored.map.tiles[0] = (int)Ground.Blocked;
        authored.map.spawns[0].x = 1;
        authored.map.spawns[0].y = 3;
        authored.fighters.units[0].attack = 55;
        authored.fighters.units[0].move = 4;
        if (authored.Validate() != null) throw new Exception(authored.Validate());
        try
        {
            AssetDatabase.CreateAsset(authored, testPath);
            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(authored);
            authored = AssetDatabase.LoadAssetAtPath<BattleScenario>(testPath);
            var state = new BattleState();
            state.Reset(authored.map.Clone());
            authored.CopyFighters().Apply(state, authored.map);
            if (!state.blocked.Contains(Vector2Int.zero) || state.units[0].cell != new Vector2Int(1, 3)
                || state.units[0].stats.attack != 55 || state.units[0].move != 4)
                throw new Exception("Saved authoring changes did not reach battle initialization");
            state.units[0].hp = 1;
            state.units[0].cell = new Vector2Int(9, 9);
            if (authored.map.spawns[0].Cell != new Vector2Int(1, 3) || authored.fighters.units[0].hp != 100)
                throw new Exception("Battle mutated authored configuration");
            Debug.Log("DESIGN_ASSET_ROUNDTRIP_PASS");
        }
        finally { AssetDatabase.DeleteAsset(testPath); }
        VerifySavedPreview();
    }
}
