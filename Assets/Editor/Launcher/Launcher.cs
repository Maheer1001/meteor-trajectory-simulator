using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MTS.Launcher
{
    /// <summary>
    /// Entry points for running the simulator. Used by run.ps1 / run.bat at the repository root, and from the
    /// editor's Tools menu.
    ///   Build:  Unity -batchmode -quit -projectPath . -executeMethod MTS.Launcher.Launcher.BuildWindows [-buildOut path]
    ///   Editor: Unity -projectPath . -executeMethod MTS.Launcher.Launcher.OpenMainMenu   (opens the editor on the main menu)
    /// </summary>
    public static class Launcher
    {
        public const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
        public const string DefaultBuildPath = "Builds/Windows/MeteorTrajectorySimulator.exe";

        [MenuItem("Tools/Meteor Simulator/Build Windows player", priority = 0)]
        public static void BuildWindows()
        {
            string output = Arg("-buildOut", DefaultBuildPath);
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes[0] != MainMenuScene)
            {
                Fail("Build settings must list " + MainMenuScene + " first (found: " + string.Join(", ", scenes) + ").");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Fail("Build " + summary.result + " with " + summary.totalErrors + " error(s).");
                return;
            }

            Debug.Log("[Launcher] Built " + Path.GetFullPath(output) + " (" + (summary.totalSize / (1024 * 1024)) + " MB, " +
                      summary.totalTime.TotalSeconds.ToString("F0") + " s).");
        }

        [MenuItem("Tools/Meteor Simulator/Open main menu scene", priority = 1)]
        public static void OpenMainMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(MainMenuScene, OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/Meteor Simulator/Play from main menu", priority = 2)]
        public static void PlayFromMainMenu()
        {
            OpenMainMenu();
            EditorApplication.isPlaying = true;
        }

        private static string Arg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        private static void Fail(string message)
        {
            Debug.LogError("[Launcher] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
