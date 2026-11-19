using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MTS.Results
{
    public readonly struct ChartMarker
    {
        public readonly double X;
        public readonly double Y;
        public readonly string Label;

        public ChartMarker(double x, double y, string label)
        {
            X = x;
            Y = y;
            Label = label;
        }
    }

    /// <summary>What to plot: one series with axis titles (including units) and optional annotations.</summary>
    public sealed class ChartSpec
    {
        public string Title = "";
        public string Subtitle = "";
        public string XLabel = "";
        public string YLabel = "";
        public double[] X = new double[0];
        public double[] Y = new double[0];
        public Color Color = ResultsTheme.Series[0];
        public bool XIncludeZero;
        public bool YIncludeZero;
        /// <summary>Dashed horizontal reference lines (y value, label).</summary>
        public readonly List<ChartMarker> HorizontalLines = new List<ChartMarker>();
        /// <summary>Highlighted points (x, y, label).</summary>
        public readonly List<ChartMarker> Points = new List<ChartMarker>();
    }

    /// <summary>
    /// Line chart drawn with uGUI: nice-number ticks, gridlines, axis titles with units, a filled series and
    /// annotations. Lines are redrawn when the rect changes size; labels are anchored proportionally.
    /// </summary>
    public sealed class ChartView : MonoBehaviour
    {
        private const float PadLeft = 110f;
        private const float PadBottom = 70f;
        private const float PadRight = 36f;
        private const float PadTop = 80f;

        private ChartSpec spec;
        private ResultsAssets assets;
        private RectTransform plotArea;
        private UiLines grid;
        private UiLines series;
        private AxisScale xAxis;
        private AxisScale yAxis;
        private Vector2 lastSize;

        public void Plot(ChartSpec chart, ResultsAssets art)
        {
            spec = chart;
            assets = art;
            var root = (RectTransform)transform;
            ResultsUi.DestroyChildren(root);

            TextMeshProUGUI title = ResultsUi.Heading(root, spec.Title.ToUpperInvariant(), assets, 30);
            ResultsUi.TopBand(title.rectTransform, 22, 36, 32, 32);
            if (!string.IsNullOrEmpty(spec.Subtitle))
            {
                TextMeshProUGUI sub = ResultsUi.Text(root, "Subtitle", spec.Subtitle, assets.BodyFont, 20, ResultsTheme.Muted, TextAlignmentOptions.TopRight);
                sub.enableWordWrapping = false;
                ResultsUi.TopBand(sub.rectTransform, 28, 28, 32, 32);
            }

            plotArea = ResultsUi.Stretch(ResultsUi.Node("Plot", root), PadLeft, PadBottom, PadRight, PadTop);
            grid = ResultsUi.Stretch(ResultsUi.Node("Grid", plotArea)).gameObject.AddComponent<UiLines>();
            series = ResultsUi.Stretch(ResultsUi.Node("Series", plotArea)).gameObject.AddComponent<UiLines>();

            int n = Mathf.Min(spec.X.Length, spec.Y.Length);
            if (n < 2)
            {
                TextMeshProUGUI empty = ResultsUi.Text(plotArea, "Empty", "Not enough data recorded for this chart.",
                    assets.BodyFont, 22, ResultsTheme.Muted, TextAlignmentOptions.Center);
                ResultsUi.Stretch(empty.rectTransform);
                return;
            }

            double xMin = double.MaxValue, xMax = double.MinValue, yMin = double.MaxValue, yMax = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                xMin = System.Math.Min(xMin, spec.X[i]);
                xMax = System.Math.Max(xMax, spec.X[i]);
                yMin = System.Math.Min(yMin, spec.Y[i]);
                yMax = System.Math.Max(yMax, spec.Y[i]);
            }

            foreach (ChartMarker h in spec.HorizontalLines)
            {
                yMin = System.Math.Min(yMin, h.Y);
                yMax = System.Math.Max(yMax, h.Y);
            }

            xAxis = ChartScale.Nice(xMin, xMax, 6, spec.XIncludeZero);
            yAxis = ChartScale.Nice(yMin, yMax, 5, spec.YIncludeZero);

            for (int i = 0; i < xAxis.TickCount; i++)
            {
                double v = xAxis.Tick(i);
                TextMeshProUGUI t = ResultsUi.Text(plotArea, "XTick", ChartScale.Label(v, xAxis.Step), assets.BodyFont, 20,
                    ResultsTheme.Muted, TextAlignmentOptions.Top);
                t.enableWordWrapping = false;
                float f = (float)xAxis.Normalise(v);
                ResultsUi.Place(t.rectTransform, new Vector2(f, 0), new Vector2(0, -10), new Vector2(120, 24));
                t.rectTransform.pivot = new Vector2(0.5f, 1);
            }

            for (int i = 0; i < yAxis.TickCount; i++)
            {
                double v = yAxis.Tick(i);
                TextMeshProUGUI t = ResultsUi.Text(plotArea, "YTick", ChartScale.Label(v, yAxis.Step), assets.BodyFont, 20,
                    ResultsTheme.Muted, TextAlignmentOptions.Right);
                t.enableWordWrapping = false;
                float f = (float)yAxis.Normalise(v);
                ResultsUi.Place(t.rectTransform, new Vector2(0, f), new Vector2(-12, 0), new Vector2(90, 24));
                t.rectTransform.pivot = new Vector2(1, 0.5f);
            }

            TextMeshProUGUI xTitle = ResultsUi.Text(plotArea, "XTitle", spec.XLabel, assets.BodyFont, 21, ResultsTheme.Label, TextAlignmentOptions.Bottom);
            xTitle.enableWordWrapping = false;
            ResultsUi.Place(xTitle.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -PadBottom + 4), new Vector2(600, 28));
            xTitle.rectTransform.pivot = new Vector2(0.5f, 0);

            TextMeshProUGUI yTitle = ResultsUi.Text(plotArea, "YTitle", spec.YLabel, assets.BodyFont, 21, ResultsTheme.Label, TextAlignmentOptions.Center);
            yTitle.enableWordWrapping = false;
            ResultsUi.Place(yTitle.rectTransform, new Vector2(0, 0.5f), new Vector2(-PadLeft + 16, 0), new Vector2(400, 28));
            yTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            yTitle.rectTransform.localEulerAngles = new Vector3(0, 0, 90);

            foreach (ChartMarker h in spec.HorizontalLines)
            {
                // Label at the left end: on these charts the curve crosses reference lines on the right.
                TextMeshProUGUI t = ResultsUi.Text(plotArea, "LineLabel", h.Label, assets.BodyFont, 20, ResultsTheme.Accent, TextAlignmentOptions.BottomLeft);
                t.enableWordWrapping = false;
                ResultsUi.Place(t.rectTransform, new Vector2(0, (float)yAxis.Normalise(h.Y)), new Vector2(8, 3), new Vector2(400, 24));
                t.rectTransform.pivot = new Vector2(0, 0);
            }

            foreach (ChartMarker p in spec.Points)
            {
                if (string.IsNullOrEmpty(p.Label))
                {
                    continue;
                }

                float fx = (float)xAxis.Normalise(p.X);
                float fy = (float)yAxis.Normalise(p.Y);
                bool right = fx < 0.6f;
                TextMeshProUGUI t = ResultsUi.Text(plotArea, "PointLabel", p.Label, assets.BodyFont, 20, ResultsTheme.Text,
                    right ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
                t.enableWordWrapping = false;
                // Below the point when there is room (curves here mostly come in from above), else above it.
                bool below = fy > 0.2f;
                ResultsUi.Place(t.rectTransform, new Vector2(fx, fy), new Vector2(right ? 12 : -12, below ? -14 : 14), new Vector2(360, 24));
                t.rectTransform.pivot = new Vector2(right ? 0 : 1, below ? 1f : 0f);
            }

            lastSize = Vector2.zero;
            Redraw();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (spec != null && plotArea != null && plotArea.rect.size != lastSize)
            {
                Redraw();
            }
        }

        private Vector2 ToPlot(double x, double y, Vector2 size) =>
            new Vector2((float)xAxis.Normalise(x) * size.x, (float)yAxis.Normalise(y) * size.y);

        public void Redraw()
        {
            if (grid == null || series == null)
            {
                return;
            }

            Vector2 size = plotArea.rect.size;
            lastSize = size;
            grid.Clear();
            series.Clear();
            int n = Mathf.Min(spec.X.Length, spec.Y.Length);
            if (n < 2 || size.x <= 0 || size.y <= 0)
            {
                grid.Commit();
                series.Commit();
                return;
            }

            for (int i = 0; i < xAxis.TickCount; i++)
            {
                float x = (float)xAxis.Normalise(xAxis.Tick(i)) * size.x;
                grid.Segment(new Vector2(x, 0), new Vector2(x, size.y), 1.5f, ResultsTheme.Grid);
                grid.Segment(new Vector2(x, 0), new Vector2(x, -7), 2f, ResultsTheme.Axis);
            }

            for (int i = 0; i < yAxis.TickCount; i++)
            {
                float y = (float)yAxis.Normalise(yAxis.Tick(i)) * size.y;
                grid.Segment(new Vector2(0, y), new Vector2(size.x, y), 1.5f, ResultsTheme.Grid);
                grid.Segment(new Vector2(0, y), new Vector2(-7, y), 2f, ResultsTheme.Axis);
            }

            grid.Segment(new Vector2(0, -1), new Vector2(size.x, -1), 2.5f, ResultsTheme.Axis);
            grid.Segment(new Vector2(-1, 0), new Vector2(-1, size.y), 2.5f, ResultsTheme.Axis);

            foreach (ChartMarker h in spec.HorizontalLines)
            {
                float y = (float)yAxis.Normalise(h.Y) * size.y;
                grid.Dashed(new Vector2(0, y), new Vector2(size.x, y), 2f, 10f, 7f, ResultsTheme.Accent * new Color(1, 1, 1, 0.8f));
            }

            var points = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                Vector2 p = ToPlot(spec.X[i], spec.Y[i], size);
                if (points.Count == 0 || (p - points[points.Count - 1]).sqrMagnitude > 0.25f || i == n - 1)
                {
                    points.Add(p);
                }
            }

            Color fill = spec.Color;
            fill.a = 0.13f;
            series.FillToY(points, 0, fill);
            series.Polyline(points, 6f, new Color(spec.Color.r * 0.3f, spec.Color.g * 0.3f, spec.Color.b * 0.3f, 0.5f));
            series.Polyline(points, 3.2f, spec.Color);
            foreach (ChartMarker p in spec.Points)
            {
                Vector2 q = ToPlot(p.X, p.Y, size);
                series.Disc(q, 8f, new Color(0, 0, 0, 0.6f));
                series.Disc(q, 5.5f, ResultsTheme.Text);
            }

            grid.Commit();
            series.Commit();
        }
    }
}
