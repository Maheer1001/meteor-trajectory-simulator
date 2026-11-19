using System;
using System.Collections.Generic;

namespace MTS.Core.Display
{
    /// <summary>
    /// Discrete time-warp levels expressed as real-time multipliers (simulated seconds per real second).
    /// The adapters advance the runner once per physics tick, so the simulated time requested per tick is
    /// multiplier * fixedDeltaTime. The runner's step size never changes with the warp; it only takes more steps.
    /// </summary>
    public sealed class TimeWarpLadder
    {
        /// <summary>Space stage: up to x100,000 (the coast from the start point can take days).</summary>
        public static readonly double[] SpaceLevels =
            { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000 };

        /// <summary>Atmosphere stage: slow motion to x50 (the flight lasts seconds to minutes).</summary>
        public static readonly double[] AtmosphereLevels = { 0.1, 0.25, 0.5, 1, 2, 5, 10, 20, 50 };

        private readonly double[] levels;

        public TimeWarpLadder(IReadOnlyList<double> levels, double initial)
        {
            if (levels == null || levels.Count == 0)
            {
                throw new ArgumentException("At least one level is required.", nameof(levels));
            }

            this.levels = new double[levels.Count];
            for (int i = 0; i < levels.Count; i++)
            {
                if (!(levels[i] > 0) || (i > 0 && levels[i] <= levels[i - 1]))
                {
                    throw new ArgumentException("Levels must be positive and strictly increasing.", nameof(levels));
                }

                this.levels[i] = levels[i];
            }

            Index = NearestIndex(initial);
        }

        public int Index { get; private set; }
        public int Count => levels.Length;
        public double Multiplier => levels[Index];
        public bool AtMax => Index == levels.Length - 1;
        public bool AtMin => Index == 0;

        public double this[int i] => levels[i];

        public bool Up()
        {
            if (AtMax) return false;
            Index++;
            return true;
        }

        public bool Down()
        {
            if (AtMin) return false;
            Index--;
            return true;
        }

        public void Set(int index) => Index = Math.Max(0, Math.Min(levels.Length - 1, index));

        /// <summary>Selects the level closest (in log space) to the given multiplier.</summary>
        public void SetNearest(double multiplier) => Index = NearestIndex(multiplier);

        /// <summary>Simulated seconds to request per physics tick for the current level.</summary>
        public double SimSecondsPerTick(double fixedDeltaTime) => Multiplier * fixedDeltaTime;

        /// <summary>Real-time multiplier achieved when simSeconds of simulated time pass in realSeconds of wall time.</summary>
        public static double Achieved(double simSeconds, double realSeconds) =>
            realSeconds > 0 ? simSeconds / realSeconds : 0.0;

        private int NearestIndex(double multiplier)
        {
            if (!(multiplier > 0)) return 0;
            double target = Math.Log(multiplier);
            int best = 0;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < levels.Length; i++)
            {
                double d = Math.Abs(Math.Log(levels[i]) - target);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            return best;
        }
    }
}
