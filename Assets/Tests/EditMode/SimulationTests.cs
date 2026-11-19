using System;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    public class SimulationTests
    {
        private static readonly double[] Masses = { 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9 };
        private static readonly double[] Densities = { 1000, 3000, 5500, 8000 };
        private static readonly double[,] Winds = { { 0, 0 }, { 0, 0.5 }, { 10, 0.5 }, { 105, -0.25 }, { 15, 0.25 } };

        private static void AssertSaneThroughout(SimulationRunner runner, string label)
        {
            double density = runner.Properties.Material.Density;
            int guard = 0;
            while (!runner.HasEnded)
            {
                runner.Step(runner.StepSizeFor(runner.State.Altitude));
                MeteorState s = runner.State;
                Assert.That(s.IsFinite, Is.True, label + " " + s);
                Assert.That(s.Mass, Is.GreaterThanOrEqualTo(0), label);
                Assert.That(s.EquivalentRadius(density), Is.GreaterThanOrEqualTo(0), label);
                Assert.That(s.Temperature, Is.GreaterThanOrEqualTo(0), label);
                Assert.That(s.Temperature, Is.LessThanOrEqualTo(runner.Properties.Material.AblationTemperature + 1e-9), label);
                Assert.That(++guard, Is.LessThan(5_000_000), label + " did not end");
            }

            SimulationResult r = runner.Result;
            Assert.That(r.Reason, Is.Not.EqualTo(EndReason.Error), label + ": " + r.Message);
            Assert.That(r.Reason, Is.Not.EqualTo(EndReason.Timeout), label);
            Assert.That(r.Reason, Is.Not.EqualTo(EndReason.None), label);
            Assert.That(double.IsNaN(r.ImpactSpeed), Is.False, label);
            ImpactAssessment a = ImpactModel.Assess(r, runner.Properties);
            foreach (double radius in a.BlastRadii)
            {
                Assert.That(double.IsNaN(radius) || double.IsInfinity(radius), Is.False, label);
                Assert.That(radius, Is.LessThan(1.0e7), label + " blast radius implausibly large");
            }
        }

        [Test]
        public void NoNaNAcrossMassDensityWindSweep([Values(EntryMode.ClassicNea, EntryMode.RealisticEntry)] EntryMode mode)
        {
            foreach (double m in Masses)
            {
                foreach (double rho in Densities)
                {
                    for (int w = 0; w < Winds.GetLength(0); w++)
                    {
                        var p = MeteorProperties.FromInputs(m, rho, 800, 0.5, 1, 1, 1, mode);
                        var runner = new SimulationRunner(p, Winds[w, 0], Winds[w, 1]);
                        AssertSaneThroughout(runner, $"{mode} m={m} rho={rho} wind={Winds[w, 0]}/{Winds[w, 1]}");
                    }
                }
            }
        }

        [Test]
        public void NoNaNAcrossInputScreenRanges()
        {
            // Random combinations over the input screen's ranges, both modes.
            var rng = new Random(42);
            for (int i = 0; i < 40; i++)
            {
                double Coord() => (rng.Next(2) == 0 ? -1 : 1) * (1 + rng.NextDouble() * 49);
                var mode = i % 2 == 0 ? EntryMode.ClassicNea : EntryMode.RealisticEntry;
                var p = MeteorProperties.FromInputs(5000 + rng.NextDouble() * 55000, 1000 + rng.NextDouble() * 7000,
                    400 + rng.NextDouble() * 1100, rng.NextDouble(), Coord(), Coord(), Coord(), mode);
                var runner = new SimulationRunner(p, 10 + rng.NextDouble() * 95, rng.NextDouble() * 0.5 - 0.25);
                AssertSaneThroughout(runner, "random case " + i);
            }
        }

        [Test]
        public void PreviouslyNaNInputsNowFinish()
        {
            foreach (double m in new[] { 20000.0, 5000.0 })
            {
                var p = MeteorProperties.FromInputs(m, 3000, 50, 0.5, 1, 1, 1);
                SimulationResult r = new SimulationRunner(p, 10, 0.05).RunToEnd();
                Assert.That(r.Reason, Is.EqualTo(EndReason.Impact).Or.EqualTo(EndReason.Airburst).Or.EqualTo(EndReason.Disintegrated));
                Assert.That(r.FinalState.IsFinite, Is.True);
            }
        }

        [Test]
        public void ClassicReleaseFromRestFallsRadially()
        {
            var p = MeteorProperties.FromInputs(60000, 7800, 800, 0.5, 1, 1, 1);
            SimulationResult r = new SimulationRunner(p, 0, 0).RunToEnd();
            Assert.That(r.Reason, Is.EqualTo(EndReason.Impact));
            Assert.That(r.ImpactAngleDegrees, Is.GreaterThan(89.9));
            // Entry speed from energy conservation: sqrt(2 mu (1/r_e - 1/r_0)) (drag above 120 km is negligible)
            double r0 = p.StartPosition.Magnitude;
            double re = PhysicalConstants.EarthRadius + PhysicalConstants.AtmosphereEntryAltitude;
            double expected = Math.Sqrt(2 * PhysicalConstants.EarthMu * (1 / re - 1 / r0));
            Assert.That(r.AtmosphereEntrySpeed, Is.EqualTo(expected).Within(expected * 2e-3));
            Assert.That(r.MassAtEnd, Is.LessThan(60000).And.GreaterThan(1000));
        }

        [Test]
        public void RealisticModeReachesEntryInterfaceAtRequestedSpeed()
        {
            var p = MeteorProperties.FromInputs(60000, 7800, 800, 0.5, 10, -20, 5, EntryMode.RealisticEntry);
            var runner = new SimulationRunner(p, 15, 0);
            while (runner.Stage == SimulationStage.Space)
            {
                runner.Step(runner.StepSizeFor(runner.State.Altitude));
            }

            MeteorState s = runner.State;
            double angle = Math.Asin(-Vec3d.Dot(s.Velocity.Normalized, s.Position.Normalized)) * 180 / Math.PI;
            Assert.That(s.Speed, Is.EqualTo(17000).Within(30));
            Assert.That(angle, Is.EqualTo(45).Within(0.5));
        }

        [Test]
        public void StonyBodyBreaksUpNear28KmAt4KmPerSecond()
        {
            // Y(3000 kg/m^3) = 3.35e5 Pa; rho_air v^2 = Y at 4 km/s gives rho_air of about 0.021 kg/m^3, i.e. about 27-28 km.
            var mat = MeteorMaterial.CreateCustom(3000, 800, 0);
            var start = new Vec3d(0, 0, PhysicalConstants.EarthRadius + 60000);
            var p = MeteorProperties.Create(20000, mat, 0, start, initialVelocity: new Vec3d(0, 0, -4000));
            SimulationResult r = new SimulationRunner(p).RunToEnd();
            Assert.That(r.BreakupAltitude, Is.EqualTo(27500).Within(2500));
            Assert.That(r.Reason, Is.EqualTo(EndReason.Airburst).Or.EqualTo(EndReason.Impact));
        }

        [Test]
        public void TunguskaLikeAirburst()
        {
            // EIEP example: 75 m stony body (2000 kg/m^3) at 17 km/s and 45 degrees breaks up near 66 km and bursts near
            // 5 km (exponential atmosphere). With USSA76 and a time-domain pancake we expect the same picture.
            var mat = MeteorMaterial.CreateCustom(2000, 800, 0);
            double mass = 2000 * Math.PI / 6 * Math.Pow(75, 3);
            var start = new Vec3d(0, 0, PhysicalConstants.EarthRadius + PhysicalConstants.AtmosphereEntryAltitude);
            var p = MeteorProperties.Create(mass, mat, 0, start, EntryMode.RealisticEntry, 17000, 45);
            SimulationResult r = new SimulationRunner(p).RunToEnd();
            Assert.That(r.Reason, Is.EqualTo(EndReason.Airburst));
            Assert.That(r.BreakupAltitude, Is.InRange(55000, 70000));
            Assert.That(r.EndAltitude, Is.InRange(1000, 15000));
            ImpactAssessment a = ImpactModel.Assess(r, p);
            Assert.That(a.EnergyKilotons / 1000, Is.InRange(5, 20)); // Mt
            Assert.That(a.BlastRadii[3], Is.InRange(5000, 60000));   // 2 psi ring: tens of km
        }

        [Test]
        public void EndReasonImpact()
        {
            var p = MeteorProperties.FromInputs(60000, 8000, 800, 0, 1, 1, 1);
            Assert.That(new SimulationRunner(p).RunToEnd().Reason, Is.EqualTo(EndReason.Impact));
        }

        [Test]
        public void EndReasonDisintegrated()
        {
            // Small fragile body at high speed without fragmentation: ablates away.
            var mat = MeteorMaterial.CreateCustom(1000, 800, 0);
            var start = new Vec3d(0, 0, PhysicalConstants.EarthRadius + PhysicalConstants.AtmosphereEntryAltitude);
            var p = MeteorProperties.Create(20, mat, 0, start, EntryMode.RealisticEntry, 30000, 60, enableFragmentation: false);
            SimulationResult r = new SimulationRunner(p).RunToEnd();
            Assert.That(r.Reason, Is.EqualTo(EndReason.Disintegrated));
            Assert.That(ImpactModel.Assess(r, p).Regime, Is.EqualTo(ImpactRegime.None));
        }

        [Test]
        public void EndReasonTimeout()
        {
            // Circular orbit at 2000 km never comes down.
            double r = PhysicalConstants.EarthRadius + 2.0e6;
            var start = new Vec3d(r, 0, 0);
            var v = new Vec3d(0, 0, Math.Sqrt(PhysicalConstants.EarthMu / r));
            var p = MeteorProperties.Create(1000, MeteorMaterial.CreateCustom(3000, 800, 0), 0, start, initialVelocity: v);
            var runner = new SimulationRunner(p) { MaxSimulatedTime = 2000 };
            Assert.That(runner.RunToEnd().Reason, Is.EqualTo(EndReason.Timeout));
        }

        [Test]
        public void EndReasonErrorInsteadOfHanging()
        {
            var bad = new MeteorMaterial("bad", double.NaN, 800, 8e6, 2000, 0.8, 0.1, 1e6);
            var start = new Vec3d(0, 0, PhysicalConstants.EarthRadius + 50000);
            var p = MeteorProperties.Create(1000, bad, 0, start, initialVelocity: new Vec3d(0, 0, -3000));
            SimulationResult r = new SimulationRunner(p).RunToEnd();
            Assert.That(r.Reason, Is.EqualTo(EndReason.Error));
            Assert.That(r.Message, Does.Contain("Non-finite").Or.Contain("Exception"));
            Assert.That(r.FinalState.IsFinite, Is.True, "the last good state is kept");
        }

        [Test]
        public void SessionResetGivesIdenticalResults()
        {
            SimulationSession session = SimulationSession.Instance;
            var p1 = MeteorProperties.FromInputs(20000, 3000, 800, 0.5, 1, 1, 1);
            session.Begin(p1);
            session.SetWind(10, 0.05);
            SimulationResult first = session.Runner.RunToEnd();
            double crater1 = session.Impact.FinalCraterDiameter;

            session.Reset();
            Assert.That(session.HasRun, Is.False);
            Assert.That(session.Result, Is.Null);
            Assert.That(session.IsPaused, Is.False);

            var p2 = MeteorProperties.FromInputs(20000, 3000, 800, 0.5, 1, 1, 1);
            session.Begin(p2);
            session.SetWind(10, 0.05);
            SimulationResult second = session.Runner.RunToEnd();

            Assert.That(second.Reason, Is.EqualTo(first.Reason));
            Assert.That(second.EndTime, Is.EqualTo(first.EndTime));
            Assert.That(second.ImpactSpeed, Is.EqualTo(first.ImpactSpeed));
            Assert.That(second.MassAtEnd, Is.EqualTo(first.MassAtEnd));
            Assert.That(second.FinalState.Position, Is.EqualTo(first.FinalState.Position));
            Assert.That(session.Impact.FinalCraterDiameter, Is.EqualTo(crater1));
            session.Reset();
        }

        [Test]
        public void ResultDoesNotDependOnTimeWarp()
        {
            // The same run advanced in frame-sized chunks for several time-scale settings gives identical results.
            SimulationResult reference = null;
            foreach (double slider in new[] { 1.0, 5.0, 50.0, 500.0 })
            {
                var p = MeteorProperties.FromInputs(30000, 4500, 800, 0.5, 1, 1, 1);
                var runner = new SimulationRunner(p, 15, 0);
                int guard = 0;
                while (!runner.HasEnded && guard++ < 10_000_000)
                {
                    AdvanceReport rep = runner.Advance(slider / 60.0, 1_000_000, false);
                    Assert.That(rep.Capped, Is.False);
                }

                if (reference == null)
                {
                    reference = runner.Result;
                    continue;
                }

                Assert.That(runner.Result.Reason, Is.EqualTo(reference.Reason));
                Assert.That(runner.Result.EndTime, Is.EqualTo(reference.EndTime).Within(1e-9));
                Assert.That(runner.Result.ImpactSpeed, Is.EqualTo(reference.ImpactSpeed).Within(1e-9));
            }
        }

        [Test]
        public void StepCapIsReported()
        {
            var p = MeteorProperties.FromInputs(30000, 4500, 800, 0.5, 1, 1, 1);
            var runner = new SimulationRunner(p);
            AdvanceReport rep = runner.Advance(1000, 10);
            Assert.That(rep.Capped, Is.True);
            Assert.That(rep.StepsTaken, Is.EqualTo(10));
            Assert.That(rep.DroppedSeconds, Is.GreaterThan(0));
        }

        [Test]
        public void BlankInputsUseDefaults()
        {
            var p = MeteorProperties.FromInputs(0, 0, 0, double.NaN, 0, 0, 0);
            Assert.That(p.Mass, Is.EqualTo(MeteorProperties.DefaultMass));
            Assert.That(p.Material.Density, Is.EqualTo(MeteorProperties.DefaultDensity));
            Assert.That(p.Material.SpecificHeat, Is.EqualTo(MeteorProperties.DefaultSpecificHeat));
            Assert.That(p.StartPosition.X, Is.EqualTo(25 * MeteorProperties.MetresPerCoordinateUnit));
            Assert.That(p.Substitutions.Count, Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public void TimeSeriesIsBoundedAndEndsAtTheEnd()
        {
            var p = MeteorProperties.FromInputs(30000, 4500, 800, 0.5, 50, 50, 50);
            SimulationResult r = new SimulationRunner(p).RunToEnd();
            Assert.That(r.Series.Count, Is.InRange(2, TimeSeries.Capacity));
            Assert.That(r.Series.Time[r.Series.Count - 1], Is.EqualTo(r.EndTime));
            Assert.That(r.Series.Distance[r.Series.Count - 1], Is.EqualTo(r.FinalState.DistanceTravelled));
        }
    }
}
