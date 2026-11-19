using System.Linq;
using MTS.Core;
using MTS.Menus;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    public class MenusLogicTests
    {
        [TestCase("30000", 30000)]
        [TestCase("30,000", 30000)]
        [TestCase("30 000", 30000)]
        [TestCase("1,000,000", 1e6)]
        [TestCase("60,000.5", 60000.5)]
        [TestCase("2,5", 2.5)]
        [TestCase("1.2e7", 1.2e7)]
        [TestCase("1.25E+15", 1.25e15)]
        [TestCase("1,5e7", 1.5e7)]
        [TestCase("12 kt", 1.2e7)]
        [TestCase("5t", 5000)]
        [TestCase("3.5 Mt", 3.5e9)]
        [TestCase("1250 Gt", 1.25e15)]
        [TestCase("4500 kg", 4500)]
        public void MassParsesPlainScientificAndUnits(string text, double expected)
        {
            Assert.IsTrue(MassInput.TryParse(text, out double kg), text);
            Assert.AreEqual(expected, kg, expected * 1e-12);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("-5")]
        [TestCase("0")]
        [TestCase("1e400")]
        [TestCase("NaN")]
        public void MassRejectsInvalidText(string text)
        {
            Assert.IsFalse(MassInput.TryParse(text, out _));
        }

        [Test]
        public void MassClampsToTheWideRange()
        {
            Assert.AreEqual(1e3, MassInput.Clamp(10));
            Assert.AreEqual(1e16, MassInput.Clamp(1e20));
            Assert.AreEqual(1.25e15, MassInput.Clamp(1.25e15));
            Assert.AreEqual(MassInput.DefaultMass, MassInput.Clamp(double.NaN));
        }

        [TestCase(30000, "30 t")]
        [TestCase(1000, "1 t")]
        [TestCase(1.2e7, "12 kt")]
        [TestCase(3.5e8, "350 kt")]
        [TestCase(1.25e9, "1.25 Mt")]
        [TestCase(1.7e12, "1.7 Gt")]
        [TestCase(1.25e15, "1,250 Gt")]
        [TestCase(1e16, "10,000 Gt")]
        public void MassFormatsWithTonneUnits(double kg, string expected)
        {
            Assert.AreEqual(expected, MassInput.Format(kg));
        }

        [Test]
        public void MassFormatsKgAndFieldText()
        {
            Assert.AreEqual("30,000 kg", MassInput.FormatKg(30000));
            Assert.AreEqual("1.2 × 10<sup>7</sup> kg", MassInput.FormatKg(1.2e7));
            Assert.AreEqual("1.25 × 10<sup>15</sup> kg", MassInput.FormatKg(1.25e15));
            Assert.AreEqual("1 × 10<sup>16</sup> kg", MassInput.FormatKg(9.999e15));
            Assert.AreEqual("30000", MassInput.ToFieldText(30000));
            Assert.AreEqual("1.25e15", MassInput.ToFieldText(1.25e15));
            Assert.IsTrue(MassInput.TryParse(MassInput.ToFieldText(1.2e7), out double back));
            Assert.AreEqual(1.2e7, back, 1);
        }

        [Test]
        public void ImpliedDiameterMatchesTheScenarioDiameter()
        {
            foreach (ImpactScenario s in ImpactScenarios.All)
            {
                Assert.AreEqual(s.Diameter, MassInput.SphereDiameter(s.Mass, s.Composition.Material.Density), s.Diameter * 1e-9, s.Name);
            }

            Assert.AreEqual("19.2 m", MassInput.FormatLength(19.2));
            Assert.AreEqual("190 m", MassInput.FormatLength(190.4));
            Assert.AreEqual("2.6 m", MassInput.FormatLength(2.6));
            Assert.AreEqual("10.3 km", MassInput.FormatLength(10300));
        }

        [Test]
        public void MixerDefaultsToAllStony()
        {
            var mixer = new CompositionMixer();
            Assert.AreEqual(1, mixer.Count);
            Assert.AreEqual(MeteorMaterialPresets.OrdinaryChondriteId, mixer.Rows[0].MaterialId);
            MeteorComposition c = mixer.Build();
            Assert.AreEqual(1, c.Parts.Count);
            Assert.AreSame(MeteorMaterialPresets.OrdinaryChondrite, c.Material);
            Assert.IsFalse(mixer.CanRemove);
        }

        [Test]
        public void MixerAddsUpToFourDistinctMaterials()
        {
            var mixer = new CompositionMixer();
            Assert.AreEqual(1, mixer.Add());
            Assert.AreEqual(2, mixer.Add());
            Assert.AreEqual(3, mixer.Add());
            Assert.IsFalse(mixer.CanAdd);
            Assert.AreEqual(-1, mixer.Add());
            Assert.AreEqual(4, mixer.Rows.Select(r => r.MaterialId).Distinct().Count());
            MeteorComposition c = mixer.Build();
            Assert.AreEqual(4, c.Parts.Count);
            Assert.AreEqual(1.0, c.Parts.Sum(p => p.Fraction), 1e-12);
        }

        [Test]
        public void MixerNormalisesAmountsToShares()
        {
            var mixer = new CompositionMixer();
            mixer.Add();
            mixer.SetMaterial(1, MeteorMaterialPresets.IronId);
            mixer.SetAmount(0, 30);
            mixer.SetAmount(1, 90);
            Assert.AreEqual(0.25, mixer.Share(0), 1e-12);
            Assert.AreEqual(0.75, mixer.Share(1), 1e-12);
            CollectionAssert.AreEqual(new[] { 25, 75 }, mixer.DisplayPercents());
            MeteorComposition c = mixer.Build();
            Assert.AreEqual(MeteorMaterialPresets.IronId, c.Parts[0].Id);
            Assert.AreEqual(0.75, c.Parts[0].Fraction, 1e-12);
        }

        [Test]
        public void DisplayPercentsAlwaysAddUpToOneHundred()
        {
            var mixer = new CompositionMixer();
            mixer.Add();
            mixer.Add();
            mixer.SetAmount(0, 50);
            mixer.SetAmount(1, 50);
            mixer.SetAmount(2, 50);
            int[] p = mixer.DisplayPercents();
            Assert.AreEqual(100, p.Sum());
            CollectionAssert.AreEqual(new[] { 34, 33, 33 }, p);
        }

        [Test]
        public void MixerWithNoAmountIsInvalid()
        {
            var mixer = new CompositionMixer();
            mixer.SetAmount(0, 0);
            Assert.IsFalse(mixer.IsValid);
            Assert.IsNull(mixer.Build());
            CollectionAssert.AreEqual(new[] { 0 }, mixer.DisplayPercents());
            mixer.SetAmount(0, 500);
            Assert.AreEqual(CompositionMixer.MaxAmount, mixer.Rows[0].Amount);
        }

        [Test]
        public void MixerPickerSkipsMaterialsUsedByOtherRows()
        {
            var mixer = new CompositionMixer(); // stony
            mixer.Add(); // carbonaceous
            mixer.Cycle(0, 1); // stony -> carbonaceous is taken -> enstatite
            Assert.AreEqual(MeteorMaterialPresets.EnstatiteId, mixer.Rows[0].MaterialId);
            mixer.Cycle(1, -1); // carbonaceous -> stony (free again)
            Assert.AreEqual(MeteorMaterialPresets.OrdinaryChondriteId, mixer.Rows[1].MaterialId);
            mixer.SetMaterial(1, MeteorMaterialPresets.EnstatiteId); // taken by row 0: swap
            Assert.AreEqual(MeteorMaterialPresets.OrdinaryChondriteId, mixer.Rows[0].MaterialId);
            Assert.AreEqual(MeteorMaterialPresets.EnstatiteId, mixer.Rows[1].MaterialId);
        }

        [Test]
        public void MixerRemovesRowsButKeepsOne()
        {
            var mixer = new CompositionMixer();
            mixer.Add();
            Assert.IsTrue(mixer.Remove(0));
            Assert.AreEqual(MeteorMaterialPresets.CarbonaceousId, mixer.Rows[0].MaterialId);
            Assert.IsFalse(mixer.Remove(0));
        }

        [Test]
        public void MixerRoundTripsAComposition()
        {
            MeteorComposition original = MeteorComposition.Create(new[]
            {
                (MeteorMaterialPresets.IronId, 60.0), (MeteorMaterialPresets.OrdinaryChondriteId, 25.0),
                (MeteorMaterialPresets.CometaryId, 10.0), (MeteorMaterialPresets.RubblePileId, 5.0),
            });
            CompositionMixer mixer = CompositionMixer.From(original);
            Assert.AreEqual(4, mixer.Count);
            Assert.IsTrue(SetupLogic.SameComposition(original, mixer.Build()));
            CollectionAssert.AreEqual(new[] { 60, 25, 10, 5 }, mixer.DisplayPercents());

            CompositionMixer custom = CompositionMixer.From(MeteorComposition.FromMaterial(MeteorMaterial.CreateCustom(2000, 800, 0.5)));
            Assert.AreEqual(MeteorMaterialPresets.OrdinaryChondriteId, custom.Rows[0].MaterialId);
        }

        [Test]
        public void ApplyingAScenarioFillsTheSessionAndMatchesBack()
        {
            var session = new SimulationSessionProbe();
            ImpactScenario s = ImpactScenarios.Chicxulub;
            double mass = SetupLogic.Apply(s, session.Session);
            Assert.AreEqual(1.25e15, mass);
            Assert.AreEqual(EntryMode.Inbound, session.Session.Mode);
            Assert.AreEqual(20000, session.Session.EntrySpeed);
            Assert.AreEqual(60, session.Session.EntryAngleDegrees);
            Assert.AreSame(s.Composition, session.Session.Composition);
            Assert.AreSame(s, SetupLogic.Match(mass, session.Session.Composition, session.Session.Mode,
                session.Session.EntrySpeed, session.Session.EntryAngleDegrees));
            // A value round-tripped through a float field still matches.
            Assert.AreSame(s, SetupLogic.Match((float)mass, s.Composition, EntryMode.Inbound, 20000, 60));
            session.Restore();
        }

        [Test]
        public void EditingAScenarioMakesItCustom()
        {
            ImpactScenario s = ImpactScenarios.Tunguska;
            Assert.AreSame(s, SetupLogic.Match(s.Mass, s.Composition, s.Mode, s.EntrySpeed, s.EntryAngleDegrees));
            Assert.IsNull(SetupLogic.Match(s.Mass * 2, s.Composition, s.Mode, s.EntrySpeed, s.EntryAngleDegrees));
            Assert.IsNull(SetupLogic.Match(s.Mass, MeteorComposition.Single(MeteorMaterialPresets.IronId), s.Mode, s.EntrySpeed, s.EntryAngleDegrees));
            Assert.IsNull(SetupLogic.Match(s.Mass, s.Composition, EntryMode.FromRest, s.EntrySpeed, s.EntryAngleDegrees));
            Assert.IsNull(SetupLogic.Match(s.Mass, s.Composition, s.Mode, s.EntrySpeed, 30));
        }

        [Test]
        public void SummaryFormatting()
        {
            Assert.AreEqual("3,300 kg/m³", SetupLogic.FormatDensity(3300));
            Assert.AreEqual("200 kPa", SetupLogic.FormatStrength(2e5));
            Assert.AreEqual("50 MPa", SetupLogic.FormatStrength(5e7));
            Assert.AreEqual("500 kt TNT", SetupLogic.FormatEnergy(500));
            Assert.AreEqual("12 Mt TNT", SetupLogic.FormatEnergy(12000));
            Assert.AreEqual(ImpactScenarios.Chelyabinsk.EntryEnergyKilotons,
                SetupLogic.EnergyKilotons(1.2e7, 19000), 1e-6);
            Assert.AreEqual(25, SetupLogic.Coordinate(0.5));
            Assert.AreEqual(-50, SetupLogic.Coordinate(-80));
            Assert.AreEqual(25 * System.Math.Sqrt(3) * 4050, SetupLogic.StartDistanceKm(25, 25, 25), 1e-6);
        }

        /// <summary>Saves and restores the shared session's settings around a test.</summary>
        private sealed class SimulationSessionProbe
        {
            public readonly SimulationSession Session = SimulationSession.Instance;
            private readonly MeteorComposition composition;
            private readonly EntryMode mode;
            private readonly double speed, angle;

            public SimulationSessionProbe()
            {
                composition = Session.Composition;
                mode = Session.Mode;
                speed = Session.EntrySpeed;
                angle = Session.EntryAngleDegrees;
            }

            public void Restore()
            {
                Session.Composition = composition;
                Session.Mode = mode;
                Session.EntrySpeed = speed;
                Session.EntryAngleDegrees = angle;
            }
        }
    }
}
