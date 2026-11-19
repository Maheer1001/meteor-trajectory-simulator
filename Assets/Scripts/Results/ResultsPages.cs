using System;
using System.Collections.Generic;
using MTS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Results
{
    /// <summary>Fills the Summary, Charts and Physics pages of the results screen.</summary>
    public static class ResultsPages
    {
        // ---------------------------------------------------------------- Summary

        public static void BuildSummary(ResultsScreen screen, SimulationSession session)
        {
            OutcomeReport report = screen.Report;
            ResultsAssets a = screen.Assets;
            RectTransform left = screen.SummaryLeft;
            RectTransform right = screen.SummaryRight;
            ResultsUi.DestroyChildren(left);
            ResultsUi.DestroyChildren(right);

            // Left card: the headline event, energy comparison and key numbers.
            string heading = report.Kind == OutcomeKind.Disintegrated ? "WHAT HAPPENED"
                : report.Kind == OutcomeKind.Airburst ? "ENERGY RELEASED IN THE AIR"
                : report.Kind == OutcomeKind.Timeout || report.Kind == OutcomeKind.Error || report.Kind == OutcomeKind.NoRun ? "STATUS"
                : "ENERGY RELEASED";
            TextMeshProUGUI h = ResultsUi.Heading(left, heading, a, 28);
            ResultsUi.TopBand(h.rectTransform, 26, 34, 34, 34);

            TextMeshProUGUI big = ResultsUi.Text(left, "Headline", report.Headline, a.BodyFont, 76, ResultsTheme.Accent, TextAlignmentOptions.TopLeft, true);
            ResultsUi.TopBand(big.rectTransform, 64, 92, 34, 34);
            big.enableAutoSizing = true;
            big.fontSizeMin = 40;
            big.fontSizeMax = 76;
            big.enableWordWrapping = false;

            TextMeshProUGUI sub = ResultsUi.Text(left, "Subtitle", report.Subtitle, a.BodyFont, 23, ResultsTheme.Text);
            ResultsUi.TopBand(sub.rectTransform, 160, 64, 34, 34);

            float y = 236;
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

        /// <summary>Log-scale bars comparing the event with Hiroshima, Chelyabinsk and Tunguska.</summary>
        private static float EnergyBars(RectTransform card, OutcomeReport report, ResultsAssets a, float top)
        {
            double kt = report.EnergyKilotons;
            var rows = new List<(string name, string value, double kt, bool self)>
            {
                ("This event", ResultsFormat.Tnt(report.Energy), kt, true),
                ("Hiroshima bomb (1945)", "15 kt", ResultsFormat.HiroshimaKilotons, false),
                ("Chelyabinsk meteor (2013)", "~500 kt", ResultsFormat.ChelyabinskKilotons, false),
                ("Tunguska event (1908)", "10-15 Mt", ResultsFormat.TunguskaKilotons, false),
            };

            double lo = Math.Min(1e-3, kt / 3.0);
            double hi = Math.Max(ResultsFormat.TunguskaKilotons, kt) * 2.0;
            double span = Math.Log10(hi / lo);

            TextMeshProUGUI caption = ResultsUi.Text(card, "BarsCaption", report.Comparison.Length > 0
                    ? "<b><color=#FFFFFF>" + report.Comparison + "</color></b>   <size=18>TNT equivalent, log scale</size>"
                    : "TNT equivalent, log scale",
                a.BodyFont, 22, ResultsTheme.Muted);
            ResultsUi.TopBand(caption.rectTransform, top, 30, 34, 34);
            top += 38;

            const float rowHeight = 40f;
            const float labelWidth = 270f;
            foreach (var row in rows)
            {
                TextMeshProUGUI name = ResultsUi.Text(card, "BarName", row.name, a.BodyFont, 20,
                    row.self ? ResultsTheme.Text : ResultsTheme.Muted, TextAlignmentOptions.Left, row.self);
                name.enableWordWrapping = false;
                RectTransform nr = name.rectTransform;
                nr.anchorMin = new Vector2(0, 1);
                nr.anchorMax = new Vector2(0, 1);
                nr.pivot = new Vector2(0, 1);
                nr.anchoredPosition = new Vector2(34, -top);
                nr.sizeDelta = new Vector2(labelWidth, rowHeight - 8);

                RectTransform track = ResultsUi.Node("Track", card);
                track.anchorMin = new Vector2(0, 1);
                track.anchorMax = new Vector2(1, 1);
                track.pivot = new Vector2(0, 1);
                track.offsetMin = new Vector2(34 + labelWidth, -top - (rowHeight - 8));
                track.offsetMax = new Vector2(-34, -top);

                float f = (float)Math.Max(0.04, Math.Min(1.0, Math.Log10(Math.Max(row.kt, lo) / lo) / span));
                Color c = row.self ? ResultsTheme.Red : new Color(0.55f, 0.5f, 0.62f, 1f);
                RectTransform bar = ResultsUi.PixelBox(track, "Bar", a, c, row.self ? ResultsTheme.DarkRed : new Color(0.3f, 0.27f, 0.36f), false);
                bar.anchorMin = new Vector2(0, 0);
                bar.anchorMax = new Vector2(f * 0.78f, 1);
                bar.offsetMin = Vector2.zero;
                bar.offsetMax = Vector2.zero;

                TextMeshProUGUI value = ResultsUi.Text(track, "Value", row.value, a.BodyFont, 20,
                    row.self ? ResultsTheme.Accent : ResultsTheme.Muted, TextAlignmentOptions.Left, row.self);
                value.enableWordWrapping = false;
                value.rectTransform.anchorMin = new Vector2(f * 0.78f, 0);
                value.rectTransform.anchorMax = new Vector2(1, 1);
                value.rectTransform.offsetMin = new Vector2(12, 0);
                value.rectTransform.offsetMax = Vector2.zero;
                top += rowHeight;
            }

            return top + 18;
        }

        private static void StatGrid(RectTransform card, List<ReportStat> stats, ResultsAssets a, float top)
        {
            RectTransform grid = ResultsUi.Node("Stats", card);
            grid.anchorMin = new Vector2(0, 0);
            grid.anchorMax = new Vector2(1, 1);
            grid.offsetMin = new Vector2(26, 26);
            grid.offsetMax = new Vector2(-26, -top);
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
                TextMeshProUGUI label = ResultsUi.Text(tile, "Label", s.Label.ToUpperInvariant(), a.BodyFont, 16, ResultsTheme.Label, TextAlignmentOptions.TopLeft, true);
                label.characterSpacing = 2;
                ResultsUi.TopBand(label.rectTransform, 14, 24, 16, 12);
                TextMeshProUGUI value = ResultsUi.Text(tile, "Value", s.Value, a.BodyFont, 34, ResultsTheme.Text, TextAlignmentOptions.TopLeft, true);
                value.enableAutoSizing = true;
                value.fontSizeMin = 20;
                value.fontSizeMax = 34;
                value.enableWordWrapping = false;
                ResultsUi.TopBand(value.rectTransform, 40, 44, 16, 12);
                if (s.Note.Length > 0)
                {
                    TextMeshProUGUI note = ResultsUi.Text(tile, "Note", s.Note, a.BodyFont, 17, ResultsTheme.Muted);
                    ResultsUi.TopBand(note.rectTransform, 86, Math.Max(24, hgt - 94), 16, 12);
                }
            }
        }

        private static void RingTable(RectTransform card, OutcomeReport report, ResultsAssets a)
        {
            TextMeshProUGUI h = ResultsUi.Heading(card, "BLAST ON THE GROUND", a, 28);
            ResultsUi.TopBand(h.rectTransform, 26, 34, 34, 34);
            TextMeshProUGUI note = ResultsUi.Text(card, "Effects", report.GroundEffects, a.BodyFont, 21, ResultsTheme.Text);
            ResultsUi.TopBand(note.rectTransform, 66, 56, 34, 34);

            float top = 128;
            TextMeshProUGUI c1 = ResultsUi.Text(card, "ColPressure", "PEAK OVERPRESSURE", a.BodyFont, 15, ResultsTheme.Label, TextAlignmentOptions.Left, true);
            c1.characterSpacing = 2;
            ResultsUi.TopBand(c1.rectTransform, top, 22, 78, 34);
            TextMeshProUGUI c2 = ResultsUi.Text(card, "ColRange", "REACHES OUT TO", a.BodyFont, 15, ResultsTheme.Label, TextAlignmentOptions.Right, true);
            c2.characterSpacing = 2;
            ResultsUi.TopBand(c2.rectTransform, top, 22, 34, 34);
            top += 30;

            Canvas.ForceUpdateCanvases();
            float available = card.rect.height - top - 24;
            int count = Math.Max(1, report.Rings.Count);
            float rowHeight = Mathf.Clamp(available / count, 44f, 68f);
            for (int i = 0; i < report.Rings.Count; i++)
            {
                RingRow r = report.Rings[i];
                float alpha = r.Reached ? 1f : 0.38f;
                if (i % 2 == 0)
                {
                    Image stripe = ResultsUi.Image(card, "Stripe", null, new Color(1, 1, 1, 0.035f), false);
                    ResultsUi.TopBand(stripe.rectTransform, top, rowHeight, 22, 22);
                }

                Color col = ResultsTheme.Rings[r.Index % ResultsTheme.Rings.Length];
                RectTransform sw = ResultsUi.PixelBox(card, "Swatch", a, col * new Color(1, 1, 1, 0.6f * alpha), col * new Color(1, 1, 1, alpha), false, 6f, ResultsUi.SmallBoxPixelScale);
                ResultsUi.Place(sw, new Vector2(0, 1), new Vector2(34, -top - (rowHeight - 28) / 2), new Vector2(28, 28));

                TextMeshProUGUI label = ResultsUi.Text(card, "Pressure", "<b>" + ResultsFormat.Psi(r.Overpressure) + "</b>  <size=17><color=#B8B8CC>" +
                    ResultsFormat.Pressure(r.Overpressure) + "</color></size>", a.BodyFont, 21, new Color(1, 1, 1, alpha), TextAlignmentOptions.TopLeft);
                label.enableWordWrapping = false;
                ResultsUi.TopBand(label.rectTransform, top + 5, 26, 78, 200);
                TextMeshProUGUI effect = ResultsUi.Text(card, "Effect", r.Effect, a.BodyFont, 17, ResultsTheme.Muted * new Color(1, 1, 1, alpha), TextAlignmentOptions.TopLeft);
                effect.enableWordWrapping = false;
                ResultsUi.TopBand(effect.rectTransform, top + 31, 24, 78, 190);

                TextMeshProUGUI range = ResultsUi.Text(card, "Range", r.Reached ? ResultsFormat.Distance(r.Radius) : "not reached", a.BodyFont,
                    r.Reached ? 26 : 18, r.Reached ? ResultsTheme.Accent : ResultsTheme.Muted * new Color(1, 1, 1, 0.7f), TextAlignmentOptions.Right, r.Reached);
                range.enableWordWrapping = false;
                ResultsUi.TopBand(range.rectTransform, top, rowHeight, 34, 34);
                top += rowHeight;
            }
        }

        private static void NoGroundEffects(RectTransform card, OutcomeReport report, SimulationSession session, ResultsAssets a)
        {
            TextMeshProUGUI h = ResultsUi.Heading(card, "ON THE GROUND", a, 28);
            ResultsUi.TopBand(h.rectTransform, 26, 34, 34, 34);
            TextMeshProUGUI note = ResultsUi.Text(card, "Effects", report.GroundEffects, a.BodyFont, 23, ResultsTheme.Text);
            ResultsUi.TopBand(note.rectTransform, 68, 64, 34, 34);

            RectTransform chartRoot = ResultsUi.Node("Chart", card);
            ResultsUi.Stretch(chartRoot, 8, 12, 12, 140);
            ChartSpec spec = report.Kind == OutcomeKind.Disintegrated
                ? MassAltitudeChart(session.Result, "How it burned up")
                : AltitudeTimeChart(session.Result, "Altitude");
            if (spec != null)
            {
                chartRoot.gameObject.AddComponent<ChartView>().Plot(spec, a);
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
            bool tonnes = maxMass >= 10000;
            var spec = new ChartSpec
            {
                Title = title,
                Subtitle = "ablation strips mass from the hot surface",
                XLabel = tonnes ? "Mass (t)" : "Mass (kg)",
                YLabel = "Altitude (km)",
                X = Map(s.Mass, m => tonnes ? m / 1000.0 : m),
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

        public static void BuildPhysics(ResultsScreen screen)
        {
            OutcomeReport report = screen.Report;
            ResultsUi.DestroyChildren(screen.PhysicsLeft);
            ResultsUi.DestroyChildren(screen.PhysicsRight);
            if (report.Details.Count == 0)
            {
                TextMeshProUGUI t = ResultsUi.Text(screen.PhysicsLeft, "Empty", "No finished run.", screen.Assets.BodyFont, 24, ResultsTheme.Muted, TextAlignmentOptions.Center);
                ResultsUi.Stretch(t.rectTransform);
                return;
            }

            int half = (report.Details.Count + 1) / 2;
            float top = 0;
            for (int i = 0; i < report.Details.Count; i++)
            {
                if (i == half) top = 0;
                RectTransform column = i < half ? screen.PhysicsLeft : screen.PhysicsRight;
                top = Section(column, report.Details[i], screen.Assets, top) + 22;
            }
        }

        private static float Section(RectTransform column, ReportSection section, ResultsAssets a, float top)
        {
            const float rowHeight = 40f;
            float height = 70 + section.Rows.Count * rowHeight + 14;
            RectTransform card = ResultsUi.Card(column, "Section", a);
            ResultsUi.TopBand(card, top, height);
            TextMeshProUGUI h = ResultsUi.Heading(card, section.Title.ToUpperInvariant(), a, 26);
            ResultsUi.TopBand(h.rectTransform, 20, 32, 30, 30);
            float y = 66;
            for (int i = 0; i < section.Rows.Count; i++)
            {
                ReportStat row = section.Rows[i];
                if (i % 2 == 0)
                {
                    Image stripe = ResultsUi.Image(card, "Stripe", null, new Color(1, 1, 1, 0.035f), false);
                    ResultsUi.TopBand(stripe.rectTransform, y, rowHeight, 16, 16);
                }

                TextMeshProUGUI l = ResultsUi.Text(card, "Label", row.Label, a.BodyFont, 20, ResultsTheme.Muted, TextAlignmentOptions.Left);
                l.enableWordWrapping = false;
                l.rectTransform.anchorMin = new Vector2(0, 1);
                l.rectTransform.anchorMax = new Vector2(0.36f, 1);
                l.rectTransform.offsetMin = new Vector2(30, -y - rowHeight);
                l.rectTransform.offsetMax = new Vector2(0, -y);

                TextMeshProUGUI v = ResultsUi.Text(card, "Value", row.Value, a.BodyFont, 21, ResultsTheme.Text, TextAlignmentOptions.Left, true);
                v.enableWordWrapping = false;
                v.rectTransform.anchorMin = new Vector2(0.36f, 1);
                v.rectTransform.anchorMax = new Vector2(row.Note.Length > 0 ? 0.62f : 1f, 1);
                v.rectTransform.offsetMin = new Vector2(8, -y - rowHeight);
                v.rectTransform.offsetMax = new Vector2(-8, -y);

                if (row.Note.Length > 0)
                {
                    TextMeshProUGUI n = ResultsUi.Text(card, "Note", row.Note, a.BodyFont, 17, ResultsTheme.Muted * new Color(1, 1, 1, 0.85f), TextAlignmentOptions.Right);
                    n.enableWordWrapping = false;
                    n.rectTransform.anchorMin = new Vector2(0.62f, 1);
                    n.rectTransform.anchorMax = new Vector2(1, 1);
                    n.rectTransform.offsetMin = new Vector2(0, -y - rowHeight);
                    n.rectTransform.offsetMax = new Vector2(-30, -y);
                }

                y += rowHeight;
            }

            return top + height;
        }
    }
}
