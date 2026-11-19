using System;
using System.Globalization;
using MTS.Core;
using UnityEngine;

/// <summary>
/// Bridges the input screen's static values and the simulation session.
/// The session is the single owner of run state; this class only snapshots inputs and applies start-up options.
/// </summary>
public static class SimulationInputs
{
    public const string ResultsScene = "EarthCollision";

    private static bool commandLineApplied;

    /// <summary>Snapshots the current input-screen values into validated simulation properties.</summary>
    public static MeteorProperties Snapshot(SimulationSession session)
    {
        MeteorProperties properties = MeteorProperties.FromComposition(
            MassRangeInput.Mass, session.Composition, Xcord.X, Ycord.Y, Zcord.Z,
            session.Mode, session.EntrySpeed, session.EntryAngleDegrees);

        if (properties.Substitutions.Count > 0)
        {
            Debug.Log("Simulation inputs replaced with defaults: " + string.Join(", ", properties.Substitutions));
        }

        return properties;
    }

    /// <summary>Starts a new run from the current inputs (resets the session first).</summary>
    public static SimulationRunner BeginNewRun()
    {
        SimulationSession session = SimulationSession.Instance;
        ApplyCommandLineOnce(session);
        SimulationRunner runner = session.Begin(Snapshot(session));
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(runner.SetupNote))
        {
            Debug.LogWarning(runner.SetupNote);
        }

        return runner;
    }

    /// <summary>
    /// Temporary, non-UI way to choose the start mode until a toggle is added to the screens:
    /// -mtsMode rest|inbound, -mtsScenario <id>, -mtsEntrySpeed m/s, -mtsEntryAngle degrees.
    /// </summary>
    public static void ApplyCommandLineOnce(SimulationSession session)
    {
        if (commandLineApplied)
        {
            return;
        }

        commandLineApplied = true;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            string value = args[i + 1];
            switch (args[i].ToLowerInvariant())
            {
                case "-mtsmode":
                    session.Mode = value.Equals("inbound", StringComparison.OrdinalIgnoreCase) ||
                                   value.Equals("realistic", StringComparison.OrdinalIgnoreCase)
                        ? EntryMode.Inbound
                        : EntryMode.FromRest;
                    break;
                case "-mtsscenario":
                    ImpactScenario scenario = ImpactScenarios.ById(value);
                    if (scenario != null)
                    {
                        scenario.ApplyTo(session);
                        MassRangeInput.Mass = (float)scenario.Mass;
                    }

                    break;
                case "-mtsentryspeed":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double speed) && speed > 0)
                    {
                        session.EntrySpeed = speed;
                    }

                    break;
                case "-mtsentryangle":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double angle) && angle > 0 && angle <= 90)
                    {
                        session.EntryAngleDegrees = angle;
                    }

                    break;
            }
        }
    }

    public static Vector3 ToVector3(Vec3d v, double scale) =>
        new Vector3((float)(v.X / scale), (float)(v.Y / scale), (float)(v.Z / scale));
}
