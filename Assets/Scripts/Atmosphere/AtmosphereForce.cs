using System;
using System.Globalization;
using MTS.Core;
using MTS.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Atmosphere-stage adapter: reads the time and wind sliders, advances the shared simulation and shows its state.
/// Every end of the run (impact, airburst, ablated away, error, timeout) goes to the results scene.
/// All physics lives in MTS.Core; the run state lives in <see cref="SimulationSession"/>.
/// When the runtime HUD is attached (see MTS.Hud.HudBootstrap) the time warp and wind come from the HUD via
/// <see cref="SimulationClock"/>; the legacy UI fields below are optional and may be null or hidden.
/// </summary>
public class Atmosphere_Force : MonoBehaviour
{
    // Metres per Unity unit in the atmosphere scene.
    private const double MetresPerUnit = 1000.0;

    public TMP_Text Timestamp;
    public TMP_Text DistanceToEarth;
    public TMP_Text MeteorVelocity;
    public TMP_Text MeteorAcceleration;
    public TMP_Text MeteorMass;
    public TMP_Text MeteorRadius;
    public TMP_Text AtmosphereTemperature;
    public TMP_Text AtmospherePressure;
    public UnityEngine.UI.Slider slider;   // time scale
    public UnityEngine.UI.Slider slider2;  // wind speed (m/s)
    public UnityEngine.UI.Slider slider3;  // wind direction (fraction of pi from local east towards north)
    public string sceneName1;
    public string sceneName2;

    [Tooltip("Real seconds to wait after the run ends before opening the results scene (lets the HUD show the end event).")]
    public float endTransitionDelay = 1.5f;

    private static bool warnedAboutCap;
    private float leaveAt = -1f;

    private void Start()
    {
        SimulationSession session = SimulationSession.Instance;
        if (!session.HasRun)
        {
            SimulationInputs.BeginNewRun(); // scene opened directly (e.g. in the editor)
        }

        Windspeed();
        Time.timeScale = session.IsPaused ? 0f : 1f;
        UpdateView();
    }

    void FixedUpdate()
    {
        SimulationSession session = SimulationSession.Instance;
        SimulationRunner runner = session.Runner;
        if (runner == null || session.IsPaused || SimulationClock.HoldForLaunch)
        {
            return;
        }

        Windspeed();
        if (!runner.HasEnded)
        {
            double simSeconds = SimulationClock.TickSeconds((slider != null ? slider.value : 1f) / 60.0);
            AdvanceReport report = runner.Advance(simSeconds, SimulationRunner.DefaultMaxStepsPerAdvance, false);
            SimulationClock.Report(report);
            if (report.Capped && !warnedAboutCap && !SimulationClock.ExternalControl)
            {
                warnedAboutCap = true;
                Debug.LogWarning("Simulation is running slower than the selected time scale (step cap reached).");
            }
        }

        if (runner.HasEnded && leaveAt < 0f)
        {
            if (runner.Result.Reason == EndReason.Error)
            {
                Debug.LogError("Simulation stopped: " + runner.Result.Message);
            }

            leaveAt = Time.unscaledTime + Mathf.Max(0f, endTransitionDelay);
        }
    }

    void Update()
    {
        UpdateView();
        if (leaveAt >= 0f && Time.unscaledTime >= leaveAt)
        {
            leaveAt = -1f;
            string results = string.IsNullOrEmpty(sceneName2) ? SimulationInputs.ResultsScene : sceneName2;
            SceneManager.LoadScene(results, LoadSceneMode.Single);
        }
    }

    // Bound to the wind sliders' On Value Changed events; also applied every physics tick.
    // Ignored while the HUD's wind compass owns the wind.
    public void Windspeed()
    {
        if (SimulationClock.ExternalWind)
        {
            return;
        }

        double speed = slider2 != null ? slider2.value : SimulationSession.DefaultWindSpeed;
        double direction = slider3 != null ? slider3.value : SimulationSession.DefaultWindDirection;
        SimulationSession.Instance.SetWind(speed, direction);
    }

    public void Wind_Direction()
    {
        Windspeed();
    }

    private void UpdateView()
    {
        SimulationRunner runner = SimulationSession.Instance.Runner;
        if (runner == null)
        {
            return;
        }

        MeteorState s = runner.State;
        AtmosphereSample air = runner.CurrentAir;
        CultureInfo ci = CultureInfo.InvariantCulture;
        if (Timestamp != null) Timestamp.text = Math.Round(s.Time).ToString(ci);
        if (DistanceToEarth != null) DistanceToEarth.text = Math.Max(0.0, s.Altitude).ToString("F3", ci);
        if (MeteorVelocity != null) MeteorVelocity.text = s.Speed.ToString("F3", ci);
        if (MeteorAcceleration != null) MeteorAcceleration.text = runner.CurrentAcceleration.Magnitude.ToString("F3", ci);
        if (MeteorMass != null) MeteorMass.text = s.Mass.ToString("F3", ci);
        if (MeteorRadius != null) MeteorRadius.text = s.EquivalentRadius(runner.Properties.Material.Density).ToString("F3", ci);
        if (AtmosphereTemperature != null) AtmosphereTemperature.text = air.Temperature.ToString("F3", ci);
        if (AtmospherePressure != null) AtmospherePressure.text = air.Pressure.ToString("F3", ci);
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
