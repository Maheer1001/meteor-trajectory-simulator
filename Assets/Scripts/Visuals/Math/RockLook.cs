using System;
using System.Collections.Generic;
using MTS.Core;

namespace MTS.Visuals
{
    /// <summary>Surface pattern family of one material in the procedural rock shader (MeteorRock.shader).</summary>
    public enum RockStyle
    {
        /// <summary>Grey-brown matrix with pale chondrules and metal flecks (ordinary chondrite, custom stony).</summary>
        Chondrite = 0,
        /// <summary>Near-black matrix with white CAIs.</summary>
        Carbonaceous = 1,
        /// <summary>Grey matrix full of bright metal flakes.</summary>
        Enstatite = 2,
        /// <summary>Light grey with dark pyroxene speckle.</summary>
        Basaltic = 3,
        /// <summary>Fe-Ni matrix holding gold-green olivine crystals.</summary>
        Pallasite = 4,
        /// <summary>Metallic with Widmanstaetten-like banding and thumbprint dimples.</summary>
        Iron = 5,
        /// <summary>Dark dust with icy frost.</summary>
        Cometary = 6,
        /// <summary>Lumpy aggregate of boulders with dark gaps.</summary>
        Rubble = 7,
    }

    /// <summary>One material of the rock's look with its share of the surface.</summary>
    public readonly struct RockLayer
    {
        public readonly RockStyle Style;
        public readonly MaterialAppearance Appearance;
        public readonly float Fraction;

        public RockLayer(RockStyle style, MaterialAppearance appearance, float fraction)
        {
            Style = style;
            Appearance = appearance;
            Fraction = fraction;
        }
    }

    /// <summary>
    /// How the meteoroid looks, derived from its composition: up to four layers (one per material, largest first) that
    /// the rock shader lays out as patches and veins in proportion to their mass fractions, plus whole-body values
    /// (lumpiness from porosity, ice for a coma). Pure maths: no Unity types, so it is unit-tested.
    /// </summary>
    public sealed class RockLook
    {
        public const int MaxLayers = 4;

        private readonly RockLayer[] layers;

        public IReadOnlyList<RockLayer> Layers => layers;

        private RockLook(RockLayer[] layers)
        {
            this.layers = layers;
        }

        /// <summary>Fraction-weighted porosity (0 solid .. 1 rubble pile).</summary>
        public float Porosity => Weighted(a => a.Porosity);

        /// <summary>Fraction-weighted ice content (cometary material).</summary>
        public float Ice => Weighted(a => a.Ice);

        /// <summary>Fraction-weighted metal content.</summary>
        public float Metallic => Weighted(a => a.Metallic);

        /// <summary>
        /// How strongly the mesh is built as a boulder aggregate (0 a single solid body .. 1 a loose rubble pile).
        /// Only clearly porous bodies get lumps; solid rock and metal stay smooth-ish.
        /// </summary>
        public float Lumpiness => LumpinessFor(Porosity);

        public static float LumpinessFor(float porosity)
        {
            float x = Clamp01((porosity - 0.25f) / 0.65f);
            return x * x * (3f - 2f * x);
        }

        /// <summary>
        /// Cumulative fraction edges between consecutive layers (length 3; unused edges are 1). The shader assigns
        /// layer i to the part of an equalised noise field between edge i-1 and edge i, so each material covers about
        /// its mass fraction of the surface.
        /// </summary>
        public float[] Edges()
        {
            var edges = new float[MaxLayers - 1];
            float sum = 0f;
            for (int i = 0; i < edges.Length; i++)
            {
                if (i < layers.Length - 1)
                {
                    sum += layers[i].Fraction;
                    edges[i] = Clamp01(sum);
                }
                else
                {
                    edges[i] = 1f;
                }
            }

            return edges;
        }

        /// <summary>Look of a composition.</summary>
        public static RockLook From(MeteorComposition composition)
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            int n = Math.Min(MaxLayers, composition.Parts.Count);
            var result = new RockLayer[n];
            float total = 0f;
            for (int i = 0; i < n; i++) total += (float)Math.Max(0.0, composition.Parts[i].Fraction);
            if (!(total > 0f)) total = 1f;
            for (int i = 0; i < n; i++)
            {
                CompositionPart part = composition.Parts[i];
                MaterialAppearance a = part.Material.Appearance;
                result[i] = new RockLayer(StyleFor(part.Id, a), a, (float)Math.Max(0.0, part.Fraction) / total);
            }

            return new RockLook(result);
        }

        /// <summary>
        /// Look of the body actually flown: the session's composition when it is what was flown, otherwise (a run
        /// started without a composition, e.g. custom density) the flown material on its own.
        /// </summary>
        public static RockLook For(MeteorComposition sessionComposition, MeteorMaterial flown)
        {
            if (sessionComposition != null && (flown == null || ReferenceEquals(sessionComposition.Material, flown)))
            {
                return From(sessionComposition);
            }

            if (flown == null) return From(MeteorComposition.Default);
            return From(MeteorComposition.FromMaterial(flown));
        }

        /// <summary>Pattern family for a built-in material id, or guessed from the appearance for custom materials.</summary>
        public static RockStyle StyleFor(string id, MaterialAppearance a)
        {
            switch (id)
            {
                case MeteorMaterialPresets.OrdinaryChondriteId: return RockStyle.Chondrite;
                case MeteorMaterialPresets.CarbonaceousId: return RockStyle.Carbonaceous;
                case MeteorMaterialPresets.EnstatiteId: return RockStyle.Enstatite;
                case MeteorMaterialPresets.BasalticAchondriteId: return RockStyle.Basaltic;
                case MeteorMaterialPresets.StonyIronId: return RockStyle.Pallasite;
                case MeteorMaterialPresets.IronId: return RockStyle.Iron;
                case MeteorMaterialPresets.CometaryId: return RockStyle.Cometary;
                case MeteorMaterialPresets.RubblePileId: return RockStyle.Rubble;
            }

            if (a.Ice > 0.3f) return RockStyle.Cometary;
            if (a.Metallic > 0.75f) return RockStyle.Iron;
            if (a.Metallic > 0.45f) return RockStyle.Pallasite;
            if (a.Porosity > 0.7f) return RockStyle.Rubble;
            if ((a.BaseR + a.BaseG + a.BaseB) / 3f < 0.18f) return RockStyle.Carbonaceous;
            return RockStyle.Chondrite;
        }

        private float Weighted(Func<MaterialAppearance, float> f)
        {
            float sum = 0f;
            foreach (RockLayer l in layers) sum += l.Fraction * f(l.Appearance);
            return sum;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
    }
}
