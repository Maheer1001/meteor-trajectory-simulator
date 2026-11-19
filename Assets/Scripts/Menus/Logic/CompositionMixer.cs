using System;
using System.Collections.Generic;
using MTS.Core;

namespace MTS.Menus
{
    /// <summary>
    /// Editable state behind the input screen's composition mixer: one to <see cref="MaxRows"/> rows, each a built-in
    /// material and a relative amount (0..100, the slider value). Shares are the amounts normalised to 100%.
    /// Rows never repeat a material (the picker skips materials used by other rows).
    /// </summary>
    public sealed class CompositionMixer
    {
        public const int MaxRows = MeteorComposition.MaxComponents;
        public const double MaxAmount = 100.0;

        public sealed class Row
        {
            public string MaterialId;
            public double Amount;

            public Row(string materialId, double amount)
            {
                MaterialId = materialId;
                Amount = amount;
            }

            public MeteorMaterial Material => MeteorMaterialPresets.ById(MaterialId);
        }

        private readonly List<Row> rows = new List<Row>();

        public IReadOnlyList<Row> Rows => rows;
        public int Count => rows.Count;
        public bool CanAdd => rows.Count < MaxRows;
        public bool CanRemove => rows.Count > 1;
        public double Total
        {
            get
            {
                double t = 0;
                foreach (Row r in rows) t += r.Amount;
                return t;
            }
        }

        /// <summary>True when at least one row has a positive amount.</summary>
        public bool IsValid => Total > 0;

        /// <summary>100% stony (ordinary chondrite).</summary>
        public CompositionMixer()
        {
            rows.Add(new Row(MeteorMaterialPresets.OrdinaryChondriteId, MaxAmount));
        }

        /// <summary>Rows from an existing composition (largest part first; amounts are its percentages).
        /// Parts without a built-in id (custom materials) cannot be edited, so they fall back to the default.</summary>
        public static CompositionMixer From(MeteorComposition composition)
        {
            var mixer = new CompositionMixer();
            if (composition == null) return mixer;
            var loaded = new List<Row>();
            foreach (CompositionPart part in composition.Parts)
            {
                if (part.Id == null || MeteorMaterialPresets.ById(part.Id) == null) return mixer;
                loaded.Add(new Row(part.Id, part.Fraction * MaxAmount));
            }

            if (loaded.Count == 0 || loaded.Count > MaxRows) return mixer;
            mixer.rows.Clear();
            mixer.rows.AddRange(loaded);
            return mixer;
        }

        /// <summary>Adds a row with the first unused material and an equal share; returns its index or -1 when full.</summary>
        public int Add()
        {
            if (!CanAdd) return -1;
            double share = rows.Count == 0 ? MaxAmount : Math.Min(MaxAmount, Math.Max(1.0, Total / rows.Count));
            rows.Add(new Row(FirstUnused(-1), share));
            return rows.Count - 1;
        }

        public bool Remove(int index)
        {
            if (!CanRemove || index < 0 || index >= rows.Count) return false;
            rows.RemoveAt(index);
            return true;
        }

        public void SetAmount(int index, double amount)
        {
            if (index < 0 || index >= rows.Count) return;
            rows[index].Amount = double.IsNaN(amount) ? 0 : Math.Max(0, Math.Min(MaxAmount, amount));
        }

        /// <summary>Sets a row's material; if another row already uses it, the two rows swap materials.</summary>
        public void SetMaterial(int index, string materialId)
        {
            if (index < 0 || index >= rows.Count || MeteorMaterialPresets.ById(materialId) == null) return;
            int other = IndexOf(materialId);
            if (other >= 0 && other != index) rows[other].MaterialId = rows[index].MaterialId;
            rows[index].MaterialId = MeteorMaterialPresets.IdOf(MeteorMaterialPresets.ById(materialId));
        }

        /// <summary>Steps a row's material through the library (direction +1 / -1), skipping materials used elsewhere.</summary>
        public void Cycle(int index, int direction)
        {
            if (index < 0 || index >= rows.Count || direction == 0) return;
            IReadOnlyList<(string Id, MeteorMaterial Material)> all = MeteorMaterialPresets.All;
            int at = LibraryIndex(rows[index].MaterialId);
            int step = direction > 0 ? 1 : -1;
            for (int n = 1; n <= all.Count; n++)
            {
                int candidate = ((at + step * n) % all.Count + all.Count) % all.Count;
                string id = all[candidate].Id;
                int user = IndexOf(id);
                if (user < 0 || user == index)
                {
                    rows[index].MaterialId = id;
                    return;
                }
            }
        }

        /// <summary>Normalised share of a row (0..1); 0 for every row when nothing has a positive amount.</summary>
        public double Share(int index)
        {
            double total = Total;
            return total > 0 && index >= 0 && index < rows.Count ? rows[index].Amount / total : 0;
        }

        /// <summary>Whole-number percentages that always add up to exactly 100 (largest remainder rounding).</summary>
        public int[] DisplayPercents()
        {
            var result = new int[rows.Count];
            double total = Total;
            if (!(total > 0)) return result;
            var remainders = new List<(double Remainder, int Index)>();
            int sum = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                double exact = rows[i].Amount / total * 100.0;
                result[i] = (int)Math.Floor(exact);
                sum += result[i];
                remainders.Add((exact - result[i], i));
            }

            remainders.Sort((a, b) => b.Remainder != a.Remainder ? b.Remainder.CompareTo(a.Remainder) : a.Index.CompareTo(b.Index));
            for (int k = 0; sum < 100 && k < remainders.Count; k++, sum++) result[remainders[k].Index]++;
            return result;
        }

        /// <summary>The composition these rows describe, or null when no row has a positive amount.</summary>
        public MeteorComposition Build()
        {
            if (!IsValid) return null;
            var amounts = new List<(string, double)>(rows.Count);
            foreach (Row r in rows) amounts.Add((r.MaterialId, r.Amount));
            return MeteorComposition.Create(amounts);
        }

        private int IndexOf(string materialId)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].MaterialId, materialId, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }

        private static int LibraryIndex(string id)
        {
            IReadOnlyList<(string Id, MeteorMaterial Material)> all = MeteorMaterialPresets.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (string.Equals(all[i].Id, id, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return 0;
        }

        private string FirstUnused(int except)
        {
            foreach ((string id, MeteorMaterial _) in MeteorMaterialPresets.All)
            {
                int user = IndexOf(id);
                if (user < 0 || user == except) return id;
            }

            return MeteorMaterialPresets.OrdinaryChondriteId;
        }
    }
}
