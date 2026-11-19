using System;

namespace MTS.Visuals
{
    /// <summary>
    /// Sizes for the end-of-run visuals, from the real event scale. Pure maths (unit-tested).
    /// </summary>
    public static class ImpactVisualScale
    {
        /// <summary>
        /// Maximum radius (m) of the impact fireball, R ~ 0.002 E^(1/3) with E in joules (Earth Impact Effects
        /// Program, Collins et al. 2005). 1 Mt gives ~320 m; Chicxulub (~2.5e23 J) gives ~126 km.
        /// </summary>
        public static double FireballRadius(double energyJoules) => energyJoules > 0 ? 0.002 * Math.Pow(energyJoules, 1.0 / 3.0) : 0.0;

        /// <summary>
        /// 0..1 catastrophe scale on a log axis: 0 at 1 t TNT, ~0.43 at 1 Mt (Tunguska ~0.5), 1 at 1e11 kt (beyond
        /// Chicxulub's ~6e10 kt).
        /// </summary>
        public static float Severity(double kilotons)
        {
            if (!(kilotons > 0)) return 0f;
            double x = (Math.Log10(kilotons) + 3.0) / 14.0;
            return (float)Math.Max(0.0, Math.Min(1.0, x));
        }

        /// <summary>
        /// How far (m) the end camera should stand from ground zero to frame an event whose interesting radius is
        /// <paramref name="eventRadius"/> (crater, fireball or the main blast ring), so a small impact fills the view
        /// and a Chicxulub-scale one is seen from orbit-like heights. Clamped to [minimum, maximum].
        /// </summary>
        public static double EndCameraDistance(double eventRadius, double burstHeight, double fieldOfViewDegrees,
            double minimum = 60.0, double maximum = 4.0e6)
        {
            double r = Math.Max(1.0, Math.Max(eventRadius, burstHeight * 0.9));
            double halfFov = Math.Max(5.0, Math.Min(80.0, fieldOfViewDegrees)) * 0.5 * Math.PI / 180.0;
            // the event spans about 70% of the view's height
            double d = r / Math.Tan(halfFov) / 0.7;
            return Math.Max(minimum, Math.Min(maximum, d));
        }

        /// <summary>
        /// Chase-camera distance (m) for a body of radius <paramref name="bodyRadius"/> with a glowing sheath of
        /// <paramref name="sheathRadius"/>: close enough that the body reads (several body sizes), never inside the
        /// sheath. Grows with brightness so a blazing fireball is not clipped.
        /// </summary>
        public static double ChaseDistance(double bodyRadius, double sheathRadius, double brightness)
        {
            double r = Math.Max(0.05, bodyRadius);
            double s = Math.Max(r, sheathRadius);
            double b = Math.Max(0.0, Math.Min(1.0, brightness));
            return Math.Max(9.0 * r + 4.0, 3.2 * s) * (1.0 + 0.6 * b);
        }
    }
}
