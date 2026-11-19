using System;
using System.Collections.Generic;

namespace MTS.Core
{
    /// <summary>
    /// Built-in meteoroid materials and a mixing rule for compositions. Values are representative bulk properties from
    /// the meteoritics literature (densities: Britt and Consolmagno 2003, Macke et al. 2011; strengths: Popova et al. 2011
    /// bulk/fireball strengths; heats of ablation and ablation temperatures as used in fireball modelling, e.g. Ceplecha
    /// and ReVelle 2005; cometary values after Borovicka 2006 and Ceplecha et al. 1998). They are rounded,
    /// order-of-magnitude-correct presets for a teaching simulator, not measurements of any one meteorite.
    /// </summary>
    public static class MeteorMaterialPresets
    {
        public const string OrdinaryChondriteId = "ordinary-chondrite";
        public const string CarbonaceousId = "carbonaceous-chondrite";
        public const string EnstatiteId = "enstatite-chondrite";
        public const string BasalticAchondriteId = "basaltic-achondrite";
        public const string StonyIronId = "stony-iron";
        public const string IronId = "iron";
        public const string CometaryId = "cometary";
        public const string RubblePileId = "rubble-pile";

        //                                                name                          rho     c     Q*      T_abl  eps   Lambda Y
        public static readonly MeteorMaterial OrdinaryChondrite = new MeteorMaterial("Stony (ordinary chondrite)", 3300.0, 750.0, 8.0e6, 2000.0, 0.90, 0.10, 1.0e6,
            new MaterialAppearance(0.45f, 0.42f, 0.38f, 0.66f, 0.62f, 0.54f, 0.12f, 0.80f, 0.65f, 0.15f));

        public static readonly MeteorMaterial Carbonaceous = new MeteorMaterial("Carbonaceous chondrite", 2200.0, 900.0, 5.0e6, 1700.0, 0.95, 0.10, 2.0e5,
            new MaterialAppearance(0.11f, 0.10f, 0.09f, 0.86f, 0.83f, 0.76f, 0.00f, 0.92f, 0.35f, 0.40f));

        public static readonly MeteorMaterial Enstatite = new MeteorMaterial("Enstatite chondrite", 3600.0, 700.0, 8.0e6, 2000.0, 0.88, 0.10, 2.0e6,
            new MaterialAppearance(0.31f, 0.32f, 0.33f, 0.72f, 0.70f, 0.64f, 0.25f, 0.70f, 0.55f, 0.10f));

        public static readonly MeteorMaterial BasalticAchondrite = new MeteorMaterial("Basaltic achondrite (HED)", 3000.0, 800.0, 8.0e6, 1900.0, 0.90, 0.10, 5.0e6,
            new MaterialAppearance(0.56f, 0.53f, 0.47f, 0.18f, 0.18f, 0.17f, 0.00f, 0.75f, 0.45f, 0.10f));

        public static readonly MeteorMaterial StonyIron = new MeteorMaterial("Stony-iron (pallasite)", 4800.0, 600.0, 7.0e6, 1900.0, 0.85, 0.10, 1.0e7,
            new MaterialAppearance(0.55f, 0.55f, 0.57f, 0.66f, 0.60f, 0.20f, 0.65f, 0.50f, 0.85f, 0.05f));

        public static readonly MeteorMaterial Iron = new MeteorMaterial("Iron (Fe-Ni)", 7800.0, 450.0, 6.0e6, 1811.0, 0.80, 0.10, 5.0e7,
            new MaterialAppearance(0.42f, 0.42f, 0.44f, 0.72f, 0.72f, 0.74f, 0.92f, 0.42f, 0.12f, 0.00f));

        public static readonly MeteorMaterial Cometary = new MeteorMaterial("Cometary (ice and dust)", 600.0, 1800.0, 2.5e6, 800.0, 0.90, 0.10, 1.0e4,
            new MaterialAppearance(0.17f, 0.16f, 0.16f, 0.78f, 0.84f, 0.90f, 0.00f, 0.70f, 0.40f, 0.70f, 0.65f));

        public static readonly MeteorMaterial RubblePile = new MeteorMaterial("Rubble pile (porous aggregate)", 1900.0, 750.0, 8.0e6, 2000.0, 0.90, 0.10, 1.0e3,
            new MaterialAppearance(0.40f, 0.37f, 0.33f, 0.56f, 0.51f, 0.44f, 0.05f, 0.90f, 0.30f, 0.95f));

        private static readonly (string Id, MeteorMaterial Material)[] all =
        {
            (OrdinaryChondriteId, OrdinaryChondrite),
            (CarbonaceousId, Carbonaceous),
            (EnstatiteId, Enstatite),
            (BasalticAchondriteId, BasalticAchondrite),
            (StonyIronId, StonyIron),
            (IronId, Iron),
            (CometaryId, Cometary),
            (RubblePileId, RubblePile),
        };

        /// <summary>All materials in display order.</summary>
        public static IReadOnlyList<(string Id, MeteorMaterial Material)> All => all;

        /// <summary>Material by id, or null when the id is unknown.</summary>
        public static MeteorMaterial ById(string id)
        {
            foreach ((string Id, MeteorMaterial Material) p in all)
            {
                if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return p.Material;
                }
            }

            return null;
        }

        /// <summary>Id of a built-in material, or null for a custom one.</summary>
        public static string IdOf(MeteorMaterial material)
        {
            foreach ((string Id, MeteorMaterial Material) p in all)
            {
                if (ReferenceEquals(p.Material, material)) return p.Id;
            }

            return null;
        }

        /// <summary>
        /// Blends materials by mass fraction (fractions are normalised; non-positive ones are ignored).
        /// Density combines by volume (1/rho = sum w_i/rho_i); specific heat, heat of ablation, ablation temperature,
        /// emissivity and Lambda by mass fraction; strength geometrically (weakest-link-ish behaviour of mixed rubble);
        /// appearance by mass fraction.
        /// </summary>
        public static MeteorMaterial Mix(IReadOnlyList<(MeteorMaterial Material, double Fraction)> parts, string name = null)
        {
            if (parts == null || parts.Count == 0)
            {
                throw new ArgumentException("At least one component is required.", nameof(parts));
            }

            double total = 0;
            foreach ((MeteorMaterial m, double f) in parts)
            {
                if (m == null)
                {
                    throw new ArgumentException("Null component.", nameof(parts));
                }

                if (f > 0 && !double.IsInfinity(f))
                {
                    total += f;
                }
            }

            if (!(total > 0))
            {
                throw new ArgumentException("Fractions must sum to a positive number.", nameof(parts));
            }

            double invRho = 0, c = 0, q = 0, t = 0, eps = 0, lambda = 0, logY = 0;
            var looks = new MaterialAppearance[parts.Count];
            var weights = new double[parts.Count];
            var label = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                (MeteorMaterial m, double f) = parts[i];
                looks[i] = m.Appearance;
                if (!(f > 0) || double.IsInfinity(f))
                {
                    continue;
                }

                double w = f / total;
                weights[i] = w;
                invRho += w / m.Density;
                c += w * m.SpecificHeat;
                q += w * m.HeatOfAblation;
                t += w * m.AblationTemperature;
                eps += w * m.Emissivity;
                lambda += w * m.HeatTransferCoefficient;
                logY += w * Math.Log(m.Strength);
                if (label.Length > 0)
                {
                    label.Append(" + ");
                }

                label.Append(Math.Round(w * 100.0)).Append("% ").Append(m.Name);
            }

            return new MeteorMaterial(name ?? "Mix: " + label, 1.0 / invRho, c, q, t, eps, lambda, Math.Exp(logY),
                MaterialAppearance.Blend(looks, weights));
        }
    }
}
