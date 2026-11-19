using System;

namespace MTS.Visuals
{
    /// <summary>
    /// Colour of a black body and simple brightness mappings used by the meteor visuals (pure maths, no Unity types).
    /// </summary>
    public static class Blackbody
    {
        /// <summary>Below this temperature (K) a body gives off no visible glow.</summary>
        public const double GlowOnset = 798.0; // Draper point

        /// <summary>
        /// Display colour (sRGB, 0..1, brightest channel = 1) of a black body at the given temperature (K).
        /// Fit by Tanner Helland to Mitchell Charity's blackbody table; valid 1000..40000 K and clamped outside.
        /// </summary>
        public static void Rgb(double kelvin, out double r, out double g, out double b)
        {
            double t = Math.Max(1000.0, Math.Min(40000.0, kelvin)) / 100.0;

            if (t <= 66.0)
            {
                r = 255.0;
                g = 99.4708025861 * Math.Log(t) - 161.1195681661;
            }
            else
            {
                r = 329.698727446 * Math.Pow(t - 60.0, -0.1332047592);
                g = 288.1221695283 * Math.Pow(t - 60.0, -0.0755148492);
            }

            if (t >= 66.0)
            {
                b = 255.0;
            }
            else if (t <= 19.0)
            {
                b = 0.0;
            }
            else
            {
                b = 138.5177312231 * Math.Log(t - 10.0) - 305.0447927307;
            }

            r = Clamp01(r / 255.0);
            g = Clamp01(g / 255.0);
            b = Clamp01(b / 255.0);
        }

        /// <summary>
        /// Visible glow weight 0..1 of a surface at temperature T (K): 0 below the Draper point, then rising with
        /// the radiated power (T^4) and saturating around 2500 K.
        /// </summary>
        public static double GlowWeight(double kelvin)
        {
            if (!(kelvin > GlowOnset))
            {
                return 0.0;
            }

            double x = (kelvin - GlowOnset) / (2500.0 - GlowOnset);
            x = Clamp01(x);
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>
        /// Maps a luminous power (W) to a perceptual brightness 0..~1.5 on a log scale:
        /// 0 at or below 10^4 W (a faint naked-eye meteor), 1 at 10^10 W (a superbolide).
        /// </summary>
        public static double LuminosityToBrightness(double watts)
        {
            if (!(watts > 1e4) || double.IsInfinity(watts))
            {
                return double.IsPositiveInfinity(watts) ? 1.5 : 0.0;
            }

            return Math.Min(1.5, (Math.Log10(watts) - 4.0) / 6.0);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
