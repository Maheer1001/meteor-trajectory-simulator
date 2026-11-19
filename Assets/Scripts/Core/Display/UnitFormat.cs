using System;
using System.Globalization;

namespace MTS.Core.Display
{
    /// <summary>
    /// Human-readable formatting of SI quantities for the HUD: sensible units, thousands separators and a bounded
    /// number of decimals. Input is always SI (m, m/s, kg, s, Pa, K, W). Output uses the invariant culture so it
    /// looks the same on every machine. Non-finite input gives <see cref="Missing"/>.
    /// Only ASCII plus the degree sign is used, so any TMP font can render it.
    /// </summary>
    public static class UnitFormat
    {
        public const string Missing = "--";

        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        private static bool Bad(double v) => double.IsNaN(v) || double.IsInfinity(v);

        private static string N(double v, int decimals) => v.ToString("N" + decimals, Ci);

        /// <summary>Distance or altitude: metres below 10 km, kilometres above (1 decimal below 1,000 km).</summary>
        public static string Distance(double metres)
        {
            if (Bad(metres)) return Missing;
            double a = Math.Abs(metres);
            if (a < 10000.0) return N(metres, 0) + " m";
            double km = metres / 1000.0;
            return a < 1.0e6 ? N(km, 1) + " km" : N(km, 0) + " km";
        }

        /// <summary>Speed: m/s below 1 km/s, km/s with two decimals above.</summary>
        public static string Speed(double metresPerSecond)
        {
            if (Bad(metresPerSecond)) return Missing;
            double a = Math.Abs(metresPerSecond);
            if (a < 10.0) return N(metresPerSecond, 1) + " m/s";
            if (a < 1000.0) return N(metresPerSecond, 0) + " m/s";
            return N(metresPerSecond / 1000.0, 2) + " km/s";
        }

        /// <summary>Mass: kilograms below one tonne, tonnes above.</summary>
        public static string Mass(double kilograms)
        {
            if (Bad(kilograms)) return Missing;
            double a = Math.Abs(kilograms);
            if (a < 100.0) return N(kilograms, 1) + " kg";
            if (a < 1000.0) return N(kilograms, 0) + " kg";
            double t = kilograms / 1000.0;
            double at = Math.Abs(t);
            if (at < 100.0) return N(t, 2) + " t";
            if (at < 10000.0) return N(t, 1) + " t";
            return N(t, 0) + " t";
        }

        /// <summary>Mass-loss rate: g/s below 1 kg/s, kg/s, t/s above 1 t/s.</summary>
        public static string MassRate(double kilogramsPerSecond)
        {
            if (Bad(kilogramsPerSecond)) return Missing;
            double a = Math.Abs(kilogramsPerSecond);
            if (a == 0) return "0 kg/s";
            if (a < 1.0) return N(kilogramsPerSecond * 1000.0, a < 0.01 ? 2 : 0) + " g/s";
            if (a < 100.0) return N(kilogramsPerSecond, 1) + " kg/s";
            if (a < 1000.0) return N(kilogramsPerSecond, 0) + " kg/s";
            return N(kilogramsPerSecond / 1000.0, 2) + " t/s";
        }

        /// <summary>Elapsed time: "12.3 s" below a minute, "h:mm:ss" below a day, "N d hh:mm:ss" above.</summary>
        public static string Duration(double seconds)
        {
            if (Bad(seconds)) return Missing;
            string sign = seconds < 0 ? "-" : "";
            double a = Math.Abs(seconds);
            if (a < 60.0) return sign + a.ToString("0.0", Ci) + " s";
            long total = (long)Math.Floor(a);
            long days = total / 86400;
            long h = total / 3600 % 24;
            long m = total / 60 % 60;
            long s = total % 60;
            if (days == 0) return sign + h.ToString(Ci) + ":" + m.ToString("00", Ci) + ":" + s.ToString("00", Ci);
            return sign + days.ToString("N0", Ci) + " d " + h.ToString("00", Ci) + ":" + m.ToString("00", Ci) + ":" + s.ToString("00", Ci);
        }

        /// <summary>Pressure: Pa, kPa or MPa.</summary>
        public static string Pressure(double pascals)
        {
            if (Bad(pascals)) return Missing;
            double a = Math.Abs(pascals);
            if (a < 1000.0) return N(pascals, a < 10.0 ? 2 : 0) + " Pa";
            if (a < 1.0e6) return N(pascals / 1000.0, a < 1.0e5 ? 2 : 1) + " kPa";
            return N(pascals / 1.0e6, 2) + " MPa";
        }

        /// <summary>Acceleration in multiples of standard gravity.</summary>
        public static string GForce(double g)
        {
            if (Bad(g)) return Missing;
            double a = Math.Abs(g);
            return N(g, a < 10.0 ? 2 : a < 1000.0 ? 1 : 0) + " g";
        }

        public static string Temperature(double kelvin) => Bad(kelvin) ? Missing : N(kelvin, 0) + " K";

        public static string Mach(double mach) => Bad(mach) ? Missing : "Mach " + N(mach, mach < 10.0 ? 2 : 1);

        /// <summary>Radiant power with an SI prefix and three significant figures (W, kW, MW, GW, TW).</summary>
        public static string Power(double watts)
        {
            if (Bad(watts)) return Missing;
            double a = Math.Abs(watts);
            if (a < 1.0) return "0 W";
            string[] prefixes = { "W", "kW", "MW", "GW", "TW", "PW" };
            int i = 0;
            double v = watts;
            while (Math.Abs(v) >= 1000.0 && i < prefixes.Length - 1)
            {
                v /= 1000.0;
                i++;
            }

            return ThreeSignificant(v) + " " + prefixes[i];
        }

        /// <summary>Absolute magnitude with sign ("-12.4"); "not glowing" at or beyond the dark limit.</summary>
        public static string Magnitude(double magnitude, double darkLimit = FlightTelemetry.DarkMagnitude)
        {
            if (Bad(magnitude)) return Missing;
            if (magnitude >= darkLimit) return "not glowing";
            return (magnitude >= 0 ? "+" : "") + magnitude.ToString("0.0", Ci) + " mag";
        }

        /// <summary>Length for object sizes: cm below 1 m, m below 10 km.</summary>
        public static string Length(double metres)
        {
            if (Bad(metres)) return Missing;
            double a = Math.Abs(metres);
            if (a < 1.0) return N(metres * 100.0, 0) + " cm";
            if (a < 100.0) return N(metres, 2) + " m";
            return Distance(metres);
        }

        public static string Percent(double fraction, int decimals = 1) =>
            Bad(fraction) ? Missing : (fraction * 100.0).ToString("F" + decimals, Ci) + " %";

        public static string Angle(double degrees, int decimals = 0) =>
            Bad(degrees) ? Missing : degrees.ToString("F" + decimals, Ci) + "°";

        /// <summary>Time-warp multiplier, e.g. "x1,000" or "x0.25".</summary>
        public static string Warp(double multiplier)
        {
            if (Bad(multiplier)) return Missing;
            double a = Math.Abs(multiplier);
            if (a < 1.0) return "x" + multiplier.ToString("0.##", Ci);
            if (a < 10.0) return "x" + multiplier.ToString("0.#", Ci);
            return "x" + N(multiplier, 0);
        }

        private static string ThreeSignificant(double v)
        {
            double a = Math.Abs(v);
            if (a >= 100.0) return v.ToString("0", Ci);
            if (a >= 10.0) return v.ToString("0.0", Ci);
            return v.ToString("0.00", Ci);
        }
    }
}
