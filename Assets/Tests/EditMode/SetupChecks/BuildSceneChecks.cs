using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MTS.SetupChecks.Tests
{
    /// <summary>
    /// Setup checks for the scene flow: the six build scenes are present in the order the game expects, each one opens
    /// with every script resolved, and every scene name the code or a scene hands to SceneManager.LoadScene is a build
    /// scene. A failure here usually means a bad checkout (missing .meta files, Git LFS, wrong branch), not a code bug.
    /// </summary>
    public class BuildSceneChecks
    {
        /// <summary>Build index order the menus and stage hand-offs rely on.</summary>
        internal static readonly string[] ExpectedScenes =
        {
            "MainMenu",          // 0  title screen
            "SpaceSimulation",   // 1  stage 1: space traversal
            "AtmosphereReached", // 2  stage 2: atmospheric entry
            "EarthCollision",    // 3  results
            "VariableInput",     // 4  input / setup screen
            "MainMenu2",         // 5  in-simulation pause menu
        };

        /// <summary>MainMenuButton methods whose string argument is a scene name.</summary>
        private static readonly HashSet<string> SceneLoadingMethods = new HashSet<string> { "MainMenu", "MainMenuS1", "MainMenu2", "Restart" };

        private static string PathOf(string sceneName) =>
            EditorBuildSettings.scenes.Select(s => s.path).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sceneName);

        [Test]
        public void BuildSettingsListTheSixScenesInOrder()
        {
            string[] names = EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
            Assert.That(names, Is.EqualTo(ExpectedScenes), "File > Build Settings scene list");
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                Assert.That(scene.enabled, Is.True, scene.path + " is unticked in Build Settings");
            }
        }

        [TestCaseSource(nameof(ExpectedScenes))]
        public void SceneLoadsByNameAtItsBuildIndex(string sceneName)
        {
            string path = PathOf(sceneName);
            Assert.That(path, Is.Not.Null, sceneName + " is not in Build Settings");
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path + " is missing from the checkout");
            Assert.That(SceneUtility.GetBuildIndexByScenePath(path), Is.EqualTo(Array.IndexOf(ExpectedScenes, sceneName)));
            // SceneManager.LoadScene(name) at runtime picks the build scene with that file name, so it must be unique.
            int sameName = EditorBuildSettings.scenes.Count(s => Path.GetFileNameWithoutExtension(s.path) == sceneName);
            Assert.That(sameName, Is.EqualTo(1), "SceneManager.LoadScene(\"" + sceneName + "\") is ambiguous");
        }

        [TestCaseSource(nameof(ExpectedScenes))]
        public void SceneOpensWithAllScriptsAndSceneLinksResolved(string sceneName)
        {
            Scene scene = EditorSceneManager.OpenScene(PathOf(sceneName), OpenSceneMode.Additive);
            try
            {
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True, sceneName + " did not open");
                Assert.That(scene.rootCount, Is.GreaterThan(0), sceneName + " is empty");

                var missing = new List<string>();
                var badLinks = new List<string>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                        {
                            missing.Add(HierarchyPath(t));
                        }

                        foreach (Component c in t.GetComponents<Component>())
                        {
                            if (c != null)
                            {
                                CollectSceneLinks(c, HierarchyPath(t), badLinks);
                            }
                        }
                    }
                }

                Assert.That(missing, Is.Empty, sceneName + ": objects with a missing script (deleted script or missing .meta)");
                Assert.That(badLinks, Is.Empty, sceneName + ": scene names that are not in Build Settings");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SceneNamesInCodeAreBuildScenes()
        {
            var names = new HashSet<string>();

            // Constants the stage hand-offs use (in Assembly-CSharp, which a test assembly cannot reference directly).
            foreach ((string type, string field) in new[]
                     {
                         ("SimulationInputs", "ResultsScene"),
                         ("MTS.Hud.HudBootstrap", "SpaceScene"),
                         ("MTS.Hud.HudBootstrap", "AtmosphereScene"),
                     })
            {
                Type t = Type.GetType(type + ", Assembly-CSharp");
                Assert.That(t, Is.Not.Null, type + " not found in Assembly-CSharp (did the game scripts compile?)");
                FieldInfo f = t.GetField(field, BindingFlags.Public | BindingFlags.Static);
                Assert.That(f, Is.Not.Null, type + "." + field);
                names.Add((string)f.GetRawConstantValue());
            }

            // Literal scene names in the game scripts: LoadScene("X"), sceneName = "X", IsNullOrEmpty(s) ? "X".
            var literal = new Regex(@"LoadScene\(\s*""([^""]+)""|sceneName\s*=\s*""([^""]+)""|IsNullOrEmpty\(\w+\)\s*\?\s*""([^""]+)""");
            foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in literal.Matches(File.ReadAllText(file)))
                {
                    names.Add(m.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value);
                }
            }

            Assert.That(names, Is.Not.Empty);
            Assert.That(names, Is.SubsetOf(ExpectedScenes), "scene names used in Assets/Scripts");
        }

        private static void CollectSceneLinks(Component component, string where, List<string> bad)
        {
            var so = new SerializedObject(component);
            SerializedProperty it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.String || string.IsNullOrEmpty(it.stringValue))
                {
                    continue;
                }

                bool isSceneField = Regex.IsMatch(it.name, @"^sceneName\d*$");
                bool isButtonArgument = false;
                if (it.name == "m_StringArgument" && it.propertyPath.EndsWith(".m_Arguments.m_StringArgument", StringComparison.Ordinal))
                {
                    string callPath = it.propertyPath.Substring(0, it.propertyPath.Length - ".m_Arguments.m_StringArgument".Length);
                    SerializedProperty method = so.FindProperty(callPath + ".m_MethodName");
                    isButtonArgument = method != null && SceneLoadingMethods.Contains(method.stringValue);
                }

                if ((isSceneField || isButtonArgument) && !ExpectedScenes.Contains(it.stringValue))
                {
                    bad.Add(where + " (" + component.GetType().Name + "." + it.propertyPath + " = \"" + it.stringValue + "\")");
                }
            }
        }

        private static string HierarchyPath(Transform t) => t.parent == null ? t.name : HierarchyPath(t.parent) + "/" + t.name;
    }
}
