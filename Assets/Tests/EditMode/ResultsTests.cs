using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    /// <summary>
    /// Tests of the results screen's pure logic (MTS.Results: map projection, geodesic rings, chart scales, formatting
    /// and the outcome report). The test assembly only references MTS.Core, so MTS.Results is reached by reflection;
    /// once the test asmdef references MTS.Results these helpers can be replaced by direct calls.
    /// </summary>
    public class ResultsTests
    {
        private static Assembly results;

        private static Assembly Results
        {
            get
            {
                if (results == null)
                {
                    results = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MTS.Results");
                    Assert.That(results, Is.Not.Null, "MTS.Results assembly is not loaded");
                }

                return results;
            }
        }

        private static Type T(string name) => Results.GetType("MTS.Results." + name, true);

        private static object Call(string type, string method, params object[] args)
        {
            MethodInfo m = T(type).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(x => x.Name == method && x.GetParameters().Length >= args.Length &&
                            x.GetParameters().Skip(args.Length).All(p => p.IsOptional));
            ParameterInfo[] ps = m.GetParameters();
            object[] full = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                full[i] = i < args.Length ? args[i] : ps[i].DefaultValue;
            }

            return m.Invoke(null, full);
        }

        private static object New(string type, params object[] args) => Activator.CreateInstance(T(type), args);

        private static object Get(object o, string member)
        {
            Type t = o.GetType();
            PropertyInfo p = t.GetProperty(member);
            if (p != null) return p.GetValue(o);
            FieldInfo f = t.GetField(member);
            Assert.That(f, Is.Not.Null, t.Name + "." + member);
            return f.GetValue(o);
        }

        private static double D(object o, string member) => Convert.ToDouble(Get(o, member));

        private static object Geo(double lat, double lon) => New("GeoPoint", lat, lon);

        // ---------------------------------------------------------------- map projection

        [TestCase(0.0, 0.0, 0.5, 0.5)]
        [TestCase(90.0, 179.999999, 1.0, 1.0)]
        [TestCase(-90.0, -180.0, 0.0, 0.0)]
        [TestCase(51.5, -0.1278, 0.499645, 0.786111)]       // London
        [TestCase(-33.8688, 151.2093, 0.920026, 0.311840)]  // Sydney
        [TestCase(0.0, 190.0, 0.027778, 0.5)]               // wraps to 170 W
        public void LatLonToMapPosition(double lat, double lon, double x, double y)
        {
            object m = Call("MapProjection", "ToMap", Geo(lat, lon), true);
            Assert.That(D(m, "X"), Is.EqualTo(x).Within(1e-5));
            Assert.That(D(m, "Y"), Is.EqualTo(y).Within(1e-5));
        }

        [Test]
        public void MapPositionRoundTrips()
        {
            for (double lat = -85; lat <= 85; lat += 17)
            {
                for (double lon = -175; lon < 180; lon += 23)
                {
                    object m = Call("MapProjection", "ToMap", Geo(lat, lon), true);
                    object g = Call("MapProjection", "FromMap", m);
                    Assert.That(D(g, "Lat"), Is.EqualTo(lat).Within(1e-9));
                    Assert.That(D(g, "Lon"), Is.EqualTo(lon).Within(1e-9));
                }
            }
        }

        [Test]
        public void EastWestScaleShrinksWithCosLatitude()
        {
            double equator = (double)Call("MapProjection", "MetresPerMapWidth", 0.0);
            double sixty = (double)Call("MapProjection", "MetresPerMapWidth", 60.0);
            Assert.That(equator, Is.EqualTo(2 * Math.PI * PhysicalConstants.EarthRadius).Within(1));
            Assert.That(sixty / equator, Is.EqualTo(0.5).Within(1e-9));
        }

        // ---------------------------------------------------------------- geodesy

        [Test]
        public void DestinationPointMovesAlongMeridiansAndTheEquator()
        {
            double oneDegree = PhysicalConstants.EarthRadius * Math.PI / 180.0;
            object north = Call("Geodesy", "Destination", Geo(0, 0), 0.0, oneDegree);
            Assert.That(D(north, "Lat"), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(D(north, "Lon"), Is.EqualTo(0.0).Within(1e-9));
            object east = Call("Geodesy", "Destination", Geo(0, 179.5), 90.0, oneDegree);
            Assert.That(D(east, "Lat"), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(D(east, "Lon"), Is.EqualTo(180.5).Within(1e-9)); // unwrapped, continuous across the antimeridian
        }

        [Test]
        public void RingPointsAreAllAtTheRingRadius()
        {
            object centre = Geo(55.15, 61.4);
            const double radius = 120000;
            var ring = (Array)Call("Geodesy", "Circle", centre, radius, 90);
            Assert.That(ring.Length, Is.EqualTo(91));
            foreach (object p in ring)
            {
                double d = (double)Call("Geodesy", "Distance", centre, p);
                Assert.That(d, Is.EqualTo(radius).Within(1e-6 * radius));
            }
        }

        [Test]
        public void RingsStretchEastWestByOneOverCosLatitude()
        {
            // A small ring at 60 N spans twice as many degrees of longitude as of latitude.
            var ring = (Array)Call("Geodesy", "Circle", Geo(60, 10), 50000.0, 360);
            double latMin = double.MaxValue, latMax = double.MinValue, lonMin = double.MaxValue, lonMax = double.MinValue;
            foreach (object p in ring)
            {
                latMin = Math.Min(latMin, D(p, "Lat"));
                latMax = Math.Max(latMax, D(p, "Lat"));
                lonMin = Math.Min(lonMin, D(p, "Lon"));
                lonMax = Math.Max(lonMax, D(p, "Lon"));
            }

            Assert.That((lonMax - lonMin) / (latMax - latMin), Is.EqualTo(2.0).Within(0.02));
        }

        [Test]
        public void RingAcrossTheAntimeridianStaysContinuous()
        {
            var ring = (Array)Call("Geodesy", "Circle", Geo(-10, 179.8), 200000.0, 72);
            for (int i = 1; i < ring.Length; i++)
            {
                Assert.That(Math.Abs(D(ring.GetValue(i), "Lon") - D(ring.GetValue(i - 1), "Lon")), Is.LessThan(1.0));
            }
        }

        [Test]
        public void PoleEnclosure()
        {
            Assert.That(Call("Geodesy", "EnclosedPole", Geo(85, 0), 1000000.0), Is.EqualTo(1));
            Assert.That(Call("Geodesy", "EnclosedPole", Geo(-85, 0), 1000000.0), Is.EqualTo(-1));
            Assert.That(Call("Geodesy", "EnclosedPole", Geo(40, 0), 1000000.0), Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- chart scales

        [Test]
        public void NiceAxisCoversTheDataWithRoundSteps()
        {
            object axis = Call("ChartScale", "Nice", 0.0, 87.3, 5, false);
            Assert.That(D(axis, "Min"), Is.EqualTo(0));
            Assert.That(D(axis, "Max"), Is.EqualTo(100));
            Assert.That(D(axis, "Step"), Is.EqualTo(20));

            axis = Call("ChartScale", "Nice", 3.21, 17.9, 6, false);
            Assert.That(D(axis, "Step"), Is.EqualTo(2.5));
            Assert.That(D(axis, "Min"), Is.LessThanOrEqualTo(3.21));
            Assert.That(D(axis, "Max"), Is.GreaterThanOrEqualTo(17.9));

            axis = Call("ChartScale", "Nice", 42.0, 42.0, 5, false); // degenerate range is widened
            Assert.That(D(axis, "Max"), Is.GreaterThan(D(axis, "Min")));
            Assert.That(D(axis, "Min"), Is.LessThanOrEqualTo(42.0));
            Assert.That(D(axis, "Max"), Is.GreaterThanOrEqualTo(42.0));
        }

        [Test]
        public void TickLabelsUseJustEnoughDecimals()
        {
            Assert.That(Call("ChartScale", "Label", 20.0, 20.0), Is.EqualTo("20"));
            Assert.That(Call("ChartScale", "Label", 2.5, 2.5), Is.EqualTo("2.5"));
            Assert.That(Call("ChartScale", "Label", 0.75, 0.25), Is.EqualTo("0.75"));
            Assert.That(Call("ChartScale", "Label", 12000.0, 2000.0), Is.EqualTo("12,000"));
        }

        // ---------------------------------------------------------------- formatting

        [Test]
        public void FormatsEnergyDistancesAndCoordinates()
        {
            Assert.That(Call("ResultsFormat", "Tnt", 4.184e12), Is.EqualTo("1.00 kt TNT"));
            Assert.That(Call("ResultsFormat", "Tnt", 500 * 4.184e12), Is.EqualTo("500 kt TNT"));
            Assert.That(Call("ResultsFormat", "Tnt", 12.5e3 * 4.184e12), Is.EqualTo("12.5 Mt TNT"));
            Assert.That(Call("ResultsFormat", "Tnt", 4.184e8), Is.EqualTo("100 kg TNT"));
            Assert.That(Call("ResultsFormat", "Joules", 2.092e15, false), Is.EqualTo("2.09 × 10^15 J"));
            Assert.That(Call("ResultsFormat", "Distance", 1234.0), Is.EqualTo("1.23 km"));
            Assert.That(Call("ResultsFormat", "Distance", 123456.0), Is.EqualTo("123 km"));
            Assert.That(Call("ResultsFormat", "Distance", 45.6), Is.EqualTo("45.6 m"));
            Assert.That(Call("ResultsFormat", "Sig", 12345.0, 3), Is.EqualTo("12,300"));
            Assert.That(Call("ResultsFormat", "Sig", 9.996, 3), Is.EqualTo("10.0"));
            Assert.That(Call("ResultsFormat", "LatLon", 55.15, -61.4), Is.EqualTo("55.15° N, 61.40° W"));
            Assert.That(Call("ResultsFormat", "Duration", 252.0), Is.EqualTo("4 min 12 s"));
        }

        [Test]
        public void EnergyComparisonsUseTheNearestReferenceEvent()
        {
            const double kt = 4.184e12;
            Assert.That(Call("ResultsFormat", "Comparison", 15 * kt), Is.EqualTo("About the same as Hiroshima"));
            Assert.That(Call("ResultsFormat", "Comparison", 45 * kt), Is.EqualTo("3.0× Hiroshima"));
            Assert.That(Call("ResultsFormat", "Comparison", 520 * kt), Is.EqualTo("About the same as Chelyabinsk (2013)"));
            Assert.That(Call("ResultsFormat", "Comparison", 15000 * kt), Is.EqualTo("About the same as Tunguska (1908)"));
            Assert.That(Call("ResultsFormat", "Comparison", 1.5 * kt), Is.EqualTo("10% of Hiroshima"));
        }

        [TestCase(0.0, -30.0, "Atlantic Ocean")]
        [TestCase(0.0, -150.0, "Pacific Ocean")]
        [TestCase(-20.0, 80.0, "Indian Ocean")]
        [TestCase(80.0, 0.0, "Arctic Ocean")]
        [TestCase(-65.0, 100.0, "Southern Ocean")]
        [TestCase(35.0, 18.0, "Mediterranean Sea")]
        [TestCase(30.0, 160.0, "Pacific Ocean")]
        public void OceanNamesForPointsInTheSea(double lat, double lon, string name)
        {
            Assert.That(Call("OceanNames", "Guess", lat, lon), Is.EqualTo(name));
        }

        // ---------------------------------------------------------------- outcome report

        private static object Report(SimulationResult r, MeteorProperties p) =>
            Call("OutcomeReport", "Build", r, r == null ? null : ImpactModel.Assess(r, p), p);

        private static MeteorProperties AtEntry(double mass, MeteorMaterial mat, double speed, double angle, bool fragmentation = true)
        {
            var start = new Vec3d(0, 0, PhysicalConstants.EarthRadius + PhysicalConstants.AtmosphereEntryAltitude);
            return MeteorProperties.Create(mass, mat, 0, start, EntryMode.Inbound, speed, angle, fragmentation);
        }

        private static string StatValue(object report, string label)
        {
            foreach (object s in (System.Collections.IEnumerable)Get(report, "Stats"))
            {
                if ((string)Get(s, "Label") == label) return (string)Get(s, "Value");
            }

            return null;
        }

        [Test]
        public void ImpactReportHasCraterAndRings()
        {
            var p = MeteorProperties.FromInputs(60000, 7800, 800, 0.5, 1, 1, 1);
            SimulationResult r = new SimulationRunner(p, 0, 0).RunToEnd();
            object report = Report(r, p);
            Assert.That(Get(report, "Kind").ToString(), Is.EqualTo("GroundImpact").Or.EqualTo("MeteoriteFall"));
            Assert.That(D(report, "Energy"), Is.GreaterThan(0));
            Assert.That(StatValue(report, "Crater size"), Is.Not.Null.And.Not.EqualTo("0"));
            Assert.That((bool)Get(report, "HasRings"), Is.True);
            Assert.That((bool)Get(report, "HasLocation"), Is.True);
        }

        [Test]
        public void AirburstReportShowsBurstAltitudeAndGroundEffects()
        {
            // Chelyabinsk-like: 19 m stony body at 19 km/s, 18 degrees.
            double mass = 3300 * Math.PI / 6 * Math.Pow(19, 3);
            MeteorProperties p = AtEntry(mass, MeteorMaterialPresets.OrdinaryChondrite, 19000, 18);
            SimulationResult r = new SimulationRunner(p).RunToEnd();
            Assert.That(r.Reason, Is.EqualTo(EndReason.Airburst));
            object report = Report(r, p);
            Assert.That(Get(report, "Kind").ToString(), Is.EqualTo("Airburst"));
            Assert.That(D(report, "EnergyKilotons"), Is.InRange(100, 2000));
            Assert.That(StatValue(report, "Burst altitude"), Does.EndWith("km"));
            // With the high-altitude blast model the shock reaches the ground with a few kPa, not zero.
            Assert.That(D(report, "GroundZeroOverpressure"), Is.InRange(300, 10000));
            Assert.That((bool)Get(report, "HasRings"), Is.True);
            Assert.That((string)Get(report, "GroundEffects"), Does.Contain("windows break out to"));
        }

        [Test]
        public void DisintegratedReportShowsAblationAltitudeAndEnergyDeposited()
        {
            MeteorProperties p = AtEntry(20, MeteorMaterial.CreateCustom(1000, 800, 0), 30000, 60, false);
            SimulationResult r = new SimulationRunner(p).RunToEnd();
            Assert.That(r.Reason, Is.EqualTo(EndReason.Disintegrated));
            object report = Report(r, p);
            Assert.That(Get(report, "Kind").ToString(), Is.EqualTo("Disintegrated"));
            Assert.That((string)Get(report, "Headline"), Does.StartWith("Ablated away at").And.EndWith("km"));
            // Nearly all of the entry kinetic energy (0.5 * 20 kg * (30 km/s)^2 = 9 GJ) goes into the air.
            Assert.That(D(report, "Energy"), Is.InRange(7e9, 9.5e9));
            Assert.That((bool)Get(report, "HasRings"), Is.False);
            Assert.That(StatValue(report, "Gone at"), Is.Not.Null);
        }

        [Test]
        public void TimeoutErrorAndNoRunReportsAreGraceful()
        {
            double radius = PhysicalConstants.EarthRadius + 2.0e6;
            var v = new Vec3d(0, 0, Math.Sqrt(PhysicalConstants.EarthMu / radius));
            MeteorProperties p = MeteorProperties.Create(1000, MeteorMaterial.CreateCustom(3000, 800, 0), 0, new Vec3d(radius, 0, 0), initialVelocity: v);
            SimulationResult r = new SimulationRunner(p) { MaxSimulatedTime = 500 }.RunToEnd();
            object timeout = Report(r, p);
            Assert.That(Get(timeout, "Kind").ToString(), Is.EqualTo("Timeout"));
            Assert.That((string)Get(timeout, "Title"), Is.EqualTo("STILL IN FLIGHT"));

            object none = Report(null, null);
            Assert.That(Get(none, "Kind").ToString(), Is.EqualTo("NoRun"));
        }

        // ---------------------------------------------------------------- time series

        [Test]
        public void TimeSeriesRecordsTemperatureMachAndAnEntrySeries()
        {
            double mass = 3300 * Math.PI / 6 * Math.Pow(19, 3);
            MeteorProperties p = AtEntry(mass, MeteorMaterialPresets.OrdinaryChondrite, 19000, 18);
            SimulationResult r = new SimulationRunner(p).RunToEnd();
            TimeSeries s = r.Series;
            Assert.That(s.Temperature.Count, Is.EqualTo(s.Count));
            Assert.That(s.Mach.Count, Is.EqualTo(s.Count));
            Assert.That(s.DynamicPressure.Count, Is.EqualTo(s.Count));
            TimeSeries entry = s.Entry;
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.Count, Is.InRange(100, TimeSeries.Capacity));
            Assert.That(entry.Time[entry.Count - 1], Is.EqualTo(r.EndTime));
            Assert.That(entry.Altitude.Max(), Is.LessThanOrEqualTo(PhysicalConstants.AtmosphereEntryAltitude));
            Assert.That(entry.Temperature.Max(), Is.EqualTo(r.PeakTemperature).Within(r.PeakTemperature * 0.05));
            Assert.That(entry.Mach.Max(), Is.GreaterThan(40)); // 19 km/s is about Mach 60 in the stratosphere
        }
    }
}
