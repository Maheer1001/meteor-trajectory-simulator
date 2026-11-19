using System;

namespace MTS.Core
{
    /// <summary>
    /// Aerodynamic drag on a blunt (roughly spherical) meteoroid.
    /// Cd is a function of Mach number in continuum flow (sphere data, Hoerner 1965 / Bailey and Hiatt 1972:
    /// about 0.47 subsonic, peaking near 1.0 around Mach 1.5, about 0.92 hypersonic) and of Knudsen number:
    /// free-molecular flow (Kn > 10) gives Cd = 2, with a smooth log-Kn bridge between Kn = 0.01 and Kn = 10.
    /// A subsonic Reynolds-number correlation is not used because meteors fly hypersonically through most of
    /// their path, where such correlations do not apply.
    /// </summary>
    public static class Drag
    {
        public const double FreeMolecularCd = 2.0;
        public const double HypersonicContinuumCd = 0.92;

        private static readonly double[] MachTable = { 0.0, 0.6, 0.8, 1.0, 1.2, 1.5, 2.0, 3.0, 5.0 };
        private static readonly double[] CdTable = { 0.47, 0.50, 0.58, 0.80, 0.95, 1.00, 0.98, 0.95, 0.92 };

        /// <summary>Continuum-flow sphere drag coefficient as a function of Mach number.</summary>
        public static double ContinuumCd(double mach)
        {
            if (!(mach > 0))
            {
                return CdTable[0];
            }

            if (mach >= MachTable[MachTable.Length - 1])
            {
                return HypersonicContinuumCd;
            }

            int i = 0;
            while (mach > MachTable[i + 1])
            {
                i++;
            }

            double f = (mach - MachTable[i]) / (MachTable[i + 1] - MachTable[i]);
            return CdTable[i] + f * (CdTable[i + 1] - CdTable[i]);
        }

        /// <summary>Drag coefficient for a given Mach number and Knudsen number (mean free path / diameter).</summary>
        public static double Coefficient(double mach, double knudsen)
        {
            double cdc = ContinuumCd(mach);
            if (double.IsNaN(knudsen) || knudsen <= 0.01)
            {
                return cdc;
            }

            if (knudsen >= 10.0 || double.IsInfinity(knudsen))
            {
                return FreeMolecularCd;
            }

            double s = (Math.Log10(knudsen) + 2.0) / 3.0; // 0 at Kn = 0.01, 1 at Kn = 10
            s = s * s * (3.0 - 2.0 * s);                  // smoothstep
            return cdc + (FreeMolecularCd - cdc) * s;
        }

        /// <summary>Drag coefficient for a body of the given diameter moving at relativeSpeed through the air.</summary>
        public static double Coefficient(AtmosphereSample air, double relativeSpeed, double diameter)
        {
            double mach = relativeSpeed / air.SpeedOfSound;
            double kn = diameter > 0 ? air.MeanFreePath / diameter : double.PositiveInfinity;
            return Coefficient(mach, kn);
        }

        /// <summary>
        /// Drag force vector F = -1/2 rho Cd A |v_rel| v_rel, where v_rel is the velocity relative to the air
        /// (meteor velocity minus wind). It always points opposite to v_rel.
        /// </summary>
        public static Vec3d Force(double airDensity, double cd, double area, Vec3d relativeVelocity)
        {
            double speed = relativeVelocity.Magnitude;
            if (speed <= 0 || airDensity <= 0 || area <= 0)
            {
                return Vec3d.Zero;
            }

            return relativeVelocity * (-0.5 * airDensity * cd * area * speed);
        }
    }
}
