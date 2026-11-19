using System;

namespace MTS.Core
{
    /// <summary>Complete dynamic state of the meteor at one instant (SI units).</summary>
    public struct MeteorState
    {
        /// <summary>Simulated time since release (s).</summary>
        public double Time;
        /// <summary>Earth-centred inertial position (m).</summary>
        public Vec3d Position;
        /// <summary>Inertial velocity (m/s).</summary>
        public Vec3d Velocity;
        /// <summary>Mass (kg).</summary>
        public double Mass;
        /// <summary>Lumped surface-layer temperature (K).</summary>
        public double Temperature;
        /// <summary>Total path length travelled (m).</summary>
        public double DistanceTravelled;
        /// <summary>True once aerodynamic loading has exceeded the material strength.</summary>
        public bool Broken;
        /// <summary>Diameter at break-up L0 (m); 0 while intact.</summary>
        public double BreakupDiameter;
        /// <summary>Pancake (debris cloud) diameter L (m); 0 while intact.</summary>
        public double PancakeDiameter;
        /// <summary>Pancake spreading rate dL/dt (m/s).</summary>
        public double PancakeRate;

        public double Altitude => Geo.Altitude(Position);
        public double Speed => Velocity.Magnitude;

        /// <summary>Radius of a sphere of the same mass and density (m).</summary>
        public double EquivalentRadius(double density) =>
            Mass > 0 ? Math.Pow(3.0 * Mass / (4.0 * Math.PI * density), 1.0 / 3.0) : 0.0;

        /// <summary>True when every component is a finite number.</summary>
        public bool IsFinite =>
            Position.IsFinite && Velocity.IsFinite && IsFiniteNumber(Mass) && IsFiniteNumber(Temperature) &&
            IsFiniteNumber(Time) && IsFiniteNumber(DistanceTravelled) && IsFiniteNumber(PancakeDiameter) &&
            IsFiniteNumber(PancakeRate);

        private static bool IsFiniteNumber(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "t={0:F3}s r={1} v={2} h={3:F1}m m={4:G6}kg T={5:F1}K broken={6} L={7:G4}m",
                Time, Position, Velocity, Altitude, Mass, Temperature, Broken, PancakeDiameter);
    }
}
