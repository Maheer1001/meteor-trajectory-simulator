using System;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    public class MaterialAndTelemetryTests
    {
        private static Vec3d StartHigh => new Vec3d(1, 1, 1) * MeteorProperties.MetresPerCoordinateUnit;

        [Test]
        public void PresetsAreListedAndLookedUpById()
        {
            Assert.That(MeteorMaterialPresets.All.Count, Is.EqualTo(4));
            foreach ((string id, MeteorMaterial m) in MeteorMaterialPresets.All)
            {
                Assert.That(MeteorMaterialPresets.ById(id), Is.SameAs(m));
                Assert.That(m.SpecificHeat, Is.InRange(400.0, 1500.0), id + " SHC outside the input range");
                Assert.That(m.Emissivity, Is.InRange(0.0, 1.0));
                Assert.That(m.Strength, Is.GreaterThan(0));
            }

            Assert.That(MeteorMaterialPresets.ById(null), Is.Null);
            Assert.That(MeteorMaterialPresets.ById("unknown"), Is.Null);
            Assert.That(MeteorMaterialPresets.Iron.IsIronLike, Is.True);
            Assert.That(MeteorMaterialPresets.OrdinaryChondrite.IsIronLike, Is.False);
        }

        [Test]
        public void MixOfOneMaterialIsThatMaterial()
        {
            MeteorMaterial iron = MeteorMaterialPresets.Iron;
            MeteorMaterial mix = MeteorMaterialPresets.Mix(new[] { (iron, 3.0) });
            Assert.That(mix.Density, Is.EqualTo(iron.Density).Within(1e-9));
            Assert.That(mix.SpecificHeat, Is.EqualTo(iron.SpecificHeat).Within(1e-9));
            Assert.That(mix.Strength, Is.EqualTo(iron.Strength).Within(1e-3));
        }

        [Test]
        public void HalfAndHalfMixCombinesDensityByVolume()
        {
            MeteorMaterial a = MeteorMaterialPresets.OrdinaryChondrite;
            MeteorMaterial b = MeteorMaterialPresets.Iron;
            MeteorMaterial mix = MeteorMaterialPresets.Mix(new[] { (a, 0.5), (b, 0.5) });
            double expectedRho = 1.0 / (0.5 / a.Density + 0.5 / b.Density);
            Assert.That(mix.Density, Is.EqualTo(expectedRho).Within(1e-9));
            Assert.That(mix.SpecificHeat, Is.EqualTo(0.5 * (a.SpecificHeat + b.SpecificHeat)).Within(1e-9));
            Assert.That(mix.Strength, Is.EqualTo(Math.Sqrt(a.Strength * b.Strength)).Within(1e-3));
            Assert.That(mix.Name, Does.Contain("50%"));
        }

        [Test]
        public void MixRejectsEmptyOrZeroFractions()
        {
            Assert.Throws<ArgumentException>(() => MeteorMaterialPresets.Mix(new (MeteorMaterial, double)[0]));
            Assert.Throws<ArgumentException>(() => MeteorMaterialPresets.Mix(new[] { (MeteorMaterialPresets.Iron, 0.0) }));
        }

        [Test]
        public void PresetOverridesDensityAndShcInputs()
        {
            MeteorProperties p = MeteorProperties.FromInputs(60000, 1234, 999, 0.5, 1, 1, 1,
                preset: MeteorMaterialPresets.Iron);
            Assert.That(p.Material, Is.SameAs(MeteorMaterialPresets.Iron));

            MeteorProperties custom = MeteorProperties.FromInputs(60000, 1234, 999, 0.5, 1, 1, 1);
            Assert.That(custom.Material.Density, Is.EqualTo(1234));
            Assert.That(custom.Material.SpecificHeat, Is.EqualTo(999));
        }

        [Test]
        public void EveryPresetRunsToACleanEnd()
        {
            foreach ((string id, MeteorMaterial m) in MeteorMaterialPresets.All)
            {
                foreach (EntryMode mode in new[] { EntryMode.ClassicNea, EntryMode.RealisticEntry })
                {
                    var runner = new SimulationRunner(MeteorProperties.Create(60000, m, 0.5, StartHigh, mode), 10, 0.05);
                    SimulationResult r = runner.RunToEnd();
                    Assert.That(r.Reason, Is.Not.EqualTo(EndReason.Error), id + " " + mode + ": " + r.Message);
                    Assert.That(r.Reason, Is.Not.EqualTo(EndReason.Timeout), id + " " + mode);
                }
            }
        }

        [Test]
        public void TelemetryIsQuietInSpaceAndGlowsInTheAtmosphere()
        {
            var runner = new SimulationRunner(MeteorProperties.Create(60000, MeteorMaterialPresets.OrdinaryChondrite, 0.5,
                StartHigh, EntryMode.RealisticEntry));
            FlightTelemetry t0 = runner.Telemetry;
            Assert.That(t0.Stage, Is.EqualTo(SimulationStage.Space));
            Assert.That(t0.DynamicPressure, Is.LessThan(1.0), "near-vacuum at the start altitude");
            Assert.That(t0.MassLossRate, Is.EqualTo(0));
            Assert.That(t0.AbsoluteMagnitude, Is.EqualTo(FlightTelemetry.DarkMagnitude));

            double brightest = FlightTelemetry.DarkMagnitude;
            double maxQ = 0;
            while (!runner.HasEnded)
            {
                runner.Step(runner.StepSizeFor(runner.State.Altitude));
                FlightTelemetry t = runner.Telemetry;
                Assert.That(double.IsNaN(t.Mach) || double.IsNaN(t.Luminosity) || double.IsNaN(t.DecelerationG), Is.False);
                Assert.That(t.MassLossRate, Is.GreaterThanOrEqualTo(0));
                Assert.That(t.Diameter, Is.GreaterThanOrEqualTo(0));
                if (t.Altitude < 80000 && t.Altitude > 0)
                {
                    brightest = Math.Min(brightest, t.AbsoluteMagnitude);
                    maxQ = Math.Max(maxQ, t.DynamicPressure);
                }
            }

            Assert.That(brightest, Is.LessThan(-5), "a 60 t entry at 17 km/s should be a bright fireball");
            Assert.That(maxQ, Is.GreaterThan(1e5));
        }

        [Test]
        public void MagnitudeFromPowerFollowsPogsonScale()
        {
            Assert.That(FlightTelemetry.MagnitudeFromPower(FlightTelemetry.ZeroMagnitudePower), Is.EqualTo(0).Within(1e-12));
            Assert.That(FlightTelemetry.MagnitudeFromPower(100 * FlightTelemetry.ZeroMagnitudePower), Is.EqualTo(-5).Within(1e-9));
            Assert.That(FlightTelemetry.MagnitudeFromPower(0), Is.EqualTo(FlightTelemetry.DarkMagnitude));
        }
    }
}
