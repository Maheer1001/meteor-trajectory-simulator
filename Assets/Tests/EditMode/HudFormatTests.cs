using MTS.Core.Display;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    public class HudFormatTests
    {
        [TestCase(0.0, "0 m")]
        [TestCase(9999.4, "9,999 m")]
        [TestCase(12345.0, "12.3 km")]
        [TestCase(120000.0, "120.0 km")]
        [TestCase(175000000.0, "175,000 km")]
        [TestCase(-50.0, "-50 m")]
        public void DistanceSwitchesToKilometresAboveTenKm(double metres, string expected)
        {
            Assert.That(UnitFormat.Distance(metres), Is.EqualTo(expected));
        }

        [TestCase(3.26, "3.3 m/s")]
        [TestCase(850.0, "850 m/s")]
        [TestCase(17000.0, "17.00 km/s")]
        [TestCase(72123.0, "72.12 km/s")]
        public void SpeedUsesKilometresPerSecondWhenFast(double v, string expected)
        {
            Assert.That(UnitFormat.Speed(v), Is.EqualTo(expected));
        }

        [TestCase(1.5, "1.5 kg")]
        [TestCase(850.0, "850 kg")]
        [TestCase(30000.0, "30.00 t")]
        [TestCase(250000.0, "250.0 t")]
        [TestCase(2500000.0, "2.50 kt")]
        [TestCase(1.2e7, "12.0 kt")]
        [TestCase(1.0e8, "100 kt")]
        [TestCase(5.1e8, "510 kt")]
        [TestCase(1.7e12, "1.70 Gt")]
        [TestCase(1.25e15, "1,250 Gt")]
        [TestCase(1.0e16, "10,000 Gt")]
        public void MassUsesTonnesAboveOneTonne(double kg, string expected)
        {
            Assert.That(UnitFormat.Mass(kg), Is.EqualTo(expected));
        }

        [TestCase(0.0, "0 kg/s")]
        [TestCase(0.25, "250 g/s")]
        [TestCase(12.34, "12.3 kg/s")]
        [TestCase(4200.0, "4.20 t/s")]
        [TestCase(3.0e6, "3.00 kt/s")]
        [TestCase(8.0e12, "8.00 Gt/s")]
        public void MassRate(double rate, string expected)
        {
            Assert.That(UnitFormat.MassRate(rate), Is.EqualTo(expected));
        }

        [TestCase(12.34, "12.3 s")]
        [TestCase(75.0, "0:01:15")]
        [TestCase(3725.9, "1:02:05")]
        [TestCase(2.0 * 86400 + 3 * 3600 + 4 * 60 + 5, "2 d 03:04:05")]
        public void DurationUsesClockFormat(double seconds, string expected)
        {
            Assert.That(UnitFormat.Duration(seconds), Is.EqualTo(expected));
        }

        [TestCase(5.0, "5.00 Pa")]
        [TestCase(500.0, "500 Pa")]
        [TestCase(12346.0, "12.35 kPa")]
        [TestCase(250000.0, "250.0 kPa")]
        [TestCase(8.0e6, "8.00 MPa")]
        [TestCase(4.5e8, "450.0 MPa")]
        [TestCase(2.3e9, "2.30 GPa")]
        [TestCase(4.0e11, "400.0 GPa")]
        public void PressureUsesKiloAndMegaPascals(double pa, string expected)
        {
            Assert.That(UnitFormat.Pressure(pa), Is.EqualTo(expected));
        }

        [TestCase(4.184e9, "1.00 t TNT")]
        [TestCase(2.1e12, "502 t TNT")]
        [TestCase(500.0 * 4.184e12, "500 kt TNT")]
        [TestCase(15.0e3 * 4.184e12, "15.0 Mt TNT")]
        [TestCase(2.5e23, "59.8 Tt TNT")]
        [TestCase(1.0, "< 1 t TNT")]
        public void EnergyUsesTntEquivalent(double joules, string expected)
        {
            Assert.That(UnitFormat.Energy(joules), Is.EqualTo(expected));
        }

        [Test]
        public void ScenarioEnergiesReadCleanly()
        {
            // 0.5 m v^2 of the scenarios, from a house-sized fireball to Chicxulub.
            Assert.That(UnitFormat.TntKilotons(ImpactScenarios.Chelyabinsk.EntryEnergyKilotons), Does.EndWith("kt TNT"));
            Assert.That(UnitFormat.TntKilotons(ImpactScenarios.Tunguska.EntryEnergyKilotons), Does.EndWith("Mt TNT"));
            Assert.That(UnitFormat.TntKilotons(ImpactScenarios.Chicxulub.EntryEnergyKilotons), Is.EqualTo("59.8 Tt TNT"));
        }

        [TestCase(0.42, "42 cm")]
        [TestCase(2.5, "2.50 m")]
        [TestCase(19.0, "19.00 m")]
        [TestCase(560.0, "560 m")]
        [TestCase(1250.0, "1.25 km")]
        [TestCase(10500.0, "10.5 km")]
        [TestCase(2.0e6, "2,000 km")]
        public void LengthGoesFromCentimetresToKilometres(double metres, string expected)
        {
            Assert.That(UnitFormat.Length(metres), Is.EqualTo(expected));
        }

        [TestCase(4.56e9, "4.56 GW")]
        [TestCase(999.7e12, "1.00 PW")]
        [TestCase(3.2e18, "3.20 EW")]
        [TestCase(7.0e24, "7.00 YW")]
        [TestCase(4.0e27, "4,000 YW")]
        public void PowerHasPrefixesForHugeFireballs(double watts, string expected)
        {
            Assert.That(UnitFormat.Power(watts), Is.EqualTo(expected));
        }

        [Test]
        public void HugeValuesStayShort()
        {
            double[] samples = { 1e3, 1e6, 1e9, 1e12, 1e15, 1e16, 1e18 };
            foreach (double v in samples)
            {
                foreach (string s in new[] { UnitFormat.Mass(v), UnitFormat.MassRate(v), UnitFormat.Energy(v), UnitFormat.Power(v), UnitFormat.Pressure(v / 1e6) })
                {
                    Assert.That(s.Length, Is.LessThanOrEqualTo(14), s);
                    Assert.That(s, Does.Not.Contain("E+"), s);
                }
            }
        }

        [Test]
        public void OtherQuantities()
        {
            Assert.That(UnitFormat.GForce(3.456), Is.EqualTo("3.46 g"));
            Assert.That(UnitFormat.GForce(1234.7), Is.EqualTo("1,235 g"));
            Assert.That(UnitFormat.Temperature(2345.6), Is.EqualTo("2,346 K"));
            Assert.That(UnitFormat.Mach(52.34), Is.EqualTo("Mach 52.3"));
            Assert.That(UnitFormat.Power(0.5), Is.EqualTo("0 W"));
            Assert.That(UnitFormat.Power(1234.0), Is.EqualTo("1.23 kW"));
            Assert.That(UnitFormat.Power(4.56e9), Is.EqualTo("4.56 GW"));
            Assert.That(UnitFormat.Power(123.4e12), Is.EqualTo("123 TW"));
            Assert.That(UnitFormat.Magnitude(-12.44), Is.EqualTo("-12.4 mag"));
            Assert.That(UnitFormat.Magnitude(3.0), Is.EqualTo("+3.0 mag"));
            Assert.That(UnitFormat.Magnitude(10.0), Is.EqualTo("not glowing"));
            Assert.That(UnitFormat.Percent(0.9734), Is.EqualTo("97.3 %"));
            Assert.That(UnitFormat.Angle(45.0), Is.EqualTo("45°"));
            Assert.That(UnitFormat.Length(0.42), Is.EqualTo("42 cm"));
            Assert.That(UnitFormat.Length(2.5), Is.EqualTo("2.50 m"));
        }

        [Test]
        public void WarpLabels()
        {
            Assert.That(UnitFormat.Warp(0.25), Is.EqualTo("x0.25"));
            Assert.That(UnitFormat.Warp(1), Is.EqualTo("x1"));
            Assert.That(UnitFormat.Warp(1000), Is.EqualTo("x1,000"));
            Assert.That(UnitFormat.Warp(100000), Is.EqualTo("x100,000"));
        }

        [Test]
        public void NonFiniteValuesShowPlaceholder()
        {
            Assert.That(UnitFormat.Distance(double.NaN), Is.EqualTo(UnitFormat.Missing));
            Assert.That(UnitFormat.Speed(double.PositiveInfinity), Is.EqualTo(UnitFormat.Missing));
            Assert.That(UnitFormat.Duration(double.NaN), Is.EqualTo(UnitFormat.Missing));
            Assert.That(UnitFormat.Pressure(double.NegativeInfinity), Is.EqualTo(UnitFormat.Missing));
        }

        [Test]
        public void NoValueEverHasMoreThanTwoDecimals()
        {
            double[] samples = { 1e-3, 0.123456789, 1.23456789, 123.456789, 12345.6789, 1.23456789e7, 9.87654321e10 };
            foreach (double v in samples)
            {
                foreach (string s in new[]
                         {
                             UnitFormat.Distance(v), UnitFormat.Speed(v), UnitFormat.Mass(v), UnitFormat.MassRate(v),
                             UnitFormat.Pressure(v), UnitFormat.GForce(v), UnitFormat.Power(v), UnitFormat.Duration(v),
                         })
                {
                    int dot = s.IndexOf('.');
                    if (dot < 0) continue;
                    int decimals = 0;
                    for (int i = dot + 1; i < s.Length && char.IsDigit(s[i]); i++) decimals++;
                    Assert.That(decimals, Is.LessThanOrEqualTo(2), s);
                }
            }
        }
    }
}
