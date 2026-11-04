using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mizonglin.EditorTools
{
    // Builds the playable WebGL demo into docs/ so GitHub Pages can serve it from the repository.
    public static class WebGLBuild
    {
        const string ScenePath = "Assets/Scenes/BattleDemo.unity";
        const string OutputDir = "docs";

        [MenuItem("迷踪林战棋 Demo/构建 WebGL 试玩")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void Build()
        {
            // GitHub Pages cannot send Content-Encoding headers, so the loader must decompress in JS.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.template = "PROJECT:MizonglinWeb";

            Directory.CreateDirectory(OutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"WEBGL_BUILD result={summary.result} errors={summary.totalErrors} " +
                      $"size={summary.totalSize / 1048576f:F1}MB time={summary.totalTime}");
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
