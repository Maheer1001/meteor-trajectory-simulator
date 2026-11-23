using System.Collections.Generic;
using System.Diagnostics;
using MTS.Core;
using NUnit.Framework;

namespace MTS.SetupChecks.Tests
{
    /// <summary>
    /// End-to-end setup check of the run the game performs, without the scenes: a scenario is applied to the shared
    /// <see cref="SimulationSession"/>, inputs are built the way the input screen builds them, and the run is stepped
    /// through space and the atmosphere to a result with ground effects, as the stage scenes and results screen do.
    /// </summary>
    public class SimulationWiringChecks
    {
        [TearDown]
        public void ResetSession() => SimulationSession.Instance.Reset();

        [Test]
        public void RealisticModeScenarioRunsThroughEveryStageToAResult()
        {
            SimulationSession session = SimulationSession.Instance;
            session.Reset();
            EntryMode previousMode = session.Mode;
            MeteorComposition previousComposition = session.Composition;
            double previousSpeed = session.EntrySpeed, previousAngle = session.EntryAngleDegrees;
            try
            {
                ImpactScenario scenario = ImpactScenarios.ById("chelyabinsk");
                Assert.That(scenario, Is.Not.Null, "the Chelyabinsk scenario is missing");
                scenario.ApplyTo(session);
                Assert.That(session.Mode, Is.EqualTo(EntryMode.Inbound), "scenarios use realistic (inbound) entry");

                // Same call as SimulationInputs.Snapshot, with the input screen's blank-field coordinates (25, 25, 25).
                double c = MeteorProperties.DefaultCoordinate;
                MeteorProperties inputs = MeteorProperties.FromComposition(scenario.Mass, session.Composition, c, c, c,
                    session.Mode, session.EntrySpeed, session.EntryAngleDegrees);
                Assert.That(inputs.Substitutions, Is.Empty, "valid scenario inputs should not be replaced by defaults");

                SimulationRunner runner = session.Begin(inputs);
                session.SetWind(SimulationSession.DefaultWindSpeed, SimulationSession.DefaultWindDirection);
                Assert.That(runner.SetupNote, Is.Empty, "realistic entry fell back to release from rest");

                var stages = new List<SimulationStage> { session.Stage };
                var clock = Stopwatch.StartNew();
                while (!runner.HasEnded)
                {
                    runner.Step(runner.StepSizeFor(runner.State.Altitude));
                    if (stages[stages.Count - 1] != session.Stage)
                    {
                        stages.Add(session.Stage);
                    }

                    if (clock.Elapsed.TotalSeconds > 60)
                    {
                        Assert.Fail("the run did not finish in a minute (stage " + session.Stage + ", " + runner.State + ")");
                    }
                }

                Assert.That(stages, Is.EqualTo(new[] { SimulationStage.Space, SimulationStage.Atmosphere, SimulationStage.Ended }));

                SimulationResult result = session.Result;
                Assert.That(result.Reason, Is.EqualTo(EndReason.Airburst).Or.EqualTo(EndReason.Impact).Or.EqualTo(EndReason.Disintegrated), result.Message);
                Assert.That(result.FinalState.IsFinite, Is.True);
                Assert.That(result.EndTime, Is.GreaterThan(0));

                ImpactAssessment impact = session.Impact;
                Assert.That(impact, Is.Not.Null, "no ground effects for a finished run");
                Assert.That(impact.EnergyKilotons, Is.GreaterThan(0).And.LessThan(1e6));
                Assert.That(impact.BlastRadii, Is.Not.Empty);

                Geo.LatitudeLongitude(result.FinalState.Position, result.EndTime, out double lat, out double lon);
                Assert.That(lat, Is.InRange(-90.0, 90.0));
                Assert.That(lon, Is.InRange(-180.0, 180.0));
                TestContext.WriteLine($"Chelyabinsk run: {result.Reason} at {result.EndAltitude / 1000:F1} km after {result.EndTime:F0} s, " +
                                      $"{impact.EnergyKilotons:F0} kt, {lat:F2}, {lon:F2}");
            }
            finally
            {
                session.Mode = previousMode;
                session.Composition = previousComposition;
                session.EntrySpeed = previousSpeed;
                session.EntryAngleDegrees = previousAngle;
            }
        }
    }
}
