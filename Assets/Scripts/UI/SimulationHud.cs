using System.Globalization;
using MTS.Core;
using MTS.Core.Display;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Hud
{
    public enum HudStage
    {
        Space,
        Atmosphere,
    }

    /// <summary>
    /// The simulation HUD of one stage scene: flight readouts, stage indicator, time warp and pause, keyboard
    /// shortcuts, launch settings (Space) or wind compass (Atmosphere), toasts and tooltips.
    /// Reads <see cref="SimulationSession"/> every frame; drives the adapters through <see cref="SimulationClock"/>.
    /// Built by <see cref="HudFactory"/>; attached at runtime by <see cref="HudBootstrap"/>.
    /// </summary>
    public sealed class SimulationHud : MonoBehaviour
    {
        private const float TextRefreshInterval = 0.1f;
        private const float RateWindow = 0.5f;

        public HudStage stage;

        [Header("Root")]
        public Canvas canvas;
        public CanvasGroup content;
        public GameObject hiddenHint;

        [Header("Top bar")]
        public Image[] stageChips = new Image[3];
        public TMP_Text[] stageChipTexts = new TMP_Text[3];
        public TMP_Text elapsedValue;
        public TMP_Text modeValue;

        [Header("Flight data")]
        public TMP_Text altitudeValue;
        public RectTransform altitudeBarFill;
        public TMP_Text altitudeBarCaption;
        public GameObject atmosphereDistanceRow;
        public TMP_Text atmosphereDistanceValue;
        public TMP_Text speedValue;
        public TMP_Text machValue;
        public TMP_Text pressureValue;
        public TMP_Text decelerationValue;
        public TMP_Text temperatureValue;
        public TMP_Text massValue;
        public TMP_Text massLossValue;
        public TMP_Text brightnessValue;
        public TMP_Text diameterValue;
        public TMP_Text structureValue;

        [Header("Time control")]
        public Button warpDownButton;
        public Button warpUpButton;
        public Button pauseButton;
        public TMP_Text pauseLabel;
        public TMP_Text warpValue;
        public TMP_Text warpStatus;
        public Button menuButton;
        public TMP_Text legendText;

        [Header("Parts")]
        public LaunchPanel launchPanel;
        public WindCompass windCompass;
        public ToastFeed toasts;
        public HudTooltip tooltip;
        public MainMenuButton menu;

        /// <summary>The HUD of the active simulation scene (null when none).</summary>
        public static SimulationHud Current { get; private set; }

        // Warp level per stage survives scene loads (e.g. a trip to the pause menu).
        private static double spaceWarp = 1000;
        private static double atmosphereWarp = 1;

        private TimeWarpLadder ladder;
        private float nextTextRefresh;
        private float rateWindowStart;
        private double rateWindowSimStart;
        private double achievedRate = double.NaN;
        private string cameraName = "";
        private int cameraIndex = -1;

        public TimeWarpLadder Ladder => ladder;

        private void Awake()
        {
            Current = this;
            ladder = stage == HudStage.Space
                ? new TimeWarpLadder(TimeWarpLadder.SpaceLevels, spaceWarp)
                : new TimeWarpLadder(TimeWarpLadder.AtmosphereLevels, atmosphereWarp);
            ApplyWarp();

            SimulationSession session = SimulationSession.Instance;
            if (stage == HudStage.Space)
            {
                // A fresh run (or one not yet launched) waits on the launch panel; a resumed run keeps going.
                SimulationRunner runner = session.Runner;
                SimulationClock.HoldForLaunch = runner == null || runner.HasEnded ||
                                                (runner.Stage == SimulationStage.Space && runner.State.Time <= 0);
            }
            else
            {
                SimulationClock.HoldForLaunch = false;
                SimulationClock.ExternalWind = true;
            }

            if (menu == null) menu = gameObject.AddComponent<MainMenuButton>();
        }

        private void OnEnable()
        {
            SimulationSignals.CameraChanged += OnCameraChanged;
        }

        private void OnDisable()
        {
            SimulationSignals.CameraChanged -= OnCameraChanged;
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                SimulationClock.Release();
                SimulationSignals.PublishSafeViewport(new Rect(0f, 0f, 1f, 1f));
            }
        }

        private void Start()
        {
            if (warpDownButton != null) warpDownButton.onClick.AddListener(WarpDown);
            if (warpUpButton != null) warpUpButton.onClick.AddListener(WarpUp);
            if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
            if (menuButton != null) menuButton.onClick.AddListener(OpenMenu);
            if (launchPanel != null)
            {
                launchPanel.LaunchRequested += Launch;
                launchPanel.SetExpanded(SimulationClock.HoldForLaunch);
            }

            UpdateLegend();
            RefreshAll();
        }

        private void Update()
        {
            HandleKeys();
            ApplyWarp();

            SimulationRunner runner = SimulationSession.Instance.Runner;
            HudRunTracker.Track(runner);
            MeasureRate(runner);

            if (Time.unscaledTime >= nextTextRefresh)
            {
                nextTextRefresh = Time.unscaledTime + TextRefreshInterval;
                RefreshAll();
            }
        }

        private void LateUpdate()
        {
            LayoutToastsAndSafeArea();

            // A clicked button stays "selected" and the input module would click it again on Space (Submit),
            // which clashes with the Space shortcut; buttons here never need keyboard focus.
            UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<Button>() != null)
            {
                es.SetSelectedGameObject(null);
            }
        }

        // ---------------------------------------------------------------- controls

        public void WarpUp()
        {
            ladder.Up();
            RememberWarp();
            RefreshTimeBar();
        }

        public void WarpDown()
        {
            ladder.Down();
            RememberWarp();
            RefreshTimeBar();
        }

        /// <summary>Pause/play, or launch while the run is waiting on the launch panel.</summary>
        public void TogglePause()
        {
            if (stage == HudStage.Space && SimulationClock.HoldForLaunch)
            {
                Launch();
                return;
            }

            SimulationSession session = SimulationSession.Instance;
            session.IsPaused = !session.IsPaused;
            Time.timeScale = session.IsPaused ? 0f : 1f;
            RefreshTimeBar();
        }

        /// <summary>Starts a new run with the launch panel's settings (they are already in the session).</summary>
        public void Launch()
        {
            SimulationInputs.BeginNewRun();
            SimulationClock.HoldForLaunch = false;
            SimulationSession.Instance.IsPaused = false;
            Time.timeScale = 1f;
            LaunchPanel.MarkApplied();
            HudToasts.Clear();
            SimulationSession session = SimulationSession.Instance;
            HudToasts.Post("LAUNCH", session.Mode == EntryMode.Inbound
                ? "Inbound entry at " + UnitFormat.Speed(session.EntrySpeed) + ", " + UnitFormat.Angle(session.EntryAngleDegrees) + " below horizontal"
                : "From rest: falling under gravity", HudTheme.Launch);
            if (launchPanel != null) launchPanel.SetExpanded(false);
            RefreshAll();
        }

        /// <summary>Existing in-simulation pause menu (MainMenu2), exactly like the old Back button.</summary>
        public void OpenMenu()
        {
            if (menu == null) return;
            if (stage == HudStage.Space) menu.MainMenuS1("MainMenu2");
            else menu.MainMenu2("MainMenu2");
        }

        public void SetHudVisible(bool visible)
        {
            if (content != null)
            {
                content.alpha = visible ? 1f : 0f;
                content.blocksRaycasts = visible;
                content.interactable = visible;
            }

            if (hiddenHint != null) hiddenHint.SetActive(!visible);
        }

        public bool HudVisible => content == null || content.alpha > 0.5f;

        // ---- Screen space left for the 3D view ----

        private RectTransform flightPanel, sidePanel, stageBar, timeBar, toastArea;
        private bool layoutFound;
        private readonly Vector3[] corners = new Vector3[4];

        /// <summary>Gap kept between the HUD panels and the framed subject (px at 1080p).</summary>
        public const float SafeAreaGap = 12f;

        private void FindLayout()
        {
            layoutFound = true;
            if (content == null) return;
            Transform c = content.transform;
            flightPanel = c.Find("FlightPanel") as RectTransform;
            sidePanel = (c.Find("LaunchPanel") ?? c.Find("WindPanel")) as RectTransform;
            stageBar = c.Find("StageBar") as RectTransform;
            timeBar = c.Find("TimeBar") as RectTransform;
            toastArea = c.Find("Toasts") as RectTransform;
        }

        /// <summary>
        /// Keeps the event toasts in the right-hand column under the side panel (out of the view's centre) and publishes
        /// the screen area between the panels, which the cameras use to frame the meteor.
        /// </summary>
        private void LayoutToastsAndSafeArea()
        {
            if (!layoutFound) FindLayout();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            float gap = SafeAreaGap * scale;

            if (toastArea != null && sidePanel != null)
            {
                sidePanel.GetWorldCorners(corners);
                toastArea.pivot = new Vector2(1f, 1f);
                toastArea.sizeDelta = new Vector2(sidePanel.rect.width, toastArea.sizeDelta.y);
                toastArea.position = corners[3] + Vector3.down * gap;
            }

            Rect safe = new Rect(0f, 0f, 1f, 1f);
            float w = Screen.width, h = Screen.height;
            if (HudVisible && w > 0 && h > 0)
            {
                float x0 = 0f, x1 = w, y0 = 0f, y1 = h;
                if (Corners(flightPanel)) x0 = Mathf.Max(x0, corners[2].x + gap);
                if (Corners(sidePanel)) x1 = Mathf.Min(x1, corners[0].x - gap);
                if (Corners(stageBar)) y1 = Mathf.Min(y1, corners[0].y - gap);
                if (Corners(timeBar)) y0 = Mathf.Max(y0, corners[1].y + gap);
                if (x1 - x0 > 0.3f * w && y1 - y0 > 0.3f * h)
                {
                    safe = new Rect(x0 / w, y0 / h, (x1 - x0) / w, (y1 - y0) / h);
                }
            }

            SimulationSignals.PublishSafeViewport(safe);
        }

        private bool Corners(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(corners);
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Camera uiCamera = canvas.worldCamera;
                for (int i = 0; i < 4; i++) corners[i] = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
            }

            return true; // in pixels (Screen Space - Overlay world space is already screen pixels)
        }

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus)) WarpUp();
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Underscore)) WarpDown();
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SimulationSignals.RequestCamera(0);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SimulationSignals.RequestCamera(1);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SimulationSignals.RequestCamera(2);
            if (Input.GetKeyDown(KeyCode.H)) SetHudVisible(!HudVisible);
            if (Input.GetKeyDown(KeyCode.Escape)) OpenMenu();
        }

        private void OnCameraChanged(int index, string name)
        {
            bool changed = index != cameraIndex || (name ?? "") != cameraName;
            cameraName = name ?? "";
            cameraIndex = index;
            if (changed) HudToasts.Post("CAMERA " + (index + 1), cameraName, HudTheme.Info);
            UpdateLegend();
        }

        private void ApplyWarp() => SimulationClock.TakeControl(ladder.SimSecondsPerTick(Time.fixedDeltaTime));

        private void RememberWarp()
        {
            if (stage == HudStage.Space) spaceWarp = ladder.Multiplier;
            else atmosphereWarp = ladder.Multiplier;
        }

        private void MeasureRate(SimulationRunner runner)
        {
            if (runner == null) return;
            float now = Time.unscaledTime;
            if (now - rateWindowStart < RateWindow) return;
            double sim = runner.State.Time;
            if (rateWindowStart > 0f && sim >= rateWindowSimStart)
            {
                achievedRate = TimeWarpLadder.Achieved(sim - rateWindowSimStart, now - rateWindowStart);
            }

            rateWindowStart = now;
            rateWindowSimStart = sim;
        }

        // ---------------------------------------------------------------- view

        /// <summary>Refreshes every readout now (normally throttled to 10 Hz).</summary>
        public void RefreshAll()
        {
            SimulationSession session = SimulationSession.Instance;
            SimulationRunner runner = session.Runner;
            RefreshTopBar(session, runner);
            RefreshFlightData(runner);
            RefreshTimeBar();
            if (launchPanel != null) launchPanel.Refresh();
            if (windCompass != null) windCompass.Refresh();
        }

        private void RefreshTopBar(SimulationSession session, SimulationRunner runner)
        {
            int active = runner == null ? 0
                : runner.HasEnded ? 2
                : runner.Stage == SimulationStage.Atmosphere ? 1
                : 0;
            if (runner == null && stage == HudStage.Atmosphere) active = 1;
            for (int i = 0; i < stageChips.Length; i++)
            {
                bool done = i < active;
                bool now = i == active;
                if (stageChips[i] != null) stageChips[i].color = now ? HudTheme.Header : done ? HudTheme.ButtonOff : new Color(0, 0, 0, 0.35f);
                if (stageChipTexts[i] != null) stageChipTexts[i].color = now ? HudTheme.Value : done ? HudTheme.Label : HudTheme.Dim;
            }

            if (elapsedValue != null) elapsedValue.text = "T+ " + (runner != null ? UnitFormat.Duration(runner.State.Time) : UnitFormat.Missing);
            if (modeValue != null)
            {
                if (stage == HudStage.Space && SimulationClock.HoldForLaunch)
                {
                    // Before launch, describe what the launch will use (the session settings).
                    Set(modeValue, "Next: " + RunSummary(session.Mode, session.EntrySpeed, session.EntryAngleDegrees,
                        LaunchPanel.InputMass, LaunchPanel.ShortComposition(session.Composition)));
                }
                else
                {
                    MeteorProperties p = runner?.Properties;
                    Set(modeValue, p == null ? "" : RunSummary(p.Mode, p.EntrySpeed, p.EntryAngleDegrees, p.Mass, MaterialSummary(session, p.Material)));
                }
            }
        }

        /// <summary>One line for the stage bar, e.g. "Inbound entry 20.00 km/s at 60°  |  1,250 Gt Carbonaceous chondrite".</summary>
        public static string RunSummary(EntryMode mode, double entrySpeed, double entryAngle, double mass, string material)
        {
            string m = mode == EntryMode.Inbound
                ? LaunchPanel.ModeName(mode) + " " + UnitFormat.Speed(entrySpeed) + " at " + UnitFormat.Angle(entryAngle)
                : LaunchPanel.ModeName(mode);
            return m + "   |   " + UnitFormat.Mass(mass) + " " + material;
        }

        private static string MaterialSummary(SimulationSession session, MeteorMaterial material)
        {
            if (material == null) return "";
            if (session.Composition != null && ReferenceEquals(session.Composition.Material, material)) return LaunchPanel.ShortComposition(session.Composition);
            return material.Name.Contains(" + ") ? "mixed composition" : LaunchPanel.ShortName(material);
        }

        private void RefreshFlightData(SimulationRunner runner)
        {
            if (runner == null)
            {
                return;
            }

            FlightTelemetry t = runner.Telemetry;
            double entry = PhysicalConstants.AtmosphereEntryAltitude;
            Set(altitudeValue, UnitFormat.Distance(t.Altitude));

            float progress;
            if (t.Altitude > entry && stage == HudStage.Space)
            {
                double start = System.Math.Max(entry + 1.0, HudRunTracker.StartAltitude);
                progress = (float)((start - t.Altitude) / (start - entry));
                Set(altitudeBarCaption, "Start " + UnitFormat.Distance(start) + "  >  atmosphere 120 km");
            }
            else
            {
                progress = (float)((entry - t.Altitude) / entry);
                Set(altitudeBarCaption, "Atmosphere 120 km  >  ground");
            }

            if (altitudeBarFill != null) altitudeBarFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

            if (atmosphereDistanceRow != null) atmosphereDistanceRow.SetActive(t.Altitude > entry);
            Set(atmosphereDistanceValue, UnitFormat.Distance(System.Math.Max(0.0, t.Altitude - entry)));
            Set(speedValue, UnitFormat.Speed(t.Speed));
            Set(machValue, t.Mach > 0 ? UnitFormat.Mach(t.Mach) : "-- (vacuum)");
            Set(pressureValue, UnitFormat.Pressure(t.DynamicPressure));
            Set(decelerationValue, UnitFormat.GForce(t.DecelerationG));
            Set(temperatureValue, UnitFormat.Temperature(t.Temperature));
            double startMass = HudRunTracker.StartMass > 0 ? HudRunTracker.StartMass : t.Mass;
            Set(massValue, UnitFormat.Mass(t.Mass) + Secondary(UnitFormat.Percent(startMass > 0 ? t.Mass / startMass : 1.0)));
            Set(massLossValue, UnitFormat.MassRate(t.MassLossRate));
            Set(brightnessValue, t.AbsoluteMagnitude >= FlightTelemetry.DarkMagnitude
                ? "not glowing"
                : UnitFormat.Power(t.Luminosity) + Secondary(UnitFormat.Magnitude(t.AbsoluteMagnitude)));
            Set(diameterValue, UnitFormat.Length(t.Diameter));
            if (structureValue != null)
            {
                structureValue.text = t.Broken ? "Broken: fragment cloud" : "Intact";
                structureValue.color = t.Broken ? HudTheme.Warning : HudTheme.Value;
            }
        }

        private void RefreshTimeBar()
        {
            SimulationSession session = SimulationSession.Instance;
            bool waiting = stage == HudStage.Space && SimulationClock.HoldForLaunch;
            if (pauseLabel != null) pauseLabel.text = waiting ? "LAUNCH" : session.IsPaused ? "PLAY" : "PAUSE";
            if (pauseButton != null && pauseButton.targetGraphic != null)
            {
                pauseButton.targetGraphic.color = waiting ? HudTheme.Launch : HudTheme.Button;
            }

            if (warpValue != null) warpValue.text = "real " + UnitFormat.Warp(ladder.Multiplier);
            if (warpDownButton != null) warpDownButton.interactable = !ladder.AtMin;
            if (warpUpButton != null) warpUpButton.interactable = !ladder.AtMax;
            if (warpStatus == null) return;

            SimulationRunner runner = session.Runner;
            if (waiting)
            {
                warpStatus.text = "Waiting for launch";
                warpStatus.color = HudTheme.Dim;
            }
            else if (session.IsPaused)
            {
                warpStatus.text = "PAUSED";
                warpStatus.color = HudTheme.Warning;
            }
            else if (runner != null && runner.HasEnded)
            {
                warpStatus.text = "Run finished";
                warpStatus.color = HudTheme.Label;
            }
            else if (SimulationClock.CappedTicks > 2)
            {
                warpStatus.text = "STEP CAP - actual " + (double.IsNaN(achievedRate) ? UnitFormat.Missing : UnitFormat.Warp(achievedRate));
                warpStatus.color = HudTheme.Warning;
            }
            else
            {
                double step = runner != null ? runner.StepSizeFor(runner.State.Altitude) : 0;
                warpStatus.text = "1 s real = " + UnitFormat.Duration(ladder.Multiplier).Replace(".0 s", " s") +
                                  " sim  (step " + step.ToString("0.###", CultureInfo.InvariantCulture) + " s)";
                warpStatus.color = HudTheme.Dim;
            }
        }

        private void UpdateLegend()
        {
            if (legendText == null) return;
            // Camera names come from the active scene's visuals and vary in length: shrink to fit the bar only as a
            // last resort. The active camera is shown in white instead of repeating its name.
            legendText.enableWordWrapping = false;
            legendText.enableAutoSizing = true;
            legendText.fontSizeMax = HudTheme.LegendSize;
            legendText.fontSizeMin = 16;
            string[] names = SimulationSignals.CameraNames;
            string cams;
            if (names != null && names.Length > 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                for (int i = 0; i < names.Length && i < 3; i++)
                {
                    bool active = i == cameraIndex || (cameraIndex < 0 && cameraName.Length > 0 && names[i] == cameraName);
                    parts.Add(active ? "<color=#FFFFFF><b>" + (i + 1) + "</b> " + names[i] + "</color>" : "<b>" + (i + 1) + "</b> " + names[i]);
                }

                cams = string.Join("  ", parts);
            }
            else
            {
                cams = "<b>1-3</b> cameras";
            }

            legendText.text = "<b>SPACE</b> " + (stage == HudStage.Space ? "launch / pause" : "pause") +
                              "    <b>+ / -</b> time warp    " + cams + "    <b>H</b> hide HUD    <b>ESC</b> menu";
        }

        /// <summary>Smaller, dimmer trailing part of a value ("1,250 Gt  97.3 %").</summary>
        private static string Secondary(string text) => "  <size=80%><color=#FFC9C0>" + text + "</color></size>";

        private static void Set(TMP_Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }
    }
}
