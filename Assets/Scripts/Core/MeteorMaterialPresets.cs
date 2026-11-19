using System;
using System.Collections.Generic;

namespace MTS.Core
{
    /// <summary>
    /// Built-in meteoroid compositions and a mixing rule for composition blends. Values are representative
    /// bulk properties from the meteoritics literature (densities: Britt and Consolmagno 2003; strengths: Popova
    /// et al. 2011 bulk/fireball strengths; heats of ablation and ablation temperatures as used in fireball
    /// modelling, e.g. Ceplecha and ReVelle 2005). They are rounded, order-of-magnitude-correct presets for a
    /// teaching simulator, not measurements of any one meteorite.
    /// </summary>
    public static class MeteorMaterialPresets
    {
        public const string OrdinaryChondriteId = "ordinary-chondrite";
        public const string CarbonaceousId = "carbonaceous-chondrite";
        public const string StonyIronId = "stony-iron";
        public const string IronId = "iron";

        public static readonly MeteorMaterial OrdinaryChondrite =
            new MeteorMaterial("Stony (ordinary chondrite)", 3300.0, 750.0, 8.0e6, 2000.0, 0.90, 0.10, 1.0e6);

        public static readonly MeteorMaterial Carbonaceous =
            new MeteorMaterial("Carbonaceous chondrite", 2200.0, 900.0, 5.0e6, 1700.0, 0.95, 0.10, 2.0e5);

        public static readonly MeteorMaterial StonyIron =
            new MeteorMaterial("Stony-iron (pallasite)", 4800.0, 600.0, 7.0e6, 1900.0, 0.85, 0.10, 1.0e7);

        public static readonly MeteorMaterial Iron =
            new MeteorMaterial("Iron (Fe-Ni)", 7800.0, 450.0, 6.0e6, 1811.0, 0.80, 0.10, 5.0e7);

        private static readonly (string Id, MeteorMaterial Material)[] all =
        {
            (OrdinaryChondriteId, OrdinaryChondrite),
            (CarbonaceousId, Carbonaceous),
            (StonyIronId, StonyIron),
            (IronId, Iron),
        };

        /// <summary>All presets in display order.</summary>
        public static IReadOnlyList<(string Id, MeteorMaterial Material)> All => all;

        /// <summary>Preset by id, or null when the id is unknown (null/empty means "custom").</summary>
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

        /// <summary>
        /// Blends materials by mass fraction (fractions are normalised; non-positive ones are ignored).
        /// Density combines by volume (1/rho = sum w_i/rho_i); specific heat, heat of ablation, ablation temperature
        /// and emissivity by mass fraction; strength geometrically (weakest-link-ish behaviour of mixed rubble).
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
            var label = new System.Text.StringBuilder();
            foreach ((MeteorMaterial m, double f) in parts)
            {
                if (!(f > 0) || double.IsInfinity(f))
                {
                    continue;
                }

                double w = f / total;
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

            return new MeteorMaterial(name ?? "Mix: " + label, 1.0 / invRho, c, q, t, eps, lambda, Math.Exp(logY));
        }
    }
}
