using System;

namespace MTS.Core
{
    /// <summary>Atmospheric properties at one altitude (SI units).</summary>
    public readonly struct AtmosphereSample
    {
        /// <summary>Geometric altitude (m).</summary>
        public readonly double Altitude;
        /// <summary>Temperature (K).</summary>
        public readonly double Temperature;
        /// <summary>Pressure (Pa).</summary>
        public readonly double Pressure;
        /// <summary>Density (kg m^-3).</summary>
        public readonly double Density;

        public AtmosphereSample(double altitude, double temperature, double pressure, double density)
        {
            Altitude = altitude;
            Temperature = temperature;
            Pressure = pressure;
            Density = density;
        }

        /// <summary>Speed of sound sqrt(gamma R T) (m/s).</summary>
        public double SpeedOfSound => Math.Sqrt(Atmosphere.Gamma * Atmosphere.SpecificGasConstant * Temperature);

        /// <summary>Dynamic viscosity from Sutherland's law (Pa s), as used by USSA76.</summary>
        public double DynamicViscosity => 1.458e-6 * Math.Pow(Temperature, 1.5) / (Temperature + 110.4);

        /// <summary>Mean free path (m), USSA76 hard-sphere model (collision diameter 3.65e-10 m).</summary>
        public double MeanFreePath => Density > 0 ? Atmosphere.MeanFreePathConstant / Density : double.PositiveInfinity;
    }

    /// <summary>
    /// U.S. Standard Atmosphere 1976.
    /// 0-86 km: the seven geopotential layers of USSA76 (exact hydrostatic formulas).
    /// 86-1000 km: log-linear interpolation of the USSA76 tabulated values; above 1000 km the last
    /// segment's exponential decay is extrapolated. Below 86 km the returned temperature is the
    /// molecular-scale temperature (it differs from the kinetic temperature by under 0.05 % between
    /// 80 and 86 km).
    /// </summary>
    public static class Atmosphere
    {
        public const double UniversalGasConstant = 8.31432;     // J/(mol K), USSA76 value
        public const double MolarMass = 0.0289644;              // kg/mol, sea-level air
        public const double SpecificGasConstant = UniversalGasConstant / MolarMass; // 287.053 J/(kg K)
        public const double Gamma = 1.4;
        public const double EarthRadiusForGeopotential = 6356766.0; // m, USSA76 r0
        public const double TopOfLayeredModel = 86000.0;        // m geometric (= 84.852 km geopotential)

        // lambda = M0 / (sqrt(2) pi d^2 N_A rho) with collision diameter d = 3.65e-10 m
        internal const double MeanFreePathConstant = MolarMass / (1.4142135623730951 * Math.PI * 3.65e-10 * 3.65e-10 * 6.022169e23);

        private const double Gmr = PhysicalConstants.StandardGravity * MolarMass / UniversalGasConstant;

        // Geopotential layer bases (m'), base temperatures (K), lapse rates (K/m').
        private static readonly double[] LayerBaseH = { 0, 11000, 20000, 32000, 47000, 51000, 71000, 84852 };
        private static readonly double[] LayerBaseT = { 288.15, 216.65, 216.65, 228.65, 270.65, 270.65, 214.65, 186.946 };
        private static readonly double[] LayerLapse = { -0.0065, 0.0, 0.001, 0.0028, 0.0, -0.0028, -0.002 };
        private static readonly double[] LayerBaseP;

        // USSA76 table above 86 km: geometric altitude (m), kinetic temperature (K), pressure (Pa), density (kg/m^3).
        private static readonly double[] UpperZ = { 86000, 90000, 95000, 100000, 110000, 120000, 130000, 140000, 150000, 160000, 200000, 250000, 300000, 400000, 500000, 600000, 700000, 800000, 900000, 1000000 };
        private static readonly double[] UpperT = { 186.87, 186.87, 188.42, 195.08, 240.00, 360.00, 469.27, 559.63, 634.39, 696.29, 854.56, 941.33, 976.01, 995.83, 999.24, 999.85, 999.97, 999.99, 1000.0, 1000.0 };
        private static readonly double[] UpperP = { 3.7338e-1, 1.8359e-1, 7.5966e-2, 3.2011e-2, 7.1042e-3, 2.5382e-3, 1.2505e-3, 7.2028e-4, 4.5422e-4, 3.0395e-4, 8.4736e-5, 2.4767e-5, 8.7704e-6, 1.4518e-6, 3.0236e-7, 8.2130e-8, 3.1908e-8, 1.7036e-8, 1.0873e-8, 7.5138e-9 };
        private static readonly double[] UpperRho = { 6.958e-6, 3.416e-6, 1.393e-6, 5.604e-7, 9.708e-8, 2.222e-8, 8.152e-9, 3.831e-9, 2.076e-9, 1.233e-9, 2.541e-10, 6.073e-11, 1.916e-11, 2.803e-12, 5.215e-13, 1.137e-13, 3.070e-14, 1.136e-14, 5.759e-15, 3.561e-15 };

        static Atmosphere()
        {
            // Base pressures are derived from sea level so the layers are exactly continuous.
            LayerBaseP = new double[LayerBaseH.Length];
            LayerBaseP[0] = 101325.0;
            for (int i = 1; i < LayerBaseH.Length; i++)
            {
                LayerBaseP[i] = LayerPressure(i - 1, LayerBaseH[i]);
            }
        }

        /// <summary>Geopotential altitude (m') for a geometric altitude (m).</summary>
        public static double GeopotentialAltitude(double geometricAltitude) =>
            EarthRadiusForGeopotential * geometricAltitude / (EarthRadiusForGeopotential + geometricAltitude);

        /// <summary>Geometric altitude (m) for a geopotential altitude (m').</summary>
        public static double GeometricAltitude(double geopotentialAltitude) =>
            EarthRadiusForGeopotential * geopotentialAltitude / (EarthRadiusForGeopotential - geopotentialAltitude);

        /// <summary>Atmosphere at a geometric altitude (m). Altitudes below -5 km are clamped to -5 km.</summary>
        public static AtmosphereSample Sample(double geometricAltitude)
        {
            if (double.IsNaN(geometricAltitude))
            {
                throw new ArgumentException("Altitude is NaN", nameof(geometricAltitude));
            }

            double z = Math.Max(geometricAltitude, -5000.0);
            if (z <= TopOfLayeredModel)
            {
                return SampleGeopotential(GeopotentialAltitude(z), z);
            }

            return SampleUpper(z);
        }

        /// <summary>Atmosphere at a geopotential altitude (m'), valid from -5 km' to 84.852 km'.</summary>
        public static AtmosphereSample SampleAtGeopotential(double geopotentialAltitude) =>
            SampleGeopotential(geopotentialAltitude, GeometricAltitude(geopotentialAltitude));

        private static AtmosphereSample SampleGeopotential(double h, double z)
        {
            int layer = 0;
            for (int i = LayerLapse.Length - 1; i >= 0; i--)
            {
                if (h >= LayerBaseH[i])
                {
                    layer = i;
                    break;
                }
            }

            double t = LayerBaseT[layer] + LayerLapse[layer] * (h - LayerBaseH[layer]);
            double p = LayerPressure(layer, h);
            double rho = p / (SpecificGasConstant * t);
            return new AtmosphereSample(z, t, p, rho);
        }

        private static double LayerPressure(int layer, double h)
        {
            double tb = LayerBaseT[layer];
            double lapse = LayerLapse[layer];
            double pb = LayerBaseP[layer];
            double dh = h - LayerBaseH[layer];
            if (lapse == 0.0)
            {
                return pb * Math.Exp(-Gmr * dh / tb);
            }

            return pb * Math.Pow(tb / (tb + lapse * dh), Gmr / lapse);
        }

        private static AtmosphereSample SampleUpper(double z)
        {
            int n = UpperZ.Length;
            int i = 0;
            if (z >= UpperZ[n - 1])
            {
                i = n - 2; // extrapolate the last segment
            }
            else
            {
                while (i < n - 2 && z > UpperZ[i + 1])
                {
                    i++;
                }
            }

            double f = (z - UpperZ[i]) / (UpperZ[i + 1] - UpperZ[i]);
            double t = z >= UpperZ[n - 1] ? UpperT[n - 1] : UpperT[i] + f * (UpperT[i + 1] - UpperT[i]);
            double p = LogLerp(UpperP[i], UpperP[i + 1], f);
            double rho = LogLerp(UpperRho[i], UpperRho[i + 1], f);
            return new AtmosphereSample(z, t, p, rho);
        }

        private static double LogLerp(double a, double b, double f)
        {
            double v = Math.Exp(Math.Log(a) + f * (Math.Log(b) - Math.Log(a)));
            return double.IsNaN(v) ? 0.0 : v; // exp underflows to 0 far above the atmosphere
        }
    }
}
