using System;
using System.Globalization;
using MTS.Core;

namespace MTS.Results
{
    /// <summary>
    /// Culture-invariant number formatting for the results screen. Methods return plain text unless
    /// <c>rich</c> is set, in which case powers of ten use TextMesh Pro superscript tags.
    /// </summary>
    public static class ResultsFormat
    {
        public const double HiroshimaKilotons = 15.0;
        public const double ChelyabinskKilotons = 500.0;
        /// <summary>Tunguska 1908: estimates range from 10 to 15 Mt; the midpoint is used for ratios.</summary>
        public const double TunguskaKilotons = 12500.0;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Rounds to the given number of significant figures and formats without exponent (thousands separated).</summary>
        public static string Sig(double value, int figures = 3)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "-";
            }

            if (value == 0)
            {
                return "0";
            }

            int magnitude = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            int decimals = Math.Max(0, figures - 1 - magnitude);
            double rounded;
            if (magnitude >= figures)
            {
                double factor = Math.Pow(10, magnitude - figures + 1);
                rounded = Math.Round(value / factor, MidpointRounding.AwayFromZero) * factor;
            }
            else
            {
                rounded = Math.Round(value, Math.Min(15, decimals), MidpointRounding.AwayFromZero);
            }

            // Rounding can carry into the next power of ten (9.996 -> 10.0): recompute the decimals for it.
            int roundedMagnitude = rounded == 0 ? magnitude : (int)Math.Floor(Math.Log10(Math.Abs(rounded)));
            if (roundedMagnitude > magnitude)
            {
                decimals = Math.Max(0, figures - 1 - roundedMagnitude);
            }

            return rounded.ToString("N" + decimals, Inv);
        }

        /// <summary>Mantissa x 10^exponent with the given significant figures, e.g. "5.23 × 10^13".</summary>
        public static string Scientific(double value, int figures = 3, bool rich = false)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "-";
            }

            if (value == 0)
            {
                return "0";
            }

            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            double mantissa = value / Math.Pow(10, exponent);
            mantissa = Math.Round(mantissa, figures - 1, MidpointRounding.AwayFromZero);
            if (Math.Abs(mantissa) >= 10.0)
            {
                mantissa /= 10.0;
                exponent++;
            }

            string m = mantissa.ToString("F" + Math.Max(0, figures - 1), Inv);
            return rich
                ? m + " × 10<sup>" + exponent.ToString(Inv) + "</sup>"
                : m + " × 10^" + exponent.ToString(Inv);
        }

        /// <summary>Energy in joules in scientific notation, e.g. "2.09 × 10^15 J".</summary>
        public static string Joules(double joules, bool rich = false) => Scientific(joules, 3, rich) + " J";

        /// <summary>TNT equivalent with a sensible unit: kg, t, kt, Mt or Gt of TNT.</summary>
        public static string Tnt(double joules)
        {
            double kt = joules / PhysicalConstants.JoulesPerKilotonTnt;
            if (!(kt > 0))
            {
                return "0 t TNT";
            }

            if (kt >= 1e6) return Sig(kt / 1e6) + " Gt TNT";
            if (kt >= 1e3) return Sig(kt / 1e3) + " Mt TNT";
            if (kt >= 1) return Sig(kt) + " kt TNT";
            if (kt >= 1e-3) return Sig(kt * 1e3) + " t TNT";
            return Sig(kt * 1e6) + " kg TNT";
        }

        /// <summary>
        /// Plain-language comparison with reference events: Hiroshima (15 kt), Chelyabinsk 2013 (about 500 kt) and
        /// Tunguska 1908 (10-15 Mt). The largest reference not above the energy is used; below Hiroshima the result
        /// is a fraction of Hiroshima.
        /// </summary>
        public static string Comparison(double joules)
        {
            double kt = joules / PhysicalConstants.JoulesPerKilotonTnt;
            if (!(kt > 0))
            {
                return "No energy released";
            }

            string name;
            double reference;
            if (kt >= TunguskaKilotons * 0.8)
            {
                name = "Tunguska (1908)";
                reference = TunguskaKilotons;
            }
            else if (kt >= ChelyabinskKilotons * 0.8)
            {
                name = "Chelyabinsk (2013)";
                reference = ChelyabinskKilotons;
            }
            else
            {
                name = "Hiroshima";
                reference = HiroshimaKilotons;
            }

            double ratio = kt / reference;
            if (ratio >= 0.8 && ratio < 1.25)
            {
                return "About the same as " + name;
            }

            if (ratio >= 1.0)
            {
                return Sig(ratio, ratio < 10 ? 2 : 3) + "× " + name;
            }

            if (ratio >= 0.01)
            {
                return Sig(ratio * 100.0, 2) + "% of " + name;
            }

            return "1/" + Sig(1.0 / ratio, 2) + " of " + name;
        }

        /// <summary>Length: "350 m", "1.23 km", "12.3 km", "1,234 km".</summary>
        public static string Distance(double metres)
        {
            if (double.IsNaN(metres) || double.IsInfinity(metres))
            {
                return "-";
            }

            double a = Math.Abs(metres);
            if (a < 1.0) return Sig(metres * 100.0, 2) + " cm";
            if (a < 1000.0) return Sig(metres, a < 10 ? 2 : 3) + " m";
            if (a < 100000.0) return Sig(metres / 1000.0, 3) + " km";
            return Math.Round(metres / 1000.0).ToString("N0", Inv) + " km";
        }

        public static string Speed(double metresPerSecond)
        {
            if (double.IsNaN(metresPerSecond)) return "-";
            return Math.Abs(metresPerSecond) >= 1000.0
                ? Sig(metresPerSecond / 1000.0, 3) + " km/s"
                : Sig(metresPerSecond, 3) + " m/s";
        }

        public static string Mass(double kg)
        {
            if (double.IsNaN(kg)) return "-";
            double a = Math.Abs(kg);
            if (a < 1.0) return Sig(kg * 1000.0, 3) + " g";
            if (a < 1000.0) return Sig(kg, 3) + " kg";
            if (a < 1e9) return Sig(kg / 1000.0, 3) + " t";
            return Scientific(kg / 1000.0) + " t";
        }

        public static string Pressure(double pascals)
        {
            if (double.IsNaN(pascals)) return "-";
            double a = Math.Abs(pascals);
            if (a < 1000.0) return Sig(pascals, 3) + " Pa";
            if (a < 1e6) return Sig(pascals / 1000.0, 3) + " kPa";
            if (a < 1e9) return Sig(pascals / 1e6, 3) + " MPa";
            return Sig(pascals / 1e9, 3) + " GPa";
        }

        public const double PascalsPerPsi = 6894.757;

        public static string Psi(double pascals) => Sig(pascals / PascalsPerPsi, 2) + " psi";

        public static string Temperature(double kelvin) => double.IsNaN(kelvin) ? "-" : Sig(kelvin, 4) + " K";

        public static string Angle(double degrees) => double.IsNaN(degrees) ? "-" : degrees.ToString("F1", Inv) + "°";

        public static string Acceleration(double metresPerSecondSquared) =>
            double.IsNaN(metresPerSecondSquared)
                ? "-"
                : Sig(metresPerSecondSquared / PhysicalConstants.StandardGravity, 3) + " g";

        /// <summary>"55.15° N, 61.41° E".</summary>
        public static string LatLon(double latitude, double longitude)
        {
            string lat = Math.Abs(latitude).ToString("F2", Inv) + "° " + (latitude >= 0 ? "N" : "S");
            string lon = Math.Abs(longitude).ToString("F2", Inv) + "° " + (longitude >= 0 ? "E" : "W");
            return lat + ", " + lon;
        }

        /// <summary>"12.3 s", "4 min 12 s", "3 h 05 min", "2 d 4 h".</summary>
        public static string Duration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "-";
            if (seconds < 60.0) return seconds.ToString(seconds < 10 ? "F2" : "F1", Inv) + " s";
            long s = (long)Math.Round(seconds);
            if (s < 3600) return (s / 60).ToString(Inv) + " min " + (s % 60).ToString("00", Inv) + " s";
            if (s < 86400) return (s / 3600).ToString(Inv) + " h " + (s % 3600 / 60).ToString("00", Inv) + " min";
            return (s / 86400).ToString(Inv) + " d " + (s % 86400 / 3600).ToString(Inv) + " h";
        }

        public static string Percent(double fraction) => Sig(fraction * 100.0, fraction < 0.1 ? 2 : 3) + "%";
    }
}
