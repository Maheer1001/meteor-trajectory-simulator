using System;

namespace MTS.Core
{
    /// <summary>
    /// How a meteoroid material looks, for the procedural rock texture. Display data only: the physics never reads it.
    /// Colours are linear-ish RGB in 0..1; the other parameters are 0..1 weights.
    /// </summary>
    public readonly struct MaterialAppearance
    {
        /// <summary>Matrix (bulk) colour.</summary>
        public readonly float BaseR, BaseG, BaseB;
        /// <summary>Colour of inclusions: chondrules, CAIs, olivine crystals, Widmanstaetten lamellae, ice.</summary>
        public readonly float AccentR, AccentG, AccentB;
        /// <summary>Metal content (0 dull rock .. 1 bare Fe-Ni): specular strength and tint.</summary>
        public readonly float Metallic;
        /// <summary>Surface roughness (0 polished .. 1 very rough).</summary>
        public readonly float Roughness;
        /// <summary>How densely the accent inclusions are scattered through the matrix.</summary>
        public readonly float Inclusions;
        /// <summary>Lumpiness / porosity: 0 a solid body, 1 a loose rubble pile of boulders.</summary>
        public readonly float Porosity;
        /// <summary>Volatile ice fraction (cometary material): bright, slightly blue frost.</summary>
        public readonly float Ice;

        public MaterialAppearance(float baseR, float baseG, float baseB, float accentR, float accentG, float accentB,
            float metallic, float roughness, float inclusions, float porosity, float ice = 0f)
        {
            BaseR = Clamp01(baseR);
            BaseG = Clamp01(baseG);
            BaseB = Clamp01(baseB);
            AccentR = Clamp01(accentR);
            AccentG = Clamp01(accentG);
            AccentB = Clamp01(accentB);
            Metallic = Clamp01(metallic);
            Roughness = Clamp01(roughness);
            Inclusions = Clamp01(inclusions);
            Porosity = Clamp01(porosity);
            Ice = Clamp01(ice);
        }

        /// <summary>A neutral grey stony look, or a metallic one for iron-like densities.</summary>
        public static MaterialAppearance ForDensity(double density) => density >= MeteorMaterial.IronDensityThreshold
            ? new MaterialAppearance(0.42f, 0.42f, 0.44f, 0.70f, 0.70f, 0.72f, 0.9f, 0.45f, 0.1f, 0f)
            : new MaterialAppearance(0.45f, 0.42f, 0.38f, 0.62f, 0.58f, 0.50f, 0.1f, 0.8f, 0.5f, 0.2f);

        /// <summary>Weighted blend (weights need not be normalised; non-positive ones are ignored).</summary>
        public static MaterialAppearance Blend(MaterialAppearance[] looks, double[] weights)
        {
            if (looks == null || weights == null || looks.Length != weights.Length || looks.Length == 0)
            {
                throw new ArgumentException("Need one weight per appearance.");
            }

            double total = 0;
            for (int i = 0; i < weights.Length; i++) if (weights[i] > 0) total += weights[i];
            if (!(total > 0)) return looks[0];

            float br = 0, bg = 0, bb = 0, ar = 0, ag = 0, ab = 0, m = 0, r = 0, inc = 0, por = 0, ice = 0;
            for (int i = 0; i < looks.Length; i++)
            {
                if (!(weights[i] > 0)) continue;
                float w = (float)(weights[i] / total);
                MaterialAppearance a = looks[i];
                br += w * a.BaseR; bg += w * a.BaseG; bb += w * a.BaseB;
                ar += w * a.AccentR; ag += w * a.AccentG; ab += w * a.AccentB;
                m += w * a.Metallic; r += w * a.Roughness; inc += w * a.Inclusions; por += w * a.Porosity; ice += w * a.Ice;
            }

            return new MaterialAppearance(br, bg, bb, ar, ag, ab, m, r, inc, por, ice);
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
    }
}
