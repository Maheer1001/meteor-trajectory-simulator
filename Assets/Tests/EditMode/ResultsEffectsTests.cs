using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    /// <summary>
    /// Results-screen logic for the catastrophe effects: the land/ocean mask, planet-scale formatting, circle bands on
    /// the map, chart units and the Effects data of the outcome report. MTS.Results is reached by reflection (the test
    /// assembly only references MTS.Core), as in <see cref="ResultsTests"/>.
    /// </summary>
    public class ResultsEffectsTests
    {
        private static Assembly Results =>
            AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "MTS.Results");

        private static Type T(string name) => Results.GetType("MTS.Results." + name, true);

        private static object Call(string type, string method, params object[] args)
        {
            MethodInfo m = T(type).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(x => x.Name == method && x.GetParameters().Length >= args.Length &&
                            x.GetParameters().Skip(args.Length).All(p => p.IsOptional));
            ParameterInfo[] ps = m.GetParameters();
            var full = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++) full[i] = i < args.Length ? args[i] : ps[i].DefaultValue;
            return m.Invoke(null, full);
        }

        private static object Get(object o, string member)
        {
            PropertyInfo p = o.GetType().GetProperty(member);
            return p != null ? p.GetValue(o) : o.GetType().GetField(member).GetValue(o);
        }

        private static object GeoPoint(double lat, double lon) => Activator.CreateInstance(T("GeoPoint"), lat, lon);

        [TestCase(0, -150, false, TestName = "Mid-Pacific is ocean")]
        [TestCase(30, -40, false, TestName = "North Atlantic is ocean")]
        [TestCase(-20, 80, false, TestName = "Indian Ocean is ocean")]
        [TestCase(35, 18, false, TestName = "Mediterranean is ocean")]
        [TestCase(87, 0, false, TestName = "Arctic Ocean is ocean")]
        [TestCase(23, 10, true, TestName = "Sahara is land")]
        [TestCase(38, -98, true, TestName = "Kansas is land")]
        [TestCase(-25, 134, true, TestName = "Central Australia is land")]
        [TestCase(55.7, 37.6, true, TestName = "Moscow is land")]
        [TestCase(35.03, -111.02, true, TestName = "Meteor Crater is land")]
        [TestCase(72, -40, true, TestName = "Greenland ice sheet is land")]
        [TestCase(-80, 0, true, TestName = "Antarctica is land")]
        public void LandMaskKnownPoints(double lat, double lon, bool land)
        {
            Assert.That((bool)Call("LandMask", "IsLand", lat, lon), Is.EqualTo(land));
            Assert.That((bool)Call("LandMask", "IsOcean", lat, lon), Is.EqualTo(!land));
        }

        [Test]
        public void LandMaskWrapsLongitudeAndHasARealisticLandShare()
        {
            Assert.That(Call("LandMask", "IsLand", 23.0, 370.0), Is.EqualTo(Call("LandMask", "IsLand", 23.0, 10.0)));
            Assert.That(Call("LandMask", "IsLand", 0.0, 210.0), Is.EqualTo(Call("LandMask", "IsLand", 0.0, -150.0)));
            // About 29% of the surface is land, but high latitudes are over-represented on a lat-lon grid (Antarctica).
            Assert.That((double)Call("LandMask", "LandCellFraction"), Is.InRange(0.3, 0.45));
        }

        [Test]
        public void PlanetScaleFormatting()
        {
            double kt = PhysicalConstants.JoulesPerKilotonTnt;
            Assert.That(Call("ResultsFormat", "Tnt", 6e10 * kt), Is.EqualTo("60.0 Tt TNT"));
            Assert.That(Call("ResultsFormat", "Tnt", 8e7 * kt), Is.EqualTo("80.0 Gt TNT"));
            Assert.That(Call("ResultsFormat", "Comparison", 1e11 * kt), Is.EqualTo("About the same as Chicxulub (dinosaur killer)"));
            Assert.That(Call("ResultsFormat", "Comparison", 8e7 * kt), Is.EqualTo("1/1,300 of Chicxulub (dinosaur killer)"));
            Assert.That(Call("ResultsFormat", "Comparison", 15000 * kt), Is.EqualTo("About the same as Tunguska (1908)"));
            Assert.That(Call("ResultsFormat", "Reach", ImpactModel.MaxGroundRange), Is.EqualTo("Whole Earth"));
            Assert.That(Call("ResultsFormat", "Reach", 1.2e7), Is.EqualTo("12,000 km (global)"));
            Assert.That(Call("ResultsFormat", "Reach", 1.2e7, false), Is.EqualTo("12,000 km"));
            Assert.That(Call("ResultsFormat", "Reach", 5000.0), Is.EqualTo("5.00 km"));
            Assert.That(Call("ResultsFormat", "Distance", 0.0), Is.EqualTo("0 m"));
            Assert.That(Call("ResultsFormat", "Distance", 0.0032), Is.EqualTo("3.2 mm"));
            Assert.That(Call("ResultsFormat", "Distance", 0.00011), Is.EqualTo("110 µm"));
            Assert.That(Call("ResultsFormat", "ShortDuration", 5040.0), Is.EqualTo("1 h 24 min"));
            Assert.That(Call("ResultsFormat", "ShortDuration", 0.62), Is.EqualTo("0.6 s"));
            Assert.That(Call("ResultsUi", "RichPowers", "1.25 × 10^12 t"), Is.EqualTo("1.25 × 10<sup>12</sup> t"));
        }

        [Test]
        public void CircleBandsHandleHugeCircles()
        {
            object c = GeoPoint(20, -90);
            double R = PhysicalConstants.EarthRadius;
            // A small circle: half-width at its own latitude is about radius / (R cos lat), in degrees.
            double w = (double)Call("Geodesy", "HalfWidthAtLatitude", c, 100000.0, 20.0);
            Assert.That(w, Is.EqualTo(100000.0 / (R * Math.Cos(20 * Math.PI / 180)) * 180 / Math.PI).Within(0.01));
            Assert.That((double)Call("Geodesy", "HalfWidthAtLatitude", c, 100000.0, 40.0), Is.LessThan(0), "outside");
            // 150 degrees around 20 N contains both poles: whole parallels inside except near the antipode.
            double big = 150 * Math.PI / 180 * R;
            Assert.That((double)Call("Geodesy", "HalfWidthAtLatitude", c, big, 85.0), Is.EqualTo(180.0));
            Assert.That((double)Call("Geodesy", "HalfWidthAtLatitude", c, big, -85.0), Is.EqualTo(180.0));
            double nearAntipode = (double)Call("Geodesy", "HalfWidthAtLatitude", c, big, -20.0);
            // The antipode (20 S, 90 E) is outside, in a 30 degree cap: on its parallel the cap spans about +-32 degrees.
            Assert.That(nearAntipode, Is.EqualTo(148.0).Within(0.5));
            Assert.That((double)Call("Geodesy", "HalfWidthAtLatitude", c, ImpactModel.MaxGroundRange, -20.0), Is.EqualTo(180.0));
        }

        [Test]
        public void MassAxisUnitsStayShort()
        {
            object[] args = { 1.25e15, 0.0, "" };
            T("ResultsPages").GetMethod("MassUnit").Invoke(null, args);
            Assert.That((double)args[1], Is.EqualTo(1e12 * 1e3));
            Assert.That((string)args[2], Is.EqualTo("10<sup>12</sup> t"));
            args = new object[] { 5e6, 0.0, "" };
            T("ResultsPages").GetMethod("MassUnit").Invoke(null, args);
            Assert.That((string)args[2], Is.EqualTo("t"));
        }

        private static MeteorProperties AtEntry(ImpactScenario s, MeteorMaterial material, double lat, double lon)
        {
            double r = PhysicalConstants.EarthRadius + PhysicalConstants.AtmosphereEntryAltitude;
            double phi = lat * Math.PI / 180.0, lambda = lon * Math.PI / 180.0;
            var start = new Vec3d(Math.Cos(phi) * Math.Cos(lambda) * r, Math.Sin(phi) * r, -Math.Cos(phi) * Math.Sin(lambda) * r);
            return MeteorProperties.Create(s.Mass, material, 0, start, EntryMode.Inbound, s.EntrySpeed, s.EntryAngleDegrees, true);
        }

        [Test]
        public void OceanCatastropheReportHasTsunamiCardsRingsAndModeWording()
        {
            ImpactScenario s = ImpactScenarios.RegionalKiller;
            MeteorComposition mix = MeteorComposition.Create(new[]
            {
                (MeteorMaterialPresets.OrdinaryChondriteId, 60.0), (MeteorMaterialPresets.IronId, 40.0),
            });
            MeteorProperties p = AtEntry(s, mix.Material, 30, -140);
            SimulationResult r = new SimulationRunner(p).RunToEnd();
            Assert.That(r.Reason, Is.EqualTo(EndReason.Impact));
            object report = Call("OutcomeReport", "Build", r, ImpactModel.Assess(r, p), p, mix);
            Assert.That((bool)Get(report, "IsOcean"), Is.True);
            Assert.That(Get(report, "ModeText"), Is.EqualTo("Inbound entry"));
            Assert.That(Call2(report, "CompositionText"), Is.EqualTo("60% Stony (ordinary chondrite) + 40% Iron (Fe-Ni)"));
            var effects = (ImpactEffects)Get(report, "Effects");
            Assert.That(effects.HasTsunami && effects.HasThermal && effects.HasSeismic, Is.True);
            Assert.That(((ICollection)Get(report, "EffectCards")).Count, Is.EqualTo(4));
            Assert.That(((ICollection)Get(report, "DistanceRows")).Count, Is.EqualTo(5));
            bool tsunamiRing = false;
            foreach (object ring in (IEnumerable)Get(report, "EffectRings"))
            {
                tsunamiRing |= Get(ring, "Group").ToString() == "Tsunami";
            }

            Assert.That(tsunamiRing, Is.True);
            Assert.That((bool)Get(report, "ShowGlobalBanner"), Is.True);
            // Never the old mode names.
            foreach (object section in (IEnumerable)Get(report, "Details"))
            {
                foreach (object row in (IEnumerable)Get(section, "Rows"))
                {
                    string text = Get(row, "Label") + " " + Get(row, "Value");
                    Assert.That(text, Does.Not.Contain("NEA").And.Not.Contain("Classic").And.Not.Contain("Realistic"));
                }
            }

            // The same impact on land has no tsunami.
            object land = Call("OutcomeReport", "Build", r, ImpactModel.Assess(r, p), p, mix, (Func<double, double, bool>)((a, b) => false));
            Assert.That(((ImpactEffects)Get(land, "Effects")).HasTsunami, Is.False);
        }

        private static object Call2(object o, string method) => o.GetType().GetMethod(method).Invoke(o, null);
    }
}
