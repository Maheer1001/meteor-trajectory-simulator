using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MTS.Core
{
    /// <summary>One material in a composition, with its normalised mass fraction (0..1).</summary>
    public readonly struct CompositionPart
    {
        /// <summary>Built-in material id (see <see cref="MeteorMaterialPresets"/>), or null for a custom material.</summary>
        public readonly string Id;
        public readonly MeteorMaterial Material;
        public readonly double Fraction;

        public CompositionPart(string id, MeteorMaterial material, double fraction)
        {
            Id = id;
            Material = material;
            Fraction = fraction;
        }
    }

    /// <summary>
    /// What the meteoroid is made of: one to <see cref="MaxComponents"/> materials by mass fraction. The physics uses the
    /// blended <see cref="Material"/>; the visuals can also use the individual <see cref="Parts"/> (for example to texture
    /// iron veins through a stony matrix). Immutable.
    /// </summary>
    public sealed class MeteorComposition
    {
        public const int MaxComponents = 4;

        private readonly CompositionPart[] parts;

        /// <summary>Components sorted by decreasing fraction; fractions sum to 1.</summary>
        public IReadOnlyList<CompositionPart> Parts => parts;

        /// <summary>Blended bulk material (the single material itself for a one-component composition).</summary>
        public MeteorMaterial Material { get; }

        /// <summary>Mass-weighted blend of the components' looks.</summary>
        public MaterialAppearance Appearance => Material.Appearance;

        private MeteorComposition(CompositionPart[] parts)
        {
            this.parts = parts;
            if (parts.Length == 1)
            {
                Material = parts[0].Material;
            }
            else
            {
                var mix = new List<(MeteorMaterial, double)>(parts.Length);
                foreach (CompositionPart p in parts) mix.Add((p.Material, p.Fraction));
                Material = MeteorMaterialPresets.Mix(mix, Describe(parts));
            }
        }

        /// <summary>Default composition: a stony ordinary chondrite.</summary>
        public static MeteorComposition Default { get; } = Single(MeteorMaterialPresets.OrdinaryChondriteId);

        /// <summary>A single built-in material.</summary>
        public static MeteorComposition Single(string id) => Create(new[] { (id, 1.0) });

        /// <summary>
        /// Builds a composition from built-in material ids and relative amounts (any positive scale, e.g. percentages).
        /// Duplicate ids are merged; zero amounts are dropped. Throws for unknown ids, more than
        /// <see cref="MaxComponents"/> materials, or no positive amount.
        /// </summary>
        public static MeteorComposition Create(IEnumerable<(string Id, double Amount)> amounts)
        {
            if (amounts == null) throw new ArgumentNullException(nameof(amounts));
            var merged = new List<(string Id, MeteorMaterial Material, double Amount)>();
            foreach ((string id, double amount) in amounts)
            {
                if (!(amount > 0) || double.IsInfinity(amount)) continue;
                MeteorMaterial m = MeteorMaterialPresets.ById(id) ?? throw new ArgumentException("Unknown material id '" + id + "'.", nameof(amounts));
                int at = merged.FindIndex(e => e.Material == m);
                if (at >= 0) merged[at] = (merged[at].Id, m, merged[at].Amount + amount);
                else merged.Add((MeteorMaterialPresets.IdOf(m), m, amount));
            }

            return Build(merged);
        }

        /// <summary>Wraps any material (for example a custom one built from density/SHC/SCF) as a one-part composition.</summary>
        public static MeteorComposition FromMaterial(MeteorMaterial material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            return Build(new List<(string, MeteorMaterial, double)> { (MeteorMaterialPresets.IdOf(material), material, 1.0) });
        }

        private static MeteorComposition Build(List<(string Id, MeteorMaterial Material, double Amount)> merged)
        {
            if (merged.Count == 0) throw new ArgumentException("A composition needs at least one material with a positive amount.");
            if (merged.Count > MaxComponents) throw new ArgumentException("At most " + MaxComponents + " materials are allowed.");
            double total = 0;
            foreach (var e in merged) total += e.Amount;
            merged.Sort((a, b) => b.Amount.CompareTo(a.Amount));
            var parts = new CompositionPart[merged.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = new CompositionPart(merged[i].Id, merged[i].Material, merged[i].Amount / total);
            return new MeteorComposition(parts);
        }

        /// <summary>Short description, e.g. "70% Iron (Fe-Ni) + 30% Stony (ordinary chondrite)" or just the name.</summary>
        public string Describe() => Describe(parts);

        public override string ToString() => Describe();

        private static string Describe(CompositionPart[] parts)
        {
            if (parts.Length == 1) return parts[0].Material.Name;
            var sb = new StringBuilder();
            foreach (CompositionPart p in parts)
            {
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append(Math.Round(p.Fraction * 100.0).ToString(CultureInfo.InvariantCulture)).Append("% ").Append(p.Material.Name);
            }

            return sb.ToString();
        }
    }
}
