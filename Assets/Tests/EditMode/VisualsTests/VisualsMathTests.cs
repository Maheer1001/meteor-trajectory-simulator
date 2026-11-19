using System;
using MTS.Core;
using NUnit.Framework;

namespace MTS.Visuals.Tests
{
    public class VisualsMathTests
    {
        [Test]
        public void Blackbody_IsRedWhenCoolAndBluishWhenHot()
        {
            Blackbody.Rgb(1500, out double r1, out double g1, out double b1);
            Assert.That(r1, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(g1, Is.LessThan(0.6));
            Assert.That(b1, Is.LessThan(0.2));

            Blackbody.Rgb(6600, out double r2, out double g2, out double b2);
            Assert.That(r2, Is.GreaterThan(0.95));
            Assert.That(g2, Is.GreaterThan(0.9));
            Assert.That(b2, Is.GreaterThan(0.9));

            Blackbody.Rgb(20000, out double r3, out _, out double b3);
            Assert.That(b3, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(r3, Is.LessThan(b3));
        }

        [Test]
        public void Blackbody_ClampsOutOfRangeAndNaN()
        {
            Blackbody.Rgb(10, out double r, out double g, out double b);
            Blackbody.Rgb(1000, out double r0, out double g0, out double b0);
            Assert.That(new[] { r, g, b }, Is.EqualTo(new[] { r0, g0, b0 }));
            Assert.That(Blackbody.GlowWeight(double.NaN), Is.EqualTo(0));
        }

        [Test]
        public void GlowWeight_IsZeroBelowDraperPointAndMonotonic()
        {
            Assert.That(Blackbody.GlowWeight(300), Is.EqualTo(0));
            Assert.That(Blackbody.GlowWeight(Blackbody.GlowOnset), Is.EqualTo(0));
            double prev = 0;
            for (double t = 800; t <= 3000; t += 100)
            {
                double w = Blackbody.GlowWeight(t);
                Assert.That(w, Is.GreaterThanOrEqualTo(prev));
                prev = w;
            }

            Assert.That(Blackbody.GlowWeight(3000), Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void LuminosityBrightness_IsLogScaled()
        {
            Assert.That(Blackbody.LuminosityToBrightness(0), Is.EqualTo(0));
            Assert.That(Blackbody.LuminosityToBrightness(1e4), Is.EqualTo(0));
            Assert.That(Blackbody.LuminosityToBrightness(1e7), Is.EqualTo(0.5).Within(1e-12));
            Assert.That(Blackbody.LuminosityToBrightness(1e10), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(Blackbody.LuminosityToBrightness(1e30), Is.EqualTo(1.5));
        }

        [Test]
        public void DisplayFlip_IsItsOwnInverse()
        {
            var v = new Vec3d(1, 2, 3);
            Assert.That(VisualFrame.FromDisplay(VisualFrame.ToDisplay(v)), Is.EqualTo(v));
            Assert.That(VisualFrame.ToDisplay(v).Z, Is.EqualTo(-3));
        }

        [Test]
        public void CurvatureDrop_MatchesSphereGeometry()
        {
            double r = PhysicalConstants.EarthRadius;
            Assert.That(VisualFrame.CurvatureDrop(0), Is.EqualTo(0));
            // ~78.5 m at 31.6 km (d^2 / 2R for small d)
            Assert.That(VisualFrame.CurvatureDrop(31623), Is.EqualTo(31623.0 * 31623.0 / (2 * r)).Within(0.01));
            Assert.That(VisualFrame.CurvatureDrop(r), Is.EqualTo(r));
            Assert.That(VisualFrame.CurvatureDrop(2 * r), Is.EqualTo(r));
        }

        [Test]
        public void LocalFrame_RoundTripsAndPutsAnchorAtOrigin()
        {
            var p = new Vec3d(3.0e6, 4.0e6, -2.5e6);
            var frame = new LocalFrame(p);
            Vec3d anchorLocal = frame.ToLocal(frame.Anchor);
            Assert.That(anchorLocal.Magnitude, Is.LessThan(1e-6));
            Assert.That(frame.AltitudeOf(new Vec3d(0, 1000, 0)), Is.EqualTo(1000).Within(1e-6));

            var q = new Vec3d(3.1e6, 4.05e6, -2.4e6);
            Vec3d back = frame.ToInertial(frame.ToLocal(q));
            Assert.That((back - q).Magnitude, Is.LessThan(1e-6));

            // Orthonormal axes
            Assert.That(Vec3d.Dot(frame.East, frame.Up), Is.EqualTo(0).Within(1e-12));
            Assert.That(Vec3d.Dot(frame.North, frame.Up), Is.EqualTo(0).Within(1e-12));
            Assert.That(Vec3d.Dot(frame.North, frame.East), Is.EqualTo(0).Within(1e-12));
            Assert.That(frame.North.Magnitude, Is.EqualTo(1).Within(1e-12));
        }

        [Test]
        public void LocalFrame_NorthPointsTowardsThePole()
        {
            var frame = new LocalFrame(new Vec3d(PhysicalConstants.EarthRadius, 0, 0));
            Assert.That(frame.North.Y, Is.EqualTo(1).Within(1e-12));
            Assert.That(frame.East.Z, Is.EqualTo(-1).Within(1e-12)); // Geo.East at lon 0 is -Z in the inertial frame
        }

        [Test]
        public void PathPredictor_ReachesTheEndWithoutTouchingTheSession()
        {
            MeteorProperties props = MeteorProperties.FromInputs(60000, 3500, 800, 0.5, 1, 1, 1,
                EntryMode.Inbound, 17000, 45);
            var session = new SimulationRunner(props);
            MeteorState before = session.State;

            var predictor = new PathPredictor(props, session.State, 0, 0, 500);
            int guard = 0;
            while (!predictor.Advance(5000) && guard++ < 1000)
            {
            }

            Assert.That(predictor.IsDone, Is.True);
            Assert.That(predictor.Result, Is.Not.Null);
            Assert.That(predictor.Points.Count, Is.GreaterThan(10));
            Assert.That(predictor.Times[predictor.Times.Count - 1], Is.GreaterThan(predictor.Times[0]));
            Assert.That(session.State.Position, Is.EqualTo(before.Position));
            Assert.That(session.HasEnded, Is.False);

            // Same physics as a straight run of the real runner.
            SimulationResult direct = new SimulationRunner(props).RunToEnd();
            Assert.That(predictor.Result.Reason, Is.EqualTo(direct.Reason));
            Assert.That((predictor.Result.FinalState.Position - direct.FinalState.Position).Magnitude, Is.LessThan(5000));
        }

        [Test]
        public void PathPredictor_BoundsPointCount()
        {
            MeteorProperties props = MeteorProperties.FromInputs(60000, 3500, 800, 0.5, 25, 25, 25);
            var predictor = new PathPredictor(props, new SimulationRunner(props).State, 0, 0, 100, 64);
            predictor.RunToEnd();
            Assert.That(predictor.Points.Count, Is.LessThanOrEqualTo(64));
            Assert.That(predictor.Result, Is.Not.Null);
        }
    }
}
