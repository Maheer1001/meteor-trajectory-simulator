using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MTS.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MTS.Menus.Editor
{
    /// <summary>
    /// Plays the menus and the input screen and saves PNG screenshots (play mode, so Awake/Start and the TMP font
    /// atlases behave as in the game). Batch (needs graphics, so no -nographics; no -quit, it exits by itself):
    ///   Unity.exe -batchmode -projectPath P -executeMethod MTS.Menus.Editor.MenusCapture.Run [-menusCaptureDir DIR]
    /// MenusCapture.BuildAndRun rebuilds the three scenes first. Overlay canvases are switched to Screen Space - Camera
    /// for the capture only.
    /// </summary>
    [InitializeOnLoad]
    public static class MenusCapture
    {
        private const string PendingKey = "MTS.MenusCapture.Out";
        private static readonly Vector2Int Hd = new Vector2Int(1920, 1080);
        private static readonly Vector2Int[] AllSizes =
        {
            new Vector2Int(1366, 768), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
        };

        private static readonly Stack<IEnumerator> routine = new Stack<IEnumerator>();
        private static bool started;

        static MenusCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void BuildAndRun()
        {
            MenusSceneBuilder.BuildAll();
            Run();
        }

        [MenuItem("Tools/Menus/Capture Menu Screenshots")]
        public static void Run()
        {
            string dir = Argument("-menusCaptureDir") ?? Path.Combine(Path.GetTempPath(), "claude", "mts-r2-menus");
            Directory.CreateDirectory(dir);
            SessionState.SetString(PendingKey, dir);
            EditorSceneManager.OpenScene(MenusSceneBuilder.MainMenuScene, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            string dir = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(dir) || !EditorApplication.isPlaying) return;
            if (!started)
            {
                started = true;
                routine.Push(Script(dir));
            }

            bool more;
            try
            {
                while (routine.Count > 0)
                {
                    IEnumerator top = routine.Peek();
                    if (!top.MoveNext())
                    {
                        routine.Pop();
                        continue;
                    }

                    if (top.Current is IEnumerator nested)
                    {
                        routine.Push(nested);
                        continue;
                    }

                    break;
                }

                more = routine.Count > 0;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                routine.Clear();
                more = false;
            }

            if (!more)
            {
                started = false;
                SessionState.EraseString(PendingKey);
                Debug.Log("[Menus] Screenshots done: " + dir);
                if (Application.isBatchMode) EditorApplication.Exit(0);
                else EditorApplication.ExitPlaymode();
            }
        }

        private static IEnumerator Script(string dir)
        {
            yield return Menu(MenusSceneBuilder.MainMenuScene, "mainmenu", dir, false, AllSizes);
            yield return Menu(MenusSceneBuilder.MainMenuScene, "mainmenu-info", dir, true, AllSizes);
            yield return Menu(MenusSceneBuilder.PauseMenuScene, "pausemenu", dir, false, Hd);
            yield return Menu(MenusSceneBuilder.PauseMenuScene, "pausemenu-info", dir, true, Hd);
            yield return Input("input-default", dir, s => { }, AllSizes);
            yield return Input("input-mix4", dir, Mix4, Hd, new Vector2Int(1366, 768));
            yield return Input("input-chicxulub", dir, s => s.PickScenario(IndexOf(ImpactScenarios.Chicxulub)), Hd, new Vector2Int(3840, 2160));
            yield return Input("input-barringer-edited", dir, s =>
            {
                s.PickScenario(IndexOf(ImpactScenarios.Barringer));
                s.CommitMass("abc");
                s.SetAmount(0, 100);
            }, Hd);
            // Round trip: values set on the screen survive leaving and re-entering it.
            yield return Input("input-roundtrip", dir, Mix4, Array.Empty<Vector2Int>());
            yield return Reload("input-roundtrip", dir, Hd);
        }

        private static void Mix4(VariableInputScreen s)
        {
            s.AddMaterial();
            s.AddMaterial();
            s.AddMaterial();
            s.SetMaterial(0, MeteorMaterialPresets.IronId);
            s.SetMaterial(1, MeteorMaterialPresets.OrdinaryChondriteId);
            s.SetMaterial(2, MeteorMaterialPresets.StonyIronId);
            s.SetMaterial(3, MeteorMaterialPresets.CometaryId);
            s.SetAmount(0, 70);
            s.SetAmount(1, 40);
            s.SetAmount(2, 20);
            s.SetAmount(3, 10);
            s.CommitMass("2.5e9");
            s.SetMode(EntryMode.Inbound);
        }

        private static int IndexOf(ImpactScenario s)
        {
            for (int i = 0; i < ImpactScenarios.All.Count; i++)
            {
                if (ReferenceEquals(ImpactScenarios.All[i], s)) return i;
            }

            return -1;
        }

        private static void ResetInputs()
        {
            SimulationSession session = SimulationSession.Instance;
            session.Reset();
            session.Composition = null;
            session.Mode = EntryMode.FromRest;
            session.EntrySpeed = MeteorProperties.DefaultEntrySpeed;
            session.EntryAngleDegrees = MeteorProperties.DefaultEntryAngleDegrees;
            MassRangeInput.Mass = (float)MassInput.DefaultMass;
            Xcord.X = 25f;
            Ycord.Y = 25f;
            Zcord.Z = 25f;
        }

        private static IEnumerator Load(string scene)
        {
            EditorSceneManager.LoadSceneInPlayMode(scene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return Frames(4);
        }

        private static IEnumerator Menu(string scene, string name, string dir, bool info, params Vector2Int[] sizes)
        {
            ResetInputs();
            yield return Load(scene);
            MenuScreen screen = UnityEngine.Object.FindObjectOfType<MenuScreen>(true);
            screen.ShowInformation(info);
            if (screen.stageText != null) screen.stageText.text = MenuScreen.StageLine(SimulationStage.Space);
            yield return Frames(2);
            foreach (Vector2Int size in sizes) Render(screen.GetComponent<Canvas>(), dir, name, size);
        }

        private static IEnumerator Input(string name, string dir, Action<VariableInputScreen> setup, params Vector2Int[] sizes)
        {
            ResetInputs();
            yield return Load(MenusSceneBuilder.InputScene);
            VariableInputScreen screen = UnityEngine.Object.FindObjectOfType<VariableInputScreen>(true);
            setup(screen);
            yield return Frames(2);
            Debug.Log("[Menus] " + name + ": " + SimulationSession.Instance.Composition.Describe() + ", " + MassRangeInput.Mass + " kg, " +
                      SimulationSession.Instance.Mode);
            foreach (Vector2Int size in sizes) Render(screen.GetComponent<Canvas>(), dir, name, size);
        }

        /// <summary>Leaves to the main menu and comes back (without resetting), then captures.</summary>
        private static IEnumerator Reload(string name, string dir, params Vector2Int[] sizes)
        {
            yield return Load(MenusSceneBuilder.MainMenuScene);
            yield return Load(MenusSceneBuilder.InputScene);
            VariableInputScreen screen = UnityEngine.Object.FindObjectOfType<VariableInputScreen>(true);
            Debug.Log("[Menus] after round trip: " + SimulationSession.Instance.Composition.Describe() + ", " + MassRangeInput.Mass +
                      " kg, " + SimulationSession.Instance.Mode + ", mixer rows " + screen.Mixer.Count + ", field '" + screen.massField.text + "'");
            foreach (Vector2Int size in sizes) Render(screen.GetComponent<Canvas>(), dir, name, size);
        }

        private static IEnumerator Frames(int n)
        {
            int target = Time.frameCount + n;
            while (Time.frameCount < target) yield return null;
        }

        private static void Render(Canvas canvas, string dir, string name, Vector2Int size)
        {
            Camera cam = Camera.main;
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderMode previousMode = canvas.renderMode;
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.enabled = false;
            scaler.enabled = true;
            Canvas.ForceUpdateCanvases();
            foreach (LayoutGroup g in canvas.GetComponentsInChildren<LayoutGroup>(false))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)g.transform);
            }

            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            string file = Path.Combine(dir, string.Format("{0}-{1}x{2}.png", name, size.x, size.y));
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Debug.Log("[Menus] Saved " + file);
            UnityEngine.Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            canvas.renderMode = previousMode;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }

            return null;
        }
    }
}
