using MTS.Core;
using NUnit.Framework;

namespace MTS.Visuals.Tests
{
    public class RockLookTests
    {
        [Test]
        public void EveryPresetGetsItsOwnStyle()
        {
            var seen = new System.Collections.Generic.HashSet<RockStyle>();
            foreach ((string id, MeteorMaterial m) in MeteorMaterialPresets.All)
            {
                RockLook look = RockLook.From(MeteorComposition.Single(id));
                Assert.That(look.Layers.Count, Is.EqualTo(1));
                Assert.That(look.Layers[0].Fraction, Is.EqualTo(1f).Within(1e-6));
                seen.Add(look.Layers[0].Style);
            }

            Assert.That(seen.Count, Is.EqualTo(8));
            Assert.That(RockLook.StyleFor(MeteorMaterialPresets.IronId, default), Is.EqualTo(RockStyle.Iron));
            Assert.That(RockLook.StyleFor(MeteorMaterialPresets.StonyIronId, default), Is.EqualTo(RockStyle.Pallasite));
        }

        [Test]
        public void MixKeepsOneLayerPerPartWithCumulativeEdges()
        {
            MeteorComposition c = MeteorComposition.Create(new[]
            {
                (MeteorMaterialPresets.OrdinaryChondriteId, 50.0), (MeteorMaterialPresets.IronId, 30.0),
                (MeteorMaterialPresets.CarbonaceousId, 20.0),
            });
            RockLook look = RockLook.From(c);
            Assert.That(look.Layers.Count, Is.EqualTo(3));
            Assert.That(look.Layers[0].Style, Is.EqualTo(RockStyle.Chondrite));
            Assert.That(look.Layers[1].Style, Is.EqualTo(RockStyle.Iron));
            float[] edges = look.Edges();
            Assert.That(edges, Has.Length.EqualTo(3));
            Assert.That(edges[0], Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(edges[1], Is.EqualTo(0.8f).Within(1e-5));
            Assert.That(edges[2], Is.EqualTo(1f));
        }

        [Test]
        public void CustomMaterialFallsBackToItsAppearance()
        {
            MeteorMaterial custom = MeteorMaterial.CreateCustom(7900, 450, 0.5);
            RockLook look = RockLook.For(MeteorComposition.Default, custom);
            Assert.That(look.Layers.Count, Is.EqualTo(1));
            Assert.That(look.Layers[0].Style, Is.EqualTo(RockStyle.Iron));

            RockLook same = RockLook.For(MeteorComposition.Single(MeteorMaterialPresets.CometaryId), MeteorMaterialPresets.Cometary);
            Assert.That(same.Layers[0].Style, Is.EqualTo(RockStyle.Cometary));
            Assert.That(same.Ice, Is.GreaterThan(0.5f));
        }

        [Test]
        public void LumpinessFollowsPorosity()
        {
            Assert.That(RockLook.LumpinessFor(0f), Is.EqualTo(0f));
            Assert.That(RockLook.LumpinessFor(1f), Is.EqualTo(1f));
            Assert.That(RockLook.From(MeteorComposition.Single(MeteorMaterialPresets.RubblePileId)).Lumpiness, Is.GreaterThan(0.9f));
            Assert.That(RockLook.From(MeteorComposition.Single(MeteorMaterialPresets.IronId)).Lumpiness, Is.EqualTo(0f));
        }

        [Test]
        public void FireballRadiusFollowsCubeRootOfEnergy()
        {
            Assert.That(ImpactVisualScale.FireballRadius(0), Is.EqualTo(0));
            double megaton = 4.184e15;
            Assert.That(ImpactVisualScale.FireballRadius(megaton), Is.EqualTo(322).Within(2));
            Assert.That(ImpactVisualScale.FireballRadius(megaton * 1000) / ImpactVisualScale.FireballRadius(megaton), Is.EqualTo(10).Within(1e-6));
        }

        [Test]
        public void SeverityAndEndCameraScaleWithTheEvent()
        {
            Assert.That(ImpactVisualScale.Severity(0), Is.EqualTo(0f));
            Assert.That(ImpactVisualScale.Severity(1e-3), Is.EqualTo(0f).Within(1e-6));
            Assert.That(ImpactVisualScale.Severity(6e10), Is.GreaterThan(0.95f));
            Assert.That(ImpactVisualScale.Severity(500), Is.LessThan(ImpactVisualScale.Severity(1e4)));

            double small = ImpactVisualScale.EndCameraDistance(10, 0, 55);
            double huge = ImpactVisualScale.EndCameraDistance(400000, 0, 55);
            Assert.That(small, Is.LessThan(200));
            Assert.That(huge, Is.GreaterThan(500000));
            Assert.That(ImpactVisualScale.EndCameraDistance(1e9, 0, 55), Is.EqualTo(4.0e6));
        }

        [Test]
        public void ChaseDistanceStaysOutsideTheSheath()
        {
            Assert.That(ImpactVisualScale.ChaseDistance(1, 1, 0), Is.EqualTo(13).Within(1e-9));
            Assert.That(ImpactVisualScale.ChaseDistance(1, 50, 1), Is.GreaterThan(50 * 3));
            Assert.That(ImpactVisualScale.ChaseDistance(5000, 5000, 0), Is.GreaterThan(5000 * 8));
        }
    }
}
