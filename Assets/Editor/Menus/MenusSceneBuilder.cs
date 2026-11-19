using System.Collections.Generic;
using System.IO;
using MTS.Core;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MTS.Menus.Editor
{
    /// <summary>
    /// Rebuilds the main menu (MainMenu), the pause menu (MainMenu2) and the input screen (VariableInput) from code,
    /// so the layout is reproducible:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod MTS.Menus.Editor.MenusSceneBuilder.BuildAll
    /// Art is copied into Assets/Art/Menus (the original 9-slice sprites and cover image stay untouched).
    /// Layout: 1920x1080 reference, Canvas Scaler scale with screen size, match 0.5; 48 px screen margins,
    /// 24 px card padding, 16 px gaps; titles centred, labels left-aligned, inputs on a common column.
    /// </summary>
    public static class MenusSceneBuilder
    {
        public const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
        public const string PauseMenuScene = "Assets/Scenes/MainMenu2.unity";
        public const string InputScene = "Assets/Scenes/VariableInput.unity";
        public const string ArtFolder = "Assets/Art/Menus";

        private const string BoxFillPath = ArtFolder + "/MenuBoxFill.png";
        private const string BoxOutlinePath = ArtFolder + "/MenuBoxOutline.png";
        private const string BackgroundPath = ArtFolder + "/MenuBackground.jpg";
        private const string BodyFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string PixelFontPath = "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset";

        private const string LabelHex = "#FF7A70";
        private const string AccentHex = "#FFCC47";

        [MenuItem("Tools/Menus/Rebuild All Menu Scenes")]
        public static void BuildAll()
        {
            BuildMainMenu();
            BuildPauseMenu();
            BuildInputScreen();
        }

        // =====================================================================================================
        // Shared scaffolding
        // =====================================================================================================

        private sealed class Ctx
        {
            public MenuAssets A;
            public RectTransform Root;
            public Scene Scene;
        }

        private static Ctx NewScene(string canvasName)
        {
            // New scene first: opening a scene unloads unused assets, which would invalidate fonts loaded before it.
            var ctx = new Ctx { Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single) };
            ctx.A = PrepareArt();

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f, 1f);
            cam.orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);
            cameraGo.AddComponent<AudioListener>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            var canvasGo = new GameObject(canvasName, typeof(RectTransform));
            canvasGo.layer = 5;
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;
            canvasGo.AddComponent<GraphicRaycaster>();
            ctx.Root = (RectTransform)canvasGo.transform;

            RectTransform holder = MenuUi.Stretch(MenuUi.Node("Background", ctx.Root));
            Image bg = MenuUi.Image(holder, "Sky", ctx.A.Background, Color.white, false);
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = ctx.A.Background != null ? ctx.A.Background.rect.width / ctx.A.Background.rect.height : 16f / 9f;
            Image dim = MenuUi.Image(holder, "Dim", null, new Color(0.01f, 0.01f, 0.04f, 0.5f), false);
            MenuUi.Stretch(dim.rectTransform);
            return ctx;
        }

        private static void Save(Ctx ctx, string path)
        {
            EditorSceneManager.MarkSceneDirty(ctx.Scene);
            EditorSceneManager.SaveScene(ctx.Scene, path);
            AssetDatabase.SaveAssets();
            Debug.Log("Menu scene rebuilt: " + path);
        }

        /// <summary>Centred title in a red pixel box.</summary>
        private static TextMeshProUGUI TitleBox(RectTransform parent, MenuAssets a, string text, float y, float width, float height = 96f,
            float size = MenuTheme.Title)
        {
            RectTransform box = MenuUi.PixelBox(parent, "Title", a, MenuTheme.Red, MenuTheme.DarkRed);
            MenuUi.Place(box, new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(width, height));
            TextMeshProUGUI t = MenuUi.Text(box, "Text", text, a.PixelFont, size, Color.white, TextAlignmentOptions.Center);
            t.enableWordWrapping = false;
            MenuUi.Stretch(t.rectTransform, 24, 6, 24, 6);
            return t;
        }

        private static TextMeshProUGUI Label(Transform parent, MenuAssets a, string text, float x, float y, float width, float height = MenuTheme.RowHeight)
        {
            TextMeshProUGUI t = MenuUi.Text(parent, "Label", text, a.BodyFont, MenuTheme.LabelSize, MenuTheme.Label, TextAlignmentOptions.Left);
            t.enableWordWrapping = false;
            MenuUi.TopLeft(t.rectTransform, x, y, width, height);
            return t;
        }

        private static TextMeshProUGUI Value(Transform parent, MenuAssets a, string text, float x, float y, float width, float height = MenuTheme.RowHeight,
            TextAlignmentOptions align = TextAlignmentOptions.Left, float size = MenuTheme.Body)
        {
            TextMeshProUGUI t = MenuUi.Text(parent, "Value", text, a.BodyFont, size, MenuTheme.Text, align);
            t.enableWordWrapping = false;
            MenuUi.TopLeft(t.rectTransform, x, y, width, height);
            return t;
        }

        private static TextMeshProUGUI CardHeading(Transform card, MenuAssets a, string text)
        {
            TextMeshProUGUI h = MenuUi.Heading(card, text, a);
            h.alignment = TextAlignmentOptions.Left;
            MenuUi.TopBand(h.rectTransform, 18, 44, MenuTheme.Padding, MenuTheme.Padding);
            return h;
        }

        private static Button ButtonAt(Transform parent, MenuAssets a, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size,
            float fontSize = MenuTheme.Button)
        {
            Button b = MenuUi.PixelButton(parent, name, label, a, fontSize);
            MenuUi.Place((RectTransform)b.transform, anchor, pos, size);
            return b;
        }

        // =====================================================================================================
        // Main menu and pause menu
        // =====================================================================================================

        public static void BuildMainMenu()
        {
            Ctx ctx = NewScene("Main Menu Canvas");
            MenuAssets a = ctx.A;
            var navigation = ctx.Root.gameObject.AddComponent<MainMenuButton>();
            var links = ctx.Root.gameObject.AddComponent<Links>();
            var screen = ctx.Root.gameObject.AddComponent<MenuScreen>();

            RectTransform main = MenuUi.Stretch(MenuUi.Node("Main", ctx.Root));
            TitleBox(main, a, "METEOR TRAJECTORY SIMULATOR", 200, 1200, 120);
            TextMeshProUGUI sub = MenuUi.Text(main, "Subtitle",
                "Follow a meteoroid from space, through the atmosphere, to the ground.", a.BodyFont, 28, MenuTheme.Text, TextAlignmentOptions.Center);
            MenuUi.Place(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -348), new Vector2(1200, 44));

            Button start = MenuButton(main, a, "StartButton", "START SIMULATION", 0);
            Button info = MenuButton(main, a, "InformationButton", "MORE INFORMATION", 1);
            Button exit = MenuButton(main, a, "ExitButton", "EXIT PROGRAM", 2);
            UnityEventTools.AddStringPersistentListener(start.onClick, navigation.MainMenu, "VariableInput");
            UnityEventTools.AddPersistentListener(info.onClick, screen.OpenInformation);
            UnityEventTools.AddPersistentListener(exit.onClick, navigation.ExitProgram);

            Footer(main, a, "Earth imagery: NASA Earth Observatory.  Built in Unity.");

            screen.mainButtons = main.gameObject;
            screen.informationPage = BuildInformationPage(ctx.Root, a, links, screen).gameObject;
            Save(ctx, MainMenuScene);
        }

        public static void BuildPauseMenu()
        {
            Ctx ctx = NewScene("Pause Menu Canvas");
            MenuAssets a = ctx.A;
            var navigation = ctx.Root.gameObject.AddComponent<MainMenuButton>();
            var links = ctx.Root.gameObject.AddComponent<Links>();
            var screen = ctx.Root.gameObject.AddComponent<MenuScreen>();

            RectTransform main = MenuUi.Stretch(MenuUi.Node("Main", ctx.Root));
            TitleBox(main, a, "SIMULATION PAUSED", 200, 900, 120);
            TextMeshProUGUI stage = MenuUi.Text(main, "Stage", "Paused during stage 1: space", a.BodyFont, 28, MenuTheme.Text, TextAlignmentOptions.Center);
            MenuUi.Place(stage.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -348), new Vector2(1200, 44));
            screen.stageText = stage;

            Button resume = MenuButton(main, a, "ContinueButton", "CONTINUE", 0);
            Button restart = MenuButton(main, a, "RestartButton", "RESTART", 1);
            Button info = MenuButton(main, a, "InformationButton", "MORE INFORMATION", 2);
            Button exit = MenuButton(main, a, "ExitButton", "EXIT PROGRAM", 3);
            UnityEventTools.AddPersistentListener(resume.onClick, navigation.Continue);
            UnityEventTools.AddStringPersistentListener(restart.onClick, navigation.Restart, "VariableInput");
            UnityEventTools.AddPersistentListener(info.onClick, screen.OpenInformation);
            UnityEventTools.AddPersistentListener(exit.onClick, navigation.ExitProgram);

            Footer(main, a, "Restart goes back to the setup screen with your current values.");

            screen.mainButtons = main.gameObject;
            screen.informationPage = BuildInformationPage(ctx.Root, a, links, screen).gameObject;
            Save(ctx, PauseMenuScene);
        }

        private static Button MenuButton(RectTransform parent, MenuAssets a, string name, string label, int index) =>
            ButtonAt(parent, a, name, label, new Vector2(0.5f, 1), new Vector2(0, -452 - index * 112), new Vector2(560, 88), 32);

        private static void Footer(RectTransform parent, MenuAssets a, string text)
        {
            TextMeshProUGUI t = MenuUi.Text(parent, "Footer", text, a.BodyFont, MenuTheme.Caption, MenuTheme.Muted, TextAlignmentOptions.Center);
            MenuUi.Place(t.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(1400, 32));
        }

        // ---- More Information page (same page in both menus) ----

        private static RectTransform BuildInformationPage(RectTransform root, MenuAssets a, Links links, MenuScreen screen)
        {
            RectTransform page = MenuUi.Stretch(MenuUi.Node("Information Page", root));
            Image blocker = MenuUi.Image(page, "Dim", null, new Color(0.01f, 0.01f, 0.04f, 0.55f), false);
            blocker.raycastTarget = true;
            MenuUi.Stretch(blocker.rectTransform);

            RectTransform card = MenuUi.Card(page, "Card", a);
            MenuUi.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(1824, 952));
            TitleBox(page, a, "MORE INFORMATION", 40, 640, 84, 40);

            Image split = MenuUi.Image(card, "ColumnDivider", null, MenuTheme.Divider, false);
            split.rectTransform.anchorMin = new Vector2(0.5f, 0);
            split.rectTransform.anchorMax = new Vector2(0.5f, 1);
            split.rectTransform.offsetMin = new Vector2(-1, 112);
            split.rectTransform.offsetMax = new Vector2(1, -76);

            RectTransform left = Column(card, "Left", 0f, 0.5f);
            RectTransform right = Column(card, "Right", 0.5f, 1f);

            Section(left, a, "WHAT IT DOES");
            Body(left, a, "Follow a meteoroid from deep space to the ground. Load a famous impact or design your own, then watch it fall, " +
                          "heat up, lose mass and perhaps break apart, and see what it would do on arrival.");
            Gap(left);
            Section(left, a, "THE STAGES");
            Body(left, a, Bold("Setup") + "  Pick a scenario or set the mass, up to four materials, the start position and how it " +
                          "starts: From rest (dropped) or Inbound entry (already moving).");
            Body(left, a, Bold("Stage 1: Space") + "  The meteoroid falls towards the Earth under gravity. Speed time up, pause or " +
                          "switch cameras while it travels.");
            Body(left, a, Bold("Stage 2: Atmosphere") + "  Below 120 km the air slows and heats it: it glows, ablates and may break up " +
                          "into an airburst. Wind pushes it off course.");
            Body(left, a, Bold("Stage 3: Results") + "  The landing or burst point on a map, the crater, blast and heat rings, and " +
                          "charts of the whole flight.");
            Gap(left);
            Section(left, a, "CONTROLS");
            Shortcut(left, a, "Space", "Pause or resume");
            Shortcut(left, a, "+  /  -", "Faster or slower time");
            Shortcut(left, a, "1  2  3", "Switch camera");
            Shortcut(left, a, "H", "Hide or show the display");
            Shortcut(left, a, "Esc", "Pause menu (closes this page here)");
            Shortcut(left, a, "1 to 4", "Results tabs");

            Section(right, a, "THE PHYSICS");
            Topic(right, a, "Gravity", "Newtonian gravity, stepped with the fourth-order Runge-Kutta (RK4) method for an accurate path.",
                ("RK4", links.RK4));
            Topic(right, a, "Air and drag", "Air density and temperature from the US Standard Atmosphere 1976; drag from a sphere " +
                                            "drag-coefficient fit.", ("DRAG", links.DragCof), ("ATMOSPHERE", links.StandardAtmosphere));
            Topic(right, a, "Ablation", "Heating melts and vaporises the surface, so the meteoroid shrinks and glows as it falls.",
                ("ABLATION", links.Ablation));
            Topic(right, a, "Break-up and airburst", "When the air pressure on it beats the material's strength it breaks up and spreads " +
                                                     "(pancake model). Spreading fast means an airburst.", ("PANCAKE", links.PancakeModel));
            Topic(right, a, "Impact effects", "Crater size, blast overpressure and heat follow the Earth Impact Effects Program " +
                                              "(Collins et al. 2005).", ("EIEP", links.ImpactEffects), ("OVERPRESSURE", links.Overpressure));
            Gap(right);
            Topic(right, a, "CREDITS", "Earth imagery: NASA Earth Observatory (Blue Marble and Black Marble), public domain. " +
                                       "Two TED talks on asteroid impacts for further watching.",
                ("NASA", links.NasaImagery), ("TED 1", links.TedTalk1), ("TED 2", links.TedTalk2));

            Button back = ButtonAt(card, a, "BackButton", "BACK", new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(300, 72), 30);
            UnityEventTools.AddPersistentListener(back.onClick, screen.CloseInformation);
            page.gameObject.SetActive(false);
            return page;
        }

        private static string Bold(string s) => "<b><color=" + LabelHex + ">" + s + "</color></b>";

        private static RectTransform Column(RectTransform card, string name, float from, float to)
        {
            RectTransform col = MenuUi.Node(name, card);
            col.anchorMin = new Vector2(from, 0);
            col.anchorMax = new Vector2(to, 1);
            col.pivot = new Vector2(0.5f, 1);
            col.offsetMin = new Vector2(from == 0 ? 40 : 32, 120);
            col.offsetMax = new Vector2(to == 1 ? -40 : -32, -72);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return col;
        }

        private static void Section(RectTransform col, MenuAssets a, string text)
        {
            TextMeshProUGUI h = MenuUi.Heading(col, text, a);
            h.margin = new Vector4(0, 0, 0, 2);
        }

        private static TextMeshProUGUI Body(Transform col, MenuAssets a, string text, float size = MenuTheme.BodySmall)
        {
            TextMeshProUGUI t = MenuUi.Text(col, "Body", text, a.BodyFont, size, MenuTheme.Text);
            t.lineSpacing = 4;
            return t;
        }

        private static void Gap(RectTransform col)
        {
            var le = MenuUi.Node("Gap", col).gameObject.AddComponent<LayoutElement>();
            le.minHeight = 12;
            le.preferredHeight = 12;
        }

        private static void Shortcut(RectTransform col, MenuAssets a, string key, string action)
        {
            RectTransform row = MenuUi.Node("Shortcut", col);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            TextMeshProUGUI k = MenuUi.Text(row, "Key", key, a.BodyFont, MenuTheme.BodySmall, MenuTheme.Accent, TextAlignmentOptions.Left, true);
            k.enableWordWrapping = false;
            var kle = k.gameObject.AddComponent<LayoutElement>();
            kle.minWidth = 150;
            kle.preferredWidth = 150;
            TextMeshProUGUI v = MenuUi.Text(row, "Action", action, a.BodyFont, MenuTheme.BodySmall, MenuTheme.Text);
            v.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        /// <summary>A titled paragraph with its link buttons on the title row (right-aligned).</summary>
        private static void Topic(RectTransform col, MenuAssets a, string title, string text, params (string Label, UnityAction Action)[] buttons)
        {
            RectTransform head = MenuUi.Node("Topic", col);
            var h = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            bool section = title.ToUpperInvariant() == title;
            TextMeshProUGUI t = section
                ? MenuUi.Heading(head, title, a)
                : MenuUi.Text(head, "Title", title, a.BodyFont, MenuTheme.Body, MenuTheme.Label, TextAlignmentOptions.Left, true);
            t.enableWordWrapping = false;
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            foreach ((string label, UnityAction action) in buttons)
            {
                Button b = MenuUi.PixelButton(head, "Link " + label, label, a, 20, MenuUi.SmallBoxPixelScale);
                TMP_Text bt = b.GetComponentInChildren<TMP_Text>();
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = Mathf.Max(96, bt.GetPreferredValues(label).x + 36);
                le.preferredHeight = 44;
                le.minHeight = 44;
                UnityEventTools.AddPersistentListener(b.onClick, action);
            }

            Body(col, a, text);
        }

        // =====================================================================================================
        // Input screen
        // =====================================================================================================

        private const float LeftX = MenuTheme.Margin;
        private const float CardsTop = 302f;
        private const float CardsHeight = 646f;
        private const float LeftWidth = 1000f;
        private const float RightX = LeftX + LeftWidth + 24f;
        private const float RightWidth = 1920f - MenuTheme.Margin - RightX;

        public static void BuildInputScreen()
        {
            Ctx ctx = NewScene("Setup Canvas");
            MenuAssets a = ctx.A;
            GameObject host = ctx.Root.gameObject;
            var screen = host.AddComponent<VariableInputScreen>();
            screen.navigation = host.AddComponent<MainMenuButton>();
            screen.massInput = host.AddComponent<MassRangeInput>();
            screen.xcord = host.AddComponent<Xcord>();
            screen.ycord = host.AddComponent<Ycord>();
            screen.zcord = host.AddComponent<Zcord>();

            TitleBox(ctx.Root, a, "SIMULATION SETUP", 32, 720, 92, 44);
            BuildScenarioCard(ctx.Root, a, screen);
            BuildCompositionCard(ctx.Root, a, screen);
            BuildMassAndStartCard(ctx.Root, a, screen);

            screen.backButton = ButtonAt(ctx.Root, a, "BackButton", "BACK", new Vector2(0, 0), new Vector2(LeftX, 28), new Vector2(260, 76), 32);
            screen.startButton = ButtonAt(ctx.Root, a, "CommenceButton", "COMMENCE SIMULATION", new Vector2(1, 0), new Vector2(-MenuTheme.Margin, 28),
                new Vector2(560, 76), 32);
            TextMeshProUGUI hint = MenuUi.Text(ctx.Root, "Hint", "Values are kept when you come back to this screen.", a.BodyFont,
                MenuTheme.Caption, MenuTheme.Muted, TextAlignmentOptions.Center);
            MenuUi.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(-40, 48), new Vector2(680, 36));

            // Preview the defaults in the saved scene (the screen reloads the session's values at run time).
            screen.Wire();
            screen.LoadFromSession();
            Save(ctx, InputScene);
        }

        private static void BuildScenarioCard(RectTransform root, MenuAssets a, VariableInputScreen screen)
        {
            RectTransform card = MenuUi.Card(root, "ScenarioCard", a);
            MenuUi.TopBand(card, 150, 136, LeftX, MenuTheme.Margin);
            CardHeading(card, a, "SCENARIO");

            IReadOnlyList<ImpactScenario> all = ImpactScenarios.All;
            int count = all.Count + 1;
            const float chipsX = 220f;
            float inner = 1920f - 2 * MenuTheme.Margin - chipsX - MenuTheme.Padding;
            float width = (inner - (count - 1) * 12f) / count;
            var buttons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                string label = i == 0 ? "CUSTOM" : VariableInputScreen.ShortName(all[i - 1]);
                Button b = MenuUi.PixelButton(card, "Scenario " + label, label, a, MenuTheme.SmallButton, MenuUi.SmallBoxPixelScale);
                MenuUi.TopLeft((RectTransform)b.transform, chipsX + i * (width + 12f), 18, width, 52);
                buttons[i] = b;
            }

            screen.scenarioButtons = buttons;
            TextMeshProUGUI desc = MenuUi.Text(card, "Description", "", a.BodyFont, MenuTheme.BodySmall, MenuTheme.Text, TextAlignmentOptions.Left);
            desc.enableWordWrapping = false;
            desc.overflowMode = TextOverflowModes.Ellipsis;
            MenuUi.TopBand(desc.rectTransform, 80, 40, MenuTheme.Padding, MenuTheme.Padding);
            screen.scenarioDescription = desc;
        }

        private static void BuildCompositionCard(RectTransform root, MenuAssets a, VariableInputScreen screen)
        {
            RectTransform card = MenuUi.Card(root, "CompositionCard", a);
            MenuUi.TopLeft(card, LeftX, CardsTop, LeftWidth, CardsHeight);
            CardHeading(card, a, "COMPOSITION");
            TextMeshProUGUI total = MenuUi.Text(card, "Total", "TOTAL 100%", a.PixelFont, MenuTheme.SmallButton, MenuTheme.Muted, TextAlignmentOptions.Right);
            MenuUi.TopBand(total.rectTransform, 22, 40, MenuTheme.Padding, MenuTheme.Padding);
            screen.totalText = total;

            // Columns: swatch 24..52, picker 64..536, amount 560..800, share 812..892, remove 904..952 (right padding 24 + 24).
            const float pickerX = 64f, sliderX = 560f, sliderW = 236f, shareX = 808f, removeX = 904f;
            float headY = 72f;
            Caption(card, a, "MATERIAL", pickerX, headY, 300);
            Caption(card, a, "AMOUNT", sliderX, headY, sliderW);
            Caption(card, a, "SHARE", shareX, headY, 80, TextAlignmentOptions.Right);

            var rows = new VariableInputScreen.MaterialRowView[CompositionMixer.MaxRows];
            for (int i = 0; i < rows.Length; i++)
            {
                float y = VariableInputScreen.RowTop + i * VariableInputScreen.RowPitch;
                RectTransform row = MenuUi.Node("Row " + (i + 1), card);
                MenuUi.Stretch(row);
                var view = new VariableInputScreen.MaterialRowView { root = row.gameObject };

                Image sw = MenuUi.Image(row, "Swatch", a.BoxFill, Color.gray, true, MenuUi.SmallBoxPixelScale * 2);
                MenuUi.TopLeft(sw.rectTransform, MenuTheme.Padding, y + 12, 28, 28);
                view.swatch = sw;
                Image swEdge = MenuUi.Image(sw.transform, "Edge", a.BoxOutline, new Color(1, 1, 1, 0.5f), true, MenuUi.SmallBoxPixelScale * 2);
                MenuUi.Stretch(swEdge.rectTransform);

                view.prev = SmallButton(row, a, "Prev", "<", pickerX, y, 48);
                RectTransform nameBox = MenuUi.PixelBox(row, "NameBox", a, MenuTheme.FieldFill, MenuTheme.FieldOutline, false, 0, MenuUi.SmallBoxPixelScale);
                MenuUi.TopLeft(nameBox, pickerX + 56, y, 360, 52);
                TextMeshProUGUI name = MenuUi.Text(nameBox, "Name", "", a.BodyFont, MenuTheme.BodySmall, MenuTheme.Text, TextAlignmentOptions.Center);
                name.enableWordWrapping = false;
                name.enableAutoSizing = true;
                name.fontSizeMin = 20;
                name.fontSizeMax = MenuTheme.BodySmall;
                MenuUi.Stretch(name.rectTransform, 10, 2, 10, 2);
                view.name = name;
                view.next = SmallButton(row, a, "Next", ">", pickerX + 424, y, 48);

                Slider amount = MenuUi.Slider(row, "Amount", a, 0, (float)CompositionMixer.MaxAmount, true);
                MenuUi.TopLeft((RectTransform)amount.transform, sliderX, y + 8, sliderW, 36);
                view.amount = amount;

                TextMeshProUGUI share = MenuUi.Text(row, "Share", "100%", a.BodyFont, MenuTheme.Body, MenuTheme.Text, TextAlignmentOptions.Right, true);
                share.enableWordWrapping = false;
                MenuUi.TopLeft(share.rectTransform, shareX, y, 80, 52);
                view.share = share;

                view.remove = SmallButton(row, a, "Remove", "X", removeX, y, 48);
                rows[i] = view;
            }

            screen.rows = rows;
            Button add = MenuUi.PixelButton(card, "AddButton", "+ ADD MATERIAL", a, MenuTheme.SmallButton, MenuUi.SmallBoxPixelScale);
            MenuUi.TopLeft((RectTransform)add.transform, pickerX, VariableInputScreen.RowTop + 4 * VariableInputScreen.RowPitch, 472, 52);
            screen.addButton = add;
            screen.addLabel = add.GetComponentInChildren<TMP_Text>();

            // Summary of the blend.
            float sy = 460f;
            MenuUi.Divider(card, sy - 16, MenuTheme.Padding);
            RectTransform swatch = MenuUi.PixelBox(card, "BlendSwatch", a, Color.gray, new Color(1, 1, 1, 0.55f), true, 4f, MenuUi.SmallBoxPixelScale);
            MenuUi.TopLeft(swatch, MenuTheme.Padding, sy + 6, 112, 112);
            screen.swatchBase = swatch.Find("Fill").GetComponent<Image>();
            Image accent = MenuUi.Image(swatch, "Accent", a.BoxFill, Color.white, true, MenuUi.SmallBoxPixelScale * 2);
            MenuUi.Place(accent.rectTransform, new Vector2(1, 0), new Vector2(-12, 12), new Vector2(36, 36));
            screen.swatchAccent = accent;

            const float labelX = 160f, valueX = 380f;
            TextMeshProUGUI compName = MenuUi.Text(card, "Blend", "", a.BodyFont, MenuTheme.Body, MenuTheme.Text, TextAlignmentOptions.Left, true);
            compName.enableWordWrapping = false;
            compName.overflowMode = TextOverflowModes.Ellipsis;
            MenuUi.TopLeft(compName.rectTransform, labelX, sy, LeftWidth - labelX - MenuTheme.Padding, 36);
            screen.compositionName = compName;
            Label(card, a, "Bulk density", labelX, sy + 40, 200, 32);
            screen.densityValue = Value(card, a, "", valueX, sy + 40, 300, 32);
            Label(card, a, "Strength", labelX, sy + 72, 200, 32);
            screen.strengthValue = Value(card, a, "", valueX, sy + 72, 300, 32);
            Label(card, a, "Diameter", labelX, sy + 104, 200, 32);
            screen.diameterValue = Value(card, a, "", valueX, sy + 104, 300, 32);
        }

        private static void Caption(Transform parent, MenuAssets a, string text, float x, float y, float width,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            TextMeshProUGUI t = MenuUi.Text(parent, "Caption", text, a.PixelFont, MenuTheme.Caption, MenuTheme.Muted, align);
            t.enableWordWrapping = false;
            MenuUi.TopLeft(t.rectTransform, x, y, width, 28);
        }

        private static void BodyCaption(Transform parent, MenuAssets a, string text, float x, float y, float width, TextAlignmentOptions align)
        {
            TextMeshProUGUI t = MenuUi.Text(parent, "Caption", text, a.BodyFont, MenuTheme.Caption, MenuTheme.Muted, align);
            t.enableWordWrapping = false;
            MenuUi.TopLeft(t.rectTransform, x, y, width, 28);
        }

        private static Button SmallButton(Transform parent, MenuAssets a, string name, string label, float x, float y, float width)
        {
            Button b = MenuUi.PixelButton(parent, name, label, a, 24, MenuUi.SmallBoxPixelScale);
            MenuUi.TopLeft((RectTransform)b.transform, x, y, width, 52);
            return b;
        }

        private static void BuildMassAndStartCard(RectTransform root, MenuAssets a, VariableInputScreen screen)
        {
            RectTransform card = MenuUi.Card(root, "MassStartCard", a);
            MenuUi.TopLeft(card, RightX, CardsTop, RightWidth, CardsHeight);
            const float labelX = MenuTheme.Padding, inputX = 220f;
            float inputW = RightWidth - inputX - MenuTheme.Padding;

            // Mass.
            CardHeading(card, a, "MASS");
            Label(card, a, "Mass (kg)", labelX, 72, 190);
            TMP_InputField mass = MenuUi.InputField(card, "MassField", a, "e.g. 30000 or 1.2e7");
            MenuUi.TopLeft((RectTransform)mass.transform, inputX, 72, inputW, 52);
            screen.massField = mass;
            Slider massSlider = MenuUi.Slider(card, "MassSlider", a, (float)System.Math.Log10(MassInput.MinMass), (float)System.Math.Log10(MassInput.MaxMass));
            MenuUi.TopLeft((RectTransform)massSlider.transform, inputX, 134, inputW, 32);
            screen.massSlider = massSlider;
            BodyCaption(card, a, "1 t", inputX, 166, 120, TextAlignmentOptions.Left);
            BodyCaption(card, a, "10,000 Gt", inputX + inputW - 200, 166, 200, TextAlignmentOptions.Right);
            screen.massReadout = Value(card, a, "", inputX, 196, inputW, 34);
            TextMeshProUGUI hint = MenuUi.Text(card, "MassHint", "", a.BodyFont, MenuTheme.Caption, MenuTheme.Muted, TextAlignmentOptions.Left);
            hint.enableWordWrapping = false;
            MenuUi.TopLeft(hint.rectTransform, inputX, 230, inputW, 30);
            screen.massHint = hint;

            // Start mode.
            MenuUi.Divider(card, 272, MenuTheme.Padding);
            TextMeshProUGUI startHeading = MenuUi.Heading(card, "START", a);
            MenuUi.TopBand(startHeading.rectTransform, 284, 44, MenuTheme.Padding, MenuTheme.Padding);
            Label(card, a, "Start mode", labelX, 336, 190);
            float half = (inputW - 12) / 2;
            screen.fromRestButton = MenuUi.PixelButton(card, "FromRestButton", "FROM REST", a, MenuTheme.SmallButton, MenuUi.SmallBoxPixelScale);
            MenuUi.TopLeft((RectTransform)screen.fromRestButton.transform, inputX, 338, half, 52);
            screen.inboundButton = MenuUi.PixelButton(card, "InboundButton", "INBOUND ENTRY", a, MenuTheme.SmallButton, MenuUi.SmallBoxPixelScale);
            MenuUi.TopLeft((RectTransform)screen.inboundButton.transform, inputX + half + 12, 338, half, 52);

            RectTransform entry = MenuUi.Stretch(MenuUi.Node("EntryRows", card));
            screen.entryRows = entry.gameObject;
            const float valueW = 130f;
            float sliderW = inputW - valueW - 16;
            Label(entry, a, "Entry speed", labelX, 400, 190, 44);
            screen.speedSlider = MenuUi.Slider(entry, "SpeedSlider", a, (float)SetupLogic.MinEntrySpeedKms, (float)SetupLogic.MaxEntrySpeedKms);
            MenuUi.TopLeft((RectTransform)screen.speedSlider.transform, inputX, 406, sliderW, 32);
            screen.speedValue = Value(entry, a, "", inputX + sliderW + 16, 400, valueW, 44, TextAlignmentOptions.Right);
            Label(entry, a, "Entry angle", labelX, 448, 190, 44);
            screen.angleSlider = MenuUi.Slider(entry, "AngleSlider", a, (float)SetupLogic.MinEntryAngle, (float)SetupLogic.MaxEntryAngle, true);
            MenuUi.TopLeft((RectTransform)screen.angleSlider.transform, inputX, 454, sliderW, 32);
            screen.angleValue = Value(entry, a, "", inputX + sliderW + 16, 448, valueW, 44, TextAlignmentOptions.Right);
            TextMeshProUGUI energy = MenuUi.Text(entry, "Energy", "", a.BodyFont, MenuTheme.Caption, MenuTheme.Muted, TextAlignmentOptions.Left);
            energy.enableWordWrapping = false;
            MenuUi.TopLeft(energy.rectTransform, inputX, 492, inputW, 28);
            screen.energyValue = energy;

            TextMeshProUGUI rest = MenuUi.Text(card, "RestHint",
                "Dropped from a standstill: gravity alone pulls it in, so it reaches the air at about 11 km/s. " +
                "Choose Inbound entry to set its speed and angle at 120 km.", a.BodyFont, MenuTheme.BodySmall, MenuTheme.Muted, TextAlignmentOptions.TopLeft);
            MenuUi.TopLeft(rest.rectTransform, inputX, 404, inputW, 110);
            screen.restHint = rest;

            // Start position.
            MenuUi.Divider(card, 522, MenuTheme.Padding);
            Label(card, a, "Start position", labelX, 532, 190);
            float fieldW = (inputW - 2 * 16 - 3 * 28) / 3;
            string[] axes = { "X", "Y", "Z" };
            var fields = new TMP_InputField[3];
            for (int i = 0; i < 3; i++)
            {
                float x = inputX + i * (28 + fieldW + 16);
                TextMeshProUGUI axis = MenuUi.Text(card, "Axis " + axes[i], axes[i], a.PixelFont, MenuTheme.Body, MenuTheme.Label, TextAlignmentOptions.Left);
                MenuUi.TopLeft(axis.rectTransform, x, 532, 28, 52);
                TMP_InputField f = MenuUi.InputField(card, axes[i] + " Field", a, "25", TMP_InputField.ContentType.DecimalNumber);
                MenuUi.TopLeft((RectTransform)f.transform, x + 28, 532, fieldW, 52);
                fields[i] = f;
            }

            screen.xField = fields[0];
            screen.yField = fields[1];
            screen.zField = fields[2];
            TextMeshProUGUI pos = MenuUi.Text(card, "PositionReadout", "", a.BodyFont, MenuTheme.Caption, MenuTheme.Muted, TextAlignmentOptions.Left);
            pos.enableWordWrapping = true;
            MenuUi.TopLeft(pos.rectTransform, inputX, 588, inputW, 54);
            screen.positionReadout = pos;
        }

        // =====================================================================================================
        // Art
        // =====================================================================================================

        public static MenuAssets PrepareArt()
        {
            Directory.CreateDirectory(ArtFolder);
            bool copied = CopyIfMissing("Assets/Textures/9sprite_8bit_circle.png", BoxFillPath);
            copied |= CopyIfMissing("Assets/Textures/9sprite_8bit_circle_outline.png", BoxOutlinePath);
            copied |= CopyIfMissing("Assets/Textures/meteorshower-cover_q5gt.jpg", BackgroundPath);
            if (copied)
            {
                AssetDatabase.Refresh();
                ConfigureSprite(BoxFillPath, true);
                ConfigureSprite(BoxOutlinePath, true);
                ConfigureSprite(BackgroundPath, false);
            }

            var assets = new MenuAssets
            {
                BodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath),
                PixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontPath),
                BoxFill = AssetDatabase.LoadAssetAtPath<Sprite>(BoxFillPath),
                BoxOutline = AssetDatabase.LoadAssetAtPath<Sprite>(BoxOutlinePath),
                Background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath),
            };
            Debug.Log("[Menus] Art: body font " + assets.BodyFont + ", pixel font " + assets.PixelFont + ", box " + assets.BoxFill +
                      ", outline " + assets.BoxOutline + ", background " + assets.Background);
            if (assets.PixelFont == null || assets.BodyFont == null || assets.BoxFill == null || assets.BoxOutline == null)
            {
                throw new System.InvalidOperationException("Menu art missing (see the log line above).");
            }

            return assets;
        }

        private static bool CopyIfMissing(string from, string to)
        {
            if (File.Exists(to)) return false;
            File.Copy(from, to);
            return true;
        }

        private static void ConfigureSprite(string path, bool nineSlice)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = nineSlice ? new Vector4(128, 128, 128, 128) : Vector4.zero;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = nineSlice ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }
}
