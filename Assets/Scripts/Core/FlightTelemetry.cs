using System;

namespace MTS.Core
{
    /// <summary>
    /// Read-only snapshot of derived flight quantities for the HUD and the visuals (SI units unless stated).
    /// Built by <see cref="SimulationRunner.Telemetry"/>; nothing here feeds back into the physics.
    /// </summary>
    public readonly struct FlightTelemetry
    {
        /// <summary>Luminous efficiency tau: fraction of the kinetic energy lost to ablation that becomes visible light.</summary>
        public const double LuminousEfficiency = 0.01;
        /// <summary>Radiant power of a zero-magnitude meteor seen from 100 km (W), panchromatic (Ceplecha et al. 1998).</summary>
        public const double ZeroMagnitudePower = 1500.0;
        /// <summary>Absolute magnitude reported when the meteor is not glowing.</summary>
        public const double DarkMagnitude = 10.0;

        public readonly SimulationStage Stage;
        public readonly double Time;
        public readonly double Altitude;
        /// <summary>Inertial speed (m/s).</summary>
        public readonly double Speed;
        /// <summary>Speed relative to the (windy) air (m/s); equals <see cref="Speed"/> in vacuum.</summary>
        public readonly double AirSpeed;
        public readonly double Mach;
        /// <summary>Dynamic (ram) pressure 0.5 rho v^2 (Pa).</summary>
        public readonly double DynamicPressure;
        /// <summary>Non-gravitational deceleration over the last step, in units of g0.</summary>
        public readonly double DecelerationG;
        public readonly double Mass;
        /// <summary>Ablation mass-loss rate over the last step (kg/s, positive when losing mass).</summary>
        public readonly double MassLossRate;
        /// <summary>Surface-layer temperature (K).</summary>
        public readonly double Temperature;
        /// <summary>Visible radiant power I = tau * 0.5 * |dm/dt| * v^2 (W).</summary>
        public readonly double Luminosity;
        /// <summary>Absolute visual magnitude M = -2.5 log10(I / I0); <see cref="DarkMagnitude"/> when not glowing.</summary>
        public readonly double AbsoluteMagnitude;
        public readonly bool Broken;
        /// <summary>Effective body diameter (m): equal-mass sphere when intact, pancake diameter when broken.</summary>
        public readonly double Diameter;
        public readonly double AirDensity;

        public FlightTelemetry(SimulationStage stage, double time, double altitude, double speed, double airSpeed,
            double mach, double dynamicPressure, double decelerationG, double mass, double massLossRate,
            double temperature, bool broken, double diameter, double airDensity)
        {
            Stage = stage;
            Time = time;
            Altitude = altitude;
            Speed = speed;
            AirSpeed = airSpeed;
            Mach = mach;
            DynamicPressure = dynamicPressure;
            DecelerationG = decelerationG;
            Mass = mass;
            MassLossRate = Math.Max(0.0, massLossRate);
            Temperature = temperature;
            Broken = broken;
            Diameter = diameter;
            AirDensity = airDensity;
            Luminosity = LuminousEfficiency * 0.5 * MassLossRate * airSpeed * airSpeed;
            AbsoluteMagnitude = MagnitudeFromPower(Luminosity);
        }

        public static double MagnitudeFromPower(double watts) =>
            watts > 0 ? Math.Min(DarkMagnitude, -2.5 * Math.Log10(watts / ZeroMagnitudePower)) : DarkMagnitude;
    }
}
