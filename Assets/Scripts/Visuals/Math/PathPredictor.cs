using System;
using System.Collections.Generic;
using MTS.Core;

namespace MTS.Visuals
{
    /// <summary>
    /// Predicts the rest of a trajectory by running a private <see cref="SimulationRunner"/> from a copy of the
    /// current state. It never touches the session's runner. Work is done in slices (<see cref="Advance"/>) so a
    /// MonoBehaviour can spread it over frames.
    /// </summary>
    public sealed class PathPredictor
    {
        private readonly SimulationRunner runner;
        private readonly double sampleSpacing;
        private readonly int maxPoints;
        private Vec3d lastSample;

        /// <summary>Sampled inertial positions (m), starting at the given state.</summary>
        public List<Vec3d> Points { get; } = new List<Vec3d>();
        /// <summary>Sample times (s, simulation clock) matching <see cref="Points"/>.</summary>
        public List<double> Times { get; } = new List<double>();
        public bool IsDone { get; private set; }
        /// <summary>The prediction's end, once done (null while running or if it could not start).</summary>
        public SimulationResult Result => IsDone ? runner?.Result : null;
        /// <summary>Altitude (m) of the first break-up in the prediction, NaN if none (yet).</summary>
        public double BreakupAltitude { get; private set; } = double.NaN;
        /// <summary>Inertial position of the first break-up in the prediction.</summary>
        public Vec3d BreakupPosition { get; private set; }

        /// <param name="properties">The run's properties (material, shape, fragmentation).</param>
        /// <param name="state">Current state to continue from.</param>
        /// <param name="windSpeed">Wind speed (m/s).</param>
        /// <param name="windDirection">Wind azimuth fraction (see <see cref="Geo.WindVector"/>).</param>
        /// <param name="sampleSpacing">Minimum distance (m) between recorded points.</param>
        /// <param name="maxPoints">Upper bound on recorded points (spacing doubles when reached).</param>
        public PathPredictor(MeteorProperties properties, MeteorState state, double windSpeed, double windDirection,
            double sampleSpacing, int maxPoints = 4000)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            this.sampleSpacing = Math.Max(1.0, sampleSpacing);
            this.maxPoints = Math.Max(8, maxPoints);
            Points.Add(state.Position);
            Times.Add(state.Time);
            lastSample = state.Position;

            if (state.Position.Magnitude <= PhysicalConstants.EarthRadius || !(state.Mass > 0))
            {
                IsDone = true;
                return;
            }

            MeteorProperties copy = MeteorProperties.Create(state.Mass, properties.Material, properties.Scf, state.Position,
                properties.Mode, properties.EntrySpeed, properties.EntryAngleDegrees, properties.EnableFragmentation,
                properties.PancakeFactor, state.Velocity);
            runner = new SimulationRunner(copy, windSpeed, windDirection);
            timeOffset = state.Time;
        }

        private readonly double timeOffset;

        /// <summary>Takes up to <paramref name="maxSteps"/> integration steps. Returns true when the prediction has ended.</summary>
        public bool Advance(int maxSteps)
        {
            if (IsDone)
            {
                return true;
            }

            double spacing2 = sampleSpacing * sampleSpacing * spacingScale * spacingScale;
            for (int i = 0; i < maxSteps && !runner.HasEnded; i++)
            {
                runner.Step(runner.StepSizeFor(runner.State.Altitude));
                MeteorState s = runner.State;
                if (s.Broken && double.IsNaN(BreakupAltitude))
                {
                    BreakupAltitude = s.Altitude;
                    BreakupPosition = s.Position;
                }

                if ((s.Position - lastSample).SqrMagnitude >= spacing2)
                {
                    Record(s);
                    spacing2 = sampleSpacing * sampleSpacing * spacingScale * spacingScale;
                }
            }

            if (runner.HasEnded)
            {
                Record(runner.Result.FinalState);
                IsDone = true;
            }

            return IsDone;
        }

        /// <summary>Runs the whole prediction (bounded by <paramref name="maxSteps"/> steps in total).</summary>
        public void RunToEnd(int maxSteps = 2000000)
        {
            Advance(maxSteps);
            IsDone = true;
        }

        private double spacingScale = 1.0;

        private void Record(MeteorState s)
        {
            Points.Add(s.Position);
            Times.Add(s.Time + timeOffset);
            lastSample = s.Position;
            if (Points.Count >= maxPoints)
            {
                // Keep every other point and double the spacing, so long paths stay bounded.
                for (int i = Points.Count - 2; i > 0; i -= 2)
                {
                    Points.RemoveAt(i);
                    Times.RemoveAt(i);
                }

                spacingScale *= 2.0;
            }
        }
    }
}
