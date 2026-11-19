using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Hud
{
    /// <summary>Fonts and sprites the HUD is built with.</summary>
    public sealed class HudAssets
    {
        /// <summary>Labels and small text.</summary>
        public TMP_FontAsset LabelFont;
        /// <summary>Numbers with units (needs lower case: "km/s", "MPa").</summary>
        public TMP_FontAsset ValueFont;
        /// <summary>Pixel/LED display font for titles, stage chips and buttons (upper case only).</summary>
        public TMP_FontAsset DisplayFont;
        public Sprite Panel;
        public Sprite Outline;
        public Sprite Disc;
        public Sprite Ring;

        /// <summary>Best effort at runtime (no AssetDatabase): TMP fonts from Resources, no sprites (plain quads).</summary>
        public static HudAssets LoadRuntime() => new HudAssets
        {
            LabelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset,
            ValueFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset,
            DisplayFont = Resources.Load<TMP_FontAsset>("UI/Fonts/HudDisplay SDF") ?? TMP_Settings.defaultFontAsset,
        };
    }

    /// <summary>
    /// Builds the HUD hierarchy from code. Used by the editor prefab builder (Tools > HUD > Build HUD prefabs,
    /// which saves Resources/UI/SpaceHud and AtmosphereHud) and as a runtime fallback when a prefab is missing.
    /// Layout is for a 1920x1080 reference canvas (Scale With Screen Size, match 0.5).
    /// </summary>
    public static class HudFactory
    {
        public const int UiLayer = 5;
        public const float Margin = 24f;
        public const float SidePanelWidth = 400f;
        public const float RowHeight = 30f;

        private static HudAssets a;

        public static SimulationHud Build(HudStage stage, HudAssets assets)
        {
            a = assets;
            var root = new GameObject(stage == HudStage.Space ? "SpaceHud" : "AtmosphereHud", typeof(RectTransform));
            root.layer = UiLayer;
            root.SetActive(false); // no Awake/OnEnable until everything is wired (matters for the runtime fallback)
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            SimulationHud hud = root.AddComponent<SimulationHud>();
            hud.stage = stage;
            hud.canvas = canvas;
            hud.menu = root.AddComponent<MainMenuButton>();

            RectTransform content = Rect("Content", root.transform);
            Stretch(content);
            hud.content = content.gameObject.AddComponent<CanvasGroup>();

            BuildTopBar(hud, content);
            BuildFlightPanel(hud, content, stage);
            if (stage == HudStage.Space) hud.launchPanel = BuildLaunchPanel(content);
            else hud.windCompass = BuildWindPanel(content);
            BuildTimeBar(hud, content);
            hud.toasts = BuildToasts(content);

            // Outside the content group so it stays visible while the HUD is hidden.
            TMP_Text hint = Text(root.transform, "HiddenHint", "Press H to show the HUD", a.LabelFont, 18, HudTheme.Dim, TextAlignmentOptions.BottomLeft);
            Place(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(Margin, Margin * 0.5f), new Vector2(400, 30));
            hint.gameObject.SetActive(false);
            hud.hiddenHint = hint.gameObject;

            hud.tooltip = BuildTooltip(root.transform, canvas);
            a = null;
            root.SetActive(true);
            return hud;
        }

        // ---------------------------------------------------------------- sections

        private static void BuildTopBar(SimulationHud hud, RectTransform parent)
        {
            RectTransform bar = Panel(parent, "StageBar", null, 860, new RectOffset(16, 16, 10, 10), 4);
            Place(bar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(860, 0));

            RectTransform row = Rect("Stages", bar);
            Layout(row, 44);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            string[] names = { "SPACE", "ATMOSPHERE", "IMPACT" };
            string[] tips =
            {
                "Stage 1: coasting through space towards the Earth.",
                "Stage 2: inside the atmosphere (below 120 km): drag, heating, ablation and break-up.",
                "Stage 3: the run has ended (impact, airburst or burn-up); the results screen follows.",
            };
            for (int i = 0; i < 3; i++)
            {
                if (i > 0)
                {
                    TMP_Text arrow = Text(row, "Arrow", ">", a.DisplayFont, 26, HudTheme.Label, TextAlignmentOptions.Center);
                    Layout(arrow.rectTransform, -1, 20);
                }

                RectTransform chip = Rect("Chip " + names[i], row);
                Layout(chip, -1, i == 1 ? 210 : 150);
                Image img = Sliced(chip.gameObject, a.Panel, HudTheme.ButtonOff, 14);
                hud.stageChips[i] = img;
                TMP_Text t = Text(chip, "Label", names[i], a.DisplayFont, 24, HudTheme.Dim, TextAlignmentOptions.Center);
                Stretch(t.rectTransform);
                hud.stageChipTexts[i] = t;
                Tip(chip.gameObject, tips[i]);
            }

            TMP_Text elapsed = Value(row, "Elapsed", "T+ --", 26, HudTheme.Value, TextAlignmentOptions.MidlineRight);
            var le = Layout(elapsed.rectTransform, -1, 220);
            le.flexibleWidth = 1;
            hud.elapsedValue = elapsed;
            Tip(elapsed.gameObject, "Simulated time since the start of the run (h:mm:ss; days when long).");

            TMP_Text mode = Text(bar, "Mode", "", a.LabelFont, 17, HudTheme.Label, TextAlignmentOptions.Center);
            Layout(mode.rectTransform, 24);
            hud.modeValue = mode;
        }

        private static void BuildFlightPanel(SimulationHud hud, RectTransform parent, HudStage stage)
        {
            RectTransform panel = Panel(parent, "FlightPanel", "FLIGHT DATA", SidePanelWidth, new RectOffset(16, 16, 12, 14), 2);
            Place(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(Margin, -Margin), new Vector2(SidePanelWidth, 0));

            hud.altitudeValue = Row(panel, "Altitude", "Altitude", "Height above mean sea level (Earth radius 6,371 km).", 38, 30);

            RectTransform barBack = Rect("AltitudeBar", panel);
            Layout(barBack, 12);
            Sliced(barBack.gameObject, a.Panel, HudTheme.BarBack, 40);
            RectTransform fill = Rect("Fill", barBack);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.3f, 1);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Sliced(fill.gameObject, a.Panel, HudTheme.BarFill, 40);
            hud.altitudeBarFill = fill;
            Tip(barBack.gameObject, "Progress through this stage: start point to the 120 km entry interface in space, 120 km to the ground in the atmosphere.");

            TMP_Text caption = Text(panel, "AltitudeCaption", "", a.LabelFont, 16, HudTheme.Dim, TextAlignmentOptions.MidlineLeft);
            Layout(caption.rectTransform, 22);
            caption.margin = new Vector4(8, 0, 0, 0);
            hud.altitudeBarCaption = caption;

            hud.atmosphereDistanceValue = Row(panel, "ToAtmosphere", "To atmosphere", "Distance left to the 120 km atmospheric entry interface.");
            hud.atmosphereDistanceRow = hud.atmosphereDistanceValue.transform.parent.gameObject;
            hud.speedValue = Row(panel, "Speed", "Speed", "Speed relative to the Earth's centre (km/s above 1 km/s).");
            hud.machValue = Row(panel, "Mach", "Mach", "Air speed divided by the local speed of sound (vacuum above 1,000 km).");
            hud.pressureValue = Row(panel, "DynamicPressure", "Ram pressure", "Dynamic pressure q = 1/2 rho v^2 on the front face. The body breaks up when rho v^2 (twice this value) exceeds the material strength.");
            hud.decelerationValue = Row(panel, "Deceleration", "Deceleration", "Drag deceleration in multiples of g (9.81 m/s^2), excluding gravity.");
            hud.temperatureValue = Row(panel, "Temperature", "Surface temp.", "Temperature of the heated surface layer (kelvin).");
            hud.massValue = Row(panel, "Mass", "Mass", "Remaining mass and the percentage of the starting mass left.");
            hud.massLossValue = Row(panel, "MassLoss", "Ablation", "Mass vaporised per second by atmospheric heating.");
            hud.brightnessValue = Row(panel, "Brightness", "Brightness", "Visible light power and absolute magnitude (as seen from 100 km). Lower magnitude = brighter; -4 is as bright as Venus.");
            hud.diameterValue = Row(panel, "Diameter", "Diameter", "Size of the body: equal-mass sphere while intact, spreading fragment cloud after break-up.");
            hud.structureValue = Row(panel, "Structure", "Structure", "Intact, or broken into a fragment cloud that spreads out (pancake model).");

            if (stage == HudStage.Atmosphere)
            {
                hud.atmosphereDistanceRow.SetActive(false);
            }
        }

        private static LaunchPanel BuildLaunchPanel(RectTransform parent)
        {
            const float width = 420f;
            RectTransform panel = Panel(parent, "LaunchPanel", "LAUNCH SETTINGS", width, new RectOffset(16, 16, 12, 14), 6);
            Place(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Margin, -Margin), new Vector2(width, 0));
            LaunchPanel lp = panel.gameObject.AddComponent<LaunchPanel>();

            // Collapse toggle sits in the header.
            Transform header = panel.Find("Header");
            Button collapse = MakeButton(header, "Collapse", "HIDE", 110, 30, 16, out TMP_Text collapseLabel);
            Place((RectTransform)collapse.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(110, 30));
            ((RectTransform)collapse.transform).pivot = new Vector2(1, 0.5f);
            lp.collapseButton = collapse;
            lp.collapseLabel = collapseLabel;
            Tip(collapse.gameObject, "Show or hide the launch settings.");

            RectTransform body = Rect("Body", panel);
            Column(body, 6);
            lp.body = body.gameObject;

            Caption(body, "Start mode");
            RectTransform modes = HRow(body, "Modes", 44, 8);
            lp.classicButton = MakeButton(modes, "Classic", "CLASSIC NEA", -1, 44, 20, out _, flexible: true);
            lp.realisticButton = MakeButton(modes, "Realistic", "REALISTIC ENTRY", -1, 44, 20, out _, flexible: true);
            Tip(lp.classicButton.gameObject, "Classic NEA: the meteor is released from rest at the input coordinates and falls under gravity.");
            Tip(lp.realisticButton.gameObject, "Realistic entry: the meteor is launched on a path that reaches the atmosphere (120 km) at the chosen speed and angle.");

            RectTransform realistic = Rect("RealisticRows", body);
            Column(realistic, 4);
            lp.realisticRows = realistic.gameObject;
            lp.speedSlider = LabelledSlider(realistic, "EntrySpeed", "Entry speed", out lp.speedValue,
                "Speed at the top of the atmosphere. Real meteoroids arrive at 11 to 72 km/s.");
            lp.angleSlider = LabelledSlider(realistic, "EntryAngle", "Entry angle", out lp.angleValue,
                "Angle below the horizontal at 120 km. 90 degrees is straight down; shallow entries travel further and slow more.");

            Caption(body, "Material");
            Cycler(body, "Material", 44, out lp.materialPrev, out lp.materialName, out lp.materialNext,
                "Composition. Custom uses the density, SHC and SCF from the input screen; presets use typical meteorite values.");
            TMP_Text info = Text(body, "MaterialInfo", "", a.LabelFont, 16, HudTheme.Dim, TextAlignmentOptions.Center);
            Layout(info.rectTransform, 22);
            lp.materialInfo = info;

            RectTransform mix = Rect("MixRows", body);
            Column(mix, 4);
            lp.mixRows = mix.gameObject;
            Cycler(mix, "MixA", 36, out lp.mixAPrev, out lp.mixAName, out lp.mixANext, "First component of the mix.");
            Cycler(mix, "MixB", 36, out lp.mixBPrev, out lp.mixBName, out lp.mixBNext, "Second component of the mix.");
            lp.mixSlider = LabelledSlider(mix, "MixFraction", "Mass fraction", out lp.mixValue,
                "Mass percentage of the first / second component. Density mixes by volume, strength geometrically.");

            RectTransform spacer = Rect("Spacer", body);
            Layout(spacer, 6);
            lp.launchButton = MakeButton(body, "Launch", "LAUNCH", -1, 56, 30, out lp.launchLabel);
            Tip(lp.launchButton.gameObject, "Start a new run with these settings from the start point (Space bar).");
            TMP_Text hint = Text(body, "Hint", "", a.LabelFont, 16, HudTheme.Dim, TextAlignmentOptions.Center);
            Layout(hint.rectTransform, 22);
            lp.hint = hint;
            return lp;
        }

        private static WindCompass BuildWindPanel(RectTransform parent)
        {
            const float width = 360f;
            RectTransform panel = Panel(parent, "WindPanel", "WIND", width, new RectOffset(16, 16, 12, 14), 6);
            Place(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Margin, -Margin), new Vector2(width, 0));
            WindCompass wc = panel.gameObject.AddComponent<WindCompass>();

            RectTransform holder = Rect("CompassHolder", panel);
            Layout(holder, 260);
            RectTransform area = Rect("Compass", holder);
            Place(area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 220));
            Image disc = Simple(area.gameObject, a.Disc, new Color(0.08f, 0f, 0f, 0.75f));
            disc.raycastTarget = true;
            RectTransform ring = Rect("Ring", area);
            Stretch(ring);
            Simple(ring.gameObject, a.Ring, HudTheme.PanelOutline).raycastTarget = false;
            RectTransform half = Rect("HalfRing", area);
            Place(half, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
            Simple(half.gameObject, a.Ring, new Color(1f, 0.3f, 0.2f, 0.45f)).raycastTarget = false;
            Line(area, "AxisNS", new Vector2(2, 220), new Color(1, 1, 1, 0.18f));
            Line(area, "AxisEW", new Vector2(220, 2), new Color(1, 1, 1, 0.18f));
            CompassLabel(area, "N", new Vector2(0, 126));
            CompassLabel(area, "S", new Vector2(0, -126));
            CompassLabel(area, "E", new Vector2(126, 0));
            CompassLabel(area, "W", new Vector2(-126, 0));
            TMP_Text scale = Text(area, "Scale", "50", a.LabelFont, 13, HudTheme.Dim, TextAlignmentOptions.Center);
            Place(scale.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(48, -48), new Vector2(40, 18));
            scale.raycastTarget = false;
            TMP_Text scaleMax = Text(area, "ScaleMax", "100 m/s", a.LabelFont, 13, HudTheme.Dim, TextAlignmentOptions.Center);
            Place(scaleMax.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96, -92), new Vector2(70, 18));
            scaleMax.raycastTarget = false;

            RectTransform needle = Rect("Needle", area);
            needle.anchorMin = needle.anchorMax = new Vector2(0.5f, 0.5f);
            needle.pivot = new Vector2(0, 0.5f);
            needle.anchoredPosition = Vector2.zero;
            needle.sizeDelta = new Vector2(60, 6);
            Image ni = needle.gameObject.AddComponent<Image>();
            ni.color = HudTheme.BarFill;
            ni.raycastTarget = false;

            RectTransform knob = Rect("Knob", area);
            Place(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
            Simple(knob.gameObject, a.Disc, Color.white).raycastTarget = false;

            wc.area = area;
            wc.needle = needle;
            wc.knob = knob;
            Tip(area.gameObject, "Drag to set the wind: direction = where the air blows towards (north up), distance from the centre = speed (edge = 100 m/s). Wind acts below 86 km.");

            TMP_Text readout = Value(panel, "Readout", "", 20, HudTheme.Value, TextAlignmentOptions.Center);
            Layout(readout.rectTransform, 30);
            wc.readout = readout;

            RectTransform buttons = HRow(panel, "Buttons", 38, 8);
            wc.calmButton = MakeButton(buttons, "Calm", "CALM", -1, 38, 18, out _, flexible: true);
            wc.resetButton = MakeButton(buttons, "Reset", "DEFAULT 15 M/S E", -1, 38, 18, out _, flexible: true);
            Tip(wc.calmButton.gameObject, "No wind.");
            Tip(wc.resetButton.gameObject, "Back to the default wind: 15 m/s towards the east.");
            return wc;
        }

        private static void BuildTimeBar(SimulationHud hud, RectTransform parent)
        {
            const float width = 980f;
            RectTransform bar = Panel(parent, "TimeBar", null, width, new RectOffset(16, 16, 10, 8), 4);
            Place(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(width, 0));
            bar.pivot = new Vector2(0.5f, 0);

            RectTransform row = HRow(bar, "Controls", 64, 10);
            hud.warpDownButton = MakeButton(row, "WarpDown", "-", 64, 64, 40, out _);
            hud.pauseButton = MakeButton(row, "PausePlay", "PAUSE", 210, 64, 32, out hud.pauseLabel);
            hud.warpUpButton = MakeButton(row, "WarpUp", "+", 64, 64, 40, out _);
            Tip(hud.warpDownButton.gameObject, "Slower time warp (- key).");
            Tip(hud.warpUpButton.gameObject, "Faster time warp (+ key).");
            Tip(hud.pauseButton.gameObject, "Pause / play (Space bar). Launches the run while it is waiting on the launch panel.");

            RectTransform warp = Rect("Warp", row);
            var le = Layout(warp, 64, 420);
            le.flexibleWidth = 1;
            Column(warp, 0);
            hud.warpValue = Text(warp, "WarpValue", "real x1", a.DisplayFont, 30, HudTheme.Value, TextAlignmentOptions.MidlineLeft);
            Layout(hud.warpValue.rectTransform, 36);
            hud.warpStatus = Text(warp, "WarpStatus", "", a.LabelFont, 18, HudTheme.Dim, TextAlignmentOptions.MidlineLeft);
            Layout(hud.warpStatus.rectTransform, 24);
            Tip(warp.gameObject, "Time warp: simulated seconds per real second. The physics step stays the same; faster warp takes more steps per frame. 'STEP CAP' means the computer cannot keep up and the actual rate is lower.");

            hud.menuButton = MakeButton(row, "Menu", "MENU", 130, 64, 26, out _);
            Tip(hud.menuButton.gameObject, "Pause and open the simulation menu (Esc).");

            TMP_Text legend = Text(bar, "Legend", "", a.LabelFont, 17, HudTheme.Label, TextAlignmentOptions.Center);
            Layout(legend.rectTransform, 24);
            hud.legendText = legend;
        }

        private static ToastFeed BuildToasts(RectTransform parent)
        {
            RectTransform area = Rect("Toasts", parent);
            Place(area, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(520, 0));
            area.pivot = new Vector2(0.5f, 1);
            Column(area, 8);
            var fitter = area.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ToastFeed feed = area.gameObject.AddComponent<ToastFeed>();
            feed.container = area;

            RectTransform card = Rect("ToastTemplate", area);
            Background(card, a.Panel, new Color(0.1f, 0f, 0f, 0.82f), 16);
            card.gameObject.AddComponent<CanvasGroup>();
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(26, 16, 8, 10);
            vlg.spacing = 0;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            RectTransform accent = Rect("Accent", card);
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            accent.anchorMin = new Vector2(0, 0);
            accent.anchorMax = new Vector2(0, 1);
            accent.pivot = new Vector2(0, 0.5f);
            accent.offsetMin = new Vector2(8, 8);
            accent.offsetMax = new Vector2(16, -8);
            accent.gameObject.AddComponent<Image>().color = HudTheme.Info;
            TMP_Text title = Text(card, "Title", "EVENT", a.DisplayFont, 28, HudTheme.Info, TextAlignmentOptions.MidlineLeft);
            Layout(title.rectTransform, 34);
            TMP_Text message = Text(card, "Message", "", a.LabelFont, 18, HudTheme.Value, TextAlignmentOptions.MidlineLeft);
            Layout(message.rectTransform, 24);
            card.gameObject.SetActive(false);
            feed.template = card.gameObject;
            return feed;
        }

        private static HudTooltip BuildTooltip(Transform root, Canvas canvas)
        {
            RectTransform holder = Rect("Tooltip", root);
            Stretch(holder);
            HudTooltip tip = holder.gameObject.AddComponent<HudTooltip>();
            RectTransform panel = Rect("Panel", holder);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0, 1);
            Background(panel, a.Panel, new Color(0.06f, 0f, 0f, 0.94f), 20).raycastTarget = false;
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 8, 10);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.sizeDelta = new Vector2(360, 0);
            TMP_Text label = Text(panel, "Text", "", a.LabelFont, 17, HudTheme.Value, TextAlignmentOptions.TopLeft);
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            tip.panel = panel;
            tip.label = label;
            tip.canvas = canvas;
            panel.gameObject.SetActive(false);
            return tip;
        }

        // ---------------------------------------------------------------- building blocks

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UiLayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMin.y);
            if (anchorMin == new Vector2(0.5f, 0.5f)) rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static LayoutElement Layout(RectTransform rt, float height, float width = -1)
        {
            LayoutElement le = rt.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            if (height >= 0) le.preferredHeight = le.minHeight = height;
            if (width >= 0) le.preferredWidth = le.minWidth = width;
            return le;
        }

        private static VerticalLayoutGroup Column(RectTransform rt, float spacing)
        {
            var vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return vlg;
        }

        private static RectTransform HRow(Transform parent, string name, float height, float spacing)
        {
            RectTransform row = Rect(name, parent);
            Layout(row, height);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            return row;
        }

        private static Image Sliced(GameObject go, Sprite sprite, Color color, float pixelsPerUnitMultiplier)
        {
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            img.color = color;
            return img;
        }

        /// <summary>
        /// Sliced background on a child that the layout ignores. An Image directly on a layout group reports the
        /// sprite's 9-slice border (256 px) as preferred size, which would inflate small panels.
        /// </summary>
        private static Image Background(RectTransform parent, Sprite sprite, Color color, float pixelsPerUnitMultiplier)
        {
            RectTransform bg = Rect("Background", parent);
            bg.SetAsFirstSibling();
            Stretch(bg);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return Sliced(bg.gameObject, sprite, color, pixelsPerUnitMultiplier);
        }

        private static Image Simple(GameObject go, Sprite sprite, Color color)
        {
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = color;
            return img;
        }

        private static void Line(RectTransform parent, string name, Vector2 size, Color color)
        {
            RectTransform rt = Rect(name, parent);
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void CompassLabel(RectTransform parent, string letter, Vector2 position)
        {
            TMP_Text t = Text(parent, letter, letter, a.DisplayFont, 22, HudTheme.Label, TextAlignmentOptions.Center);
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(30, 30));
            t.raycastTarget = false;
        }

        private static TextMeshProUGUI Value(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI t = Text(parent, name, text, a.ValueFont, size, color, alignment);
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            if (font != null && a != null && font == a.DisplayFont && font != a.ValueFont)
            {
                // The LED font is a dynamic TMP asset in the frozen TextMesh Pro folder. Rendering upper case only
                // uses glyphs it already has, so TMP does not add (and save) new glyphs to it.
                t.fontStyle = FontStyles.UpperCase;
            }

            return t;
        }

        /// <summary>Translucent red 9-slice panel with outline, optional header, vertical layout and auto height.</summary>
        private static RectTransform Panel(Transform parent, string name, string title, float width, RectOffset padding, float spacing)
        {
            RectTransform panel = Rect(name, parent);
            panel.sizeDelta = new Vector2(width, 0);
            Background(panel, a.Panel, HudTheme.PanelFill, 9);
            RectTransform outline = Rect("Outline", panel);
            Stretch(outline);
            outline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Sliced(outline.gameObject, a.Outline, HudTheme.PanelOutline, 9).raycastTarget = false;

            VerticalLayoutGroup vlg = Column(panel, spacing);
            vlg.padding = padding;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (title != null)
            {
                RectTransform header = Rect("Header", panel);
                Layout(header, 40);
                Sliced(header.gameObject, a.Panel, HudTheme.Header, 16);
                TMP_Text t = Text(header, "Title", title, a.DisplayFont, 26, HudTheme.Value, TextAlignmentOptions.MidlineLeft);
                Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(14, 0);
                RectTransform gap = Rect("Gap", panel);
                Layout(gap, 4);
            }

            return panel;
        }

        /// <summary>Label on the left, value on the right; returns the value text.</summary>
        private static TMP_Text Row(RectTransform panel, string name, string label, string tooltip, float height = RowHeight, float valueSize = 24)
        {
            RectTransform row = Rect(name, panel);
            Layout(row, height);
            Image shade = row.gameObject.AddComponent<Image>();
            shade.color = HudTheme.RowShade;
            shade.raycastTarget = true; // for the tooltip
            TMP_Text l = Text(row, "Label", label, a.LabelFont, 19, HudTheme.Label, TextAlignmentOptions.MidlineLeft);
            l.rectTransform.anchorMin = new Vector2(0, 0);
            l.rectTransform.anchorMax = new Vector2(0.38f, 1);
            l.rectTransform.offsetMin = new Vector2(8, 0);
            l.rectTransform.offsetMax = Vector2.zero;
            TMP_Text v = Value(row, "Value", "--", valueSize, HudTheme.Value, TextAlignmentOptions.MidlineRight);
            v.rectTransform.anchorMin = new Vector2(0.36f, 0);
            v.rectTransform.anchorMax = new Vector2(1, 1);
            v.rectTransform.offsetMin = Vector2.zero;
            v.rectTransform.offsetMax = new Vector2(-8, 0);
            v.enableAutoSizing = true;
            v.fontSizeMin = 14;
            v.fontSizeMax = valueSize;
            Tip(row.gameObject, label + ": " + tooltip);
            return v;
        }

        private static void Caption(RectTransform parent, string text)
        {
            TMP_Text t = Text(parent, "Caption " + text, text.ToUpperInvariant(), a.LabelFont, 16, HudTheme.Dim, TextAlignmentOptions.BottomLeft);
            Layout(t.rectTransform, 20);
        }

        public static Button MakeButton(Transform parent, string name, string label, float width, float height, float fontSize,
            out TMP_Text text, bool flexible = false)
        {
            RectTransform rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(width > 0 ? width : 100, height);
            LayoutElement le = Layout(rt, height, width);
            if (flexible) le.flexibleWidth = 1;
            Image img = Sliced(rt.gameObject, a.Panel, HudTheme.Button, 14);
            RectTransform outline = Rect("Outline", rt);
            Stretch(outline);
            Sliced(outline.gameObject, a.Outline, new Color(0.25f, 0f, 0f, 0.9f), 14).raycastTarget = false;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.6f, 0.6f, 1f);
            colors.disabledColor = new Color(0.55f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            text = Text(rt, "Label", label, a.DisplayFont, fontSize, HudTheme.Value, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Min(12, fontSize);
            text.margin = new Vector4(6, 0, 6, 0);
            return button;
        }

        private static void Cycler(RectTransform parent, string name, float height, out Button prev, out TMP_Text value, out Button next, string tooltip)
        {
            RectTransform row = HRow(parent, name, height, 6);
            prev = MakeButton(row, "Prev", "<", height, height, 26, out _);
            RectTransform mid = Rect("Value", row);
            Layout(mid, height).flexibleWidth = 1;
            Sliced(mid.gameObject, a.Panel, new Color(0, 0, 0, 0.35f), 14);
            value = Text(mid, "Text", "", a.LabelFont, 19, HudTheme.Value, TextAlignmentOptions.Center);
            Stretch(value.rectTransform);
            value.enableAutoSizing = true;
            value.fontSizeMin = 12;
            value.fontSizeMax = 19;
            value.margin = new Vector4(6, 0, 6, 0);
            next = MakeButton(row, "Next", ">", height, height, 26, out _);
            Tip(mid.gameObject, tooltip);
            mid.GetComponent<Image>().raycastTarget = true;
        }

        private static Slider LabelledSlider(RectTransform parent, string name, string label, out TMP_Text value, string tooltip)
        {
            RectTransform block = Rect(name, parent);
            Layout(block, 54);
            RectTransform top = Rect("Top", block);
            top.anchorMin = new Vector2(0, 1);
            top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.sizeDelta = new Vector2(0, 26);
            top.anchoredPosition = Vector2.zero;
            TMP_Text l = Text(top, "Label", label, a.LabelFont, 18, HudTheme.Label, TextAlignmentOptions.MidlineLeft);
            Stretch(l.rectTransform);
            value = Value(top, "Value", "", 20, HudTheme.Value, TextAlignmentOptions.MidlineRight);
            Stretch(value.rectTransform);

            RectTransform sliderRt = Rect("Slider", block);
            sliderRt.anchorMin = new Vector2(0, 0);
            sliderRt.anchorMax = new Vector2(1, 0);
            sliderRt.pivot = new Vector2(0.5f, 0);
            sliderRt.sizeDelta = new Vector2(-12, 24);
            sliderRt.anchoredPosition = new Vector2(0, 2);
            Slider slider = sliderRt.gameObject.AddComponent<Slider>();

            RectTransform bg = Rect("Background", sliderRt);
            bg.anchorMin = new Vector2(0, 0.3f);
            bg.anchorMax = new Vector2(1, 0.7f);
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            Sliced(bg.gameObject, a.Panel, HudTheme.BarBack, 40);

            RectTransform fillArea = Rect("Fill Area", sliderRt);
            fillArea.anchorMin = new Vector2(0, 0.3f);
            fillArea.anchorMax = new Vector2(1, 0.7f);
            fillArea.offsetMin = new Vector2(0, 0);
            fillArea.offsetMax = new Vector2(0, 0);
            RectTransform fill = Rect("Fill", fillArea);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Sliced(fill.gameObject, a.Panel, HudTheme.BarFill, 40).raycastTarget = false;

            RectTransform handleArea = Rect("Handle Slide Area", sliderRt);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10, 0);
            handleArea.offsetMax = new Vector2(-10, 0);
            RectTransform handle = Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(22, 0);
            Image hi = Simple(handle.gameObject, a.Panel, Color.white);
            hi.preserveAspect = false;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = hi;
            slider.direction = Slider.Direction.LeftToRight;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            Tip(block.gameObject, label + ": " + tooltip);
            Image hit = block.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            return slider;
        }

        private static void Tip(GameObject go, string text)
        {
            TooltipTarget t = go.GetComponent<TooltipTarget>();
            if (t == null) t = go.AddComponent<TooltipTarget>();
            t.text = text;
        }
    }
}
