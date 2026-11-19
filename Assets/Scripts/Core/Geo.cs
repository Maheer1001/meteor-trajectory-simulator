using System;

namespace MTS.Core
{
    /// <summary>
    /// Frames and geographic helpers.
    /// The simulation frame is Earth-centred and inertial. Its +Y axis is the Earth's rotation axis (Unity's "up"),
    /// and at t = 0 the prime meridian lies along +X. Earth-fixed coordinates are (Xe, Ye, Ze) = (x, -z, y),
    /// which is a proper rotation of the simulation axes, so cross products carry over unchanged.
    /// The Earth turns about +Y at the sidereal rate; the atmosphere is treated as not co-rotating.
    /// </summary>
    public static class Geo
    {
        public static readonly Vec3d PolarAxis = Vec3d.UnitY;

        /// <summary>Altitude above the mean-radius sphere (m).</summary>
        public static double Altitude(Vec3d position) => position.Magnitude - PhysicalConstants.EarthRadius;

        /// <summary>Local unit vector pointing east (horizontal) at a position.</summary>
        public static Vec3d East(Vec3d position)
        {
            Vec3d e = Vec3d.Cross(PolarAxis, position);
            if (e.SqrMagnitude < 1e-12 * Math.Max(1.0, position.SqrMagnitude))
            {
                return Vec3d.UnitX; // over a pole east is undefined; pick a fixed horizontal axis
            }

            return e.Normalized;
        }

        /// <summary>Local unit vector pointing north (horizontal) at a position.</summary>
        public static Vec3d North(Vec3d position) => Vec3d.Cross(position.Normalized, East(position)).Normalized;

        /// <summary>
        /// Horizontal wind vector. Azimuth fraction f gives an angle f*pi measured from local east towards north
        /// (the atmosphere screen's direction slider runs from -0.25 to +0.25, i.e. -45 to +45 degrees).
        /// </summary>
        public static Vec3d WindVector(Vec3d position, double speed, double azimuthFraction)
        {
            if (speed == 0 || double.IsNaN(speed))
            {
                return Vec3d.Zero;
            }

            double angle = azimuthFraction * Math.PI;
            return (East(position) * Math.Cos(angle) + North(position) * Math.Sin(angle)) * speed;
        }

        /// <summary>Geodetic (spherical) latitude and longitude in degrees of an inertial position at time t (s).</summary>
        public static void LatitudeLongitude(Vec3d position, double timeSeconds, out double latitudeDeg, out double longitudeDeg)
        {
            double r = position.Magnitude;
            if (r <= 0)
            {
                latitudeDeg = 0;
                longitudeDeg = 0;
                return;
            }

            double xe = position.X;
            double ye = -position.Z;
            double ze = position.Y;
            latitudeDeg = Math.Asin(Math.Max(-1.0, Math.Min(1.0, ze / r))) * 180.0 / Math.PI;
            double lon = Math.Atan2(ye, xe) - PhysicalConstants.EarthRotationRate * timeSeconds;
            longitudeDeg = WrapDegrees(lon * 180.0 / Math.PI);
        }

        /// <summary>Wraps an angle in degrees to [-180, 180).</summary>
        public static double WrapDegrees(double degrees)
        {
            double d = (degrees + 180.0) % 360.0;
            if (d < 0)
            {
                d += 360.0;
            }

            return d - 180.0;
        }
    }
}
