using System;
using System.Collections.Generic;
using MTS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Results
{
    /// <summary>
    /// Fills the Summary, Effects, Charts and Physics pages of the results screen. One type scale and grid for every
    /// tab (at the 1920 x 1080 reference): pixel-font headings 30 px, body text 22 px, labels and table text 20-22 px,
    /// 32 px card padding; headings and labels are left-aligned and values right-aligned on the card's right edge.
    /// </summary>
    public static class ResultsPages
    {
        /// <summary>Inner padding of every card.</summary>
        public const float Pad = 32f;
        /// <summary>Gap between cards.</summary>
        public const float Gap = 24f;
        public const float HeadingSize = 30f;
        public const float BodySize = 22f;
        public const float LabelSize = 20f;
        /// <summary>Top of a card's heading, and the first line under it.</summary>
        public const float HeadingTop = 24f;
        public const float BelowHeading = 72f;

        // ---------------------------------------------------------------- shared layout

        private static float PreferredHeight(TextMeshProUGUI t, float width)
        {
            t.ForceMeshUpdate();
            return t.GetPreferredValues(t.text, width, 0).y;
        }

        private static float PreferredWidth(TextMeshProUGUI t)
        {
            t.ForceMeshUpdate();
            return t.GetPreferredValues(t.text, 10000, 0).x;
        }

        /// <summary>A section heading at the top-left of a card (pixel font, 30 px).</summary>
        private static TextMeshProUGUI CardHeading(RectTransform card, string text, ResultsAssets a, Color? color = null)
        {
            TextMeshProUGUI h = ResultsUi.Heading(card, text, a, HeadingSize);
            if (color.HasValue) h.color = color.Value;
            ResultsUi.TopBand(h.rectTransform, HeadingTop, 36, Pad, Pad);
            return h;
        }

        /// <summary>Wrapped body text across a card at the given top; returns its height.</summary>
        private static float Paragraph(RectTransform card, string text, ResultsAssets a, float top, float size, Color color, float left = Pad, float right = Pad)
        {
            TextMeshProUGUI t = ResultsUi.Text(card, "Text", text, a.BodyFont, size, color);
            ResultsUi.TopBand(t.rectTransform, top, 10, left, right);
            Canvas.ForceUpdateCanvases();
            float h = Mathf.Ceil(PreferredHeight(t, Mathf.Max(50f, t.rectTransform.rect.width))) + 2f;
            ResultsUi.TopBand(t.rectTransform, top, h, left, right);
            return h;
        }

        /// <summary>
        /// One label / value row: label left (muted), value right-aligned on the card's right edge (bold), the value
        /// wrapping if it does not fit beside the label, and an optional note line under the label. Returns the height.
        /// </summary>
        private static float KeyValue(RectTransform card, float top, string label, string value, string note, ResultsAssets a,
            float left = Pad, float right = Pad, Color? valueColor = null, float labelSize = 21f)
        {
            Canvas.ForceUpdateCanvases();
            float width = card.rect.width - left - right;
            TextMeshProUGUI l = ResultsUi.Text(card, "Label", label, a.BodyFont, labelSize, ResultsTheme.Muted, TextAlignmentOptions.TopLeft);
            l.enableWordWrapping = false;
            float labelWidth = Mathf.Min(width * 0.62f, PreferredWidth(l) + 18f);
            Place(l.rectTransform, left, top, labelWidth, 30);

            TextMeshProUGUI v = ResultsUi.Text(card, "Value", value, a.BodyFont, BodySize, valueColor ?? ResultsTheme.Text, TextAlignmentOptions.TopRight, true);
            float valueWidth = Mathf.Max(80f, width - labelWidth);
            float vh = Mathf.Max(28f, PreferredHeight(v, valueWidth));
            Place(v.rectTransform, left + width - valueWidth, top, valueWidth, vh + 2);
            float h = Mathf.Max(30f, vh);
            if (!string.IsNullOrEmpty(note))
            {
                TextMeshProUGUI n = ResultsUi.Text(card, "Note", note, a.BodyFont, LabelSize, ResultsTheme.Muted * new Color(1, 1, 1, 0.8f), TextAlignmentOptions.TopLeft);
                float nh = PreferredHeight(n, width);
                Place(n.rectTransform, left, top + h, width, nh + 2);
                h += nh;
            }

            return h;
        }

        /// <summary>Top-left placement inside a parent, in pixels from its top-left corner.</summary>
        private static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform CardAt(RectTransform parent, string name, ResultsAssets a, float x, float y, float width, float height)
        {
            RectTransform card = ResultsUi.Card(parent, name, a);
            Place(card, x, y, width, height);
            return card;
        }

        /// <summary>Short upper-case label (column header) in the label colour.</summary>
        private static TextMeshProUGUI ColumnLabel(RectTransform parent, string text, ResultsAssets a, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            TextMeshProUGUI t = ResultsUi.Text(parent, "Column", text, a.BodyFont, LabelSize, ResultsTheme.Label, align, true);
            t.characterSpacing = 2;
            t.enableWordWrapping = false;
            return t;
        }

        // ---------------------------------------------------------------- Summary

        public static void BuildSummary(ResultsScreen screen, SimulationSession session)
        {
            OutcomeReport report = screen.Report;
            ResultsAssets a = screen.Assets;
            RectTransform left = screen.SummaryLeft;
            RectTransform right = screen.SummaryRight;
            ResultsUi.DestroyChildren(left);
            ResultsUi.DestroyChildren(right);
            Canvas.ForceUpdateCanvases();

            // Left card: the headline event, energy comparison and key numbers.
            string heading = report.Kind == OutcomeKind.Disintegrated ? "WHAT HAPPENED"
                : report.Kind == OutcomeKind.Airburst ? "ENERGY RELEASED IN THE AIR"
                : report.Kind == OutcomeKind.Timeout || report.Kind == OutcomeKind.Error || report.Kind == OutcomeKind.NoRun ? "STATUS"
                : "ENERGY RELEASED";
            CardHeading(left, heading, a);
            if (report.ShowGlobalBanner)
            {
                GlobalPill(left, report, a);
            }

            TextMeshProUGUI big = ResultsUi.Text(left, "Headline", report.Headline, a.BodyFont, 72, ResultsTheme.Accent, TextAlignmentOptions.TopLeft, true);
            ResultsUi.TopBand(big.rectTransform, 68, 84, Pad, Pad);
            big.enableAutoSizing = true;
            big.fontSizeMin = 40;
            big.fontSizeMax = 72;
            big.enableWordWrapping = false;

            float y = 156;
            y += Paragraph(left, report.Subtitle, a, y, BodySize, ResultsTheme.Text) + 6;
            string meteor = MeteorLine(report);
            if (meteor.Length > 0)
            {
                y += Paragraph(left, meteor, a, y, LabelSize, ResultsTheme.Muted) + 6;
            }

            y += 10;
            if (report.Energy > 0)
            {
                y = EnergyBars(left, report, a, y);
            }

            StatGrid(left, report.Stats, a, y);

            // Right card: what happened on the ground.
            if (report.HasRings || report.Kind == OutcomeKind.GroundImpact || report.Kind == OutcomeKind.MeteoriteFall ||
                report.Kind == OutcomeKind.Airburst)
            {
                RingTable(right, report, a);
            }
            else
            {
                NoGroundEffects(right, report, session, a);
            }
        }

        /// <summary>"Inbound entry  ·  70% Iron (Fe-Ni) + 30% Stony (ordinary chondrite)".</summary>
        private static string MeteorLine(OutcomeReport report)
        {
            string composition = report.CompositionText();
            if (report.ModeText.Length == 0 && composition.Length == 0) return "";
            return "<b><color=#FFFFFF>" + report.ModeText + "</color></b>" + (composition.Length > 0 ? "  ·  " + composition : "");
        }

        /// <summary>A red badge at the top right of a card naming the global scale ("MASS EXTINCTION").</summary>
        private static void GlobalPill(RectTransform card, OutcomeReport report, ResultsAssets a)
        {
            bool severe = report.Effects.Global >= GlobalScale.GlobalClimate;
            RectTransform pill = ResultsUi.PixelBox(card, "GlobalPill", a, severe ? ResultsTheme.Red : ResultsTheme.TabIdleFill,
                severe ? ResultsTheme.DarkRed : ResultsTheme.PanelOutline, false, 4f);
            TextMeshProUGUI t = ResultsUi.Text(pill, "Label", report.GlobalTitle, a.PixelFont, 24, Color.white, TextAlignmentOptions.Center);
            t.enableWordWrapping = false;
            float w = PreferredWidth(t) + 36f;
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(1, 1);
            pill.anchoredPosition = new Vector2(-Pad + 6, -HeadingTop + 6);
            pill.sizeDelta = new Vector2(w, 46);
            ResultsUi.Stretch(t.rectTransform, 12, 2, 12, 2);
        }

        /// <summary>Log-scale bars comparing the event with Hiroshima, Chelyabinsk, Tunguska, Meteor Crater and Chicxulub.</summary>
        private static float EnergyBars(RectTransform card, OutcomeReport report, ResultsAssets a, float top)
        {
            double kt = report.EnergyKilotons;
            var rows = new List<(string name, string value, double kt, bool self)>
            {
                ("This event", ResultsFormat.Tnt(report.Energy), kt, true),
                ("Hiroshima bomb (1945)", "15 kt", ResultsFormat.HiroshimaKilotons, false),
                ("Chelyabinsk (2013)", "~500 kt", ResultsFormat.ChelyabinskKilotons, false),
                ("Meteor Crater, Arizona", "~10 Mt", ResultsFormat.MeteorCraterKilotons, false),
                ("Tunguska (1908)", "10-15 Mt", ResultsFormat.TunguskaKilotons, false),
                ("Chicxulub (dinosaurs)", "~100 million Mt", ResultsFormat.ChicxulubKilotons, false),
            };
            rows.Sort((x, y) => x.self ? -1 : y.self ? 1 : x.kt.CompareTo(y.kt));

            double lo = Math.Min(1e-3, kt / 3.0);
            double hi = Math.Max(ResultsFormat.ChicxulubKilotons, kt) * 3.0;
            double span = Math.Log10(hi / lo);

            string caption = (report.Comparison.Length > 0 ? "<b><color=#FFFFFF>" + report.Comparison + "</color></b>   " : "") +
                             "TNT equivalent, log scale  ·  " + ResultsFormat.Joules(report.Energy, true);
            top += Paragraph(card, caption, a, top, LabelSize, ResultsTheme.Muted) + 8;

            const float rowHeight = 32f;
            const float labelWidth = 268f;
            foreach (var row in rows)
            {
                TextMeshProUGUI name = ResultsUi.Text(card, "BarName", row.name, a.BodyFont, LabelSize,
                    row.self ? ResultsTheme.Text : ResultsTheme.Muted, TextAlignmentOptions.Left, row.self);
                name.enableWordWrapping = false;
                Place(name.rectTransform, Pad, top, labelWidth, rowHeight - 6);

                RectTransform track = ResultsUi.Node("Track", card);
                track.anchorMin = new Vector2(0, 1);
                track.anchorMax = new Vector2(1, 1);
                track.pivot = new Vector2(0, 1);
                track.offsetMin = new Vector2(Pad + labelWidth, -top - (rowHeight - 6));
                track.offsetMax = new Vector2(-Pad, -top);

                float f = (float)Math.Max(0.03, Math.Min(1.0, Math.Log10(Math.Max(row.kt, lo) / lo) / span));
                Color c = row.self ? ResultsTheme.Red : new Color(0.55f, 0.5f, 0.62f, 1f);
                RectTransform bar = ResultsUi.PixelBox(track, "Bar", a, c, row.self ? ResultsTheme.DarkRed : new Color(0.3f, 0.27f, 0.36f), false, 6f, 20f);
                bar.anchorMin = new Vector2(0, 0);
                bar.anchorMax = new Vector2(f * 0.7f, 1);
                bar.offsetMin = Vector2.zero;
                bar.offsetMax = Vector2.zero;

                TextMeshProUGUI value = ResultsUi.Text(track, "Value", row.value, a.BodyFont, LabelSize,
                    row.self ? ResultsTheme.Accent : ResultsTheme.Muted, TextAlignmentOptions.Left, row.self);
                value.enableWordWrapping = false;
                value.rectTransform.anchorMin = new Vector2(f * 0.7f, 0);
                value.rectTransform.anchorMax = new Vector2(1, 1);
                value.rectTransform.offsetMin = new Vector2(10, 0);
                value.rectTransform.offsetMax = Vector2.zero;
                top += rowHeight;
            }

            return top + 16;
        }

        private static void StatGrid(RectTransform card, List<ReportStat> stats, ResultsAssets a, float top)
        {
            RectTransform grid = ResultsUi.Node("Stats", card);
            grid.anchorMin = new Vector2(0, 0);
            grid.anchorMax = new Vector2(1, 1);
            grid.offsetMin = new Vector2(Pad, Pad - 8);
            grid.offsetMax = new Vector2(-Pad, -top);
            Canvas.ForceUpdateCanvases();
            Vector2 size = grid.rect.size;
            int columns = 4;
            int rows = Math.Max(1, (stats.Count + columns - 1) / columns);
            float gap = 12f;
            float w = (size.x - gap * (columns - 1)) / columns;
            float hgt = Math.Min(150f, (size.y - gap * (rows - 1)) / rows);
            for (int i = 0; i < stats.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                RectTransform tile = ResultsUi.PixelBox(grid, "Stat", a, new Color(1f, 1f, 1f, 0.045f), new Color(1f, 1f, 1f, 0.12f), false);
                ResultsUi.Place(tile, new Vector2(0, 1), new Vector2(col * (w + gap), -row * (hgt + gap)), new Vector2(w, hgt));
                ReportStat s = stats[i];
                TextMeshProUGUI label = ResultsUi.Text(tile, "Label", s.Label.ToUpperInvariant(), a.BodyFont, LabelSize, ResultsTheme.Label, TextAlignmentOptions.TopLeft, true);
                label.characterSpacing = 1;
                label.enableWordWrapping = false;
                ResultsUi.TopBand(label.rectTransform, 12, 26, 16, 12);
                TextMeshProUGUI value = ResultsUi.Text(tile, "Value", s.Value, a.BodyFont, 30, ResultsTheme.Text, TextAlignmentOptions.TopLeft, true);
                value.enableAutoSizing = true;
                value.fontSizeMin = 22;
                value.fontSizeMax = 30;
                value.enableWordWrapping = false;
                ResultsUi.TopBand(value.rectTransform, 40, 38, 16, 12);
                if (s.Note.Length > 0)
                {
                    TextMeshProUGUI note = ResultsUi.Text(tile, "Note", s.Note, a.BodyFont, LabelSize, ResultsTheme.Muted);
                    note.lineSpacing = -8;
                    ResultsUi.TopBand(note.rectTransform, 80, Math.Max(24, hgt - 86), 16, 12);
                }
            }
        }

        private static void RingTable(RectTransform card, OutcomeReport report, ResultsAssets a)
        {
            CardHeading(card, "BLAST ON THE GROUND", a);
            float top = BelowHeading;
            top += Paragraph(card, report.GroundEffects, a, top, BodySize, ResultsTheme.Text) + 14;

            TextMeshProUGUI c1 = ColumnLabel(card, "PEAK OVERPRESSURE", a);
            ResultsUi.TopBand(c1.rectTransform, top, 26, Pad + 44, Pad);
            TextMeshProUGUI c2 = ColumnLabel(card, "REACHES OUT TO", a, TextAlignmentOptions.TopRight);
            ResultsUi.TopBand(c2.rectTransform, top, 26, Pad, Pad);
            top += 32;

            Canvas.ForceUpdateCanvases();
            float available = card.rect.height - top - 20;
            int count = Math.Max(1, report.Rings.Count);
            float rowHeight = Mathf.Clamp(available / count, 52f, 70f);
            for (int i = 0; i < report.Rings.Count; i++)
            {
                RingRow r = report.Rings[i];
                float alpha = r.Reached ? 1f : 0.4f;
                if (i % 2 == 0)
                {
                    Image stripe = ResultsUi.Image(card, "Stripe", null, new Color(1, 1, 1, 0.035f), false);
                    ResultsUi.TopBand(stripe.rectTransform, top, rowHeight, Pad - 12, Pad - 12);
                }

                Color col = ResultsTheme.Rings[r.Index % ResultsTheme.Rings.Length];
                RectTransform sw = ResultsUi.PixelBox(card, "Swatch", a, col * new Color(1, 1, 1, 0.6f * alpha), col * new Color(1, 1, 1, alpha), false, 6f, ResultsUi.SmallBoxPixelScale);
                ResultsUi.Place(sw, new Vector2(0, 1), new Vector2(Pad, -top - (rowHeight - 26) / 2), new Vector2(26, 26));

                float textTop = top + (rowHeight - 50) / 2;
                TextMeshProUGUI label = ResultsUi.Text(card, "Pressure", "<b>" + ResultsFormat.Psi(r.Overpressure) + "</b>  <color=#B8B8CC>" +
                    ResultsFormat.Pressure(r.Overpressure) + "</color>", a.BodyFont, BodySize, new Color(1, 1, 1, alpha), TextAlignmentOptions.TopLeft);
                label.enableWordWrapping = false;
                ResultsUi.TopBand(label.rectTransform, textTop - 2, 28, Pad + 44, 220);
                TextMeshProUGUI effect = ResultsUi.Text(card, "Effect", r.Effect, a.BodyFont, LabelSize, ResultsTheme.Muted * new Color(1, 1, 1, alpha), TextAlignmentOptions.TopLeft);
                effect.enableWordWrapping = false;
                ResultsUi.TopBand(effect.rectTransform, textTop + 26, 26, Pad + 44, 210);

                TextMeshProUGUI range = ResultsUi.Text(card, "Range", r.Reached ? ResultsFormat.Reach(r.Radius) : "not reached", a.BodyFont,
                    r.Reached ? 24 : LabelSize, r.Reached ? ResultsTheme.Accent : ResultsTheme.Muted * new Color(1, 1, 1, 0.7f), TextAlignmentOptions.Right, r.Reached);
                range.enableWordWrapping = false;
                ResultsUi.TopBand(range.rectTransform, top, rowHeight, Pad, Pad);
                top += rowHeight;
            }
        }

        private static void NoGroundEffects(RectTransform card, OutcomeReport report, SimulationSession session, ResultsAssets a)
        {
            CardHeading(card, "ON THE GROUND", a);
            float top = BelowHeading;
            top += Paragraph(card, report.GroundEffects, a, top, BodySize, ResultsTheme.Text) + 12;

            RectTransform chartRoot = ResultsUi.Node("Chart", card);
            ResultsUi.Stretch(chartRoot, 8, 12, 12, top);
            ChartSpec spec = report.Kind == OutcomeKind.Disintegrated
                ? MassAltitudeChart(session.Result, "How it burned up")
                : AltitudeTimeChart(session.Result, "Altitude");
            if (spec != null)
            {
                chartRoot.gameObject.AddComponent<ChartView>().Plot(spec, a);
            }
        }

        // ---------------------------------------------------------------- Effects

        public static void BuildEffects(ResultsScreen screen)
        {
            RectTransform root = screen.EffectsRoot;
            if (root == null) return;
            OutcomeReport report = screen.Report;
            ResultsAssets a = screen.Assets;
            ResultsUi.DestroyChildren(root);
            Canvas.ForceUpdateCanvases();
            float width = root.rect.width;
            float height = root.rect.height;
            float y = 0;

            if (report.ShowGlobalBanner)
            {
                y += GlobalBanner(root, report, a, width) + Gap;
            }

            bool table = report.DistanceRows.Count > 0;
            float cardHeight = table ? Mathf.Min(372f, height - y - 300f) : height - y;
            int n = report.EffectCards.Count;
            float cardWidth = (width - Gap * (n - 1)) / Math.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                RectTransform card = CardAt(root, "EffectCard", a, i * (cardWidth + Gap), y, cardWidth, cardHeight);
                EffectCardContent(card, report.EffectCards[i], a);
            }

            y += cardHeight + Gap;
            if (table)
            {
                RectTransform card = CardAt(root, "DistanceCard", a, 0, y, width, height - y);
                DistanceTable(card, report, a);
            }
        }

        private static float GlobalBanner(RectTransform root, OutcomeReport report, ResultsAssets a, float width)
        {
            bool severe = report.Effects.Global >= GlobalScale.GlobalClimate;
            const float height = 112f;
            RectTransform banner = ResultsUi.PixelBox(root, "GlobalBanner", a,
                severe ? new Color(0.45f, 0.02f, 0.03f, 0.95f) : ResultsTheme.PanelFill, severe ? ResultsTheme.Red : ResultsTheme.PanelOutline, true, 5f);
            Place(banner, 0, 0, width, height);
            TextMeshProUGUI title = ResultsUi.Text(banner, "Title", report.GlobalTitle, a.PixelFont, 40, severe ? Color.white : ResultsTheme.Label, TextAlignmentOptions.Left);
            title.enableWordWrapping = false;
            float tw = Mathf.Min(560f, PreferredWidth(title) + 12f);
            Place(title.rectTransform, Pad, 0, tw, height);
            title.rectTransform.anchorMin = new Vector2(0, 0);
            title.rectTransform.anchorMax = new Vector2(0, 1);
            title.rectTransform.offsetMin = new Vector2(Pad, 0);
            title.rectTransform.offsetMax = new Vector2(Pad + tw, 0);

            string text = report.GlobalText + "  <color=#B8B8CC>(" + ResultsFormat.Tnt(report.Energy) + ")</color>";
            TextMeshProUGUI body = ResultsUi.Text(banner, "Text", text, a.BodyFont, BodySize, Color.white, TextAlignmentOptions.Left);
            ResultsUi.Stretch(body.rectTransform, Pad + tw + 24, 8, Pad, 8);
            return height;
        }

        private static void EffectCardContent(RectTransform card, EffectCard e, ResultsAssets a)
        {
            Color accent = ResultsTheme.GroupColor(e.Group);
            Image stripe = ResultsUi.Image(card, "Accent", null, e.Applies ? accent : accent * new Color(1, 1, 1, 0.35f), false);
            Place(stripe.rectTransform, Pad, HeadingTop + 42, 56, 4);
            CardHeading(card, e.Title, a, e.Applies ? accent : ResultsTheme.Muted);
            float y = BelowHeading + 10;
            y += Paragraph(card, e.Summary, a, y, BodySize, e.Applies ? ResultsTheme.Text : ResultsTheme.Muted) + 14;
            foreach (ReportStat row in e.Rows)
            {
                y += KeyValue(card, y, row.Label, row.Value, row.Note, a, Pad, Pad, ResultsTheme.Accent, LabelSize) + 8;
            }
        }

        private static void DistanceTable(RectTransform card, OutcomeReport report, ResultsAssets a)
        {
            CardHeading(card, "AT A DISTANCE FROM THE IMPACT", a);
            Canvas.ForceUpdateCanvases();
            float width = card.rect.width - 2 * Pad;
            bool ocean = report.Effects.HasTsunami;
            // Column layout (fractions of the inner width): distance, shaking, heat, debris, wave (ocean), arrival.
            var cols = new List<(string title, float w)>
            {
                ("DISTANCE", 0.11f),
                ("SHAKING (MERCALLI)", ocean ? 0.29f : 0.33f),
                ("HEAT", 0.16f),
                ("DEBRIS", ocean ? 0.16f : 0.18f),
            };
            if (ocean) cols.Add(("WAVE", 0.1f));
            cols.Add(("ARRIVES AFTER", ocean ? 0.18f : 0.22f));

            float top = BelowHeading - 4;
            float x = Pad;
            foreach (var c in cols)
            {
                TextMeshProUGUI h = ColumnLabel(card, c.title, a);
                Place(h.rectTransform, x, top, c.w * width - 12, 26);
                x += c.w * width;
            }

            top += 32;
            float available = card.rect.height - top - 16;
            float rowHeight = Mathf.Clamp(available / Math.Max(1, report.DistanceRows.Count), 34f, 46f);
            for (int i = 0; i < report.DistanceRows.Count; i++)
            {
                DistanceRow r = report.DistanceRows[i];
                if (i % 2 == 0)
                {
                    Image stripe = ResultsUi.Image(card, "Stripe", null, new Color(1, 1, 1, 0.035f), false);
                    ResultsUi.TopBand(stripe.rectTransform, top, rowHeight, Pad - 12, Pad - 12);
                }

                var cells = new List<string>
                {
                    "<b>" + ResultsFormat.Distance(r.Distance) + "</b>",
                    r.Intensity == "-" ? r.Shaking : "<b><color=#FFCC47>" + r.Intensity + "</color></b>  " + r.Shaking,
                    r.Heat,
                    r.Debris,
                };
                if (ocean) cells.Add(r.Wave);
                cells.Add(r.Arrival);
                x = Pad;
                for (int c = 0; c < cols.Count; c++)
                {
                    TextMeshProUGUI t = ResultsUi.Text(card, "Cell", cells[c], a.BodyFont, c == 0 ? BodySize : LabelSize,
                        c == 0 ? ResultsTheme.Text : new Color(0.9f, 0.9f, 0.95f), TextAlignmentOptions.Left);
                    t.enableWordWrapping = false;
                    Place(t.rectTransform, x, top, cols[c].w * width - 12, rowHeight);
                    x += cols[c].w * width;
                }

                top += rowHeight;
            }
        }

        // ---------------------------------------------------------------- Charts

        public static void BuildCharts(ResultsScreen screen, SimulationSession session)
        {
            SimulationResult result = session.Result;
            ChartSpec[] specs =
            {
                AltitudeTimeChart(result, "Altitude vs time"),
                SpeedAltitudeChart(result),
                MassAltitudeChart(result, "Mass vs altitude"),
                TemperatureTimeChart(result),
            };

            for (int i = 0; i < screen.ChartSlots.Length && i < specs.Length; i++)
            {
                RectTransform slot = screen.ChartSlots[i];
                if (slot == null) continue;
                ResultsUi.DestroyChildren(slot);
                if (specs[i] == null)
                {
                    TextMeshProUGUI t = ResultsUi.Text(slot, "Empty", "No data", screen.Assets.BodyFont, 22, ResultsTheme.Muted, TextAlignmentOptions.Center);
                    ResultsUi.Stretch(t.rectTransform);
                    continue;
                }

                RectTransform root = ResultsUi.Stretch(ResultsUi.Node("Chart", slot));
                root.gameObject.AddComponent<ChartView>().Plot(specs[i], screen.Assets);
            }
        }

        /// <summary>The atmospheric part of the flight (falls back to the whole series).</summary>
        private static TimeSeries FlightSeries(SimulationResult result)
        {
            TimeSeries s = result?.Series;
            if (s == null) return null;
            if (s.Entry != null && s.Entry.Count >= 2) return s.Entry;
            return s.Count >= 2 ? s : null;
        }

        private static double[] Map(List<double> values, Func<double, double> f)
        {
            var r = new double[values.Count];
            for (int i = 0; i < r.Length; i++) r[i] = f(values[i]);
            return r;
        }

        private static string EndLabel(SimulationResult result)
        {
            switch (result.Reason)
            {
                case EndReason.Impact: return "Impact";
                case EndReason.Airburst: return "Airburst";
                case EndReason.Disintegrated: return "Burned up";
                case EndReason.Timeout: return "Time limit";
                default: return "Stopped";
            }
        }

        private static void AddBreakup(ChartSpec spec, SimulationResult result)
        {
            if (!double.IsNaN(result.BreakupAltitude) && result.BreakupAltitude > 0)
            {
                spec.HorizontalLines.Add(new ChartMarker(0, result.BreakupAltitude / 1000.0,
                    "break-up at " + ResultsFormat.Distance(result.BreakupAltitude)));
            }
        }

        public static ChartSpec AltitudeTimeChart(SimulationResult result, string title)
        {
            TimeSeries s = FlightSeries(result);
            if (s == null) return null;
            double t0 = s.Time[0];
            var spec = new ChartSpec
            {
                Title = title,
                Subtitle = s == result.Series ? "whole flight" : "from the 120 km entry interface",
                XLabel = "Time since entry (s)",
                YLabel = "Altitude (km)",
                X = Map(s.Time, t => t - t0),
                Y = Map(s.Altitude, h => Math.Max(0.0, h) / 1000.0),
                Color = ResultsTheme.Series[0],
                XIncludeZero = true,
                YIncludeZero = true,
            };
            if (s == result.Series)
            {
                spec.XLabel = "Time (s)";
            }

            AddBreakup(spec, result);
            int last = s.Count - 1;
            spec.Points.Add(new ChartMarker(spec.X[last], spec.Y[last], EndLabel(result)));
            return spec;
        }

        public static ChartSpec SpeedAltitudeChart(SimulationResult result)
        {
            TimeSeries s = FlightSeries(result);
            if (s == null) return null;
            var spec = new ChartSpec
            {
                Title = "Speed vs altitude",
                Subtitle = "drag slows it in the dense lower air",
                XLabel = "Speed (km/s)",
                YLabel = "Altitude (km)",
                X = Map(s.Speed, v => v / 1000.0),
                Y = Map(s.Altitude, h => Math.Max(0.0, h) / 1000.0),
                Color = ResultsTheme.Series[1],
                XIncludeZero = true,
                YIncludeZero = true,
            };
            AddBreakup(spec, result);
            int last = s.Count - 1;
            spec.Points.Add(new ChartMarker(spec.X[last], spec.Y[last], EndLabel(result) + " at " + ResultsFormat.Speed(s.Speed[last])));
            return spec;
        }

        public static ChartSpec MassAltitudeChart(SimulationResult result, string title)
        {
            TimeSeries s = FlightSeries(result);
            if (s == null) return null;
            double maxMass = 0;
            foreach (double m in s.Mass) maxMass = Math.Max(maxMass, m);
            MassUnit(maxMass, out double unit, out string unitLabel);
            var spec = new ChartSpec
            {
                Title = title,
                Subtitle = "ablation strips mass from the hot surface",
                XLabel = "Mass (" + unitLabel + ")",
                YLabel = "Altitude (km)",
                X = Map(s.Mass, m => m / unit),
                Y = Map(s.Altitude, h => Math.Max(0.0, h) / 1000.0),
                Color = ResultsTheme.Series[2],
                XIncludeZero = true,
                YIncludeZero = true,
            };
            AddBreakup(spec, result);
            int last = s.Count - 1;
            spec.Points.Add(new ChartMarker(spec.X[last], spec.Y[last], ResultsFormat.Mass(s.Mass[last]) + " left"));
            return spec;
        }

        /// <summary>
        /// Axis unit for masses from grams to planet-killers: kg below 10 t, tonnes up to 100,000 t, then a power of ten
        /// of tonnes ("10<sup>9</sup> t") so tick labels stay short.
        /// </summary>
        public static void MassUnit(double maxMass, out double unit, out string label)
        {
            if (maxMass < 1e4)
            {
                unit = 1.0;
                label = "kg";
            }
            else if (maxMass < 1e8)
            {
                unit = 1e3;
                label = "t";
            }
            else
            {
                int exponent = (int)Math.Floor(Math.Log10(maxMass / 1e3));
                unit = 1e3 * Math.Pow(10, exponent);
                label = "10<sup>" + exponent + "</sup> t";
            }
        }

        public static ChartSpec TemperatureTimeChart(SimulationResult result)
        {
            TimeSeries s = FlightSeries(result);
            if (s == null) return null;
            double t0 = s.Time[0];
            var spec = new ChartSpec
            {
                Title = "Surface temperature vs time",
                Subtitle = "1 cm surface layer",
                XLabel = s == result.Series ? "Time (s)" : "Time since entry (s)",
                YLabel = "Temperature (K)",
                X = Map(s.Time, t => t - t0),
                Y = Map(s.Temperature, k => k),
                Color = ResultsTheme.Series[3],
                XIncludeZero = true,
                YIncludeZero = true,
            };
            int peak = 0;
            for (int i = 1; i < s.Count; i++)
            {
                if (s.Temperature[i] > s.Temperature[peak]) peak = i;
            }

            spec.Points.Add(new ChartMarker(spec.X[peak], spec.Y[peak], "peak " + ResultsFormat.Temperature(s.Temperature[peak])));
            return spec;
        }

        // ---------------------------------------------------------------- Physics details

        /// <summary>Section cards packed top to bottom into as many columns as needed (three at 16:9).</summary>
        public static void BuildPhysics(ResultsScreen screen)
        {
            OutcomeReport report = screen.Report;
            RectTransform root = screen.PhysicsRoot;
            ResultsUi.DestroyChildren(root);
            Canvas.ForceUpdateCanvases();
            if (report.Details.Count == 0)
            {
                TextMeshProUGUI t = ResultsUi.Text(root, "Empty", "No finished run.", screen.Assets.BodyFont, 24, ResultsTheme.Muted, TextAlignmentOptions.Center);
                ResultsUi.Stretch(t.rectTransform);
                return;
            }

            const int columns = 3;
            float width = root.rect.width;
            float height = root.rect.height;
            float columnWidth = (width - Gap * (columns - 1)) / columns;
            int column = 0;
            float top = 0;
            foreach (ReportSection section in report.Details)
            {
                RectTransform card = CardAt(root, "Section", screen.Assets, column * (columnWidth + Gap), top, columnWidth, 100);
                float h = Section(card, section, screen.Assets);
                if (top > 0 && top + h > height && column < columns - 1)
                {
                    column++;
                    top = 0;
                    Place(card, column * (columnWidth + Gap), top, columnWidth, h);
                }
                else
                {
                    card.sizeDelta = new Vector2(columnWidth, h);
                }

                top += h + Gap;
            }
        }

        /// <summary>Fills a section card (heading, then label/value rows) and returns the card height it needs.</summary>
        private static float Section(RectTransform card, ReportSection section, ResultsAssets a)
        {
            CardHeading(card, section.Title.ToUpperInvariant(), a);
            float y = BelowHeading - 6;
            for (int i = 0; i < section.Rows.Count; i++)
            {
                ReportStat row = section.Rows[i];
                y += KeyValue(card, y, row.Label, row.Value, row.Note, a) + 8;
            }

            return y + Pad - 14;
        }
    }
}
