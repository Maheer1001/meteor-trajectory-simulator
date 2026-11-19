using System;

namespace MTS.Core
{
    /// <summary>Small double-precision 3-D vector (metres, m/s, ...). Pure C#, no Unity dependency.</summary>
    public readonly struct Vec3d : IEquatable<Vec3d>
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3d Zero = new Vec3d(0, 0, 0);
        public static readonly Vec3d UnitX = new Vec3d(1, 0, 0);
        public static readonly Vec3d UnitY = new Vec3d(0, 1, 0);
        public static readonly Vec3d UnitZ = new Vec3d(0, 0, 1);

        public double SqrMagnitude => X * X + Y * Y + Z * Z;
        public double Magnitude => Math.Sqrt(SqrMagnitude);

        public Vec3d Normalized
        {
            get
            {
                double m = Magnitude;
                return m > 0 ? this / m : Zero;
            }
        }

        public bool IsFinite =>
            !(double.IsNaN(X) || double.IsInfinity(X) ||
              double.IsNaN(Y) || double.IsInfinity(Y) ||
              double.IsNaN(Z) || double.IsInfinity(Z));

        public static Vec3d operator +(Vec3d a, Vec3d b) => new Vec3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3d operator -(Vec3d a, Vec3d b) => new Vec3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3d operator -(Vec3d a) => new Vec3d(-a.X, -a.Y, -a.Z);
        public static Vec3d operator *(Vec3d a, double s) => new Vec3d(a.X * s, a.Y * s, a.Z * s);
        public static Vec3d operator *(double s, Vec3d a) => new Vec3d(a.X * s, a.Y * s, a.Z * s);
        public static Vec3d operator /(Vec3d a, double s) => new Vec3d(a.X / s, a.Y / s, a.Z / s);

        public static double Dot(Vec3d a, Vec3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3d Cross(Vec3d a, Vec3d b) =>
            new Vec3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static Vec3d Lerp(Vec3d a, Vec3d b, double t) => a + (b - a) * t;

        public bool Equals(Vec3d other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Vec3d v && Equals(v);
        public override int GetHashCode() => (X, Y, Z).GetHashCode();

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:G6}, {1:G6}, {2:G6})", X, Y, Z);
    }
}
