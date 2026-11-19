using System;

namespace MTS.Core.Display
{
    /// <summary>
    /// Maps the HUD's 2D wind compass (north up, east right) to the simulation's wind parameters and back.
    /// <see cref="Geo.WindVector"/> takes an azimuth fraction f: the wind blows along angle f*pi measured from
    /// local east towards local north. On a north-up compass that angle is exactly the usual maths angle of the
    /// drag vector (atan2(north, east)), so f = atan2(north, east) / pi, in (-1, 1].
    /// The wind is the air's velocity, i.e. the direction it blows towards.
    /// </summary>
    public static class WindCompassMath
    {
        /// <summary>
        /// Wind from a compass offset (east, north) in any units, where <paramref name="radius"/> is the compass
        /// radius in the same units and maps to <paramref name="maxSpeed"/>. Offsets beyond the radius are clamped.
        /// </summary>
        public static void FromOffset(double east, double north, double radius, double maxSpeed,
            out double speed, out double azimuthFraction)
        {
            double length = Math.Sqrt(east * east + north * north);
            if (!(radius > 0) || length < 1e-9)
            {
                speed = 0;
                azimuthFraction = 0;
                return;
            }

            speed = Math.Min(1.0, length / radius) * maxSpeed;
            azimuthFraction = Math.Atan2(north, east) / Math.PI;
        }

        /// <summary>Compass offset (east, north) for a wind, the inverse of <see cref="FromOffset"/>.</summary>
        public static void ToOffset(double speed, double azimuthFraction, double radius, double maxSpeed,
            out double east, out double north)
        {
            double r = maxSpeed > 0 ? Math.Max(0.0, Math.Min(1.0, speed / maxSpeed)) * radius : 0.0;
            double angle = azimuthFraction * Math.PI;
            east = Math.Cos(angle) * r;
            north = Math.Sin(angle) * r;
        }

        /// <summary>Compass bearing (degrees clockwise from north, [0, 360)) the wind blows towards.</summary>
        public static double BearingTowards(double azimuthFraction)
        {
            double bearing = 90.0 - azimuthFraction * 180.0;
            bearing %= 360.0;
            if (bearing < 0) bearing += 360.0;
            return bearing;
        }

        /// <summary>Eight-point compass name for a bearing ("N", "NE", ...).</summary>
        public static string CardinalName(double bearingDegrees)
        {
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            double b = bearingDegrees % 360.0;
            if (b < 0) b += 360.0;
            return names[(int)Math.Floor((b + 22.5) / 45.0) % 8];
        }
    }
}
