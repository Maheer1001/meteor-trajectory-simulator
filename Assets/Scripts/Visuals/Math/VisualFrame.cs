using System;
using MTS.Core;

namespace MTS.Visuals
{
    /// <summary>
    /// Coordinate frames for the visuals.
    /// The simulation's inertial frame uses right-handed maths on Unity-like axes (+Y = rotation axis). Unity renders
    /// left-handed, so drawing those numbers directly would show a mirrored Earth. <see cref="ToDisplay"/> flips Z, which
    /// turns the right-handed frame into the equivalent left-handed one; every visual uses it, so the globe, the
    /// trajectory and the impact point all agree with <see cref="Geo.LatitudeLongitude"/>.
    /// </summary>
    public static class VisualFrame
    {
        /// <summary>Inertial vector (any unit) to display axes (same unit).</summary>
        public static Vec3d ToDisplay(Vec3d v) => new Vec3d(v.X, v.Y, -v.Z);

        /// <summary>Display axes back to the inertial frame (the flip is its own inverse).</summary>
        public static Vec3d FromDisplay(Vec3d v) => new Vec3d(v.X, v.Y, -v.Z);

        /// <summary>
        /// Drop (m) of a spherical surface of radius R below its tangent plane at horizontal distance d:
        /// R - sqrt(R^2 - d^2). Exact; uses a cancellation-free form.
        /// </summary>
        public static double CurvatureDrop(double horizontalDistance, double radius = PhysicalConstants.EarthRadius)
        {
            double d2 = horizontalDistance * horizontalDistance;
            if (d2 >= radius * radius)
            {
                return radius;
            }

            return d2 / (radius + Math.Sqrt(radius * radius - d2));
        }
    }

    /// <summary>
    /// Local East-Up-North frame tangent to the Earth at an anchor point, in metres. Display axes are
    /// x = east, y = up, z = north (a standing observer facing north), which is how Unity draws a map, so no mirror
    /// is needed here. Positions are relative to the anchor's surface point, so floats stay precise near it.
    /// </summary>
    public sealed class LocalFrame
    {
        public Vec3d Anchor { get; }
        public Vec3d East { get; }
        public Vec3d Up { get; }
        public Vec3d North { get; }

        /// <summary>Builds the frame at the surface point below <paramref name="position"/> (inertial, metres).</summary>
        public LocalFrame(Vec3d position)
        {
            Up = position.Normalized;
            Anchor = Up * PhysicalConstants.EarthRadius;
            East = Geo.East(Anchor);
            North = Vec3d.Cross(Up, East).Normalized;
        }

        /// <summary>Inertial position (m) to local (east, up, north) metres relative to the anchor.</summary>
        public Vec3d ToLocal(Vec3d inertial)
        {
            Vec3d d = inertial - Anchor;
            return new Vec3d(Vec3d.Dot(d, East), Vec3d.Dot(d, Up), Vec3d.Dot(d, North));
        }

        /// <summary>Inertial direction to local axes (no translation).</summary>
        public Vec3d DirectionToLocal(Vec3d v) => new Vec3d(Vec3d.Dot(v, East), Vec3d.Dot(v, Up), Vec3d.Dot(v, North));

        /// <summary>Local (east, up, north) metres back to an inertial position.</summary>
        public Vec3d ToInertial(Vec3d local) => Anchor + East * local.X + Up * local.Y + North * local.Z;

        /// <summary>Altitude (m) of a local point above the sphere.</summary>
        public double AltitudeOf(Vec3d local) => ToInertial(local).Magnitude - PhysicalConstants.EarthRadius;
    }
}
