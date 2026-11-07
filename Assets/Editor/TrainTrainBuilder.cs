using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TrainTrain.Editor
{
    public static class TrainTrainBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Train Train / Build All Platforms")]
        public static void BuildAllPlatforms()
        {
            TrainTrainProjectSetup.RepairProject();
            BuildWindows();
            BuildMac();
            Debug.Log("Train Train / 混乱火车: Windows and macOS builds completed.");
        }

        [MenuItem("Train Train / Build Windows")]
        public static void BuildWindows()
        {
            EnsureBuildFolder();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/TrainTrain.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildAndLog(options, "Windows");
        }

        [MenuItem("Train Train / Build macOS")]
        public static void BuildMac()
        {
            EnsureBuildFolder();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/macOS/TrainTrain.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            BuildAndLog(options, "macOS");
        }

        private static void EnsureBuildFolder()
        {
            Directory.CreateDirectory("Builds/Windows");
            Directory.CreateDirectory("Builds/macOS");
        }

        private static void BuildAndLog(BuildPlayerOptions options, string platform)
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(platform + " build succeeded: " + options.locationPathName + " (" + summary.totalSize + " bytes)");
            }
            else
            {
                Debug.LogError(platform + " build failed: " + summary.result + " with " + summary.totalErrors + " error(s)");
            }
        }
    }
}
