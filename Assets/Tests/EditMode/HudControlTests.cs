using System;
using MTS.Core.Display;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    public class HudControlTests
    {
        [Test]
        public void WarpLadderStepsAndClamps()
        {
            var ladder = new TimeWarpLadder(TimeWarpLadder.SpaceLevels, 1000);
            Assert.That(ladder.Multiplier, Is.EqualTo(1000));
            Assert.That(ladder.Up(), Is.True);
            Assert.That(ladder.Multiplier, Is.EqualTo(2000));
            ladder.Set(0);
            Assert.That(ladder.Down(), Is.False);
            Assert.That(ladder.Multiplier, Is.EqualTo(1));
            ladder.Set(99);
            Assert.That(ladder.AtMax, Is.True);
            Assert.That(ladder.Up(), Is.False);
        }

        [Test]
        public void WarpLadderPicksNearestLevelInLogSpace()
        {
            var ladder = new TimeWarpLadder(TimeWarpLadder.AtmosphereLevels, 3);
            Assert.That(ladder.Multiplier, Is.EqualTo(2).Or.EqualTo(5));
            ladder.SetNearest(0.3);
            Assert.That(ladder.Multiplier, Is.EqualTo(0.25));
            ladder.SetNearest(-1);
            Assert.That(ladder.Index, Is.EqualTo(0));
        }

        [Test]
        public void WarpIsSimSecondsPerTickOverTickLength()
        {
            var ladder = new TimeWarpLadder(TimeWarpLadder.SpaceLevels, 500);
            Assert.That(ladder.SimSecondsPerTick(0.02), Is.EqualTo(10.0).Within(1e-12));
            Assert.That(TimeWarpLadder.Achieved(10.0, 0.02), Is.EqualTo(500.0).Within(1e-9));
            Assert.That(TimeWarpLadder.Achieved(1.0, 0.0), Is.EqualTo(0.0));
        }

        [Test]
        public void WarpLadderRejectsBadLevels()
        {
            Assert.Throws<ArgumentException>(() => new TimeWarpLadder(new double[0], 1));
            Assert.Throws<ArgumentException>(() => new TimeWarpLadder(new double[] { 1, 1 }, 1));
            Assert.Throws<ArgumentException>(() => new TimeWarpLadder(new double[] { 0, 1 }, 1));
        }

        [TestCase(1.0, 0.0, 0.0, 90.0)]    // dragged east  -> towards east
        [TestCase(0.0, 1.0, 0.5, 0.0)]     // dragged north -> towards north
        [TestCase(-1.0, 0.0, 1.0, 270.0)]  // dragged west
        [TestCase(0.0, -1.0, -0.5, 180.0)] // dragged south
        public void CompassOffsetMapsToGeoAzimuthFraction(double east, double north, double fraction, double bearing)
        {
            WindCompassMath.FromOffset(east * 50, north * 50, 100, 80, out double speed, out double f);
            Assert.That(speed, Is.EqualTo(40).Within(1e-9));
            Assert.That(f, Is.EqualTo(fraction).Within(1e-9));
            Assert.That(WindCompassMath.BearingTowards(f), Is.EqualTo(bearing).Within(1e-9));
        }

        [Test]
        public void CompassAgreesWithGeoWindVector()
        {
            // At a point on the equator (+X), east is +Z... whatever the frame, the wind vector must point along
            // the compass offset when expressed in the local east/north basis.
            var position = new Vec3d(PhysicalConstants.EarthRadius + 50000, 1000, 2000);
            WindCompassMath.FromOffset(30, 40, 100, 100, out double speed, out double f);
            Vec3d wind = Geo.WindVector(position, speed, f);
            double e = Vec3d.Dot(wind, Geo.East(position));
            double n = Vec3d.Dot(wind, Geo.North(position));
            Assert.That(speed, Is.EqualTo(50).Within(1e-9));
            Assert.That(e, Is.EqualTo(30).Within(1e-6));
            Assert.That(n, Is.EqualTo(40).Within(1e-6));
        }

        [Test]
        public void CompassRoundTripsAndClampsToRadius()
        {
            WindCompassMath.ToOffset(25, -0.25, 100, 50, out double east, out double north);
            WindCompassMath.FromOffset(east, north, 100, 50, out double speed, out double f);
            Assert.That(speed, Is.EqualTo(25).Within(1e-9));
            Assert.That(f, Is.EqualTo(-0.25).Within(1e-9));

            WindCompassMath.FromOffset(500, 0, 100, 50, out speed, out _);
            Assert.That(speed, Is.EqualTo(50));
            WindCompassMath.FromOffset(0, 0, 100, 50, out speed, out f);
            Assert.That(speed, Is.EqualTo(0));
            Assert.That(f, Is.EqualTo(0));
        }

        [TestCase(0.0, "N")]
        [TestCase(44.0, "NE")]
        [TestCase(181.0, "S")]
        [TestCase(350.0, "N")]
        [TestCase(-90.0, "W")]
        public void CardinalNames(double bearing, string expected)
        {
            Assert.That(WindCompassMath.CardinalName(bearing), Is.EqualTo(expected));
        }
    }
}
