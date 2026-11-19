using System;
using System.Globalization;
using MTS.Core;
using MTS.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Space-stage adapter: advances the shared simulation while the meteor is above the atmosphere and shows its state.
/// All physics lives in MTS.Core; the run state lives in <see cref="SimulationSession"/>.
/// The scene contains more than one Meteor_Force (one on the UI root, one on the meteor prefab); only one of them
/// drives the simulation, every instance renders it.
/// When the runtime HUD is attached (see MTS.Hud.HudBootstrap) the time warp, launch hold and readouts come from the
/// HUD via <see cref="SimulationClock"/>; the legacy UI fields below are optional and may be null or hidden.
/// </summary>
public class Meteor_Force : MonoBehaviour
{
    // Metres per Unity unit in the space scene (one input coordinate unit = 4050 km).
    private const double MetresPerUnit = MeteorProperties.MetresPerCoordinateUnit;

    // UI Elements
    public TMP_Text Timestamp;
    public TMP_Text DistanceRemaining;
    public TMP_Text MeteorVelocity;
    public TMP_Text MeteorAcceleration;
    public UnityEngine.UI.Slider slider;
    public string sceneName;

    [Tooltip("Real seconds to wait after the run ends before opening the results scene (lets the HUD show the end event).")]
    public float endTransitionDelay = 1.5f;

    [Header("Start mode override (testing only: normally set on the input screen and the launch panel)")]
    [Tooltip("Off: from rest (released at rest at the start point, falling under gravity). On: inbound entry, arriving at the 120 km entry interface at the entry speed and angle below.")]
    public bool realisticEntryMode = false;
    public float entrySpeed = 17000f;
    public float entryAngleDegrees = 45f;

    private static Meteor_Force driver;
    private static bool warnedAboutCap;
    private float leaveAt = -1f;

    void Start()
    {
        SimulationSession session = SimulationSession.Instance;
        if (!session.HasRun || session.Stage == SimulationStage.Ended)
        {
            if (realisticEntryMode)
            {
                session.Mode = EntryMode.Inbound;
                session.EntrySpeed = entrySpeed;
                session.EntryAngleDegrees = entryAngleDegrees;
            }

            SimulationInputs.BeginNewRun();
        }

        Time.timeScale = session.IsPaused ? 0f : 1f;
        UpdateView();
    }

    void OnDestroy()
    {
        if (driver == this)
        {
            driver = null;
        }
    }

    void FixedUpdate()
    {
        if (driver == null)
        {
            driver = this;
        }

        if (driver != this)
        {
            return;
        }

        SimulationSession session = SimulationSession.Instance;
        SimulationRunner runner = session.Runner;
        if (runner == null || session.IsPaused || SimulationClock.HoldForLaunch)
        {
            return;
        }

        if (runner.Stage == SimulationStage.Space)
        {
            // Simulated seconds per physics tick: from the HUD's time warp, else from the legacy slider.
            // The step size itself stays fixed; a faster warp only takes more steps.
            double simSeconds = SimulationClock.TickSeconds((slider != null ? slider.value : 1f) / 60.0);
            AdvanceReport report = runner.Advance(simSeconds);
            SimulationClock.Report(report);
            if (report.Capped && !warnedAboutCap && !SimulationClock.ExternalControl)
            {
                warnedAboutCap = true;
                Debug.LogWarning("Simulation is running slower than the selected time scale (step cap reached).");
            }
        }

        LeaveIfStageChanged(runner);
    }

    void Update()
    {
        UpdateView();
        if (leaveAt >= 0f && Time.unscaledTime >= leaveAt)
        {
            leaveAt = -1f;
            SceneManager.LoadScene(SimulationInputs.ResultsScene, LoadSceneMode.Single);
        }
    }

    private void LeaveIfStageChanged(SimulationRunner runner)
    {
        if (runner.Stage == SimulationStage.Atmosphere)
        {
            SceneManager.LoadScene(string.IsNullOrEmpty(sceneName) ? "AtmosphereReached" : sceneName, LoadSceneMode.Single);
        }
        else if (runner.Stage == SimulationStage.Ended)
        {
            if (runner.Result.Reason == EndReason.Error)
            {
                Debug.LogError("Simulation stopped: " + runner.Result.Message);
            }

            if (leaveAt < 0f)
            {
                leaveAt = Time.unscaledTime + Mathf.Max(0f, endTransitionDelay);
            }
        }
    }

    private void UpdateView()
    {
        SimulationRunner runner = SimulationSession.Instance.Runner;
        if (runner == null)
        {
            return;
        }

        MeteorState s = runner.State;
        CultureInfo ci = CultureInfo.InvariantCulture;
        if (Timestamp != null) Timestamp.text = Math.Round(s.Time).ToString(ci);
        if (DistanceRemaining != null) DistanceRemaining.text = Math.Max(0.0, s.Altitude - PhysicalConstants.AtmosphereEntryAltitude).ToString("F2", ci);
        if (MeteorVelocity != null) MeteorVelocity.text = s.Speed.ToString("F9", ci);
        if (MeteorAcceleration != null) MeteorAcceleration.text = runner.CurrentAcceleration.Magnitude.ToString("F9", ci);
        transform.position = SimulationInputs.ToVector3(s.Position, MetresPerUnit);
    }

    // Pause / play only stop the runner; the state stays in the session.
    public void PauseButtonPressed()
    {
        SimulationSession.Instance.IsPaused = true;
        Time.timeScale = 0;
    }

    public void PlayButtonPressed()
    {
        SimulationSession.Instance.IsPaused = false;
        Time.timeScale = 1;
    }

    // Bound to the time slider's On Value Changed event; the slider is read every physics tick.
    public void TimeStepAlteration()
    {
    }
}
