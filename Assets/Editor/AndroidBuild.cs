using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DouQuqu.EditorTools
{
    /// <summary>命令行 Android 构建入口，供本地和展会前的一键打包使用。</summary>
    public static class AndroidBuild
    {
        public static void BuildApk()
        {
            string output = GetCommandLineValue("-apkOutput");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Builds/Android/DouQuqu.apk"));

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("Build Settings 中没有启用的场景。");

            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? Directory.GetCurrentDirectory());
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.zqy.douququ");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Android 构建失败：" + report.summary.result + "，错误数 " + report.summary.totalErrors);

            Debug.Log($"[DouQuqu] APK 构建完成：{output} ({report.summary.totalSize} bytes)");
        }

        private static string GetCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return string.Empty;
        }
    }
}
