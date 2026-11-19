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
    /// Usage (no -nographics; -batchmode is fine):
    ///   Unity.exe -projectPath P -executeMethod MTS.Visuals.EditorTools.VisualsCapture.Run -mtsCapture CASE -mtsCaptureOut C:/dir
    /// Cases:
    ///   space        inbound 60 t from far away: the three space cameras, high and low
    ///   space-near   from rest at (1,1,1) (643 km): the three cameras at the start and near the air
    ///   space-big    Chicxulub-size body inbound: the three cameras
    ///   materials    space close-ups of the eight materials and a four-material mix
    ///   stony, iron, small                 atmosphere runs (250 t stony, 1000 t iron impact, 60 t stony from rest)
    ///   chelyabinsk, tunguska, barringer, onekm, chicxulub   atmosphere runs of the impact scenarios
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

        private static bool IsSpaceCase(string which) => which.StartsWith("space") || which == "materials";

        public static void Run()
        {
            string which = SceneInspector.GetArg("-mtsCapture", "space");
            string scene = IsSpaceCase(which) ? "Assets/Scenes/SpaceSimulation.unity" : "Assets/Scenes/AtmosphereReached.unity";
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
            private double startAlt;
            private double endAlt;
            private SimulationRunner runner;
            private readonly List<string> written = new List<string>();
            private int brokenFrames = -1;
            private int endFrames = -1;
            private readonly HashSet<string> done = new HashSet<string>();
            private int materialIndex;

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
                        if (which == "materials") MaterialShots();
                        else if (IsSpaceCase(which)) SpaceShots();
                        else AtmosphereShots();
                    }

                    if (frame > 8000)
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

            private static readonly string[] MaterialOrder =
            {
                MeteorMaterialPresets.OrdinaryChondriteId, MeteorMaterialPresets.CarbonaceousId, MeteorMaterialPresets.EnstatiteId,
                MeteorMaterialPresets.BasalticAchondriteId, MeteorMaterialPresets.StonyIronId, MeteorMaterialPresets.IronId,
                MeteorMaterialPresets.CometaryId, MeteorMaterialPresets.RubblePileId, "mix",
            };

            private static MeteorComposition MaterialComposition(string id) => id == "mix"
                ? MeteorComposition.Create(new[]
                {
                    (MeteorMaterialPresets.OrdinaryChondriteId, 45.0), (MeteorMaterialPresets.IronId, 25.0),
                    (MeteorMaterialPresets.CarbonaceousId, 20.0), (MeteorMaterialPresets.StonyIronId, 10.0),
                })
                : MeteorComposition.Single(id);

            private MeteorProperties PropsFor(string c)
            {
                SimulationSession session = SimulationSession.Instance;
                ImpactScenario scenario = null;
                switch (c)
                {
                    case "chelyabinsk": scenario = ImpactScenarios.Chelyabinsk; break;
                    case "tunguska": scenario = ImpactScenarios.Tunguska; break;
                    case "barringer": scenario = ImpactScenarios.Barringer; break;
                    case "onekm": scenario = ImpactScenarios.RegionalKiller; break;
                    case "chicxulub":
                    case "space-big": scenario = ImpactScenarios.Chicxulub; break;
                }

                if (scenario != null)
                {
                    scenario.ApplyTo(session);
                    return MeteorProperties.FromComposition(scenario.Mass, scenario.Composition, 2.2, 1.6, -1.4, scenario.Mode, scenario.EntrySpeed, scenario.EntryAngleDegrees);
                }

                switch (c)
                {
                    case "space":
                        session.Composition = MeteorComposition.Default;
                        return MeteorProperties.FromComposition(60000, MeteorComposition.Default, 2.2, 1.6, -1.4, EntryMode.Inbound, 17000, 40);
                    case "space-near":
                    case "small":
                        session.Composition = MeteorComposition.Default;
                        return MeteorProperties.FromComposition(60000, MeteorComposition.Default, 1, 1, 1);
                    case "iron":
                    {
                        // a fragmentation-free iron body: reaches the ground and makes a crater
                        MeteorComposition iron = MeteorComposition.Single(MeteorMaterialPresets.IronId);
                        session.Composition = iron;
                        return MeteorProperties.FromComposition(1.0e6, iron, 2.2, 1.6, -1.4, EntryMode.Inbound, 15000, 55, enableFragmentation: false);
                    }
                    default:
                        session.Composition = MeteorComposition.Default;
                        return MeteorProperties.FromComposition(250000, MeteorComposition.Default, 2.2, 1.6, -1.4, EntryMode.Inbound, 18000, 38);
                }
            }

            private void StartRun()
            {
                SimulationSession session = SimulationSession.Instance;
                if (which == "materials")
                {
                    BeginMaterial(0);
                    return;
                }

                MeteorProperties props = PropsFor(which);
                runner = session.Begin(props);
                session.IsPaused = true; // the adapters skip their FixedUpdate; this tool advances the run
                Time.timeScale = 1f;
                startAlt = runner.State.Altitude;
                if (!IsSpaceCase(which))
                {
                    while (runner.State.Altitude > 121000 && !runner.HasEnded)
                    {
                        runner.Step(runner.StepSizeFor(runner.State.Altitude));
                    }

                    // where will it end? (a throwaway copy of the run)
                    var probe = new SimulationRunner(props, session.WindSpeed, session.WindDirection);
                    probe.RunToEnd();
                    endAlt = Math.Max(0.0, probe.State.Altitude);
                    startAlt = runner.State.Altitude;
                    Debug.Log("[Capture] predicted end " + probe.Result.Reason + " at " + endAlt.ToString("F0") + " m");
                }

                Debug.Log("[Capture] run started, altitude " + runner.State.Altitude);
            }

            private void BeginMaterial(int i)
            {
                SimulationSession session = SimulationSession.Instance;
                MeteorComposition c = MaterialComposition(MaterialOrder[i]);
                session.Composition = c;
                runner = session.Begin(MeteorProperties.FromComposition(60000, c, 1, 1, 1));
                session.IsPaused = true;
                Time.timeScale = 1f;
            }

            private void StepToAltitude(double alt, int maxSteps)
            {
                for (int i = 0; i < maxSteps && runner.State.Altitude > alt && !runner.HasEnded; i++)
                {
                    runner.Step(runner.StepSizeFor(runner.State.Altitude));
                }
            }

            private void MaterialShots()
            {
                var sv = UnityEngine.Object.FindObjectOfType<SpaceVisuals>();
                if (sv == null) return;
                if (wait > 0) { wait--; return; }
                if (stage == 0)
                {
                    sv.SetMode(0);
                    sv.SetOrbit(-20f, 10f, 0.62f);
                    sv.SnapCamera();
                    wait = 4;
                    stage = 1;
                    return;
                }

                Capture("material_" + MaterialOrder[materialIndex]);
                materialIndex++;
                if (materialIndex >= MaterialOrder.Length)
                {
                    Quit(0);
                    return;
                }

                BeginMaterial(materialIndex);
                stage = 0;
                wait = 2;
            }

            private void SpaceShots()
            {
                var sv = UnityEngine.Object.FindObjectOfType<SpaceVisuals>();
                if (sv == null) return;
                if (wait > 0) { wait--; return; }

                bool near = which == "space-near";
                double firstAlt = near ? startAlt : 2500000;
                double lowAlt = near ? 160000 : 400000;
                string p = which.Replace("-", "_");
                switch (stage)
                {
                    case 0:
                    {
                        // fly part of the way in over 60 frames so the flown path is recorded
                        double target = startAlt + (firstAlt - startAlt) * Math.Min(1.0, (frame - 3) / 60.0);
                        StepToAltitude(target, 200000);
                        if (runner.State.Altitude <= firstAlt + 1) { stage++; sv.SetMode(1); sv.SnapCamera(); wait = 3; }
                        break;
                    }
                    case 1: Capture(p + "_wide"); sv.SetMode(2); sv.SnapCamera(); wait = 3; stage++; break;
                    case 2: Capture(p + "_overview"); sv.SetMode(0); sv.SetOrbit(0f, 0f, 1f); sv.SnapCamera(); wait = 3; stage++; break;
                    case 3: Capture(p + "_closeup"); stage++; break;
                    case 4:
                    {
                        double target = firstAlt + (lowAlt - firstAlt) * Math.Min(1.0, (frame - 3 - 70) / 60.0);
                        StepToAltitude(Math.Max(target, lowAlt), 400000);
                        if (runner.State.Altitude <= lowAlt + 1 || runner.HasEnded)
                        {
                            sv.SetOrbit(0f, 0f, 1f);
                            sv.SnapCamera();
                            wait = 3;
                            stage++;
                        }

                        break;
                    }
                    case 5: Capture(p + "_closeup_low"); sv.SetMode(1); sv.SnapCamera(); wait = 3; stage++; break;
                    case 6: Capture(p + "_wide_low"); sv.SetMode(2); sv.SnapCamera(); wait = 3; stage++; break;
                    case 7: Capture(p + "_overview_low"); Quit(0); break;
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
                        + " meteor=" + av.MeteorWorldPosition + " cam=" + av.CameraPosition + " vcam=" + (brain != null && brain.ActiveVirtualCamera != null ? brain.ActiveVirtualCamera.Name : "none"));
                }

                // shots at fractions of the descent from 121 km to the predicted end
                double span = Math.Max(1000.0, startAlt - endAlt);
                Shot(av, "atmo_chase_high", alt < endAlt + span * 0.72, 0);
                Shot(av, "atmo_chase", alt < endAlt + span * 0.32, 0);
                Shot(av, "atmo_side", alt < endAlt + span * 0.22, 2);
                Shot(av, "atmo_ground_observer", alt < endAlt + span * 0.12, 1);
                Shot(av, "atmo_chase_late", alt < endAlt + span * 0.05, 0);

                if (t.Broken && brokenFrames < 0) brokenFrames = 0;
                if (brokenFrames >= 0) brokenFrames++;
                Shot(av, "atmo_breakup_chase", brokenFrames == 8, 0);
                Shot(av, "atmo_breakup_side", brokenFrames == 30, 2);

                if (runner.HasEnded && endFrames < 0)
                {
                    endFrames = 0;
                    ImpactAssessment ia = SimulationSession.Instance.Impact;
                    Debug.Log("[Capture] ended: " + runner.Result.Reason + " " + runner.Result.Message + (ia != null
                        ? " E=" + ia.EnergyKilotons.ToString("G3") + " kt crater=" + ia.FinalCraterDiameter.ToString("F0") + " burst=" + ia.BurstAltitude.ToString("F0")
                        : ""));
                }

                if (endFrames >= 0) endFrames++;
                Shot(av, "end_1_flash", endFrames == 6, -1);
                Shot(av, "end_2", endFrames == 45, -1);
                Shot(av, "end_3", endFrames == 140, -1);
                Shot(av, "end_4_late", endFrames == 330, -1);
                if (endFrames > 335) Quit(0);
            }

            private string pendingName;
            private int pendingDelay;

            private void Shot(AtmosphereVisuals av, string name, bool when, int mode)
            {
                if (done.Contains(name)) return;
                if (pendingName == name)
                {
                    if (pendingDelay-- > 0) return;
                    Capture(which + "_" + name);
                    done.Add(name);
                    pendingName = null;
                    return;
                }

                if (!when || pendingName != null) return;
                if (mode >= 0) av.SetMode(mode);
                pendingName = name;
                pendingDelay = mode >= 0 ? 2 : 0; // let the camera cut and the per-frame globals settle
            }

            private void Capture(string name)
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
