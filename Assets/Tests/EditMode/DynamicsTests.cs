using System;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    public class DynamicsTests
    {
        [TestCase(6371000.0, 9.8202)]
        [TestCase(6771000.0, 8.6943)]
        [TestCase(42164000.0, 0.22421)]
        [TestCase(384400000.0, 0.0026976)]
        public void GravityAtKnownRadii(double r, double g)
        {
            Assert.That(Gravity.Magnitude(r), Is.EqualTo(g).Within(g * 1e-4));
            Vec3d a = Gravity.Acceleration(new Vec3d(0, 0, r));
            Assert.That(a.Magnitude, Is.EqualTo(g).Within(g * 1e-4));
            Assert.That(a.Z, Is.LessThan(0), "gravity points to the Earth's centre");
        }

        [Test]
        public void GravityIsCentralInEveryDirection()
        {
            var p = new Vec3d(3.0e6, -4.0e6, 5.0e6);
            Vec3d a = Gravity.Acceleration(p);
            Assert.That(Vec3d.Cross(a, p).Magnitude / (a.Magnitude * p.Magnitude), Is.LessThan(1e-12));
            Assert.That(Vec3d.Dot(a, p), Is.LessThan(0));
        }

        [Test]
        public void FreeFallConservesEnergyWithoutDrag()
        {
            // Pure two-body motion integrated with the same RK4 used by the simulation.
            var y = new KinematicState(new Vec3d(7.0e6, 1.0e6, -2.0e6), new Vec3d(-1500, 2500, 800), 0, 0);
            Func<double, KinematicState, KinematicState> f = (t, s) =>
                new KinematicState(s.Velocity, Gravity.Acceleration(s.Position), 0, 0);
            double E(KinematicState s) => 0.5 * s.Velocity.SqrMagnitude + Gravity.SpecificPotential(s.Position);
            double e0 = E(y);
            double t = 0;
            while (y.Position.Magnitude > PhysicalConstants.EarthRadius && t < 5000)
            {
                y = Rk4Integrator.Step(y, t, 0.05, f);
                t += 0.05;
            }

            Assert.That(Math.Abs(E(y) - e0) / Math.Abs(e0), Is.LessThan(1e-9));
        }

        [Test]
        public void FreeFallFromRestIsRadialAndMatchesEnergy()
        {
            // Released from rest at r0, the speed at r is sqrt(2 mu (1/r - 1/r0)) when drag is negligible.
            double r0 = PhysicalConstants.EarthRadius + 2.0e6;
            var y = new KinematicState(new Vec3d(r0, 0, 0), Vec3d.Zero, 0, 0);
            Func<double, KinematicState, KinematicState> f = (t, s) => new KinematicState(s.Velocity, Gravity.Acceleration(s.Position), 0, 0);
            double time = 0;
            double rTarget = PhysicalConstants.EarthRadius + 1.0e6;
            while (y.Position.Magnitude > rTarget)
            {
                y = Rk4Integrator.Step(y, time, 0.05, f);
                time += 0.05;
            }

            double r = y.Position.Magnitude;
            double expected = Math.Sqrt(2 * PhysicalConstants.EarthMu * (1 / r - 1 / r0));
            Assert.That(y.Velocity.Magnitude, Is.EqualTo(expected).Within(expected * 1e-8));
            Assert.That(Math.Abs(y.Velocity.Y) + Math.Abs(y.Velocity.Z), Is.LessThan(1e-9));
        }

        [Test]
        public void DragAlwaysOpposesRelativeVelocity()
        {
            var rng = new Random(1234);
            for (int i = 0; i < 2000; i++)
            {
                var v = new Vec3d(rng.NextDouble() * 4e4 - 2e4, rng.NextDouble() * 4e4 - 2e4, rng.NextDouble() * 4e4 - 2e4);
                var wind = new Vec3d(rng.NextDouble() * 200 - 100, rng.NextDouble() * 200 - 100, rng.NextDouble() * 200 - 100);
                Vec3d rel = v - wind;
                double rho = rng.NextDouble() * 1.3;
                double cd = Drag.Coefficient(rng.NextDouble() * 60, Math.Pow(10, rng.NextDouble() * 8 - 4));
                Vec3d force = Drag.Force(rho, cd, 1.0 + rng.NextDouble() * 10, rel);
                if (force.Magnitude == 0)
                {
                    continue;
                }

                double cos = Vec3d.Dot(force, rel) / (force.Magnitude * rel.Magnitude);
                Assert.That(cos, Is.EqualTo(-1.0).Within(1e-9));
            }
        }

        [Test]
        public void DragWithZeroWindOrDirectionHalfIsFinite()
        {
            var p = new Vec3d(0, 0, PhysicalConstants.EarthRadius + 30000);
            Vec3d wind0 = Geo.WindVector(p, 0, 0.5);
            Vec3d wind1 = Geo.WindVector(p, 10, 0.5);
            Assert.That(wind0.Magnitude, Is.EqualTo(0));
            Assert.That(wind1.Magnitude, Is.EqualTo(10).Within(1e-9));
            Assert.That(Vec3d.Dot(wind1, p.Normalized), Is.EqualTo(0).Within(1e-9), "wind is horizontal");
            Vec3d f = Drag.Force(0.018, Drag.Coefficient(Atmosphere.Sample(30000), 3000, 2), 3.0, new Vec3d(0, 0, -3000) - wind0);
            Assert.That(f.IsFinite && f.Magnitude > 0, Is.True);
        }

        [Test]
        public void DragCoefficientRegimes()
        {
            Assert.That(Drag.Coefficient(20, 1e-5), Is.EqualTo(Drag.HypersonicContinuumCd));
            Assert.That(Drag.Coefficient(20, 100), Is.EqualTo(Drag.FreeMolecularCd));
            Assert.That(Drag.Coefficient(0.3, 1e-6), Is.EqualTo(0.47).Within(0.02));
            double mid = Drag.Coefficient(20, 0.3);
            Assert.That(mid, Is.GreaterThan(Drag.HypersonicContinuumCd).And.LessThan(Drag.FreeMolecularCd));
        }

        [Test]
        public void ImplicitHeatingNeverGoesNegativeAndClampsAtAblationTemperature()
        {
            var mat = MeteorMaterial.CreateCustom(3000, 400, 1);
            // Tiny body, very hot start in vacuum, huge step: explicit Euler would overshoot below 0 K.
            HeatingStep cool = Heating.Step(mat, 1e-3, 1900, 1e-4, 0, 0, 2.725, 10.0);
            Assert.That(cool.Temperature, Is.GreaterThanOrEqualTo(0).And.LessThan(1900));
            // Strong heating: held at T_abl, surplus ablates.
            HeatingStep hot = Heating.Step(mat, 100, 300, 0.5, 0.05, 15000, 250, 0.01);
            Assert.That(hot.Temperature, Is.EqualTo(mat.AblationTemperature));
            Assert.That(hot.MassLost, Is.GreaterThan(0));
        }

        [Test]
        public void SpaceEquilibriumTemperatureIsAbout278K()
        {
            Assert.That(Heating.SpaceEquilibriumTemperature, Is.EqualTo(278.3).Within(0.5));
            var mat = MeteorMaterial.CreateCustom(3000, 800, 0.5);
            HeatingStep s = Heating.Step(mat, 5000, Heating.SpaceEquilibriumTemperature, 0.88, 0, 0, PhysicalConstants.CmbTemperature, 1.0);
            Assert.That(s.Temperature, Is.EqualTo(Heating.SpaceEquilibriumTemperature).Within(1e-3));
        }

        [Test]
        public void LatitudeLongitudeConvention()
        {
            Geo.LatitudeLongitude(new Vec3d(7e6, 0, 0), 0, out double lat, out double lon);
            Assert.That(lat, Is.EqualTo(0).Within(1e-9));
            Assert.That(lon, Is.EqualTo(0).Within(1e-9));
            Geo.LatitudeLongitude(new Vec3d(0, 7e6, 0), 0, out lat, out _);
            Assert.That(lat, Is.EqualTo(90).Within(1e-9));
            // After a quarter of a sidereal day the same inertial point lies 90 degrees further west.
            Geo.LatitudeLongitude(new Vec3d(7e6, 0, 0), 0.25 * 2 * Math.PI / PhysicalConstants.EarthRotationRate, out _, out lon);
            Assert.That(lon, Is.EqualTo(-90).Within(1e-6));
        }
    }
}
