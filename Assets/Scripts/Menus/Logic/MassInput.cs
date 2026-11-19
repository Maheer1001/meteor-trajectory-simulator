using System;
using System.Globalization;

namespace MTS.Menus
{
    /// <summary>
    /// Parsing and display of the input screen's mass field (pure logic; compiled into MTS.Core through an
    /// assembly reference so the EditMode tests can reach it). Accepts plain numbers ("30000", "30,000", "30 000"),
    /// scientific notation ("1.2e7", "1.25E+15") and an optional unit ("kg", "t", "kt", "Mt", "Gt").
    /// </summary>
    public static class MassInput
    {
        public const double MinMass = 1.0e3;
        public const double MaxMass = 1.0e16;
        public const double DefaultMass = 30000.0;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly (string Suffix, double Kg)[] Units =
        {
            ("kg", 1.0), ("gt", 1e12), ("mt", 1e9), ("kt", 1e6), ("t", 1e3),
        };

        /// <summary>Parses a mass in kg (before clamping). False for blank, malformed, non-finite or non-positive text.</summary>
        public static bool TryParse(string text, out double kg)
        {
            kg = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim().Replace(" ", "").Replace(" ", "").Replace("_", "");
            double scale = 1.0;
            string lower = s.ToLowerInvariant();
            foreach ((string suffix, double unitKg) in Units)
            {
                if (lower.EndsWith(suffix, StringComparison.Ordinal) && lower.Length > suffix.Length &&
                    !(suffix == "t" && lower.EndsWith("kt", StringComparison.Ordinal)))
                {
                    scale = unitKg;
                    s = s.Substring(0, s.Length - suffix.Length);
                    break;
                }
            }

            // Separators: "60,000.5" (comma = thousands); "2,5" (single comma = decimal mark); "1,000,000".
            if (s.Contains(",") && s.Contains(".")) s = s.Replace(",", "");
            else if (s.Contains(",")) s = s.IndexOf(',') == s.LastIndexOf(',') && !LooksLikeThousands(s) ? s.Replace(',', '.') : s.Replace(",", "");

            if (!double.TryParse(s, NumberStyles.Float, Inv, out double value)) return false;
            value *= scale;
            if (double.IsNaN(value) || double.IsInfinity(value) || !(value > 0)) return false;
            kg = value;
            return true;
        }

        // "30,000" is thirty thousand, not thirty: a single comma followed by exactly three digits and no exponent.
        private static bool LooksLikeThousands(string s)
        {
            int comma = s.IndexOf(',');
            string tail = s.Substring(comma + 1);
            return tail.Length == 3 && char.IsDigit(tail[0]) && char.IsDigit(tail[1]) && char.IsDigit(tail[2]) && comma > 0;
        }

        public static double Clamp(double kg)
        {
            if (double.IsNaN(kg)) return DefaultMass;
            return Math.Max(MinMass, Math.Min(MaxMass, kg));
        }

        /// <summary>Text for the input field: plain digits below 10 million kg, scientific notation above ("1.25e15").</summary>
        public static string ToFieldText(double kg)
        {
            if (kg < 1e7) return Math.Round(kg).ToString("0", Inv);
            string e = kg.ToString("0.###e0", Inv);
            return e;
        }

        /// <summary>Mass in the most readable of t / kt / Mt / Gt, e.g. "30 t", "12 kt", "1.25 Mt", "1,250 Gt".</summary>
        public static string Format(double kg)
        {
            double t = kg / 1e3;
            if (t < 1e3) return Number(t) + " t";
            if (t < 1e6) return Number(t / 1e3) + " kt";
            if (t < 1e9) return Number(t / 1e6) + " Mt";
            return Number(t / 1e9) + " Gt";
        }

        /// <summary>Mass in kg with a power of ten for big values, e.g. "30,000 kg" or "1.2 × 10<sup>7</sup> kg" (TMP rich text).</summary>
        public static string FormatKg(double kg)
        {
            if (kg < 1e7) return Math.Round(kg).ToString("#,0", Inv) + " kg";
            int exp = (int)Math.Floor(Math.Log10(kg));
            double mantissa = kg / Math.Pow(10, exp);
            if (mantissa >= 9.995)
            {
                mantissa /= 10;
                exp++;
            }

            return mantissa.ToString("0.##", Inv) + " × 10<sup>" + exp.ToString(Inv) + "</sup> kg";
        }

        /// <summary>Diameter (m) of a sphere of this mass and bulk density.</summary>
        public static double SphereDiameter(double kg, double density) =>
            density > 0 && kg > 0 ? Math.Pow(6.0 * kg / (Math.PI * density), 1.0 / 3.0) : 0;

        /// <summary>A length in m or km, e.g. "2.6 m", "19 m", "1.2 km", "10.3 km".</summary>
        public static string FormatLength(double metres)
        {
            if (metres < 1000) return Number(metres) + " m";
            return Number(metres / 1000) + " km";
        }

        /// <summary>Three significant figures, thousands separators, no trailing zeros.</summary>
        public static string Number(double v)
        {
            if (v >= 100) return Math.Round(v).ToString("#,0", Inv);
            if (v >= 10) return v.ToString("0.#", Inv);
            return v.ToString("0.##", Inv);
        }
    }
}
