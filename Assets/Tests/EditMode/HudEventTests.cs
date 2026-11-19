using System.Collections.Generic;
using System.Linq;
using MTS.Core.Display;
using NUnit.Framework;

namespace MTS.Core.Tests
{
    public class HudEventTests
    {
        private static FlightTelemetry T(SimulationStage stage, double altitude, double temperature = 300,
            double massLossRate = 0, bool broken = false, double speed = 17000) =>
            new FlightTelemetry(stage, 0, altitude, speed, speed, 0, 1000, 0, 1000, massLossRate, temperature, broken, 1, 0);

        [Test]
        public void StageAndBreakupEventsFireOnce()
        {
            var d = new FlightEventDetector();
            Assert.That(d.Update(T(SimulationStage.Space, 200000), null), Is.Empty);
            List<(FlightEvent Event, string Message)> entry = d.Update(T(SimulationStage.Atmosphere, 119000), null);
            Assert.That(entry.Select(e => e.Event), Is.EqualTo(new[] { FlightEvent.AtmosphericEntry }));
            Assert.That(d.Update(T(SimulationStage.Atmosphere, 100000), null), Is.Empty);

            List<(FlightEvent Event, string Message)> breakup = d.Update(T(SimulationStage.Atmosphere, 40000, broken: true), null);
            Assert.That(breakup.Select(e => e.Event), Is.EqualTo(new[] { FlightEvent.Breakup }));
            Assert.That(breakup[0].Message, Does.Contain("40.0 km"));
            Assert.That(d.Update(T(SimulationStage.Atmosphere, 39000, broken: true), null), Is.Empty);
        }

        [Test]
        public void PeaksFireAfterTheValueFallsBack()
        {
            var d = new FlightEventDetector();
            d.Update(T(SimulationStage.Atmosphere, 90000, 800, 0), null);
            d.Update(T(SimulationStage.Atmosphere, 60000, 2000, 50), null);
            Assert.That(d.HasFired(FlightEvent.PeakHeating), Is.False);
            Assert.That(d.HasFired(FlightEvent.PeakBrightness), Is.False);

            List<(FlightEvent Event, string Message)> events = d.Update(T(SimulationStage.Atmosphere, 30000, 1800, 1), null);
            Assert.That(events.Select(e => e.Event), Is.EquivalentTo(new[] { FlightEvent.PeakHeating, FlightEvent.PeakBrightness }));
            Assert.That(events.First(e => e.Event == FlightEvent.PeakHeating).Message, Does.Contain("2,000 K").And.Contain("60.0 km"));
        }

        [Test]
        public void ColdOrFaintFlightsHaveNoPeakEvents()
        {
            var d = new FlightEventDetector();
            d.Update(T(SimulationStage.Atmosphere, 90000, 600, 1e-9), null);
            d.Update(T(SimulationStage.Atmosphere, 80000, 300, 0), null);
            Assert.That(d.HasFired(FlightEvent.PeakHeating), Is.False);
            Assert.That(d.HasFired(FlightEvent.PeakBrightness), Is.False);
        }

        [Test]
        public void EndOfRealRunIsReported()
        {
            var props = MeteorProperties.Create(5000, MeteorMaterialPresets.Iron, 0.5,
                new Vec3d(PhysicalConstants.EarthRadius + 110000, 0, 0), EntryMode.RealisticEntry, 15000, 60);
            var runner = new SimulationRunner(props);
            var d = new FlightEventDetector();
            var seen = new List<FlightEvent>();
            while (!runner.HasEnded)
            {
                runner.Advance(0.5);
                seen.AddRange(d.Update(runner.Telemetry, runner.Result).Select(e => e.Event));
            }

            Assert.That(seen, Does.Contain(FlightEvent.AtmosphericEntry));
            FlightEvent expected = runner.Result.Reason == EndReason.Impact ? FlightEvent.Impact
                : runner.Result.Reason == EndReason.Airburst ? FlightEvent.Airburst
                : FlightEvent.Disintegration;
            Assert.That(seen, Does.Contain(expected));
            Assert.That(seen.Distinct().Count(), Is.EqualTo(seen.Count), "an event fired twice");
        }

        [Test]
        public void EveryEventHasATitle()
        {
            foreach (FlightEvent e in System.Enum.GetValues(typeof(FlightEvent)))
            {
                Assert.That(FlightEventDetector.Title(e), Is.Not.Empty);
            }
        }
    }
}
