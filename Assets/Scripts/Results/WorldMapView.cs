using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MTS.Results
{
    /// <summary>
    /// Zoomable world map with the impact point and geodesic damage rings. The map is an equirectangular texture
    /// (see <see cref="MapProjection"/>); rings are small circles on the sphere traced with the destination-point
    /// formula (<see cref="Geodesy.Circle"/>), so they stretch east-west by 1/cos(latitude) on the map exactly as the
    /// real footprint would. Drag to pan, scroll to zoom (about the cursor); zoom and pan are clamped to the map.
    /// </summary>
    public sealed class WorldMapView : MonoBehaviour
    {
        public const float MaxZoom = 96f;
        private const int RingSegments = 180;
        private const float GrowSeconds = 1.6f;
        private const float PulseSeconds = 1.8f;

        private ResultsAssets assets;
        private OutcomeReport report;
        private double craterDiameter;

        private RectTransform viewport;
        private RectTransform content;
        private RawImage map;
        private UiLines overlay;
        private UiLines marker;
        private UiLines scaleBar;
        private RectTransform closeUp;
        private double closeUpRadius;
        private TextMeshProUGUI scaleLabel;
        private TextMeshProUGUI placeLabel;
        private TextMeshProUGUI coordLabel;

        private float zoom = 1f;
        private Vector2 centre = new Vector2(0.5f, 0.5f);
        private float growth = 1f;
        private float growStart = -1f;
        private Vector2 lastViewport;
        private bool built;

        public float Zoom => zoom;
        public Vector2 Centre => centre;

        /// <summary>Builds the map for a report. Called once by the results screen.</summary>
        public void Show(OutcomeReport outcome, double finalCraterDiameter, ResultsAssets art)
        {
            report = outcome;
            craterDiameter = finalCraterDiameter;
            assets = art;
            Build();
            FitToRings();
            BuildCloseUp((RectTransform)transform);
            Redraw();
        }

        /// <summary>Sets the place name shown in the map's location box.</summary>
        public void SetPlaceName(string name)
        {
            if (placeLabel != null)
            {
                placeLabel.text = name;
            }
        }

        /// <summary>Starts the ring growth animation (when the map tab is opened).</summary>
        public void PlayRingGrowth()
        {
            if (!Application.isPlaying)
            {
                growth = 1f;
                return;
            }

            growStart = Time.unscaledTime;
            growth = 0f;
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            ResultsUi.DestroyChildren(root);

            RectTransform frame = ResultsUi.Stretch(ResultsUi.Node("Viewport", root), 14, 14, 14, 14);
            viewport = frame;
            var hit = frame.gameObject.AddComponent<Image>();
            hit.color = new Color(0.02f, 0.03f, 0.08f, 1f);
            hit.raycastTarget = true;
            frame.gameObject.AddComponent<RectMask2D>();
            // Events from the viewport are forwarded to this component.
            frame.gameObject.AddComponent<WorldMapInput>().Target = this;

            content = ResultsUi.Node("MapContent", frame);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = Vector2.zero;

            map = ResultsUi.Stretch(ResultsUi.Node("Earth", content)).gameObject.AddComponent<RawImage>();
            map.texture = assets.WorldMap;
            map.raycastTarget = false;
            overlay = ResultsUi.Stretch(ResultsUi.Node("Rings", content)).gameObject.AddComponent<UiLines>();
            marker = ResultsUi.Stretch(ResultsUi.Node("Marker", content)).gameObject.AddComponent<UiLines>();

            BuildLocationBox(root);
            BuildLegend(root);
            BuildControls(root);
            built = true;
        }

        private void BuildLocationBox(RectTransform root)
        {
            RectTransform box = ResultsUi.Card(root, "Location", assets);
            ResultsUi.Place(box, new Vector2(0, 1), new Vector2(34, -34), new Vector2(620, 104));
            placeLabel = ResultsUi.Text(box, "Place", "Looking up location...", assets.BodyFont, 26, ResultsTheme.Text, TextAlignmentOptions.TopLeft, true);
            ResultsUi.Stretch(placeLabel.rectTransform, 22, 44, 22, 14);
            placeLabel.enableAutoSizing = true;
            placeLabel.fontSizeMin = 16;
            placeLabel.fontSizeMax = 26;
            string where = report.HasLocation ? ResultsFormat.LatLon(report.Location.Lat, report.Location.Lon) : "Location unknown";
            if (report.EventAltitude > 1)
            {
                where += "  ·  " + ResultsFormat.Distance(report.EventAltitude) + " up";
            }

            coordLabel = ResultsUi.Text(box, "Coordinates", where, assets.BodyFont, 20, ResultsTheme.Accent, TextAlignmentOptions.BottomLeft);
            ResultsUi.Stretch(coordLabel.rectTransform, 22, 14, 22, 50);
        }

        private void BuildLegend(RectTransform root)
        {
            var rows = new List<RingRow>();
            foreach (RingRow r in report.Rings)
            {
                if (r.Reached) rows.Add(r);
            }

            bool crater = craterDiameter > 0;
            int lines = rows.Count + (crater ? 1 : 0);
            float height = 96 + Mathf.Max(1, lines) * 44 + 20;
            RectTransform box = ResultsUi.Card(root, "Legend", assets);
            ResultsUi.Place(box, new Vector2(1, 1), new Vector2(-34, -34), new Vector2(430, height));
            TextMeshProUGUI title = ResultsUi.Heading(box, "DAMAGE RINGS", assets, 26);
            ResultsUi.TopBand(title.rectTransform, 18, 32, 22, 22);
            TextMeshProUGUI sub = ResultsUi.Text(box, "Sub", "peak overpressure → ground range", assets.BodyFont, 17, ResultsTheme.Muted);
            ResultsUi.TopBand(sub.rectTransform, 54, 26, 22, 22);

            float y = 96;
            if (lines == 0)
            {
                TextMeshProUGUI none = ResultsUi.Text(box, "None", "No damaging blast reached the ground", assets.BodyFont, 19, ResultsTheme.Text);
                ResultsUi.TopBand(none.rectTransform, y, 40, 22, 22);
                return;
            }

            if (crater)
            {
                AddLegendRow(box, y, ResultsTheme.Crater, Color.white, "Crater rim", ResultsFormat.Distance(craterDiameter) + " wide");
                y += 44;
            }

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                RingRow r = rows[i];
                Color c = ResultsTheme.Rings[r.Index % ResultsTheme.Rings.Length];
                AddLegendRow(box, y, c * new Color(1, 1, 1, 0.55f), c, ResultsFormat.Psi(r.Overpressure), ResultsFormat.Distance(r.Radius));
                y += 44;
            }
        }

        private void AddLegendRow(RectTransform box, float y, Color fill, Color edge, string label, string value)
        {
            RectTransform swatch = ResultsUi.PixelBox(box, "Swatch", assets, fill, edge, false, 6f, ResultsUi.SmallBoxPixelScale);
            ResultsUi.Place(swatch, new Vector2(0, 1), new Vector2(24, -y - 6), new Vector2(30, 30));
            TextMeshProUGUI l = ResultsUi.Text(box, "Label", label, assets.BodyFont, 21, ResultsTheme.Text, TextAlignmentOptions.Left, true);
            ResultsUi.TopBand(l.rectTransform, y, 42, 68, 22);
            TextMeshProUGUI v = ResultsUi.Text(box, "Value", value, assets.BodyFont, 21, ResultsTheme.Accent, TextAlignmentOptions.Right, true);
            ResultsUi.TopBand(v.rectTransform, y, 42, 68, 24);
        }

        private void BuildControls(RectTransform root)
        {
            string[] labels = { "+", "-", "FIT" };
            float x = -490;
            for (int i = 0; i < labels.Length; i++)
            {
                Button b = ResultsUi.PixelButton(root, "Zoom" + labels[i], labels[i], assets, i < 2 ? 40 : 26);
                ResultsUi.Place((RectTransform)b.transform, new Vector2(1, 1), new Vector2(x, -34), new Vector2(i < 2 ? 64 : 92, 60));
                x -= i < 2 ? 80 : 108;
                int index = i;
                b.onClick.AddListener(() =>
                {
                    if (index == 0) ZoomAt(1.6f, ViewportCentreScreen());
                    else if (index == 1) ZoomAt(1f / 1.6f, ViewportCentreScreen());
                    else
                    {
                        FitToRings();
                        Redraw();
                    }
                });
            }

            RectTransform bar = ResultsUi.Card(root, "Scale", assets);
            ResultsUi.Place(bar, new Vector2(0, 0), new Vector2(34, 34), new Vector2(330, 84));
            scaleBar = ResultsUi.Stretch(ResultsUi.Node("Bar", bar), 22, 16, 22, 50).gameObject.AddComponent<UiLines>();
            scaleLabel = ResultsUi.Text(bar, "Label", "", assets.BodyFont, 19, ResultsTheme.Text, TextAlignmentOptions.TopLeft);
            ResultsUi.Stretch(scaleLabel.rectTransform, 22, 40, 18, 12);

            TextMeshProUGUI hint = ResultsUi.Text(root, "Hint", "Drag to pan  ·  scroll to zoom", assets.BodyFont, 19,
                new Color(1, 1, 1, 0.7f), TextAlignmentOptions.BottomRight);
            ResultsUi.Place(hint.rectTransform, new Vector2(1, 0), new Vector2(-34, 26), new Vector2(500, 30));
        }

        private Vector2 ViewportCentreScreen()
        {
            Vector3 world = viewport.TransformPoint(viewport.rect.center);
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, world);
        }

        private Vector2 BaseSize()
        {
            Vector2 v = viewport.rect.size;
            float width = Mathf.Max(v.x, 2f * v.y, 1f);
            return new Vector2(width, width * 0.5f);
        }

        private Vector2 ContentSize() => BaseSize() * zoom;

        /// <summary>Zoom so the outermost ring fills about 60% of the view height, centred on the event.</summary>
        public void FitToRings()
        {
            if (!built)
            {
                return;
            }

            double outer = craterDiameter / 2.0;
            foreach (RingRow r in report.Rings)
            {
                outer = Math.Max(outer, r.Radius);
            }

            Vector2 view = viewport.rect.size;
            float baseHeight = BaseSize().y;
            if (outer > 0 && view.y > 0)
            {
                double mapHeightNeeded = 0.6 * view.y * MapProjection.MetresPerMapHeight / (2.0 * outer);
                // Do not blow the 800 px map texture up beyond about 14 screen pixels per texel when fitting:
                // small footprints are shown to scale in the close-up inset instead.
                float textureCap = assets.WorldMap != null ? 14f * assets.WorldMap.width / BaseSize().x : MaxZoom;
                zoom = Mathf.Clamp((float)(mapHeightNeeded / baseHeight), 1f, Mathf.Clamp(textureCap, 1f, MaxZoom));
            }
            else
            {
                zoom = report.HasLocation ? 3f : 1f;
            }

            if (report.HasLocation)
            {
                MapPoint p = MapProjection.ToMap(report.Location);
                centre = new Vector2((float)p.X, (float)p.Y);
            }

            ClampView();
        }

        private void ClampView()
        {
            Vector2 view = viewport.rect.size;
            Vector2 size = ContentSize();
            float hx = view.x / (2f * size.x);
            float hy = view.y / (2f * size.y);
            centre.x = hx >= 0.5f ? 0.5f : Mathf.Clamp(centre.x, hx, 1f - hx);
            centre.y = hy >= 0.5f ? 0.5f : Mathf.Clamp(centre.y, hy, 1f - hy);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 size = ContentSize();
            float scale = CanvasScale();
            centre -= new Vector2(eventData.delta.x / scale / size.x, eventData.delta.y / scale / size.y);
            ClampView();
            Redraw();
        }

        public void OnScroll(PointerEventData eventData)
        {
            float steps = eventData.scrollDelta.y;
            if (Mathf.Abs(steps) < 1e-3f)
            {
                return;
            }

            ZoomAt(Mathf.Pow(1.25f, steps), eventData.position);
        }

        private float CanvasScale()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.scaleFactor : 1f;
        }

        /// <summary>Zooms by a factor keeping the map point under the given screen position fixed.</summary>
        public void ZoomAt(float factor, Vector2 screenPosition)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPosition, cam, out Vector2 local))
            {
                local = viewport.rect.center;
            }

            Vector2 fromCentre = local - viewport.rect.center;
            Vector2 before = ContentSize();
            Vector2 under = centre + new Vector2(fromCentre.x / before.x, fromCentre.y / before.y);
            zoom = Mathf.Clamp(zoom * factor, 1f, MaxZoom);
            Vector2 after = ContentSize();
            centre = under - new Vector2(fromCentre.x / after.x, fromCentre.y / after.y);
            ClampView();
            Redraw();
        }

        private void Update()
        {
            if (!built)
            {
                return;
            }

            if (viewport.rect.size != lastViewport)
            {
                ClampView();
                Redraw();
            }

            if (growStart >= 0)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - growStart) / GrowSeconds);
                growth = 1f - Mathf.Pow(1f - t, 3f);
                if (t >= 1f)
                {
                    growStart = -1f;
                }

                DrawRings();
            }

            DrawMarker(Application.isPlaying ? Time.unscaledTime : 0f);
        }

        /// <summary>Repositions the map and rebuilds all overlay geometry for the current zoom and centre.</summary>
        public void Redraw()
        {
            if (!built)
            {
                return;
            }

            lastViewport = viewport.rect.size;
            Vector2 size = ContentSize();
            content.sizeDelta = size;
            content.anchoredPosition = new Vector2(-centre.x * size.x, -centre.y * size.y);
            if (assets.WorldMap != null)
            {
                // Blocky texels at high zoom suit the pixel-art style better than a smeared bilinear blow-up.
                assets.WorldMap.filterMode = FilterMode.Bilinear;
            }

            DrawRings();
            DrawMarker(Application.isPlaying ? Time.unscaledTime : 0f);
            DrawScaleBar();
            if (closeUp != null)
            {
                // The inset is only needed while the footprint is too small to read on the map.
                closeUp.gameObject.SetActive(closeUpRadius / MapProjection.MetresPerMapHeight * size.y < 25.0);
            }
        }

        private Vector2 ToContent(GeoPoint g, Vector2 size, bool wrap)
        {
            MapPoint m = MapProjection.ToMap(g, wrap);
            return new Vector2((float)m.X * size.x, (float)m.Y * size.y);
        }

        private void DrawRings()
        {
            Vector2 size = ContentSize();
            overlay.Clear();
            DrawGraticule(size);

            if (report.HasLocation)
            {
                GeoPoint c = report.Location;
                Vector2 cp = ToContent(c, size, true);
                // Recentre the unwrapped ring longitudes on the wrapped centre.
                double lonShift = MapProjection.ToMap(c).X * 360.0 - 180.0 + MapProjection.CentralMeridian - c.Lon;
                var centreWrapped = new GeoPoint(c.Lat, c.Lon + lonShift);

                for (int i = report.Rings.Count - 1; i >= 0; i--)
                {
                    RingRow r = report.Rings[i];
                    if (!r.Reached) continue;
                    Color col = ResultsTheme.Rings[r.Index % ResultsTheme.Rings.Length];
                    DrawRing(centreWrapped, cp, r.Radius * growth, size, col * new Color(1, 1, 1, 0.17f), col, 2.6f);
                }

                if (craterDiameter > 0)
                {
                    DrawRing(centreWrapped, cp, craterDiameter / 2.0 * growth, size, ResultsTheme.Crater * new Color(1, 1, 1, 0.85f), Color.white, 2f);
                }
            }

            overlay.Commit();
        }

        private void DrawRing(GeoPoint centre, Vector2 centrePx, double radius, Vector2 size, Color fill, Color edge, float width)
        {
            if (!(radius > 0))
            {
                return;
            }

            GeoPoint[] ring = Geodesy.Circle(centre, radius, RingSegments);
            var pts = new List<Vector2>(ring.Length);
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (GeoPoint g in ring)
            {
                Vector2 p = ToContent(g, size, false);
                pts.Add(p);
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
            }

            float pixelRadius = (float)(radius / MapProjection.MetresPerMapHeight) * size.y;
            int pole = Geodesy.EnclosedPole(centre, radius);
            for (int copy = -1; copy <= 1; copy++)
            {
                if (copy == -1 && maxX <= size.x && pole == 0) continue;
                if (copy == 1 && minX >= 0 && pole == 0) continue;
                var shifted = new List<Vector2>(pts.Count);
                Vector2 offset = new Vector2(copy * size.x, 0);
                foreach (Vector2 p in pts) shifted.Add(p + offset);

                if (pole != 0)
                {
                    overlay.FillToY(shifted, pole > 0 ? size.y : 0f, fill);
                }
                else if (pixelRadius > 1.5f)
                {
                    overlay.Fan(centrePx + offset, shifted, fill);
                }

                overlay.Polyline(shifted, Mathf.Min(width, Mathf.Max(1f, pixelRadius)), edge);
            }
        }

        private void DrawGraticule(Vector2 size)
        {
            Vector2 view = viewport.rect.size;
            double visibleLon = 360.0 * view.x / size.x;
            double step = ChartScale.NiceStep(Math.Min(360.0, visibleLon), 6);
            step = step >= 30 ? 30 : step >= 15 ? 15 : step;
            Color line = new Color(1f, 1f, 1f, 0.13f);
            double lonMin = (centre.x - view.x / (2f * size.x)) * 360.0 - 180.0;
            double lonMax = (centre.x + view.x / (2f * size.x)) * 360.0 - 180.0;
            double latMin = (centre.y - view.y / (2f * size.y)) * 180.0 - 90.0;
            double latMax = (centre.y + view.y / (2f * size.y)) * 180.0 - 90.0;
            for (double lon = Math.Ceiling(Math.Max(-180, lonMin) / step) * step; lon <= Math.Min(180, lonMax); lon += step)
            {
                float x = (float)((lon + 180.0) / 360.0) * size.x;
                overlay.Segment(new Vector2(x, 0), new Vector2(x, size.y), lon == 0 ? 2f : 1.2f, line);
            }

            for (double lat = Math.Ceiling(Math.Max(-90, latMin) / step) * step; lat <= Math.Min(90, latMax); lat += step)
            {
                float y = (float)((lat + 90.0) / 180.0) * size.y;
                overlay.Segment(new Vector2(0, y), new Vector2(size.x, y), lat == 0 ? 2f : 1.2f, line);
            }
        }

        private void DrawMarker(float time)
        {
            marker.Clear();
            if (report != null && report.HasLocation)
            {
                Vector2 p = ToContent(report.Location, ContentSize(), true);
                float phase = (time % PulseSeconds) / PulseSeconds;
                if (Application.isPlaying)
                {
                    marker.Circle(p, 12f + 46f * phase, 3f, new Color(1f, 0.85f, 0.3f, 0.9f * (1f - phase)));
                }

                Color dark = new Color(0, 0, 0, 0.75f);
                Color bright = ResultsTheme.Accent;
                float gap = 9f, arm = 22f;
                foreach (Vector2 d in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                {
                    marker.Segment(p + d * gap, p + d * (gap + arm), 6f, dark);
                    marker.Segment(p + d * gap, p + d * (gap + arm), 3f, bright);
                }

                marker.Circle(p, 6.5f, 5f, dark, 20);
                marker.Circle(p, 6.5f, 2.5f, bright, 20);
            }

            marker.Commit();
        }

        private void DrawScaleBar()
        {
            if (scaleBar == null)
            {
                return;
            }

            Vector2 size = ContentSize();
            double lat = centre.y * 180.0 - 90.0;
            double metresPerPixel = MapProjection.MetresPerMapWidth(lat) / size.x;
            float maxPixels = ((RectTransform)scaleBar.transform).rect.width;
            double target = metresPerPixel * maxPixels * 0.9;
            double nice = NiceDistance(target);
            float pixels = (float)(nice / metresPerPixel);
            scaleBar.Clear();
            float y = 6f;
            scaleBar.Rect(new Rect(0, y - 4, pixels, 8), new Color(0, 0, 0, 0.6f));
            scaleBar.Rect(new Rect(0, y - 3, pixels / 2f, 6), Color.white);
            scaleBar.Rect(new Rect(pixels / 2f, y - 3, pixels / 2f, 6), ResultsTheme.Red);
            scaleBar.Commit();
            scaleLabel.text = ScaleText(nice) + "   <color=#B8B8CC><size=16>east-west at " +
                              Math.Abs(lat).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + "° " + (lat >= 0 ? "N" : "S") + "</size></color>";
        }

        private static string ScaleText(double metres) =>
            metres >= 1000
                ? (metres / 1000.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " km"
                : metres.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m";

        private double OuterRadius()
        {
            double outer = craterDiameter / 2.0;
            foreach (RingRow r in report.Rings)
            {
                outer = Math.Max(outer, r.Radius);
            }

            return outer;
        }

        /// <summary>
        /// Close-up of the damage footprint, to scale, for events too small to see on the world map. At these sizes
        /// the rings are circles to well under 1% (the map's 1/cos(latitude) stretch is a projection effect).
        /// </summary>
        private void BuildCloseUp(RectTransform root)
        {
            // Focus on the largest ring that is too small to read on the map at the fitted zoom; bigger rings are
            // clipped by the inset.
            float mapHeight = ContentSize().y;
            double outer = 0;
            foreach (RingRow r in report.Rings)
            {
                if (r.Reached && r.Radius / MapProjection.MetresPerMapHeight * mapHeight < 25.0)
                {
                    outer = Math.Max(outer, r.Radius);
                }
            }

            if (!(outer > 0) && craterDiameter > 0)
            {
                outer = craterDiameter * 2.0;
            }

            closeUpRadius = outer;
            if (!(outer > 0))
            {
                return;
            }

            closeUp = ResultsUi.Card(root, "CloseUp", assets);
            ResultsUi.Place(closeUp, new Vector2(0, 0), new Vector2(34, 134), new Vector2(400, 420));
            TextMeshProUGUI title = ResultsUi.Heading(closeUp, "CLOSE-UP", assets, 24);
            ResultsUi.TopBand(title.rectTransform, 16, 30, 22, 22);
            TextMeshProUGUI sub = ResultsUi.Text(closeUp, "Sub", "footprint to scale", assets.BodyFont, 17, ResultsTheme.Muted, TextAlignmentOptions.TopRight);
            ResultsUi.TopBand(sub.rectTransform, 22, 26, 22, 22);

            RectTransform area = ResultsUi.Node("Rings", closeUp);
            ResultsUi.Stretch(area, 20, 70, 20, 56);
            area.gameObject.AddComponent<RectMask2D>();
            Image backdrop = ResultsUi.Image(area, "Backdrop", null, new Color(0.02f, 0.05f, 0.12f, 1f), false);
            ResultsUi.Stretch(backdrop.rectTransform);
            UiLines lines = ResultsUi.Stretch(ResultsUi.Node("Lines", area)).gameObject.AddComponent<UiLines>();
            Canvas.ForceUpdateCanvases();
            Vector2 size = area.rect.size;
            Vector2 c = size * 0.5f;
            float scale = (float)(0.46 * Mathf.Min(size.x, size.y) / outer); // px per metre
            for (float g = 0; g <= size.x; g += size.x / 6f)
            {
                lines.Segment(new Vector2(g, 0), new Vector2(g, size.y), 1f, new Color(1, 1, 1, 0.07f));
            }

            for (float g = 0; g <= size.y; g += size.y / 6f)
            {
                lines.Segment(new Vector2(0, g), new Vector2(size.x, g), 1f, new Color(1, 1, 1, 0.07f));
            }

            for (int i = report.Rings.Count - 1; i >= 0; i--)
            {
                RingRow r = report.Rings[i];
                if (!r.Reached) continue;
                Color col = ResultsTheme.Rings[r.Index % ResultsTheme.Rings.Length];
                float px = (float)r.Radius * scale;
                lines.Disc(c, px, col * new Color(1, 1, 1, 0.16f), 72);
                lines.Circle(c, px, 2.4f, col, 72);
            }

            if (craterDiameter > 0)
            {
                float px = Mathf.Max(1.5f, (float)(craterDiameter / 2.0) * scale);
                lines.Disc(c, px, ResultsTheme.Crater, 48);
                lines.Circle(c, px, 1.6f, Color.white, 48);
            }

            lines.Segment(c + new Vector2(-6, 0), c + new Vector2(6, 0), 2f, ResultsTheme.Accent);
            lines.Segment(c + new Vector2(0, -6), c + new Vector2(0, 6), 2f, ResultsTheme.Accent);

            double barMetres = NiceDistance(outer * 0.8);
            float barPx = (float)barMetres * scale;
            UiLines bar = ResultsUi.Node("ScaleBar", closeUp).gameObject.AddComponent<UiLines>();
            ResultsUi.Stretch(bar.rectTransform, 22, 22, 22, 0);
            bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.offsetMax = new Vector2(-22, 50);
            bar.Rect(new Rect(0, 12, barPx / 2f, 6), Color.white);
            bar.Rect(new Rect(barPx / 2f, 12, barPx / 2f, 6), ResultsTheme.Red);
            bar.Commit();
            lines.Commit();
            TextMeshProUGUI label = ResultsUi.Text(closeUp, "ScaleLabel", ScaleText(barMetres), assets.BodyFont, 19, ResultsTheme.Text, TextAlignmentOptions.BottomLeft);
            ResultsUi.Place(label.rectTransform, new Vector2(0, 0), new Vector2(34 + barPx, 24), new Vector2(200, 28));
        }

        private static double NiceDistance(double metres)
        {
            if (!(metres > 0)) return 1000;
            double p = Math.Pow(10, Math.Floor(Math.Log10(metres)));
            double f = metres / p;
            double n = f >= 5 ? 5 : f >= 2 ? 2 : 1;
            return n * p;
        }
    }
}
