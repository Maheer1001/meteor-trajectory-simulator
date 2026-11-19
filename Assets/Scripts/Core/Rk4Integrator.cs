using System;

namespace MTS.Core
{
    /// <summary>The part of the state integrated by RK4: position, velocity and pancake diameter/rate.</summary>
    public readonly struct KinematicState
    {
        public readonly Vec3d Position;
        public readonly Vec3d Velocity;
        public readonly double PancakeDiameter;
        public readonly double PancakeRate;

        public KinematicState(Vec3d position, Vec3d velocity, double pancakeDiameter, double pancakeRate)
        {
            Position = position;
            Velocity = velocity;
            PancakeDiameter = pancakeDiameter;
            PancakeRate = pancakeRate;
        }

        public static KinematicState operator +(KinematicState a, KinematicState b) =>
            new KinematicState(a.Position + b.Position, a.Velocity + b.Velocity,
                a.PancakeDiameter + b.PancakeDiameter, a.PancakeRate + b.PancakeRate);

        public static KinematicState operator *(KinematicState a, double s) =>
            new KinematicState(a.Position * s, a.Velocity * s, a.PancakeDiameter * s, a.PancakeRate * s);
    }

    /// <summary>Classical fourth-order Runge-Kutta step for a full vector state y' = f(t, y).</summary>
    public static class Rk4Integrator
    {
        public static KinematicState Step(KinematicState y, double t, double dt, Func<double, KinematicState, KinematicState> derivative)
        {
            KinematicState k1 = derivative(t, y);
            KinematicState k2 = derivative(t + 0.5 * dt, y + k1 * (0.5 * dt));
            KinematicState k3 = derivative(t + 0.5 * dt, y + k2 * (0.5 * dt));
            KinematicState k4 = derivative(t + dt, y + k3 * dt);
            return y + (k1 + k2 * 2.0 + k3 * 2.0 + k4) * (dt / 6.0);
        }
    }
}
