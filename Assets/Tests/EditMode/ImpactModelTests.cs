using System;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    // Worked examples from Collins, Melosh and Marcus (2005), Table 6 and Table 4.
    public class ImpactModelTests
    {
        private static double SphereMass(double diameter, double density) => density * Math.PI / 6.0 * Math.Pow(diameter, 3);

        [Test]
        public void BarringerScaleIronImpact()
        {
            // 40 m iron (8000 kg/m^3), slowed from 20 to about 10 km/s, 45 degrees, sedimentary target:
            // EIEP gives E = 1.3e16 J and a 1.2 km simple crater.
            ImpactAssessment a = ImpactModel.GroundImpact(SphereMass(40, 8000), 8000, 10000, 45, 2500);
            Assert.That(a.Energy, Is.EqualTo(1.3e16).Within(0.05e16));
            Assert.That(a.FinalCraterDiameter, Is.EqualTo(1200).Within(60));
            Assert.That(a.IsComplexCrater, Is.False);
            Assert.That(a.Regime, Is.EqualTo(ImpactRegime.Crater));
            // 0.004 bar at 200 km
            Assert.That(ImpactModel.PeakOverpressure(200000, a.Energy, 0), Is.EqualTo(400).Within(40));
        }

        [Test]
        public void RiesScaleStonyImpact()
        {
            // 1.75 km stony (2700) at 20 km/s, 45 degrees, crystalline target (2750): E = 1.5e21 J, 23.7 km complex crater,
            // 0.80 bar at 200 km.
            ImpactAssessment a = ImpactModel.GroundImpact(SphereMass(1750, 2700), 2700, 20000, 45, 2750);
            Assert.That(a.Energy, Is.EqualTo(1.5e21).Within(0.05e21));
            Assert.That(a.FinalCraterDiameter, Is.EqualTo(23700).Within(500));
            Assert.That(a.IsComplexCrater, Is.True);
            Assert.That(ImpactModel.PeakOverpressure(200000, a.Energy, 0), Is.EqualTo(80000).Within(3000));
        }

        [Test]
        public void ChicxulubScaleImpact()
        {
            ImpactAssessment a = ImpactModel.GroundImpact(SphereMass(18000, 2700), 2700, 20000, 45, 2750);
            Assert.That(a.Energy, Is.EqualTo(1.65e24).Within(0.05e24));
            Assert.That(a.FinalCraterDiameter, Is.EqualTo(186000).Within(3000));
        }

        // Table 4 distances for a 1 kt explosion (Eq. 54 is the fit to these data).
        [TestCase(126.0, 426000.0)]
        [TestCase(229.0, 121000.0)]
        [TestCase(389.0, 42600.0)]
        [TestCase(549.0, 22900.0)]
        [TestCase(1160.0, 6900.0)]
        public void SurfaceBurstMatchesTable4(double distance, double pressure)
        {
            double p = ImpactModel.PeakOverpressure(distance, PhysicalConstants.JoulesPerKilotonTnt, 0);
            Assert.That(p, Is.EqualTo(pressure).Within(pressure * 0.08));
        }

        [Test]
        public void CrossoverPointAndYieldScaling()
        {
            Assert.That(ImpactModel.SurfaceBurstOverpressure1Kt(290), Is.EqualTo(75000).Within(1e-6));
            // 1 t TNT surface burst: 0.04 psi (275.8 Pa) at about 2 km
            Assert.That(ImpactModel.BlastRadius(275.8, 4.184e9, 0), Is.EqualTo(2000).Within(60));
            // r(p) / E^(1/3) is constant
            double r1 = ImpactModel.BlastRadius(20684.3, 1e15, 0);
            double r2 = ImpactModel.BlastRadius(20684.3, 8e15, 0);
            Assert.That(r2 / r1, Is.EqualTo(2.0).Within(1e-6));
        }

        [Test]
        public void BlastRadiiDecreaseWithOverpressureAndArePlausible()
        {
            // The old code reported a 23,000 km "sonic boom" ring; a 60 t iron at 2 km/s must be kilometres at most.
            ImpactAssessment a = ImpactModel.GroundImpact(60000, 7800, 2000, 90);
            for (int i = 1; i < a.BlastRadii.Length; i++)
            {
                Assert.That(a.BlastRadii[i], Is.LessThan(a.BlastRadii[i - 1]));
            }

            Assert.That(a.BlastRadii[0], Is.LessThan(20000));
            Assert.That(a.Regime, Is.EqualTo(ImpactRegime.PenetrationPit));
        }

        [Test]
        public void AirburstIsWeakerOnTheGroundThanASurfaceBurst()
        {
            double e = 1e6 * PhysicalConstants.JoulesPerKilotonTnt; // 1 Mt
            ImpactAssessment ground = ImpactModel.GroundImpact(1e9, 3000, 10000, 45);
            ImpactAssessment air = ImpactModel.Airburst(e, 10000, 8000, 45);
            Assert.That(air.FinalCraterDiameter, Is.EqualTo(0));
            Assert.That(air.BlastRadii[3], Is.GreaterThan(0)); // 2 psi reached on the ground from 8 km
            Assert.That(ImpactModel.PeakOverpressure(0, e, 8000), Is.LessThan(ImpactModel.PeakOverpressure(1, e, 0)));
            Assert.That(ground.Energy, Is.GreaterThan(0));
        }

        [Test]
        public void ChelyabinskAirburstReachesTheGroundWithAFewKilopascals()
        {
            // About 500 kt released near 30 km (Brown et al. 2013; Popova et al. 2013). Observed: windows broken out to
            // about 120 km from the track, structural damage light. The EIEP fit alone gives under 0.04 psi everywhere.
            double e = 500 * PhysicalConstants.JoulesPerKilotonTnt;
            const double zb = 30000;
            double zb1 = zb / Math.Pow(500, 1.0 / 3.0);
            Assert.That(ImpactModel.EiepAirburstOverpressure(0, zb1), Is.LessThan(275.8)); // the old behaviour

            double groundZero = ImpactModel.PeakOverpressure(0, e, zb);
            Assert.That(groundZero, Is.InRange(1000, 5000));
            Assert.That(ImpactModel.BlastRadius(275.8, e, zb), Is.InRange(60000, 250000));   // 0.04 psi: window damage
            Assert.That(ImpactModel.BlastRadius(1034.2, e, zb), Is.InRange(10000, 80000));   // 0.15 psi
            Assert.That(ImpactModel.BlastRadius(13789.5, e, zb), Is.EqualTo(0));             // no 2 psi on the ground

            ImpactAssessment a = ImpactModel.Airburst(e, 19000, zb, 18);
            Assert.That(a.BlastRadii[0], Is.GreaterThan(0));
        }

        [Test]
        public void HighAltitudeModelAgreesWithEiepForLowBursts()
        {
            // Tunguska-like 15 Mt at 8 km: the point-source model is within 15 % of the EIEP fit below the burst,
            // so the EIEP results for low bursts are unchanged.
            double e = 15000 * PhysicalConstants.JoulesPerKilotonTnt;
            double eiep = ImpactModel.EiepAirburstOverpressure(0, 8000 / Math.Pow(15000, 1.0 / 3.0));
            double point = ImpactModel.HighAltitudeAirburstOverpressure(0, e, 8000);
            Assert.That(point / eiep, Is.InRange(0.85, 1.15));
            Assert.That(ImpactModel.PeakOverpressure(0, e, 8000), Is.EqualTo(Math.Max(eiep, point)).Within(1e-6 * eiep));
        }

        [Test]
        public void HighAltitudeOverpressureFallsWithRangeAndAltitude()
        {
            double e = 100 * PhysicalConstants.JoulesPerKilotonTnt;
            double previous = double.PositiveInfinity;
            for (double r = 0; r <= 300000; r += 10000)
            {
                double p = ImpactModel.HighAltitudeAirburstOverpressure(r, e, 35000);
                Assert.That(p, Is.LessThan(previous));
                previous = p;
            }

            Assert.That(ImpactModel.HighAltitudeAirburstOverpressure(0, e, 50000),
                Is.LessThan(ImpactModel.HighAltitudeAirburstOverpressure(0, e, 30000)));
            Assert.That(ImpactModel.HighAltitudeAirburstOverpressure(0, 0, 30000), Is.EqualTo(0));
        }

        [Test]
        public void EiepStrength()
        {
            Assert.That(MeteorMaterial.EiepStrength(0), Is.EqualTo(128).Within(1));
            Assert.That(MeteorMaterial.EiepStrength(3000), Is.EqualTo(3.35e5).Within(0.05e5));
        }
    }
}
