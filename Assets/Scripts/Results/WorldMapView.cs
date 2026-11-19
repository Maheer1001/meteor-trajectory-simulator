using System;
using System.Collections.Generic;
using MTS.Core;
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

        private RectTransform legendRoot;
        /// <summary>Which ring groups (<see cref="EffectGroup"/>) are drawn; toggled from the legend.</summary>
        private readonly bool[] layerOn = { true, true, true, true, true };

        private float zoom = 1f;
        private Vector2 centre = new Vector2(0.5f, 0.5f);
        private float growth = 1f;
        private float growStart = -1f;
        private Vector2 lastViewport;
        private bool built;

        public float Zoom => zoom;
        public Vector2 Centre => centre;

        /// <summary>Order in which layers are hidden when the legend would not fit (blast is kept longest).</summary>
        private static readonly EffectGroup[] TrimOrder = { EffectGroup.Ejecta, EffectGroup.Seismic, EffectGroup.Thermal, EffectGroup.Tsunami };

        public bool IsLayerOn(EffectGroup group) => layerOn[(int)group];

        /// <summary>Shows or hides a ring group; other groups are hidden if the legend would not fit.</summary>
        public void SetLayer(EffectGroup group, bool on)
        {
            layerOn[(int)group] = on;
            TrimLayers(group);
            RebuildLegend();
            FitToRings();
            Redraw();
        }

        /// <summary>Builds the map for a report. Called once by the results screen.</summary>
        public void Show(OutcomeReport outcome, double finalCraterDiameter, ResultsAssets art)
        {
            report = outcome;
            craterDiameter = finalCraterDiameter;
            assets = art;
            for (int i = 0; i < layerOn.Length; i++) layerOn[i] = true;
            Build();
            Canvas.ForceUpdateCanvases();
            TrimLayers(null);
            RebuildLegend();
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

            // Three copies of the map side by side (the texture repeats in U), so the view can be centred on any
            // longitude and wraps round seamlessly; ring outlines are drawn on all three copies too.
            RectTransform earth = ResultsUi.Node("Earth", content);
            earth.anchorMin = new Vector2(-1, 0);
            earth.anchorMax = new Vector2(2, 1);
            earth.offsetMin = earth.offsetMax = Vector2.zero;
            map = earth.gameObject.AddComponent<RawImage>();
            map.texture = assets.WorldMap;
            map.uvRect = new Rect(-1, 0, 3, 1);
            map.raycastTarget = false;
            overlay = ResultsUi.Stretch(ResultsUi.Node("Rings", content)).gameObject.AddComponent<UiLines>();
            marker = ResultsUi.Stretch(ResultsUi.Node("Marker", content)).gameObject.AddComponent<UiLines>();

            BuildLocationBox(root);
            legendRoot = ResultsUi.Node("LegendRoot", root);
            ResultsUi.Stretch(legendRoot);
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
            placeLabel.fontSizeMin = 20;
            placeLabel.fontSizeMax = 26;
            string where = report.HasLocation ? ResultsFormat.LatLon(report.Location.Lat, report.Location.Lon) : "Location unknown";
            if (report.EventAltitude > 1)
            {
                where += "  ·  " + ResultsFormat.Distance(report.EventAltitude) + " up";
            }

            if (report.IsOcean)
            {
                where += "  ·  ocean";
            }

            coordLabel = ResultsUi.Text(box, "Coordinates", where, assets.BodyFont, 22, ResultsTheme.Accent, TextAlignmentOptions.BottomLeft);
            ResultsUi.Stretch(coordLabel.rectTransform, 22, 14, 22, 50);
        }

        private const float LegendWidth = 500f;
        private const float LegendTitleHeight = 62f;
        private const float GroupRowHeight = 42f;
        private const float RingRowHeight = 34f;

        private static string GroupName(EffectGroup g)
        {
            switch (g)
            {
                case EffectGroup.Blast: return "Blast wave";
                case EffectGroup.Thermal: return "Heat (fireball)";
                case EffectGroup.Seismic: return "Ground shaking";
                case EffectGroup.Ejecta: return "Falling debris";
                default: return "Tsunami";
            }
        }

        private List<RingRow> BlastRows()
        {
            var rows = new List<RingRow>();
            foreach (RingRow r in report.Rings)
            {
                if (r.Reached) rows.Add(r);
            }

            return rows;
        }

        private List<EffectRing> GroupRings(EffectGroup g)
        {
            var rows = new List<EffectRing>();
            foreach (EffectRing r in report.EffectRings)
            {
                if (r.Group == g) rows.Add(r);
            }

            rows.Sort((a, b) => a.Radius.CompareTo(b.Radius));
            return rows;
        }

        private int GroupRingCount(EffectGroup g) =>
            g == EffectGroup.Blast ? BlastRows().Count + (craterDiameter > 0 ? 1 : 0) : GroupRings(g).Count;

        private bool GroupPresent(EffectGroup g) => GroupRingCount(g) > 0;

        private float LegendHeight()
        {
            float h = LegendTitleHeight + 14;
            int groups = 0;
            foreach (EffectGroup g in (EffectGroup[])Enum.GetValues(typeof(EffectGroup)))
            {
                if (!GroupPresent(g)) continue;
                groups++;
                h += GroupRowHeight;
                if (layerOn[(int)g]) h += GroupRingCount(g) * RingRowHeight;
            }

            return groups == 0 ? LegendTitleHeight + 60 : h;
        }

        private float LegendCapacity() => Mathf.Max(300f, viewport.rect.height - 40f);

        /// <summary>Hides layers (least important first, never <paramref name="keep"/>) until the legend fits.</summary>
        private void TrimLayers(EffectGroup? keep)
        {
            float capacity = LegendCapacity();
            foreach (EffectGroup g in TrimOrder)
            {
                if (LegendHeight() <= capacity) return;
                if (keep.HasValue && keep.Value == g) continue;
                if (GroupPresent(g)) layerOn[(int)g] = false;
            }

            if (LegendHeight() > capacity && !(keep.HasValue && keep.Value == EffectGroup.Blast))
            {
                layerOn[(int)EffectGroup.Blast] = false;
            }
        }

        private void RebuildLegend()
        {
            ResultsUi.DestroyChildren(legendRoot);
            float height = LegendHeight();
            RectTransform box = ResultsUi.Card(legendRoot, "Legend", assets);
            ResultsUi.Place(box, new Vector2(1, 1), new Vector2(-34, -34), new Vector2(LegendWidth, height));
            TextMeshProUGUI title = ResultsUi.Heading(box, "MAP LAYERS", assets, 30);
            ResultsUi.TopBand(title.rectTransform, 18, 34, 24, 24);
            TextMeshProUGUI sub = ResultsUi.Text(box, "Sub", "click to show or hide", assets.BodyFont, 20, ResultsTheme.Muted, TextAlignmentOptions.TopRight);
            ResultsUi.TopBand(sub.rectTransform, 24, 30, 24, 24);

            float y = LegendTitleHeight + 4;
            bool any = false;
            foreach (EffectGroup g in (EffectGroup[])Enum.GetValues(typeof(EffectGroup)))
            {
                if (!GroupPresent(g)) continue;
                any = true;
                AddGroupRow(box, y, g);
                y += GroupRowHeight;
                if (!layerOn[(int)g]) continue;
                if (g == EffectGroup.Blast)
                {
                    if (craterDiameter > 0)
                    {
                        AddLegendRow(box, y, ResultsTheme.Crater, Color.white, "Crater rim", ResultsFormat.Distance(craterDiameter) + " wide", false);
                        y += RingRowHeight;
                    }

                    List<RingRow> rows = BlastRows();
                    for (int i = rows.Count - 1; i >= 0; i--)
                    {
                        RingRow r = rows[i];
                        Color c = ResultsTheme.Rings[r.Index % ResultsTheme.Rings.Length];
                        AddLegendRow(box, y, c * new Color(1, 1, 1, 0.55f), c, ResultsFormat.Psi(r.Overpressure) + " overpressure", ResultsFormat.Reach(r.Radius), false);
                        y += RingRowHeight;
                    }
                }
                else
                {
                    foreach (EffectRing r in GroupRings(g))
                    {
                        Color c = ResultsTheme.EffectColor(g, r.Level);
                        AddLegendRow(box, y, new Color(0, 0, 0, 0), c, r.Label, ResultsFormat.Reach(r.Radius), true);
                        y += RingRowHeight;
                    }
                }
            }

            if (!any)
            {
                TextMeshProUGUI none = ResultsUi.Text(box, "None", "No damaging effects reached the ground", assets.BodyFont, 22, ResultsTheme.Text);
                ResultsUi.TopBand(none.rectTransform, y, 40, 24, 24);
            }
        }

        private void AddGroupRow(RectTransform box, float y, EffectGroup g)
        {
            bool on = layerOn[(int)g];
            RectTransform row = ResultsUi.Node("Layer" + g, box);
            ResultsUi.TopBand(row, y, GroupRowHeight - 4, 14, 14);
            Image hit = row.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, on ? 0.07f : 0.03f);
            hit.raycastTarget = true;
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            EffectGroup group = g;
            button.onClick.AddListener(() => SetLayer(group, !layerOn[(int)group]));

            Color c = g == EffectGroup.Blast ? ResultsTheme.Rings[0] : ResultsTheme.EffectColor(g, 0);
            RectTransform check = ResultsUi.PixelBox(row, "Check", assets, on ? c : new Color(0, 0, 0, 0.4f), c, false, 6f, ResultsUi.SmallBoxPixelScale);
            ResultsUi.Place(check, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(24, 24));
            TextMeshProUGUI name = ResultsUi.Text(row, "Name", GroupName(g), assets.BodyFont, 22, on ? ResultsTheme.Text : ResultsTheme.Muted, TextAlignmentOptions.Left, true);
            name.enableWordWrapping = false;
            ResultsUi.Stretch(name.rectTransform, 52, 0, 10, 0);
            int count = GroupRingCount(g);
            string state = on ? count + (count == 1 ? " ring" : " rings") : "hidden";
            TextMeshProUGUI value = ResultsUi.Text(row, "State", state, assets.BodyFont, 20, ResultsTheme.Muted, TextAlignmentOptions.Right);
            value.enableWordWrapping = false;
            ResultsUi.Stretch(value.rectTransform, 52, 0, 12, 0);
        }

        private void AddLegendRow(RectTransform box, float y, Color fill, Color edge, string label, string value, bool dashed)
        {
            if (dashed)
            {
                UiLines sw = ResultsUi.Node("Swatch", box).gameObject.AddComponent<UiLines>();
                ResultsUi.Place(sw.rectTransform, new Vector2(0, 1), new Vector2(40, -y - 6), new Vector2(26, 22));
                sw.Segment(new Vector2(0, 11), new Vector2(9, 11), 4f, edge);
                sw.Segment(new Vector2(15, 11), new Vector2(26, 11), 4f, edge);
                sw.Commit();
            }
            else
            {
                RectTransform swatch = ResultsUi.PixelBox(box, "Swatch", assets, fill, edge, false, 6f, ResultsUi.SmallBoxPixelScale);
                ResultsUi.Place(swatch, new Vector2(0, 1), new Vector2(42, -y - 6), new Vector2(22, 22));
            }

            TextMeshProUGUI l = ResultsUi.Text(box, "Label", label, assets.BodyFont, 20, ResultsTheme.Text, TextAlignmentOptions.Left);
            l.enableWordWrapping = false;
            ResultsUi.TopBand(l.rectTransform, y, RingRowHeight, 80, 190);
            TextMeshProUGUI v = ResultsUi.Text(box, "Value", value, assets.BodyFont, 20, ResultsTheme.Accent, TextAlignmentOptions.Right, true);
            v.enableWordWrapping = false;
            ResultsUi.TopBand(v.rectTransform, y, RingRowHeight, 250, 26);
        }

        private void BuildControls(RectTransform root)
        {
            string[] labels = { "+", "-", "FIT" };
            float x = -34 - LegendWidth - 24;
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
            ResultsUi.Place(bar, new Vector2(0, 0), new Vector2(34, 34), new Vector2(380, 84));
            scaleBar = ResultsUi.Stretch(ResultsUi.Node("Bar", bar), 22, 16, 22, 50).gameObject.AddComponent<UiLines>();
            scaleLabel = ResultsUi.Text(bar, "Label", "", assets.BodyFont, 20, ResultsTheme.Text, TextAlignmentOptions.TopLeft);
            ResultsUi.Stretch(scaleLabel.rectTransform, 22, 40, 18, 12);

            TextMeshProUGUI hint = ResultsUi.Text(root, "Hint", "Drag to pan  ·  scroll to zoom", assets.BodyFont, 20,
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

            double outer = OuterRadius();
            Vector2 view = viewport.rect.size;
            float baseHeight = BaseSize().y;
            if (outer >= 0.25 * ImpactEffects.GlobalRange)
            {
                // Continental to planet-scale footprints: show the whole world.
                zoom = 1f;
            }
            else if (outer > 0 && view.y > 0)
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
            centre.x = Mathf.Repeat(centre.x, 1f); // east-west wraps round (see Build)
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

                if (layerOn[(int)EffectGroup.Blast])
                {
                    for (int i = report.Rings.Count - 1; i >= 0; i--)
                    {
                        RingRow r = report.Rings[i];
                        if (!r.Reached) continue;
                        Color col = ResultsTheme.Rings[r.Index % ResultsTheme.Rings.Length];
                        DrawRing(centreWrapped, cp, r.Radius * growth, size, col * new Color(1, 1, 1, 0.15f), col, 2.6f, false);
                    }
                }

                foreach (EffectRing r in report.EffectRings)
                {
                    if (!layerOn[(int)r.Group]) continue;
                    DrawRing(centreWrapped, cp, r.Radius * growth, size, Color.clear, ResultsTheme.EffectColor(r.Group, r.Level), 3f, true);
                }

                if (craterDiameter > 0 && layerOn[(int)EffectGroup.Blast])
                {
                    DrawRing(centreWrapped, cp, craterDiameter / 2.0 * growth, size, ResultsTheme.Crater * new Color(1, 1, 1, 0.85f), Color.white, 2f, false);
                }
            }

            overlay.Commit();
        }

        private void DrawRing(GeoPoint centre, Vector2 centrePx, double radius, Vector2 size, Color fill, Color edge, float width, bool dashed)
        {
            if (!(radius > 0))
            {
                return;
            }

            float pixelRadius = (float)(radius / MapProjection.MetresPerMapHeight) * size.y;
            bool whole = radius >= ImpactModel.MaxGroundRange * 0.999;
            int pole = Geodesy.EnclosedPole(centre, radius);
            bool bands = pixelRadius > 60f || pole != 0;
            if (fill.a > 0)
            {
                if (whole)
                {
                    overlay.Rect(new Rect(-size.x, 0, 3 * size.x, size.y), fill);
                }
                else if (bands)
                {
                    FillBands(centre, radius, size, fill);
                }
            }

            if (whole)
            {
                return; // the whole planet: there is no edge to draw
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

            for (int copy = -1; copy <= 1; copy++)
            {
                var shifted = new List<Vector2>(pts.Count);
                Vector2 offset = new Vector2(copy * size.x, 0);
                foreach (Vector2 p in pts) shifted.Add(p + offset);

                if (fill.a > 0 && !bands && pixelRadius > 1.5f)
                {
                    overlay.Fan(centrePx + offset, shifted, fill);
                }

                float w = Mathf.Min(width, Mathf.Max(1f, pixelRadius));
                if (dashed)
                {
                    overlay.DashedPolyline(shifted, w + 2.5f, 14f, 9f, new Color(0, 0, 0, 0.55f));
                    overlay.DashedPolyline(shifted, w, 14f, 9f, edge);
                }
                else
                {
                    overlay.Polyline(shifted, w, edge);
                }
            }
        }

        /// <summary>
        /// Fills the inside of a geodesic circle of any size as horizontal bands: on each parallel the inside is one
        /// longitude interval (<see cref="Geodesy.HalfWidthAtLatitude"/>), so circles that wrap round the map, contain a
        /// pole or even both poles (wider than a hemisphere) fill correctly.
        /// </summary>
        private void FillBands(GeoPoint centre, double radius, Vector2 size, Color fill)
        {
            double dDeg = radius / PhysicalConstants.EarthRadius * 180.0 / Math.PI;
            double latLo = Math.Max(-90.0, centre.Lat - dDeg);
            double latHi = Math.Min(90.0, centre.Lat + dDeg);
            float spanPx = (float)((latHi - latLo) / 180.0) * size.y;
            int count = Mathf.Clamp(Mathf.CeilToInt(spanPx / 4f), 8, 360);
            double cx = MapProjection.ToMap(centre, false).X * size.x;
            double pxPerDeg = size.x / 360.0;
            for (int i = 0; i < count; i++)
            {
                double la = latLo + (latHi - latLo) * i / count;
                double lb = latLo + (latHi - latLo) * (i + 1) / count;
                double wa = Geodesy.HalfWidthAtLatitude(centre, radius, la);
                double wb = Geodesy.HalfWidthAtLatitude(centre, radius, lb);
                if (wa < 0 && wb < 0) continue;
                wa = Math.Max(0.0, Math.Min(180.0, wa));
                wb = Math.Max(0.0, Math.Min(180.0, wb));
                float ya = (float)((la + 90.0) / 180.0) * size.y;
                float yb = (float)((lb + 90.0) / 180.0) * size.y;
                float ha = (float)(wa * pxPerDeg), hb = (float)(wb * pxPerDeg);
                for (int copy = -1; copy <= 1; copy++)
                {
                    float c = (float)cx + copy * size.x;
                    float h = Mathf.Max(ha, hb);
                    if (c + h < -size.x || c - h > 2 * size.x) continue;
                    overlay.Quad(new Vector2(c - ha, ya), new Vector2(c + ha, ya), new Vector2(c + hb, yb), new Vector2(c - hb, yb), fill);
                }
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
            for (double lon = Math.Ceiling(lonMin / step) * step; lon <= lonMax; lon += step)
            {
                float x = (float)((lon + 180.0) / 360.0) * size.x;
                overlay.Segment(new Vector2(x, 0), new Vector2(x, size.y), Math.Abs(Geo.WrapDegrees(lon)) < 1e-6 ? 2f : 1.2f, line);
            }

            for (double lat = Math.Ceiling(Math.Max(-90, latMin) / step) * step; lat <= Math.Min(90, latMax); lat += step)
            {
                float y = (float)((lat + 90.0) / 180.0) * size.y;
                overlay.Segment(new Vector2(-size.x, y), new Vector2(2 * size.x, y), lat == 0 ? 2f : 1.2f, line);
            }
        }

        private void DrawMarker(float time)
        {
            marker.Clear();
            if (report != null && report.HasLocation)
            {
                Vector2 size = ContentSize();
                Vector2 p0 = ToContent(report.Location, size, true);
                for (int copy = -1; copy <= 1; copy++)
                {
                    DrawMarkerAt(p0 + new Vector2(copy * size.x, 0), time);
                }
            }

            marker.Commit();
        }

        private void DrawMarkerAt(Vector2 p, float time)
        {
            {
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
            scaleLabel.text = ScaleText(nice) + "   <color=#B8B8CC>east-west at " +
                              Math.Abs(lat).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + "° " + (lat >= 0 ? "N" : "S") + "</color>";
        }

        private static string ScaleText(double metres) =>
            metres >= 1000
                ? (metres / 1000.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " km"
                : metres.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m";

        /// <summary>Largest radius among the visible layers (the crater counts with the blast layer).</summary>
        private double OuterRadius()
        {
            double outer = 0;
            if (layerOn[(int)EffectGroup.Blast])
            {
                outer = craterDiameter / 2.0;
                foreach (RingRow r in report.Rings) outer = Math.Max(outer, r.Radius);
            }

            foreach (EffectRing r in report.EffectRings)
            {
                if (layerOn[(int)r.Group]) outer = Math.Max(outer, r.Radius);
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
            TextMeshProUGUI title = ResultsUi.Heading(closeUp, "CLOSE-UP", assets, 30);
            ResultsUi.TopBand(title.rectTransform, 16, 36, 22, 22);
            TextMeshProUGUI sub = ResultsUi.Text(closeUp, "Sub", "to scale", assets.BodyFont, 20, ResultsTheme.Muted, TextAlignmentOptions.TopRight);
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
                if (!r.Reached || r.Radius > 3.0 * outer) continue; // far outside the inset
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
            TextMeshProUGUI label = ResultsUi.Text(closeUp, "ScaleLabel", ScaleText(barMetres), assets.BodyFont, 20, ResultsTheme.Text, TextAlignmentOptions.BottomLeft);
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
