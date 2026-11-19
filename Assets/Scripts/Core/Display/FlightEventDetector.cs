using System;
using System.Collections.Generic;

namespace MTS.Core.Display
{
    public enum FlightEvent
    {
        AtmosphericEntry,
        PeakHeating,
        PeakBrightness,
        Breakup,
        Airburst,
        Disintegration,
        Impact,
        Timeout,
        Error,
    }

    /// <summary>
    /// Turns a sequence of telemetry snapshots (plus the final result) into one-off flight events for the HUD's
    /// toasts. Every event fires at most once per run. Peaks are reported once the value has clearly fallen back
    /// from its maximum, so they fire shortly after the real peak.
    /// </summary>
    public sealed class FlightEventDetector
    {
        /// <summary>A temperature peak counts once the body is at least this hot (K).</summary>
        public const double PeakHeatingMinimum = 1000.0;
        /// <summary>...and has cooled to this fraction of the peak.</summary>
        public const double PeakHeatingDropFraction = 0.97;
        /// <summary>A brightness peak counts once the meteor was brighter than this absolute magnitude...</summary>
        public const double PeakBrightnessMaximumMagnitude = 0.0;
        /// <summary>...and has faded by this many magnitudes.</summary>
        public const double PeakBrightnessFade = 1.0;

        private readonly HashSet<FlightEvent> fired = new HashSet<FlightEvent>();
        private SimulationStage lastStage = SimulationStage.NotStarted;
        private bool lastBroken;
        private double maxTemperature;
        private double peakTemperatureAltitude;
        private double minMagnitude = double.MaxValue;
        private double peakMagnitudeAltitude;

        public bool HasFired(FlightEvent e) => fired.Contains(e);

        /// <summary>
        /// Feeds the latest snapshot. <paramref name="result"/> is the run's result once it has ended (else null).
        /// Returns the events detected by this update (usually none).
        /// </summary>
        public List<(FlightEvent Event, string Message)> Update(in FlightTelemetry t, SimulationResult result)
        {
            var events = new List<(FlightEvent, string)>();

            bool inAtmosphere = t.Stage == SimulationStage.Atmosphere ||
                                (t.Stage == SimulationStage.Ended && lastStage == SimulationStage.Space);
            if (inAtmosphere && lastStage != SimulationStage.Atmosphere)
            {
                Fire(events, FlightEvent.AtmosphericEntry, "Atmospheric entry at " + UnitFormat.Speed(t.Speed));
            }

            if (!lastBroken && t.Broken)
            {
                Fire(events, FlightEvent.Breakup, "Break-up at " + UnitFormat.Distance(t.Altitude) +
                                                  " (ram pressure " + UnitFormat.Pressure(t.DynamicPressure) + ")");
            }

            if (t.Temperature > maxTemperature)
            {
                maxTemperature = t.Temperature;
                peakTemperatureAltitude = t.Altitude;
            }
            else if (maxTemperature >= PeakHeatingMinimum && t.Temperature < PeakHeatingDropFraction * maxTemperature)
            {
                Fire(events, FlightEvent.PeakHeating, "Peak heating " + UnitFormat.Temperature(maxTemperature) +
                                                      " at " + UnitFormat.Distance(peakTemperatureAltitude));
            }

            if (t.AbsoluteMagnitude < minMagnitude)
            {
                minMagnitude = t.AbsoluteMagnitude;
                peakMagnitudeAltitude = t.Altitude;
            }
            else if (minMagnitude <= PeakBrightnessMaximumMagnitude && t.AbsoluteMagnitude >= minMagnitude + PeakBrightnessFade)
            {
                Fire(events, FlightEvent.PeakBrightness, "Peak brightness " + UnitFormat.Magnitude(minMagnitude) +
                                                         " at " + UnitFormat.Distance(peakMagnitudeAltitude));
            }

            if (result != null)
            {
                switch (result.Reason)
                {
                    case EndReason.Impact:
                        Fire(events, FlightEvent.Impact, ImpactMessage(result.MassAtEnd, result.ImpactSpeed));
                        break;
                    case EndReason.Airburst:
                        Fire(events, FlightEvent.Airburst, AirburstMessage(result.EndAltitude, result.BreakupEnergy));
                        break;
                    case EndReason.Disintegrated:
                        Fire(events, FlightEvent.Disintegration, "Disintegrated at " + UnitFormat.Distance(result.EndAltitude));
                        break;
                    case EndReason.Timeout:
                        Fire(events, FlightEvent.Timeout, "Simulated time limit reached");
                        break;
                    case EndReason.Error:
                        Fire(events, FlightEvent.Error, "Simulation error - see the log");
                        break;
                }
            }

            if (t.Stage != SimulationStage.Ended)
            {
                lastStage = t.Stage;
            }
            else if (inAtmosphere)
            {
                lastStage = SimulationStage.Atmosphere;
            }

            lastBroken = t.Broken;
            return events;
        }

        private void Fire(List<(FlightEvent, string)> events, FlightEvent e, string message)
        {
            if (fired.Add(e))
            {
                events.Add((e, message));
            }
        }

        /// <summary>Impact toast text: kinetic energy as TNT and the ground-impact speed, e.g. "59.8 Tt TNT at 20.00 km/s".</summary>
        public static string ImpactMessage(double mass, double speed)
        {
            double energy = 0.5 * mass * speed * speed;
            return energy > 0 ? UnitFormat.Energy(energy) + " at " + UnitFormat.Speed(speed) : "Ground impact at " + UnitFormat.Speed(speed);
        }

        /// <summary>Airburst toast text: altitude and (when known) the energy released, e.g. "At 27.3 km, 518 kt TNT".</summary>
        public static string AirburstMessage(double altitude, double energy)
        {
            string at = "At " + UnitFormat.Distance(altitude);
            return energy > 0 ? at + ", " + UnitFormat.Energy(energy) : at;
        }

        /// <summary>Short toast title for an event.</summary>
        public static string Title(FlightEvent e)
        {
            switch (e)
            {
                case FlightEvent.AtmosphericEntry: return "ATMOSPHERIC ENTRY";
                case FlightEvent.PeakHeating: return "PEAK HEATING";
                case FlightEvent.PeakBrightness: return "PEAK BRIGHTNESS";
                case FlightEvent.Breakup: return "BREAK-UP";
                case FlightEvent.Airburst: return "AIRBURST";
                case FlightEvent.Disintegration: return "DISINTEGRATED";
                case FlightEvent.Impact: return "IMPACT";
                case FlightEvent.Timeout: return "TIME LIMIT";
                case FlightEvent.Error: return "ERROR";
                default: throw new ArgumentOutOfRangeException(nameof(e), e, null);
            }
        }
    }
}
