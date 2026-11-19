namespace MTS.Core
{
    /// <summary>
    /// The single owner of the current run: committed inputs, the runner (state + results) and pause state.
    /// It survives scene loads (static instance) and replaces the old static fields and pause/play value stack.
    /// Call <see cref="Reset"/> on Restart / back to the input screen; <see cref="Begin"/> always resets first.
    /// </summary>
    public sealed class SimulationSession
    {
        public const double DefaultWindSpeed = 15.0;      // the atmosphere screen's wind slider starts at 15 m/s
        public const double DefaultWindDirection = 0.0;

        public static SimulationSession Instance { get; } = new SimulationSession();

        /// <summary>How new runs are started. Kept across <see cref="Reset"/> (it is a setting, not run state).</summary>
        public EntryMode Mode { get; set; } = EntryMode.ClassicNea;
        public double EntrySpeed { get; set; } = MeteorProperties.DefaultEntrySpeed;
        public double EntryAngleDegrees { get; set; } = MeteorProperties.DefaultEntryAngleDegrees;
        /// <summary>
        /// Material preset or composition mix for new runs (see <see cref="MeteorMaterialPresets"/>); null means
        /// "custom": build the material from the input screen's density, SHC and SCF. Kept across <see cref="Reset"/>.
        /// </summary>
        public MeteorMaterial MaterialPreset { get; set; }

        public MeteorProperties Properties { get; private set; }
        public SimulationRunner Runner { get; private set; }
        public bool IsPaused { get; set; }
        public double WindSpeed { get; private set; } = DefaultWindSpeed;
        public double WindDirection { get; private set; } = DefaultWindDirection;

        private ImpactAssessment impact;

        public bool HasRun => Runner != null;
        public SimulationStage Stage => Runner?.Stage ?? SimulationStage.NotStarted;
        public SimulationResult Result => Runner?.Result;

        /// <summary>Ground effects of the finished run (computed once); null while the run is still going.</summary>
        public ImpactAssessment Impact
        {
            get
            {
                if (impact == null && Runner != null && Runner.HasEnded)
                {
                    impact = ImpactModel.Assess(Runner.Result, Properties);
                }

                return impact;
            }
        }

        /// <summary>Clears all run state (inputs, state, results, pause, wind). Mode settings are kept.</summary>
        public void Reset()
        {
            Properties = null;
            Runner = null;
            impact = null;
            IsPaused = false;
            WindSpeed = DefaultWindSpeed;
            WindDirection = DefaultWindDirection;
        }

        /// <summary>Resets and starts a new run with the given inputs.</summary>
        public SimulationRunner Begin(MeteorProperties properties)
        {
            Reset();
            Properties = properties;
            Runner = new SimulationRunner(properties, WindSpeed, WindDirection);
            return Runner;
        }

        public void SetWind(double speed, double direction)
        {
            WindSpeed = speed;
            WindDirection = direction;
            Runner?.SetWind(speed, direction);
        }
    }
}
