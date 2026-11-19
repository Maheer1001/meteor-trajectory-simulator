using System;

namespace MTS.Core
{
    /// <summary>Point-mass (central) Newtonian gravity of the Earth.</summary>
    public static class Gravity
    {
        /// <summary>Gravitational acceleration vector at position r (m, Earth-centred), a = -mu r / |r|^3.</summary>
        public static Vec3d Acceleration(Vec3d position)
        {
            double r2 = position.SqrMagnitude;
            if (r2 <= 0)
            {
                return Vec3d.Zero;
            }

            double r = Math.Sqrt(r2);
            return position * (-PhysicalConstants.EarthMu / (r2 * r));
        }

        /// <summary>Magnitude of the gravitational acceleration at distance r (m) from the Earth's centre.</summary>
        public static double Magnitude(double radius) => PhysicalConstants.EarthMu / (radius * radius);

        /// <summary>Specific gravitational potential energy -mu / r (J/kg).</summary>
        public static double SpecificPotential(Vec3d position) => -PhysicalConstants.EarthMu / position.Magnitude;
    }
}
