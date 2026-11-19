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
        [TestCase(2500000.0, "2,500.0 t")]
        [TestCase(1.0e8, "100,000 t")]
        public void MassUsesTonnesAboveOneTonne(double kg, string expected)
        {
            Assert.That(UnitFormat.Mass(kg), Is.EqualTo(expected));
        }

        [TestCase(0.0, "0 kg/s")]
        [TestCase(0.25, "250 g/s")]
        [TestCase(12.34, "12.3 kg/s")]
        [TestCase(4200.0, "4.20 t/s")]
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
        public void PressureUsesKiloAndMegaPascals(double pa, string expected)
        {
            Assert.That(UnitFormat.Pressure(pa), Is.EqualTo(expected));
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
