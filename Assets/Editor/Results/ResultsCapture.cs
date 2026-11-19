using System;
using System.Collections.Generic;
using System.IO;
using MTS.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Results.Editor
{
    /// <summary>
    /// Renders the results screen to PNG files for review, in edit mode (no play mode needed):
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod MTS.Results.Editor.ResultsCapture.CaptureAll
    ///   [-resultsCaptureDir path]
    /// Run without -nographics. The overlay canvas is switched to Screen Space - Camera for the capture only
    /// (overlay canvases do not render into cameras); the scene file is not modified.
    /// </summary>
    public static class ResultsCapture
    {
        public static readonly Vector2Int Hd = new Vector2Int(1920, 1080);

        /// <summary>Named test scenarios used for screenshots (and handy for trying the screen in the editor).</summary>
        public static readonly Dictionary<string, Func<MeteorProperties>> Scenarios = new Dictionary<string, Func<MeteorProperties>>
        {
            // The scene's own preview: the NEA defaults (60 t iron released from rest).
            ["default"] = () => MeteorProperties.FromInputs(60000, 7800, 800, 0.5, 1, 1, 1),
            // A 60 m iron at 18 km/s, 60 degrees, entering over northern Arizona: a kilometre-wide crater.
            ["impact"] = () => AtEntry(IronSphere(60), MeteorMaterialPresets.Iron, 35.0, -112.2, 18000, 60, true),
            // Chelyabinsk-like: a 19 m stony body at 19 km/s, 18 degrees, entering east of the Urals.
            ["airburst"] = () => AtEntry(Sphere(19, 3300), MeteorMaterialPresets.OrdinaryChondrite, 54.6, 57.6, 19000, 18, true),
            // A 20 kg fragile meteoroid at 30 km/s without fragmentation: burns up high in the atmosphere.
            ["disintegrated"] = () => AtEntry(20, MeteorMaterial.CreateCustom(1000, 800, 0), 48.9, 2.3, 30000, 60, false),
            // The impact scenarios (ImpactScenarios.All) at their real locations, entering from the west.
            ["chelyabinsk"] = () => Scenario(ImpactScenarios.Chelyabinsk, 54.8, 59.0),
            ["meteor-crater"] = () => Scenario(ImpactScenarios.Barringer, 35.0, -112.4),
            ["chicxulub"] = () => Scenario(ImpactScenarios.Chicxulub, 20.6, -90.4),
            // A 1 km asteroid of mixed composition hitting the Pacific off California: a tsunami.
            ["ocean"] = () => Scenario(ImpactScenarios.RegionalKiller, 30.0, -140.0, MeteorComposition.Create(new[]
            {
                (MeteorMaterialPresets.OrdinaryChondriteId, 55.0), (MeteorMaterialPresets.IronId, 25.0),
                (MeteorMaterialPresets.CarbonaceousId, 15.0), (MeteorMaterialPresets.EnstatiteId, 5.0),
            })),
        };

        /// <summary>A scenario entering at the given point; also sets the session's composition for the report.</summary>
        public static MeteorProperties Scenario(ImpactScenario s, double lat, double lon, MeteorComposition composition = null)
        {
            composition = composition ?? s.Composition;
            SimulationSession.Instance.Composition = composition;
            return AtEntry(s.Mass, composition.Material, lat, lon, s.EntrySpeed, s.EntryAngleDegrees, true);
        }

        private static double Sphere(double diameter, double density) => density * Math.PI / 6.0 * Math.Pow(diameter, 3);

        private static double IronSphere(double diameter) => Sphere(diameter, 7800);

        /// <summary>Starts at the 120 km entry interface above the given point, heading east.</summary>
        public static MeteorProperties AtEntry(double mass, MeteorMaterial material, double lat, double lon,
            double speed, double angle, bool fragmentation)
        {
            double r = PhysicalConstants.EarthRadius + PhysicalConstants.AtmosphereEntryAltitude;
            double phi = lat * Math.PI / 180.0;
            double lambda = lon * Math.PI / 180.0;
            // Earth-fixed (Xe, Ye, Ze) = (x, -z, y) at t = 0 (see MTS.Core.Geo).
            double xe = Math.Cos(phi) * Math.Cos(lambda);
            double ye = Math.Cos(phi) * Math.Sin(lambda);
            double ze = Math.Sin(phi);
            var start = new Vec3d(xe * r, ze * r, -ye * r);
            return MeteorProperties.Create(mass, material, 0, start, EntryMode.Inbound, speed, angle, fragmentation);
        }

        public static void CaptureAll()
        {
            string dir = Argument("-resultsCaptureDir") ?? Path.Combine(Path.GetTempPath(), "claude", "mts-results");
            Directory.CreateDirectory(dir);
            try
            {
                string only = Argument("-resultsCaptureOnly");
                foreach (string scenario in new[] { "chelyabinsk", "meteor-crater", "chicxulub", "ocean", "disintegrated" })
                {
                    if (only == null || only == scenario) Capture(scenario, dir, Hd, true);
                }

                if (only == null || only == "sizes")
                {
                    Capture("chicxulub", dir, new Vector2Int(1366, 768), false);
                    Capture("chicxulub", dir, new Vector2Int(2560, 1440), false);
                    Capture("chicxulub", dir, new Vector2Int(3840, 2160), false);
                    Capture("chicxulub", dir, new Vector2Int(1366, 768), false, ResultsScreen.Tab.Effects);
                    Capture("ocean", dir, new Vector2Int(3840, 2160), false, ResultsScreen.Tab.Map);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Runs a scenario and saves PNGs of the summary (or every tab) at one resolution.</summary>
        public static void Capture(string scenario, string dir, Vector2Int size, bool allTabs, ResultsScreen.Tab only = ResultsScreen.Tab.Summary)
        {
            SimulationSession session = SimulationSession.Instance;
            session.Composition = null; // default; scenarios with a composition set their own
            session.Begin(Scenarios[scenario]()).RunToEnd();
            ImpactAssessment impact = session.Impact;
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Capture {0}: {1} '{2}' alt {3:F0} m, speed {4:F0} m/s, E {5:G4} kt, rings[0]={6:F0} m, crater {7:F0} m",
                scenario, session.Result.Reason, session.Result.Message, session.Result.EndAltitude, session.Result.ImpactSpeed,
                impact.EnergyKilotons, impact.BlastRadii[0], impact.FinalCraterDiameter));

            EditorSceneManager.OpenScene(ResultsSceneBuilder.ScenePath, OpenSceneMode.Single);
            ResultsScreen screen = UnityEngine.Object.FindObjectOfType<ResultsScreen>();
            Camera cam = Camera.main;
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;

            Canvas canvas = screen.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5;
            CanvasScaler scaler = screen.GetComponent<CanvasScaler>();
            scaler.enabled = false;
            scaler.enabled = true;
            Canvas.ForceUpdateCanvases();

            screen.Populate();
            LogEffects(scenario, screen.Report);
            screen.SetPlaceName(GeocodeApiKey.Load() == null ? "" : "(place lookup runs in play mode)");

            var tabs = allTabs
                ? (ResultsScreen.Tab[])Enum.GetValues(typeof(ResultsScreen.Tab))
                : new[] { only };
            foreach (ResultsScreen.Tab tab in tabs)
            {
                screen.ShowTab(tab);
                Canvas.ForceUpdateCanvases();
                string file = Path.Combine(dir, string.Format("{0}-{1}-{2}x{3}.png", scenario, tab.ToString().ToLowerInvariant(), size.x, size.y));
                Save(cam, rt, file);
                Debug.Log("Saved " + file);
            }

            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        private static void LogEffects(string scenario, OutcomeReport r)
        {
            ImpactEffects fx = r.Effects;
            var sb = new System.Text.StringBuilder();
            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                "Effects {0}: {1} | {2} | ocean={3} global={4} M={5:F2} fireball={6:F0} m crater={7:F0} m",
                scenario, r.Headline, r.Comparison, fx.IsOcean, fx.Global, fx.Magnitude, fx.FireballRadius, fx.FinalCraterDiameter);
            foreach (ThermalThreshold t in fx.Thermal) sb.AppendFormat(" | {0} {1:F0} m", t.Name, t.Radius);
            foreach (EffectRadius e in fx.Seismic) sb.AppendFormat(" | shaking {0} {1:F0} m", e.Name, e.Radius);
            foreach (EffectRadius e in fx.Ejecta) sb.AppendFormat(" | {0} {1:F0} m", e.Name, e.Radius);
            if (fx.HasTsunami)
            {
                sb.AppendFormat(" | water cavity {0:F0} m rim wave {1:F0} m seafloor v {2:F0}", fx.WaterCraterDiameter, fx.RimWaveAmplitude, fx.SeafloorSpeed);
                foreach (EffectRadius e in fx.Tsunami) sb.AppendFormat(" | {0} {1:F0} m", e.Name, e.Radius);
            }

            foreach (RingRow ring in r.Rings) sb.AppendFormat(" | {0} {1:F0} m", ResultsFormat.Psi(ring.Overpressure), ring.Radius);
            Debug.Log(sb.ToString());
        }

        private static void Save(Camera cam, RenderTexture rt, string file)
        {
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
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
