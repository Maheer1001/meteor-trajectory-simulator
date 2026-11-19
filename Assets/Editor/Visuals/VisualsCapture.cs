using System;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using MTS.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MTS.Visuals.EditorTools
{
    /// <summary>
    /// Screenshot tooling for the visuals (editor only). Opens a scene, enters play mode, starts its own run (the
    /// session is paused so the adapters do not advance or change scene; this tool advances the runner at 1x), steps
    /// through scripted shots and renders the camera stack to 1920x1080 PNGs.
    /// Usage (no -batchmode -nographics needed for rendering; -batchmode is fine):
    ///   Unity.exe -projectPath P -executeMethod MTS.Visuals.EditorTools.VisualsCapture.Run
    ///             -mtsCapture space|stony|iron -mtsCaptureOut C:/dir
    /// </summary>
    [InitializeOnLoad]
    public static class VisualsCapture
    {
        private const string StartedKey = "mts_capture_started";
        private static Driver driver;

        static VisualsCapture()
        {
            string which = SceneInspector.GetArg("-mtsCapture", null);
            if (which == null || !SessionState.GetBool(StartedKey, false))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying && driver == null)
                {
                    driver = new Driver(which, SceneInspector.GetArg("-mtsCaptureOut", "Temp/Captures"));
                    EditorApplication.update += driver.Tick;
                }
            };
        }

        public static void Run()
        {
            string which = SceneInspector.GetArg("-mtsCapture", "space");
            string scene = which == "space" ? "Assets/Scenes/SpaceSimulation.unity" : "Assets/Scenes/AtmosphereReached.unity";
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            SessionState.SetBool(StartedKey, true);
            EditorApplication.EnterPlaymode();
        }

        private sealed class Driver
        {
            private readonly string which;
            private readonly string outDir;
            private int frame;
            private int lastPlayerFrame = -1;
            private int ticks;
            private int stage;
            private int wait;
            private float altTarget;
            private double startAlt;
            private SimulationRunner runner;
            private readonly List<string> written = new List<string>();
            private int brokenFrames = -1;
            private int endFrames = -1;
            private readonly HashSet<string> done = new HashSet<string>();

            public Driver(string which, string outDir)
            {
                this.which = which;
                this.outDir = outDir;
                Directory.CreateDirectory(outDir);
            }

            public void Tick()
            {
                if (!EditorApplication.isPlaying)
                {
                    return;
                }

                // batch mode throttles the player loop; ask for a frame every editor tick, act once per player frame
                Application.runInBackground = true;
                if (EditorApplication.isPaused) EditorApplication.isPaused = false; // "error pause" from batch-mode mouse-ray errors
                EditorApplication.QueuePlayerLoopUpdate();
                if (++ticks % 500 == 0)
                {
                    Debug.Log("[Capture] tick " + ticks + " frameCount " + Time.frameCount + " time " + Time.time);
                }

                if (Time.frameCount == lastPlayerFrame)
                {
                    return;
                }

                lastPlayerFrame = Time.frameCount;
                try
                {
                    frame++;
                    if (frame == 3)
                    {
                        Time.captureFramerate = 60;
                        StartRun();
                    }
                    else if (frame > 3)
                    {
                        if (which == "space") SpaceShots();
                        else AtmosphereShots();
                    }

                    if (frame > 6000)
                    {
                        Debug.LogError("[Capture] timed out");
                        Quit(1);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Quit(1);
                }
            }

            private void StartRun()
            {
                SimulationSession session = SimulationSession.Instance;
                MeteorProperties props;
                if (which == "space")
                {
                    props = MeteorProperties.FromInputs(60000, 3500, 800, 0.5, 2.2, 1.6, -1.4, EntryMode.Inbound, 17000, 40);
                }
                else if (which == "iron")
                {
                    // a fragmentation-free iron body: reaches the ground and makes a crater
                    props = MeteorProperties.FromInputs(1.0e6, 7800, 450, 0.5, 2.2, 1.6, -1.4, EntryMode.Inbound, 15000, 55, enableFragmentation: false, preset: MeteorMaterialPresets.Iron);
                }
                else
                {
                    props = MeteorProperties.FromInputs(250000, 3300, 800, 0.5, 2.2, 1.6, -1.4, EntryMode.Inbound, 18000, 38);
                }

                runner = session.Begin(props);
                session.IsPaused = true; // the adapters skip their FixedUpdate; this tool advances the run
                Time.timeScale = 1f;
                startAlt = runner.State.Altitude;
                if (which != "space")
                {
                    while (runner.State.Altitude > 121000 && !runner.HasEnded)
                    {
                        runner.Step(runner.StepSizeFor(runner.State.Altitude));
                    }
                }

                Debug.Log("[Capture] run started, altitude " + runner.State.Altitude);
            }

            private void StepToAltitude(double alt, int maxSteps)
            {
                for (int i = 0; i < maxSteps && runner.State.Altitude > alt && !runner.HasEnded; i++)
                {
                    runner.Step(runner.StepSizeFor(runner.State.Altitude));
                }
            }

            private void SpaceShots()
            {
                var sv = UnityEngine.Object.FindObjectOfType<SpaceVisuals>();
                if (sv == null) return;
                if (wait > 0) { wait--; return; }

                switch (stage)
                {
                    case 0:
                    {
                        // fly part of the way in over 60 frames so the flown path is recorded
                        double target = startAlt + (2500000 - startAlt) * Math.Min(1.0, (frame - 3) / 60.0);
                        StepToAltitude(target, 200000);
                        if (runner.State.Altitude <= 2500001) { stage++; sv.SetMode(1); sv.SnapCamera(); wait = 3; }
                        break;
                    }
                    case 1: Capture(sv, "space_wide"); sv.SetMode(2); sv.SnapCamera(); wait = 3; stage++; break;
                    case 2: Capture(sv, "space_overview"); sv.SetMode(0); sv.SetOrbit(10f, 8f, 1f); sv.SnapCamera(); wait = 3; stage++; break;
                    case 3: Capture(sv, "space_closeup"); stage++; break;
                    case 4:
                        StepToAltitude(400000, 400000);
                        sv.SetOrbit(-25f, 4f, 1.1f);
                        sv.SnapCamera();
                        wait = 3;
                        stage++;
                        break;
                    case 5: Capture(sv, "space_closeup_low"); sv.SetMode(1); sv.SnapCamera(); wait = 3; stage++; break;
                    case 6: Capture(sv, "space_wide_low"); Quit(0); break;
                }
            }

            private void AtmosphereShots()
            {
                var av = UnityEngine.Object.FindObjectOfType<AtmosphereVisuals>();
                if (av == null) return;
                var brain = UnityEngine.Object.FindObjectOfType<CinemachineBrain>();
                if (brain != null) brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
                if (wait > 0) { wait--; return; }

                if (!runner.HasEnded && pendingName == null)
                {
                    runner.Advance(1.0 / 60.0, SimulationRunner.DefaultMaxStepsPerAdvance, false);
                }

                double alt = runner.State.Altitude;
                FlightTelemetry t = runner.Telemetry;
                if (frame % 30 == 0)
                {
                    Debug.Log("[Capture] t=" + runner.State.Time.ToString("F2") + " alt=" + alt.ToString("F0") + " T=" + t.Temperature.ToString("F0") + " L=" + t.Luminosity.ToString("G3") + " broken=" + t.Broken + " mlr=" + t.MassLossRate.ToString("G3")
                        + " meteor=" + av.MeteorWorldPosition + " cam=" + av.CameraPosition + " vcam=" + (brain != null && brain.ActiveVirtualCamera != null ? brain.ActiveVirtualCamera.Name : "none")
                        + " brainEnabled=" + (brain != null && brain.isActiveAndEnabled));
                }

                bool iron = which == "iron";
                Shot(av, "atmo_chase_high", alt < (iron ? 60000 : 56000), 0);
                if (alt < 80000 && which == "debug") Quit(0);
                Shot(av, "atmo_chase", alt < (iron ? 42000 : 50500), 0);
                Shot(av, "atmo_side", alt < (iron ? 34000 : 48500), 2);
                Shot(av, "atmo_ground_observer", alt < (iron ? 24000 : 47000), 1);

                if (t.Broken && brokenFrames < 0) brokenFrames = 0;
                if (brokenFrames >= 0) brokenFrames++;
                Shot(av, "atmo_breakup_chase", brokenFrames == 8, 0);
                Shot(av, "atmo_breakup_side", brokenFrames == 30, 2);
                Shot(av, "atmo_breakup_ground", brokenFrames == 45, 1);

                if (runner.HasEnded && endFrames < 0)
                {
                    endFrames = 0;
                    Debug.Log("[Capture] ended: " + runner.Result.Reason + " " + runner.Result.Message);
                }

                if (endFrames >= 0) endFrames++;
                Shot(av, "atmo_end_flash", endFrames == 10, -1);
                Shot(av, "atmo_end_rings", endFrames == 70, -1);
                Shot(av, "atmo_end_late", endFrames == 200, -1);
                if (endFrames > 205) Quit(0);
            }

            private int pendingMode = -2;
            private string pendingName;
            private int pendingDelay;

            private void Shot(AtmosphereVisuals av, string name, bool when, int mode)
            {
                if (done.Contains(name)) return;
                if (pendingName == name)
                {
                    if (pendingDelay-- > 0) return;
                    Capture(null, which + "_" + name);
                    done.Add(name);
                    pendingName = null;
                    return;
                }

                if (!when || pendingName != null) return;
                if (mode >= 0) av.SetMode(mode);
                pendingName = name;
                pendingMode = mode;
                pendingDelay = 2; // let the camera cut and the per-frame globals settle
            }

            private void Capture(SpaceVisuals sv, string name)
            {
                var cams = new List<Camera>(UnityEngine.Object.FindObjectsOfType<Camera>());
                cams.RemoveAll(c => !c.enabled || !c.gameObject.activeInHierarchy || c.targetTexture != null);
                cams.Sort((a, b) => a.depth.CompareTo(b.depth));
                var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                // Render twice: the first pass primes each camera's previous-frame matrices, so motion blur in the
                // second pass reflects this frame only (manual renders otherwise see a huge jump).
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (Camera c in cams)
                    {
                        c.targetTexture = rt;
                        c.Render();
                        c.targetTexture = null;
                    }
                }

                RenderTexture.active = rt;
                var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                string path = Path.Combine(outDir, name + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
                written.Add(path);
                Debug.Log("[Capture] wrote " + path);
            }

            private void Quit(int code)
            {
                Debug.Log("[Capture] done: " + string.Join(", ", written));
                SessionState.SetBool(StartedKey, false);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(code);
            }
        }
    }
}
