using System;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    /// <summary>
    /// Thermal, seismic, ejecta, ocean and global effects checked against the worked examples of Collins, Melosh and
    /// Marcus (2005), Table 6 (effects 200 km from a Barringer-, Ries- and Chicxulub-scale impact), and the scenarios.
    /// </summary>
    public class ImpactEffectsTests
    {
        private static double SphereMass(double diameter, double density) => density * Math.PI / 6.0 * Math.Pow(diameter, 3);

        private static ImpactAssessment Ries() => ImpactModel.GroundImpact(SphereMass(1750, 2700), 2700, 20000, 45, 2750);

        [Test]
        public void RiesThermalMatchesEiepTable6()
        {
            // 1.5e21 J: fireball 23 km, radiation peaks at 1.2 s, 14.8 MJ/m^2 at 200 km, 300 s of irradiation.
            const double e = 1.5e21;
            double rf = ImpactEffects.FireballRadiusFor(e);
            Assert.That(rf, Is.EqualTo(23000).Within(500));
            Assert.That(rf / 20000.0, Is.EqualTo(1.2).Within(0.06)); // 23 km / 20 km/s = 1.15 s, quoted as 1.2 s
            Assert.That(ImpactEffects.ThermalExposure(200000, e), Is.EqualTo(14.8e6).Within(0.4e6));
            Assert.That(ImpactEffects.IrradiationDurationFor(e, rf), Is.EqualTo(300).Within(15));
            // "Third degree burns; many combustible materials ignited" at 200 km.
            ImpactEffects fx = ImpactEffects.Compute(Ries(), 2700, false);
            Assert.That(fx.HasThermal, Is.True);
            foreach (ThermalThreshold t in fx.Thermal)
            {
                Assert.That(t.Radius, Is.GreaterThan(200000), t.Name);
            }
        }

        [Test]
        public void VisibleFractionFallsToZeroBeyondTheHorizon()
        {
            const double rf = 23000;
            Assert.That(ImpactEffects.VisibleFraction(1, rf), Is.EqualTo(1).Within(1e-6));
            Assert.That(ImpactEffects.VisibleFraction(200000, rf), Is.EqualTo(0.827).Within(0.01));
            double horizon = ImpactEffects.HorizonRange(rf);
            Assert.That(ImpactEffects.VisibleFraction(horizon * 0.999, rf), Is.GreaterThan(0));
            Assert.That(ImpactEffects.VisibleFraction(horizon * 1.001, rf), Is.EqualTo(0));
            Assert.That(ImpactEffects.ThermalRadius(1.0, 1.5e21), Is.LessThanOrEqualTo(horizon * 1.0001));
        }

        [Test]
        public void IgnitionThresholdScalesWithTheSixthRootOfEnergy()
        {
            double oneMt = ImpactEffects.JoulesPerMegaton;
            Assert.That(ImpactEffects.IgnitionExposure(1e6, oneMt), Is.EqualTo(1e6).Within(1));
            Assert.That(ImpactEffects.IgnitionExposure(1e6, 64 * oneMt), Is.EqualTo(2e6).Within(10));
        }

        [Test]
        public void SeismicMagnitudesMatchEiepTable6()
        {
            // Richter 4.9, 8.3 and 10.4 for 1.3e16, 1.5e21 and 1.65e24 J.
            Assert.That(ImpactEffects.SeismicMagnitude(1.3e16), Is.EqualTo(4.9).Within(0.05));
            Assert.That(ImpactEffects.SeismicMagnitude(1.5e21), Is.EqualTo(8.3).Within(0.05));
            Assert.That(ImpactEffects.SeismicMagnitude(1.65e24), Is.EqualTo(10.4).Within(0.05));
            // MMI at 200 km: I-II, VII-VIII and X-XI; shaking arrives after 40 s.
            Assert.That(ImpactEffects.MercalliRange(ImpactEffects.EffectiveMagnitude(4.9, 200000)), Is.EqualTo("I-II"));
            Assert.That(ImpactEffects.MercalliRange(ImpactEffects.EffectiveMagnitude(8.3, 200000)), Is.EqualTo("VII-VIII"));
            Assert.That(ImpactEffects.MercalliRange(ImpactEffects.EffectiveMagnitude(10.4, 200000)), Is.EqualTo("X-XI"));
            Assert.That(ImpactEffects.SeismicArrival(200000), Is.EqualTo(40).Within(1e-9));
        }

        [Test]
        public void EffectiveMagnitudeIsNearlyContinuousAndFalls()
        {
            const double m = 10;
            Assert.That(ImpactEffects.EffectiveMagnitude(m, 59999), Is.EqualTo(ImpactEffects.EffectiveMagnitude(m, 60001)).Within(0.05));
            Assert.That(ImpactEffects.EffectiveMagnitude(m, 699999), Is.EqualTo(ImpactEffects.EffectiveMagnitude(m, 700001)).Within(0.3));
            double felt = ImpactEffects.SeismicRadius(m, 3.0);
            double damaging = ImpactEffects.SeismicRadius(m, 5.0);
            Assert.That(felt, Is.GreaterThan(damaging));
            Assert.That(ImpactEffects.EffectiveMagnitude(m, damaging), Is.EqualTo(5.0).Within(0.01));
            Assert.That(ImpactEffects.SeismicRadius(2.0, 3.0), Is.EqualTo(0));
        }

        [Test]
        public void EjectaMatchesEiepTable6()
        {
            // Ries: 9 cm of ejecta at 200 km, mean fragment 2.4 cm, arriving after 206 s.
            ImpactAssessment a = Ries();
            Assert.That(ImpactEffects.EjectaThickness(a.TransientCraterDiameter, 200000), Is.EqualTo(0.09).Within(0.02));
            Assert.That(ImpactEffects.MeanFragmentSize(23700, 200000), Is.EqualTo(0.024).Within(0.002));
            Assert.That(ImpactEffects.EjectaArrivalTime(200000), Is.EqualTo(206).Within(3));
            // Chicxulub scale: 137 m at 200 km.
            ImpactAssessment c = ImpactModel.GroundImpact(SphereMass(18000, 2700), 2700, 20000, 45, 2750);
            Assert.That(ImpactEffects.EjectaThickness(c.TransientCraterDiameter, 200000), Is.EqualTo(137).Within(30));
            Assert.That(ImpactEffects.EjectaRadius(1000, ImpactEffects.EjectaThickness(1000, 5000)), Is.EqualTo(5000).Within(1e-6));
            Assert.That(ImpactEffects.MeanFragmentSize(23700, 10000), Is.NaN, "not reported within two crater radii");
            Assert.That(ImpactEffects.EjectaArrivalTime(1.5e7), Is.NaN, "beyond the quarter-circumference limit");
        }

        [Test]
        public void SlowSmallImpactHasNoFireballAndShortEjecta()
        {
            // Barringer-scale iron at 10 km/s: no thermal radiation (below 15 km/s), ejecta limited to the fireball range.
            ImpactAssessment a = ImpactModel.GroundImpact(SphereMass(40, 8000), 8000, 10000, 45, 2500);
            ImpactEffects fx = ImpactEffects.Compute(a, 8000, false);
            Assert.That(fx.HasThermal, Is.False);
            Assert.That(fx.HasSeismic, Is.True);
            Assert.That(fx.Magnitude, Is.EqualTo(4.9).Within(0.05));
            Assert.That(fx.HasEjecta, Is.True);
            Assert.That(fx.EjectaMaxRange, Is.EqualTo(ImpactEffects.FireballRadiusFor(a.Energy)).Within(1));
            Assert.That(fx.HasTsunami, Is.False);
            Assert.That(fx.Global, Is.EqualTo(GlobalScale.Local));
        }

        [Test]
        public void AirburstHasOnlyTheGlobalSummary()
        {
            ImpactAssessment a = ImpactModel.Airburst(2.1e15, 18000, 27000, 18);
            ImpactEffects fx = ImpactEffects.Compute(a, 3300, true);
            Assert.That(fx.HasThermal || fx.HasSeismic || fx.HasEjecta || fx.HasTsunami, Is.False);
            Assert.That(fx.Global, Is.EqualTo(GlobalScale.Local));
        }

        [Test]
        public void ChicxulubScenarioIsAMassExtinctionWithGlobalReach()
        {
            ImpactScenario s = ImpactScenarios.Chicxulub;
            ImpactAssessment a = ImpactModel.GroundImpact(s.Mass, s.Composition.Material.Density, s.EntrySpeed, s.EntryAngleDegrees);
            ImpactEffects fx = ImpactEffects.Compute(a, s.Composition.Material.Density, false);
            Assert.That(fx.Global, Is.EqualTo(GlobalScale.MassExtinction));
            Assert.That(fx.Magnitude, Is.GreaterThan(9.5)); // 2.5e23 J: Richter 9.8
            Assert.That(fx.FireballRadius, Is.GreaterThan(100000));
            Assert.That(fx.EjectaMaxRange, Is.EqualTo(ImpactEffects.MaxGroundRange), "above 200 Mt the ejecta is not limited");
            Assert.That(fx.Seismic[0].Radius, Is.GreaterThan(ImpactEffects.GlobalRange), "felt round the world");
            Assert.That(ImpactEffects.OrbitChange(fx.MomentumRatio), Is.EqualTo("No noticeable change in orbit"));
            // The overpressure rings are capped at the antipode.
            foreach (double r in a.BlastRadii)
            {
                Assert.That(r, Is.LessThanOrEqualTo(ImpactModel.MaxGroundRange));
            }

            Assert.That(a.BlastRadii[0], Is.EqualTo(ImpactModel.MaxGroundRange), "0.04 psi everywhere on Earth");
        }

        [Test]
        public void OceanImpactMakesATsunamiAndSlowsInTheWater()
        {
            ImpactScenario s = ImpactScenarios.RegionalKiller;
            double rho = s.Composition.Material.Density;
            ImpactAssessment a = ImpactModel.GroundImpact(s.Mass, rho, s.EntrySpeed, s.EntryAngleDegrees);
            ImpactEffects land = ImpactEffects.Compute(a, rho, false);
            ImpactEffects sea = ImpactEffects.Compute(a, rho, true);
            Assert.That(sea.IsOcean && sea.HasTsunami, Is.True);
            Assert.That(sea.WaterDepth, Is.EqualTo(ImpactEffects.DefaultOceanDepth));
            // A 1 km stony body loses most of its speed in 4 km of water (Eq. 65), so the sea floor takes far less energy.
            Assert.That(sea.SeafloorSpeed, Is.LessThan(a.ImpactSpeed).And.GreaterThan(0));
            Assert.That(sea.Magnitude, Is.LessThan(land.Magnitude));
            Assert.That(sea.FinalCraterDiameter, Is.LessThan(land.FinalCraterDiameter));
            Assert.That(sea.RimWaveAmplitude, Is.EqualTo(sea.WaterCraterDiameter / 14.1).Within(1e-6));
            Assert.That(sea.WaterCraterDiameter, Is.InRange(15000, 25000));
            Assert.That(ImpactEffects.RimWave(1e6, 4000), Is.EqualTo(4000), "huge cavity: wave limited by the water depth");
            Assert.That(sea.Tsunami[1].Radius, Is.GreaterThan(1e6), "10 m waves over 1000 km away");
            Assert.That(ImpactEffects.TsunamiAmplitude(sea.WaterCraterDiameter, 4000, sea.Tsunami[1].Radius), Is.EqualTo(10).Within(0.01));
            Assert.That(ImpactEffects.TsunamiArrival(1e6, 4000), Is.EqualTo(1e6 / Math.Sqrt(9.81 * 4000)).Within(1e-6));
            // Eq. 65 by hand: exp(-3 * 1000 * 0.877 * 4000 / (2 * rho * L * sin 45)).
            double expected = s.EntrySpeed * Math.Exp(-3.0 * 1000 * 0.877 * 4000 / (2.0 * rho * a.ImpactorDiameter * Math.Sin(Math.PI / 4)));
            Assert.That(sea.SeafloorSpeed, Is.EqualTo(expected).Within(1e-6 * expected));
        }

        [Test]
        public void GlobalScaleThresholds()
        {
            double mt = ImpactEffects.JoulesPerMegaton;
            Assert.That(ImpactEffects.GlobalScaleFor(10 * mt), Is.EqualTo(GlobalScale.Local));
            Assert.That(ImpactEffects.GlobalScaleFor(5e3 * mt), Is.EqualTo(GlobalScale.Regional));
            Assert.That(ImpactEffects.GlobalScaleFor(8e4 * mt), Is.EqualTo(GlobalScale.Continental));
            Assert.That(ImpactEffects.GlobalScaleFor(2e5 * mt), Is.EqualTo(GlobalScale.GlobalClimate));
            Assert.That(ImpactEffects.GlobalScaleFor(2e6 * mt), Is.EqualTo(GlobalScale.Civilisation));
            Assert.That(ImpactEffects.GlobalScaleFor(1e8 * mt), Is.EqualTo(GlobalScale.MassExtinction));
        }
    }
}
