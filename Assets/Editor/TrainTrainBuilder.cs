using System.IO;
using UnityEditor;
using UnityEditor.Build;
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
            BuildAndroid();
            Debug.Log("Train Train / 混乱火车: Windows, macOS and Android builds completed.");
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

        [MenuItem("Train Train / Build Android APK")]
        public static void BuildAndroid()
        {
            TrainTrainProjectSetup.RepairProject();
            EnsureBuildFolder();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android,
                BuildTarget.Android);
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.Android,
                "com.x4ce.traintrain");
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/TrainTrain-Android-1.0.0.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    "Android build failed: " + summary.result +
                    " with " + summary.totalErrors + " error(s)");
            }

            Debug.Log(
                "Android build succeeded: " + options.locationPathName +
                " (" + summary.totalSize + " bytes)");
        }

        private static void EnsureBuildFolder()
        {
            Directory.CreateDirectory("Builds/Windows");
            Directory.CreateDirectory("Builds/macOS");
            Directory.CreateDirectory("Builds/Android");
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
