using System;

namespace MTS.Core
{
    /// <summary>What happened during one <see cref="SimulationRunner.Advance"/> call.</summary>
    public readonly struct AdvanceReport
    {
        public readonly int StepsTaken;
        /// <summary>True if the per-call step cap was hit; the remaining requested time was dropped.</summary>
        public readonly bool Capped;
        /// <summary>Simulated seconds that were requested but dropped because of the cap.</summary>
        public readonly double DroppedSeconds;
        public readonly bool StageChanged;

        public AdvanceReport(int stepsTaken, bool capped, double droppedSeconds, bool stageChanged)
        {
            StepsTaken = stepsTaken;
            Capped = capped;
            DroppedSeconds = droppedSeconds;
            StageChanged = stageChanged;
        }
    }

    /// <summary>
    /// Advances the meteor with fixed time steps. The step size depends only on altitude (never on the UI time
    /// scale): a faster time scale takes more sub-steps per frame instead of bigger steps. Every step is checked
    /// for NaN/Infinity; a bad value ends the run with <see cref="EndReason.Error"/> instead of hanging.
    /// Stage changes (space, atmosphere, ended) are decided here from the state.
    /// </summary>
    public sealed class SimulationRunner
    {
        public const int DefaultMaxStepsPerAdvance = 20000;
        /// <summary>Default simulated-time limit (s): 30 days.</summary>
        public const double DefaultMaxSimulatedTime = 30.0 * 86400.0;
        public const double DisintegrationMass = 1.0;
        public const double DisintegrationRadius = 0.1;
        /// <summary>Wind is applied below this altitude (top of the layered USSA76 model).</summary>
        public const double WindCeiling = 86000.0;
        /// <summary>Above this altitude the air is treated as vacuum (density there is below 1e-14 kg/m^3).</summary>
        public const double VacuumAltitude = 1.0e6;

        private MeteorState state;
        private double pendingTime;
        private readonly double[] stepSizes = { 1.0, 0.25, 0.05, 0.01 };

        public MeteorProperties Properties { get; }
        public MeteorState State => state;
        public SimulationStage Stage { get; private set; }
        public SimulationResult Result { get; private set; }
        public bool HasEnded => Stage == SimulationStage.Ended;
        public TimeSeries Series { get; } = new TimeSeries();
        public double WindSpeed { get; private set; }
        public double WindAzimuthFraction { get; private set; }
        public long TotalSteps { get; private set; }
        /// <summary>Simulated-time limit (s); the run ends with <see cref="EndReason.Timeout"/> when it is reached.</summary>
        public double MaxSimulatedTime { get; set; } = DefaultMaxSimulatedTime;
        /// <summary>Notes about the initial conditions (for example a fallback in realistic mode).</summary>
        public string SetupNote { get; private set; } = "";

        private double peakTemperature;
        private double peakDeceleration;
        private double breakupAltitude = double.NaN;
        private double breakupEnergy;
        private double entryTime = double.NaN;
        private double entrySpeed = double.NaN;
        private double lastMassLossRate;
        private double lastDeceleration;

        public SimulationRunner(MeteorProperties properties, double windSpeed = 0.0, double windAzimuthFraction = 0.0)
        {
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
            SetWind(windSpeed, windAzimuthFraction);

            state = new MeteorState
            {
                Time = 0,
                Position = properties.StartPosition,
                Velocity = InitialVelocity(properties),
                Mass = properties.Mass,
                Temperature = Heating.SpaceEquilibriumTemperature,
            };
            peakTemperature = state.Temperature;

            Stage = state.Altitude > PhysicalConstants.AtmosphereEntryAltitude ? SimulationStage.Space : SimulationStage.Atmosphere;
            if (Stage == SimulationStage.Atmosphere)
            {
                entryTime = 0;
                entrySpeed = state.Speed;
            }

            Series.Record(state, true);
        }

        public void SetWind(double speed, double azimuthFraction)
        {
            WindSpeed = double.IsNaN(speed) || double.IsInfinity(speed) ? 0.0 : Math.Max(0.0, speed);
            WindAzimuthFraction = double.IsNaN(azimuthFraction) || double.IsInfinity(azimuthFraction) ? 0.0 : azimuthFraction;
        }

        /// <summary>Fixed step size for a given altitude (s).</summary>
        public double StepSizeFor(double altitude)
        {
            if (altitude > 1.0e7) return stepSizes[0];
            if (altitude > 1.0e6) return stepSizes[1];
            if (altitude > 2.0e5) return stepSizes[2];
            return stepSizes[3];
        }

        public AtmosphereSample CurrentAir => Atmosphere.Sample(Math.Max(0.0, state.Altitude));

        /// <summary>Total acceleration (gravity + drag) of the current state (m/s^2).</summary>
        public Vec3d CurrentAcceleration => Derivative(state.Time, Kinematics(state)).Velocity;

        public double CurrentGravity => Gravity.Magnitude(state.Position.Magnitude);

        /// <summary>Derived quantities of the current state for display (Mach, ram pressure, luminosity, ...).</summary>
        public FlightTelemetry Telemetry
        {
            get
            {
                double altitude = state.Altitude;
                double airSpeed = state.Speed;
                double mach = 0, q = 0, rho = 0;
                if (altitude < VacuumAltitude)
                {
                    AtmosphereSample air = Atmosphere.Sample(Math.Max(0.0, altitude));
                    airSpeed = (state.Velocity - WindAt(state.Position, altitude)).Magnitude;
                    rho = air.Density;
                    q = 0.5 * rho * airSpeed * airSpeed;
                    mach = air.SpeedOfSound > 0 ? airSpeed / air.SpeedOfSound : 0;
                }

                return new FlightTelemetry(Stage, state.Time, altitude, state.Speed, airSpeed, mach, q,
                    lastDeceleration / PhysicalConstants.StandardGravity, state.Mass, lastMassLossRate,
                    state.Temperature, state.Broken, FlowDiameter(state), rho);
            }
        }

        /// <summary>
        /// Advances by simSeconds of simulated time using fixed sub-steps. Leftover time smaller than one step is
        /// carried to the next call, so the sequence of steps does not depend on how time is handed in.
        /// </summary>
        public AdvanceReport Advance(double simSeconds, int maxSteps = DefaultMaxStepsPerAdvance, bool stopOnStageChange = true)
        {
            if (HasEnded || !(simSeconds > 0))
            {
                return new AdvanceReport(0, false, 0, false);
            }

            pendingTime += simSeconds;
            int steps = 0;
            SimulationStage startStage = Stage;
            while (!HasEnded)
            {
                double dt = StepSizeFor(state.Altitude);
                if (pendingTime < dt)
                {
                    break;
                }

                if (steps >= maxSteps)
                {
                    double dropped = pendingTime;
                    pendingTime = 0;
                    return new AdvanceReport(steps, true, dropped, Stage != startStage);
                }

                Step(dt);
                pendingTime -= dt;
                steps++;
                if (stopOnStageChange && Stage != startStage)
                {
                    // Drop the rest of this call's time: it was asked for at the old stage's time warp, and carrying
                    // it over would replay it at once in the next stage (a whole atmospheric flight in one tick).
                    pendingTime = 0;
                    break;
                }
            }

            if (HasEnded)
            {
                pendingTime = 0;
            }

            return new AdvanceReport(steps, false, 0, Stage != startStage);
        }

        /// <summary>Runs until an end event (used by tests and tools).</summary>
        public SimulationResult RunToEnd()
        {
            while (!HasEnded)
            {
                Step(StepSizeFor(state.Altitude));
            }

            return Result;
        }

        /// <summary>Takes one step of size dt.</summary>
        public void Step(double dt)
        {
            if (HasEnded)
            {
                return;
            }

            MeteorState prev = state;
            try
            {
                StepCore(dt);
            }
            catch (Exception e)
            {
                End(EndReason.Error, "Exception during step: " + e.Message + " | previous state " + prev);
                return;
            }

            if (!state.IsFinite)
            {
                MeteorState bad = state;
                state = prev;
                End(EndReason.Error, "Non-finite value after step: " + bad + " | previous state " + prev);
            }
        }

        private void StepCore(double dt)
        {
            MeteorState prev = state;
            MeteorMaterial mat = Properties.Material;

            KinematicState next = Rk4Integrator.Step(Kinematics(prev), prev.Time, dt, Derivative);
            state.Position = next.Position;
            state.Velocity = next.Velocity;
            if (prev.Broken)
            {
                state.PancakeDiameter = Math.Max(prev.PancakeDiameter, next.PancakeDiameter);
                state.PancakeRate = Math.Max(0.0, next.PancakeRate);
            }

            state.Time = prev.Time + dt;
            state.DistanceTravelled = prev.DistanceTravelled + (next.Position - prev.Position).Magnitude;

            // Non-gravitational deceleration (for the peak-g statistic)
            Vec3d gMid = Gravity.Acceleration((prev.Position + next.Position) * 0.5);
            double decel = ((next.Velocity - prev.Velocity) / dt - gMid).Magnitude;
            peakDeceleration = Math.Max(peakDeceleration, decel);
            lastDeceleration = decel;
            lastMassLossRate = 0.0;

            double altitude = state.Altitude;

            // Heating and ablation (operator split after the RK4 step; implicit in temperature)
            if (altitude < VacuumAltitude)
            {
                AtmosphereSample air = Atmosphere.Sample(Math.Max(0.0, altitude));
                Vec3d vRel = state.Velocity - WindAt(state.Position, altitude);
                double speed = vRel.Magnitude;
                double envT = altitude <= Atmosphere.TopOfLayeredModel ? air.Temperature : PhysicalConstants.CmbTemperature;
                HeatingStep h = Heating.Step(mat, state.Mass, state.Temperature, CrossSection(state), air.Density, speed, envT, dt);
                state.Temperature = h.Temperature;
                state.Mass = Math.Max(0.0, state.Mass - h.MassLost);
                lastMassLossRate = (prev.Mass - state.Mass) / dt;

                // Aerodynamic break-up: ram pressure rho v^2 exceeds the bulk strength (Collins et al. 2005, Eq. 10)
                if (Properties.EnableFragmentation && !state.Broken && air.Density * speed * speed >= mat.Strength)
                {
                    state.Broken = true;
                    state.BreakupDiameter = 2.0 * state.EquivalentRadius(mat.Density);
                    state.PancakeDiameter = state.BreakupDiameter;
                    state.PancakeRate = 0.0;
                    breakupAltitude = altitude;
                    breakupEnergy = 0.5 * state.Mass * speed * speed;
                }
            }
            else
            {
                HeatingStep h = Heating.Step(mat, state.Mass, state.Temperature, CrossSection(state), 0.0, 0.0, PhysicalConstants.CmbTemperature, dt);
                state.Temperature = h.Temperature;
            }

            peakTemperature = Math.Max(peakTemperature, state.Temperature);

            if (!state.IsFinite)
            {
                return; // Step() turns this into an Error
            }

            if (Stage == SimulationStage.Space && altitude <= PhysicalConstants.AtmosphereEntryAltitude)
            {
                Stage = SimulationStage.Atmosphere;
                entryTime = state.Time;
                entrySpeed = state.Speed;
            }

            if (altitude <= 0.0)
            {
                // Interpolate to the surface within this step.
                double h0 = prev.Altitude;
                double f = h0 > altitude ? h0 / (h0 - altitude) : 1.0;
                f = Math.Max(0.0, Math.Min(1.0, f));
                MeteorState hit = state;
                hit.Time = prev.Time + f * dt;
                hit.Position = Vec3d.Lerp(prev.Position, state.Position, f);
                hit.Velocity = Vec3d.Lerp(prev.Velocity, state.Velocity, f);
                hit.DistanceTravelled = prev.DistanceTravelled + (hit.Position - prev.Position).Magnitude;
                state = hit;
                End(EndReason.Impact, "Reached the ground.");
                return;
            }

            if (state.Mass <= DisintegrationMass || state.EquivalentRadius(mat.Density) <= DisintegrationRadius)
            {
                End(EndReason.Disintegrated, "Ablated away at " + (altitude / 1000.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " km.");
                return;
            }

            if (state.Broken && state.PancakeDiameter >= Properties.PancakeFactor * state.BreakupDiameter)
            {
                End(EndReason.Airburst, "Fragment cloud reached " + Properties.PancakeFactor + "x its break-up diameter.");
                return;
            }

            if (state.Time >= MaxSimulatedTime)
            {
                End(EndReason.Timeout, "Simulated time limit reached.");
                return;
            }

            TotalSteps++;
            Series.Record(state);
        }

        private void End(EndReason reason, string message)
        {
            Series.Record(state, true);
            Vec3d v = state.Velocity;
            double speed = v.Magnitude;
            double radial = speed > 0 ? Vec3d.Dot(v, state.Position.Normalized) / speed : 0;
            Result = new SimulationResult
            {
                Reason = reason,
                Message = message,
                FinalState = state,
                ImpactSpeed = speed,
                ImpactAngleDegrees = Math.Asin(Math.Max(-1.0, Math.Min(1.0, -radial))) * 180.0 / Math.PI,
                BreakupAltitude = breakupAltitude,
                BreakupEnergy = breakupEnergy,
                PeakTemperature = peakTemperature,
                PeakDeceleration = peakDeceleration,
                AtmosphereEntryTime = entryTime,
                AtmosphereEntrySpeed = entrySpeed,
                Series = Series,
            };
            Stage = SimulationStage.Ended;
        }

        private static KinematicState Kinematics(in MeteorState s) =>
            new KinematicState(s.Position, s.Velocity, s.PancakeDiameter, s.PancakeRate);

        private Vec3d WindAt(Vec3d position, double altitude) =>
            altitude <= WindCeiling ? Geo.WindVector(position, WindSpeed, WindAzimuthFraction) : Vec3d.Zero;

        /// <summary>Area facing the flow (m^2): intact body uses the equal-volume sphere times the shape factor; a broken body uses the pancake diameter.</summary>
        private double CrossSection(in MeteorState s)
        {
            if (s.Broken)
            {
                return Math.PI * 0.25 * s.PancakeDiameter * s.PancakeDiameter;
            }

            double r = s.EquivalentRadius(Properties.Material.Density);
            return Properties.ShapeFactor * Math.PI * r * r;
        }

        private double FlowDiameter(in MeteorState s) =>
            s.Broken ? s.PancakeDiameter : 2.0 * s.EquivalentRadius(Properties.Material.Density);

        // State derivative: position' = v; v' = gravity + drag/m; pancake L'' = Cd rho_a v^2 / (rho_i L) (Collins et al. 2005, Eq. 13).
        private KinematicState Derivative(double t, KinematicState y)
        {
            Vec3d accel = Gravity.Acceleration(y.Position);
            double altitude = y.Position.Magnitude - PhysicalConstants.EarthRadius;
            double pancakeAccel = 0.0;
            if (altitude < VacuumAltitude && state.Mass > 0)
            {
                AtmosphereSample air = Atmosphere.Sample(Math.Max(0.0, altitude));
                Vec3d vRel = y.Velocity - WindAt(y.Position, altitude);
                double speed = vRel.Magnitude;

                MeteorState s = state;
                s.PancakeDiameter = y.PancakeDiameter;
                double diameter = FlowDiameter(s);
                double area = CrossSection(s);
                double cd = Drag.Coefficient(air, speed, diameter);
                accel += Drag.Force(air.Density, cd, area, vRel) / state.Mass;

                if (state.Broken && y.PancakeDiameter > 0)
                {
                    pancakeAccel = cd * air.Density * speed * speed / (Properties.Material.Density * y.PancakeDiameter);
                }
            }

            return state.Broken
                ? new KinematicState(y.Velocity, accel, y.PancakeRate, pancakeAccel)
                : new KinematicState(y.Velocity, accel, 0.0, 0.0);
        }

        private Vec3d InitialVelocity(MeteorProperties p)
        {
            if (p.InitialVelocity.HasValue)
            {
                return p.InitialVelocity.Value;
            }

            if (p.Mode != EntryMode.Inbound)
            {
                return Vec3d.Zero; // classic NEA: released from rest
            }

            double mu = PhysicalConstants.EarthMu;
            Vec3d r0 = p.StartPosition;
            double r0m = r0.Magnitude;
            double re = PhysicalConstants.EarthRadius + PhysicalConstants.AtmosphereEntryAltitude;
            double ve = p.EntrySpeed;
            double gamma = p.EntryAngleDegrees * Math.PI / 180.0;
            Vec3d up = r0.Normalized;
            Vec3d east = Geo.East(r0);

            if (r0m <= re)
            {
                return (up * -Math.Sin(gamma) + east * Math.Cos(gamma)) * ve;
            }

            // Two-body conic through the entry interface: conserve energy and angular momentum.
            double v0Squared = ve * ve - 2.0 * mu * (1.0 / re - 1.0 / r0m);
            if (!(v0Squared > 0))
            {
                SetupNote = "Entry speed too low to be reached from this start point; released from rest instead.";
                return Vec3d.Zero;
            }

            double v0 = Math.Sqrt(v0Squared);
            double cosGamma0 = re * ve * Math.Cos(gamma) / (r0m * v0);
            if (cosGamma0 > 1.0)
            {
                SetupNote = "Requested entry angle is not reachable from this start point; released from rest instead.";
                return Vec3d.Zero;
            }

            double sinGamma0 = Math.Sqrt(Math.Max(0.0, 1.0 - cosGamma0 * cosGamma0));
            return (up * -sinGamma0 + east * cosGamma0) * v0;
        }
    }
}
