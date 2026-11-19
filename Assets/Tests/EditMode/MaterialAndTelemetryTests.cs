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
            Assert.That(MeteorMaterialPresets.All.Count, Is.EqualTo(8));
            foreach ((string id, MeteorMaterial m) in MeteorMaterialPresets.All)
            {
                Assert.That(MeteorMaterialPresets.ById(id), Is.SameAs(m));
                Assert.That(m.SpecificHeat, Is.InRange(300.0, 2500.0), id + " SHC implausible");
                Assert.That(MeteorMaterialPresets.IdOf(m), Is.EqualTo(id));
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
                foreach (EntryMode mode in new[] { EntryMode.FromRest, EntryMode.Inbound })
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
                StartHigh, EntryMode.Inbound));
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
        public void TimeLeftOverAtAStageChangeIsNotCarriedIntoTheNextStage()
        {
            var runner = new SimulationRunner(MeteorProperties.Create(60000, MeteorMaterialPresets.OrdinaryChondrite, 0.5,
                StartHigh, EntryMode.Inbound));
            int guard = 0;
            while (runner.Stage == SimulationStage.Space && ++guard < 1000)
            {
                runner.Advance(20.0); // a x1000 warp tick; stops at atmospheric entry
            }

            Assert.That(runner.Stage, Is.EqualTo(SimulationStage.Atmosphere));
            double entered = runner.State.Time;
            runner.Advance(0.02, SimulationRunner.DefaultMaxStepsPerAdvance, false); // first x1 tick in the atmosphere
            Assert.That(runner.State.Time - entered, Is.LessThanOrEqualTo(0.02 + 1e-9));
            Assert.That(runner.HasEnded, Is.False);
        }

        [Test]
        public void CompositionNormalisesMergesAndLimitsComponents()
        {
            MeteorComposition c = MeteorComposition.Create(new[]
            {
                (MeteorMaterialPresets.IronId, 30.0), (MeteorMaterialPresets.OrdinaryChondriteId, 50.0),
                (MeteorMaterialPresets.IronId, 20.0), (MeteorMaterialPresets.CometaryId, 0.0),
            });
            Assert.That(c.Parts.Count, Is.EqualTo(2));
            Assert.That(c.Parts[0].Fraction, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(c.Parts[1].Fraction, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(c.Material.Density, Is.EqualTo(1.0 / (0.5 / 3300.0 + 0.5 / 7800.0)).Within(1e-9));
            Assert.That(c.Describe(), Does.Contain("50%"));

            Assert.That(MeteorComposition.Single(MeteorMaterialPresets.IronId).Material, Is.SameAs(MeteorMaterialPresets.Iron));
            Assert.Throws<System.ArgumentException>(() => MeteorComposition.Create(new[]
            {
                ("iron", 1.0), ("cometary", 1.0), ("stony-iron", 1.0), ("rubble-pile", 1.0), ("enstatite-chondrite", 1.0),
            }));
            Assert.Throws<System.ArgumentException>(() => MeteorComposition.Create(new[] { ("unobtainium", 1.0) }));
            Assert.Throws<System.ArgumentException>(() => MeteorComposition.Create(new[] { ("iron", 0.0) }));

            MeteorComposition four = MeteorComposition.Create(new[] { ("iron", 1.0), ("cometary", 1.0), ("stony-iron", 1.0), ("rubble-pile", 1.0) });
            Assert.That(four.Parts.Count, Is.EqualTo(MeteorComposition.MaxComponents));
            Assert.That(four.Appearance.Metallic, Is.InRange(0.0, 1.0));
        }

        [Test]
        public void AppearanceBlendIsMassWeighted()
        {
            MeteorComposition c = MeteorComposition.Create(new[] { ("iron", 1.0), ("carbonaceous-chondrite", 1.0) });
            float expected = 0.5f * (MeteorMaterialPresets.Iron.Appearance.Metallic + MeteorMaterialPresets.Carbonaceous.Appearance.Metallic);
            Assert.That(c.Appearance.Metallic, Is.EqualTo(expected).Within(1e-5));
        }

        [Test]
        public void FromCompositionUsesTheMixAndDefaultsTheShapeFactor()
        {
            MeteorComposition c = MeteorComposition.Single(MeteorMaterialPresets.StonyIronId);
            MeteorProperties p = MeteorProperties.FromComposition(1e6, c, 1, 1, 1, EntryMode.Inbound, 15000, 30);
            Assert.That(p.Material, Is.SameAs(MeteorMaterialPresets.StonyIron));
            Assert.That(p.Scf, Is.EqualTo(MeteorProperties.DefaultScf));
            Assert.That(p.Mode, Is.EqualTo(EntryMode.Inbound));
            Assert.That(MeteorProperties.FromComposition(1e6, null, 1, 1, 1).Material, Is.SameAs(MeteorComposition.Default.Material));
        }

        [Test]
        public void ScenariosRunToTheExpectedKindOfEnding()
        {
            var start = new Vec3d(1, 1, 1) * MeteorProperties.MetresPerCoordinateUnit;
            foreach (ImpactScenario sc in ImpactScenarios.All)
            {
                var runner = new SimulationRunner(MeteorProperties.Create(sc.Mass, sc.Composition.Material, 0.5, start, sc.Mode,
                    sc.EntrySpeed, sc.EntryAngleDegrees), 15, 0);
                SimulationResult r = runner.RunToEnd();
                Assert.That(r.Reason, Is.Not.EqualTo(EndReason.Error), sc.Name + ": " + r.Message);
                ImpactAssessment a = ImpactModel.Assess(r, runner.Properties);
                TestContext.WriteLine(sc.Name + ": " + r.Reason + ", " + a.EnergyKilotons.ToString("G3") + " kt, crater " +
                                      a.FinalCraterDiameter.ToString("F0") + " m, diameter " + sc.Diameter.ToString("F0") + " m");
                foreach (double radius in a.BlastRadii)
                {
                    Assert.That(double.IsNaN(radius) || double.IsInfinity(radius), Is.False, sc.Name);
                }

                if (sc == ImpactScenarios.Chelyabinsk) Assert.That(r.Reason, Is.EqualTo(EndReason.Airburst));
                if (sc == ImpactScenarios.Barringer || sc == ImpactScenarios.Chicxulub)
                {
                    Assert.That(r.Reason, Is.EqualTo(EndReason.Impact), sc.Name);
                }

                if (sc == ImpactScenarios.Chicxulub)
                {
                    Assert.That(a.FinalCraterDiameter, Is.InRange(80e3, 300e3), "Chicxulub crater");
                    Assert.That(a.EnergyKilotons, Is.GreaterThan(1e10), "Chicxulub energy (~1e8 Mt)");
                }

                if (sc == ImpactScenarios.Barringer)
                {
                    Assert.That(a.FinalCraterDiameter, Is.InRange(500.0, 3000.0), "Meteor Crater is ~1.2 km");
                }
            }
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
