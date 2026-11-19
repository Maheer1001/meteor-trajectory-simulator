using System;
using System.Collections.Generic;

namespace MTS.Core
{
    public enum EndReason
    {
        /// <summary>Still running.</summary>
        None,
        /// <summary>Reached the ground (intact body or unspread fragment swarm).</summary>
        Impact,
        /// <summary>The fragment cloud spread to the pancake limit in the air and released its energy there.</summary>
        Airburst,
        /// <summary>Ablated away (mass at most 1 kg or equivalent radius at most 0.1 m).</summary>
        Disintegrated,
        /// <summary>A non-finite value appeared; the offending state is in the message.</summary>
        Error,
        /// <summary>Simulated time limit reached without an end event.</summary>
        Timeout,
    }

    public enum SimulationStage
    {
        NotStarted,
        Space,
        Atmosphere,
        Ended,
    }

    /// <summary>
    /// Time series recorded during a run, decimated so it stays bounded: samples are taken every
    /// <see cref="Interval"/> seconds and, when the buffer is full, every other sample is dropped and the
    /// interval doubled.
    /// </summary>
    public sealed class TimeSeries
    {
        public const int Capacity = 1024;

        public List<double> Time { get; } = new List<double>();
        public List<double> Distance { get; } = new List<double>();
        public List<double> Altitude { get; } = new List<double>();
        public List<double> Speed { get; } = new List<double>();
        public List<double> Mass { get; } = new List<double>();

        public double Interval { get; private set; } = 1.0;
        private double nextSampleTime;

        public int Count => Time.Count;

        public void Record(in MeteorState s, bool force = false)
        {
            if (!force && s.Time < nextSampleTime)
            {
                return;
            }

            if (Count > 0 && Time[Count - 1] == s.Time)
            {
                return;
            }

            Time.Add(s.Time);
            Distance.Add(s.DistanceTravelled);
            Altitude.Add(s.Altitude);
            Speed.Add(s.Speed);
            Mass.Add(s.Mass);
            nextSampleTime = s.Time + Interval;

            if (Count >= Capacity)
            {
                Decimate();
            }
        }

        private void Decimate()
        {
            // keep even-indexed samples (always including the first), then double the interval
            void Halve(List<double> list)
            {
                int w = 0;
                for (int i = 0; i < list.Count; i += 2)
                {
                    list[w++] = list[i];
                }

                list.RemoveRange(w, list.Count - w);
            }

            Halve(Time);
            Halve(Distance);
            Halve(Altitude);
            Halve(Speed);
            Halve(Mass);
            Interval *= 2.0;
            nextSampleTime = Time[Count - 1] + Interval;
        }
    }

    /// <summary>Outcome of a run.</summary>
    public sealed class SimulationResult
    {
        public EndReason Reason { get; internal set; }
        public string Message { get; internal set; } = "";
        /// <summary>State at the end event (for an impact, interpolated to the surface).</summary>
        public MeteorState FinalState { get; internal set; }
        public double EndTime => FinalState.Time;
        public double EndAltitude => FinalState.Altitude;
        public double EndSpeed => FinalState.Speed;
        /// <summary>Speed relative to the air at the end event (m/s); for an impact this is the ground-impact speed.</summary>
        public double ImpactSpeed { get; internal set; }
        /// <summary>Angle of the path below the local horizontal at the end event (degrees).</summary>
        public double ImpactAngleDegrees { get; internal set; }
        public double MassAtEnd => FinalState.Mass;
        /// <summary>Altitude at which break-up started (m), or NaN if the body never broke up.</summary>
        public double BreakupAltitude { get; internal set; } = double.NaN;
        /// <summary>Kinetic energy relative to the air at break-up (J), 0 if the body never broke up.</summary>
        public double BreakupEnergy { get; internal set; }
        public double PeakTemperature { get; internal set; }
        public double PeakDeceleration { get; internal set; }
        public double AtmosphereEntryTime { get; internal set; } = double.NaN;
        public double AtmosphereEntrySpeed { get; internal set; } = double.NaN;
        public TimeSeries Series { get; internal set; }
    }
}
