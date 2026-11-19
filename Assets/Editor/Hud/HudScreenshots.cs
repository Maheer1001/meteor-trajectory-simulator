using System.Collections;
using System.IO;
using System.Linq;
using MTS.Core;
using MTS.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MTS.Hud.Editor
{
    /// <summary>
    /// Plays the simulation scenes with the runtime HUD attached and saves PNG screenshots at several resolutions
    /// (pre-launch panel, space flight, atmosphere flight). Runs in play mode, so the real bootstrap, legacy-UI
    /// hiding and adapters are exercised.
    /// Batch (needs graphics, so no -nographics; no -quit, it exits by itself):
    ///   Unity.exe -batchmode -projectPath P -executeMethod MTS.Hud.Editor.HudScreenshots.Run -hudShotsOut DIR
    /// The HUD canvas is switched to Screen Space - Camera for the capture only (an overlay canvas cannot be
    /// rendered into a RenderTexture).
    /// </summary>
    [InitializeOnLoad]
    public static class HudScreenshots
    {
        private const string PendingKey = "MTS.HudShots.Out";

        private static readonly Vector2Int[] Sizes =
        {
            new Vector2Int(1366, 768), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
        };

        private static readonly System.Collections.Generic.Stack<IEnumerator> routine = new System.Collections.Generic.Stack<IEnumerator>();
        private static bool started;
        private static Camera uiCamera;

        static HudScreenshots()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/HUD/Capture HUD screenshots")]
        public static void Run()
        {
            string output = HudEditorUtil.Arg("-hudShotsOut", Path.GetFullPath("Temp/HudShots"));
            Directory.CreateDirectory(output);
            SessionState.SetString(PendingKey, output);
            SimulationSession.Instance.Reset();
            EditorSceneManager.OpenScene("Assets/Scenes/" + HudBootstrap.SpaceScene + ".unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            string output = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(output) || !EditorApplication.isPlaying)
            {
                return;
            }

            if (!started)
            {
                started = true;
                routine.Push(Script(output));
            }

            // Minimal coroutine runner: a yielded IEnumerator runs to completion before its parent continues.
            bool more = true;
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

                    break; // yield return null: wait for the next editor update
                }

                more = routine.Count > 0;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                routine.Clear();
                more = false;
            }

            if (!more)
            {
                started = false;
                SessionState.EraseString(PendingKey);
                Debug.Log("[HUD] Screenshots done: " + output);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
                else
                {
                    EditorApplication.ExitPlaymode();
                }
            }
        }

        private static IEnumerator Frames(int n)
        {
            int target = Time.frameCount + n;
            while (Time.frameCount < target)
            {
                yield return null;
            }
        }

        private static IEnumerator Script(string output)
        {
            // --- Space, waiting on the launch panel
            while (SimulationHud.Current == null || SimulationHud.Current.stage != HudStage.Space) yield return null;
            yield return Frames(10);
            SimulationHud hud = SimulationHud.Current;
            Debug.Log("[HUD] Space HUD attached; hold for launch = " + SimulationClock.HoldForLaunch +
                      "; legacy canvas enabled = " + LegacyCanvasEnabled());
            yield return Capture(output, "space-prelaunch-classic");

            hud.launchPanel.SetMode(EntryMode.RealisticEntry);
            hud.launchPanel.SelectMaterial(MeteorMaterialPresets.All.Count + 1); // two-material mix
            hud.RefreshAll();
            yield return Frames(5);
            yield return Capture(output, "space-prelaunch-realistic-mix");

            // --- Space in flight
            hud.launchPanel.SelectMaterial(1); // ordinary chondrite
            hud.Launch();
            hud.Ladder.SetNearest(500);
            SimulationSignals.RaiseToast("Signal toast from another system");
            yield return Frames(90);
            hud.RefreshAll();
            Debug.Log("[HUD] Space flight: t = " + SimulationSession.Instance.Runner.State.Time + " s, alt = " +
                      SimulationSession.Instance.Runner.State.Altitude + " m, capped ticks = " + SimulationClock.CappedTicks);
            yield return Capture(output, "space-flight");

            // --- Atmosphere in flight: start just above the entry interface and fast-forward to an eventful moment.
            var start = new Vec3d(PhysicalConstants.EarthRadius + 125000.0, 0, 0);
            MeteorProperties props = MeteorProperties.Create(20000, MeteorMaterialPresets.OrdinaryChondrite, 0.5, start,
                EntryMode.RealisticEntry, 19000, 40);
            SimulationRunner runner = SimulationSession.Instance.Begin(props);
            while (!runner.HasEnded && !runner.State.Broken)
            {
                runner.Advance(0.05);
            }

            HudRunTracker.Track(runner);
            SceneManager.LoadScene(HudBootstrap.AtmosphereScene);
            while (SimulationHud.Current == null || SimulationHud.Current.stage != HudStage.Atmosphere) yield return null;
            hud = SimulationHud.Current;
            hud.Ladder.SetNearest(0.1);
            hud.windCompass.Apply(40, 0.3);
            yield return Frames(20);
            SimulationSession.Instance.IsPaused = true; // freeze for consistent captures
            Time.timeScale = 0f;
            hud.RefreshAll();
            Debug.Log("[HUD] Atmosphere HUD attached; legacy canvas enabled = " + LegacyCanvasEnabled() + "; alt = " +
                      runner.State.Altitude + " m; ended = " + runner.HasEnded);
            yield return Capture(output, "atmosphere-flight");
        }

        private static bool LegacyCanvasEnabled()
        {
            GameObject legacy = GameObject.Find(LegacyUiHider.LegacyCanvasName);
            return legacy != null && legacy.GetComponent<Canvas>().enabled;
        }

        private static IEnumerator Capture(string output, string name)
        {
            SimulationHud hud = SimulationHud.Current;
            Canvas canvas = hud.canvas;
            if (uiCamera == null)
            {
                var go = new GameObject("HudCaptureCamera");
                uiCamera = go.AddComponent<Camera>();
                uiCamera.clearFlags = CameraClearFlags.Depth;
                uiCamera.cullingMask = 1 << HudFactory.UiLayer;
                uiCamera.orthographic = true;
                uiCamera.nearClipPlane = 0.1f;
                uiCamera.farClipPlane = 10f;
                uiCamera.depth = 100;
                uiCamera.enabled = false;
            }

            RenderMode previousMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;

            foreach (Vector2Int size in Sizes)
            {
                var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                uiCamera.targetTexture = rt;
                yield return Frames(3); // let the canvas scaler and layout pick up the new size
                hud.RefreshAll();
                Canvas.ForceUpdateCanvases();

                foreach (Camera cam in Camera.allCameras.Where(c => c != uiCamera).OrderBy(c => c.depth))
                {
                    RenderTexture previous = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = previous;
                }

                uiCamera.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                string file = Path.Combine(output, name + "-" + size.x + "x" + size.y + ".png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                uiCamera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Debug.Log("[HUD] Saved " + file);
            }

            canvas.renderMode = previousMode;
        }
    }
}
