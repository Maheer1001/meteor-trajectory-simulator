using NUnit.Framework;

namespace MTS.Core.Tests
{
    public class AtmosphereTests
    {
        // U.S. Standard Atmosphere 1976, Table 4 layer bases (geopotential altitude).
        [TestCase(0.0, 288.15, 101325.0, 1.2250)]
        [TestCase(11000.0, 216.65, 22632.06, 0.36392)]
        [TestCase(20000.0, 216.65, 5474.889, 0.088035)]
        [TestCase(32000.0, 228.65, 868.0187, 0.013225)]
        [TestCase(47000.0, 270.65, 110.9063, 0.0014275)]
        [TestCase(51000.0, 270.65, 66.93887, 0.00086160)]
        [TestCase(71000.0, 214.65, 3.956420, 6.4211e-5)]
        [TestCase(84852.0, 186.946, 0.3734, 6.958e-6)]
        public void LayerBasesMatchUssa76(double geopotential, double t, double p, double rho)
        {
            AtmosphereSample s = Atmosphere.SampleAtGeopotential(geopotential);
            Assert.That(s.Temperature, Is.EqualTo(t).Within(0.01));
            Assert.That(s.Pressure, Is.EqualTo(p).Within(p * 2e-4));
            Assert.That(s.Density, Is.EqualTo(rho).Within(rho * 1e-3));
        }

        // Geometric-altitude table values of USSA76.
        [TestCase(11000.0, 216.774, 22699.9, 0.36480)]
        [TestCase(86000.0, 186.87, 0.37338, 6.958e-6)]
        [TestCase(100000.0, 195.08, 0.032011, 5.604e-7)]
        public void GeometricAltitudesMatchUssa76(double z, double t, double p, double rho)
        {
            AtmosphereSample s = Atmosphere.Sample(z);
            Assert.That(s.Temperature, Is.EqualTo(t).Within(0.1));
            Assert.That(s.Pressure, Is.EqualTo(p).Within(p * 1e-3));
            Assert.That(s.Density, Is.EqualTo(rho).Within(rho * 2e-3));
        }

        [Test]
        public void SeaLevelDerivedQuantities()
        {
            AtmosphereSample s = Atmosphere.Sample(0);
            Assert.That(s.SpeedOfSound, Is.EqualTo(340.294).Within(0.01));
            Assert.That(s.DynamicViscosity, Is.EqualTo(1.7894e-5).Within(1e-8));
            Assert.That(s.MeanFreePath, Is.EqualTo(6.6328e-8).Within(1e-10));
        }

        [Test]
        public void DensityDecreasesMonotonicallyAndStaysFinite()
        {
            double previous = double.MaxValue;
            for (double z = -1000; z <= 5.0e8; z = z < 2e6 ? z + 250 : z * 1.5)
            {
                AtmosphereSample s = Atmosphere.Sample(z);
                Assert.That(double.IsNaN(s.Density) || double.IsInfinity(s.Density), Is.False, "z=" + z);
                Assert.That(s.Density, Is.GreaterThanOrEqualTo(0));
                Assert.That(s.Density, Is.LessThanOrEqualTo(previous * (1 + 1e-9)), "z=" + z);
                Assert.That(s.Temperature, Is.GreaterThan(150));
                previous = s.Density;
            }
        }

        [Test]
        public void GeopotentialConversionRoundTrips()
        {
            Assert.That(Atmosphere.GeopotentialAltitude(86000), Is.EqualTo(84852).Within(1.0));
            Assert.That(Atmosphere.GeometricAltitude(Atmosphere.GeopotentialAltitude(50000)), Is.EqualTo(50000).Within(1e-6));
        }
    }
}
