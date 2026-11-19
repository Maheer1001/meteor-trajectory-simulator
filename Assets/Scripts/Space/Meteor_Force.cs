using System;
using System.Globalization;
using MTS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Space-stage adapter: advances the shared simulation while the meteor is above the atmosphere and shows its state.
/// All physics lives in MTS.Core; the run state lives in <see cref="SimulationSession"/>.
/// The scene contains more than one Meteor_Force (one on the UI root, one on the meteor prefab); only one of them
/// drives the simulation, every instance renders it.
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

    [Header("Start mode (temporary, until a toggle is added to the input screen)")]
    [Tooltip("Off: classic NEA release from rest. On: inbound path reaching the atmosphere at the entry speed and angle below.")]
    public bool realisticEntryMode = false;
    public float entrySpeed = 17000f;
    public float entryAngleDegrees = 45f;

    private static Meteor_Force driver;
    private static bool warnedAboutCap;

    void Start()
    {
        SimulationSession session = SimulationSession.Instance;
        if (!session.HasRun || session.Stage == SimulationStage.Ended)
        {
            if (realisticEntryMode)
            {
                session.Mode = EntryMode.RealisticEntry;
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
        if (runner == null || session.IsPaused)
        {
            return;
        }

        if (runner.Stage == SimulationStage.Space)
        {
            // The time-scale slider sets simulated seconds per physics tick; the step size itself stays fixed.
            double simSeconds = (slider != null ? slider.value : 1f) / 60.0;
            AdvanceReport report = runner.Advance(simSeconds);
            if (report.Capped && !warnedAboutCap)
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

            SceneManager.LoadScene(SimulationInputs.ResultsScene, LoadSceneMode.Single);
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
