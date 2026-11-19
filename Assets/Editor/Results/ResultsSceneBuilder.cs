using System.IO;
using MTS.Results;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MTS.Results.Editor
{
    /// <summary>
    /// Rebuilds Assets/Scenes/EarthCollision.unity (the results screen) from code, so the layout is reproducible:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod MTS.Results.Editor.ResultsSceneBuilder.Build
    /// Also copies and configures the art in Assets/Art/Results (copies of the menu's 9-slice sprites, the menu
    /// background and the world map; the originals are left untouched).
    /// </summary>
    public static class ResultsSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/EarthCollision.unity";
        public const string ArtFolder = "Assets/Art/Results";

        private const string BoxFillPath = ArtFolder + "/PixelBoxFill.png";
        private const string BoxOutlinePath = ArtFolder + "/PixelBoxOutline.png";
        private const string BackgroundPath = ArtFolder + "/ResultsBackground.jpg";
        private const string WorldMapPath = ArtFolder + "/WorldMap.jpg";

        private const string BodyFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string PixelFontPath = "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset";

        [MenuItem("Tools/Results/Rebuild Results Scene")]
        public static void Build()
        {
            ResultsAssets assets = PrepareArt();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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

            var canvasGo = new GameObject("Results Canvas", typeof(RectTransform));
            canvasGo.layer = 5;
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            ResultsScreen screen = canvasGo.AddComponent<ResultsScreen>();
            screen.Assets = assets;

            BuildBackground(root, assets);
            BuildHeader(root, screen, assets);
            BuildTabs(root, screen, assets);
            BuildPages(root, screen, assets);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Results scene rebuilt: " + ScenePath);
        }

        private static void BuildBackground(RectTransform root, ResultsAssets a)
        {
            RectTransform holder = ResultsUi.Stretch(ResultsUi.Node("Background", root));
            Image bg = ResultsUi.Image(holder, "Sky", a.Background, Color.white, false);
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = a.Background != null ? a.Background.rect.width / a.Background.rect.height : 16f / 9f;
            Image dim = ResultsUi.Image(holder, "Dim", null, new Color(0.01f, 0.01f, 0.04f, 0.62f), false);
            ResultsUi.Stretch(dim.rectTransform);
        }

        private static void BuildHeader(RectTransform root, ResultsScreen screen, ResultsAssets a)
        {
            RectTransform titleBox = ResultsUi.PixelBox(root, "TitleBox", a, ResultsTheme.Red, ResultsTheme.DarkRed);
            ResultsUi.Place(titleBox, new Vector2(0, 1), new Vector2(40, -30), new Vector2(560, 96));
            TextMeshProUGUI title = ResultsUi.Text(titleBox, "Title", "GROUND IMPACT", a.PixelFont, 50, Color.white, TextAlignmentOptions.Center);
            title.enableWordWrapping = false;
            title.enableAutoSizing = true;
            title.fontSizeMin = 28;
            title.fontSizeMax = 50;
            ResultsUi.Stretch(title.rectTransform, 18, 8, 18, 8);
            screen.TitleText = title;

            TextMeshProUGUI headline = ResultsUi.Text(root, "Headline", "", a.BodyFont, 36, Color.white, TextAlignmentOptions.Left, true);
            headline.enableWordWrapping = false;
            headline.enableAutoSizing = true;
            headline.fontSizeMin = 22;
            headline.fontSizeMax = 36;
            ResultsUi.TopBand(headline.rectTransform, 30, 50, 636, 600);
            screen.HeadlineText = headline;

            TextMeshProUGUI location = ResultsUi.Text(root, "Location", "", a.BodyFont, 23, ResultsTheme.Text, TextAlignmentOptions.Left);
            location.enableWordWrapping = false;
            ResultsUi.TopBand(location.rectTransform, 82, 40, 636, 600);
            screen.LocationText = location;

            Button newRun = ResultsUi.PixelButton(root, "NewRunButton", "NEW RUN", a, 32);
            ResultsUi.Place((RectTransform)newRun.transform, new Vector2(1, 1), new Vector2(-40, -34), new Vector2(250, 84));
            Button menu = ResultsUi.PixelButton(root, "MainMenuButton", "MAIN MENU", a, 32);
            ResultsUi.Place((RectTransform)menu.transform, new Vector2(1, 1), new Vector2(-314, -34), new Vector2(270, 84));

            // Navigation uses the existing MainMenuButton component (MainMenu resets the run for the input screen).
            var navigation = root.gameObject.AddComponent<MainMenuButton>();
            UnityEventTools.AddStringPersistentListener(menu.onClick, navigation.MainMenu, "MainMenu");
            UnityEventTools.AddStringPersistentListener(newRun.onClick, navigation.MainMenu, "VariableInput");
        }

        private static void BuildTabs(RectTransform root, ResultsScreen screen, ResultsAssets a)
        {
            string[] names = { "SUMMARY", "MAP", "CHARTS", "PHYSICS" };
            screen.TabButtons = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Button tab = ResultsUi.PixelButton(root, "Tab" + names[i], names[i], a, 30);
                ResultsUi.Place((RectTransform)tab.transform, new Vector2(0, 1), new Vector2(40 + i * 278, -156), new Vector2(260, 66));
                TextMeshProUGUI hint = ResultsUi.Text(tab.transform, "Key", (i + 1).ToString(), a.BodyFont, 15, new Color(1, 1, 1, 0.45f), TextAlignmentOptions.TopRight);
                ResultsUi.Stretch(hint.rectTransform, 10, 6, 14, 8);
                screen.TabButtons[i] = tab;
            }
        }

        private static void BuildPages(RectTransform root, ResultsScreen screen, ResultsAssets a)
        {
            RectTransform pages = ResultsUi.Stretch(ResultsUi.Node("Pages", root), 40, 40, 40, 246);
            screen.Pages = new GameObject[4];

            // Summary
            RectTransform summary = ResultsUi.Stretch(ResultsUi.Node("SummaryPage", pages));
            RectTransform left = ResultsUi.Card(summary, "EventCard", a);
            left.anchorMin = new Vector2(0, 0);
            left.anchorMax = new Vector2(0.56f, 1);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(-12, 0);
            RectTransform right = ResultsUi.Card(summary, "GroundCard", a);
            right.anchorMin = new Vector2(0.56f, 0);
            right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(12, 0);
            right.offsetMax = Vector2.zero;
            screen.SummaryLeft = ResultsUi.Stretch(ResultsUi.Node("Content", left));
            screen.SummaryRight = ResultsUi.Stretch(ResultsUi.Node("Content", right));
            screen.Pages[0] = summary.gameObject;

            // Map
            RectTransform mapPage = ResultsUi.Stretch(ResultsUi.Node("MapPage", pages));
            RectTransform mapCard = ResultsUi.Card(mapPage, "MapCard", a);
            ResultsUi.Stretch(mapCard);
            RectTransform mapRoot = ResultsUi.Stretch(ResultsUi.Node("WorldMap", mapCard));
            screen.MapView = mapRoot.gameObject.AddComponent<WorldMapView>();
            screen.Pages[1] = mapPage.gameObject;

            // Charts: 2 x 2
            RectTransform charts = ResultsUi.Stretch(ResultsUi.Node("ChartsPage", pages));
            screen.ChartSlots = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2;
                int row = i / 2;
                RectTransform card = ResultsUi.Card(charts, "ChartCard" + (i + 1), a);
                card.anchorMin = new Vector2(col * 0.5f, row == 0 ? 0.5f : 0f);
                card.anchorMax = new Vector2(col * 0.5f + 0.5f, row == 0 ? 1f : 0.5f);
                card.offsetMin = new Vector2(col == 0 ? 0 : 12, row == 0 ? 12 : 0);
                card.offsetMax = new Vector2(col == 0 ? -12 : 0, row == 0 ? 0 : -12);
                screen.ChartSlots[i] = ResultsUi.Stretch(ResultsUi.Node("Content", card));
            }

            screen.Pages[2] = charts.gameObject;

            // Physics details: two columns of section cards
            RectTransform physics = ResultsUi.Stretch(ResultsUi.Node("PhysicsPage", pages));
            RectTransform pl = ResultsUi.Node("LeftColumn", physics);
            pl.anchorMin = new Vector2(0, 0);
            pl.anchorMax = new Vector2(0.5f, 1);
            pl.offsetMin = Vector2.zero;
            pl.offsetMax = new Vector2(-12, 0);
            RectTransform pr = ResultsUi.Node("RightColumn", physics);
            pr.anchorMin = new Vector2(0.5f, 0);
            pr.anchorMax = new Vector2(1, 1);
            pr.offsetMin = new Vector2(12, 0);
            pr.offsetMax = Vector2.zero;
            screen.PhysicsLeft = pl;
            screen.PhysicsRight = pr;
            screen.Pages[3] = physics.gameObject;

            for (int i = 1; i < 4; i++)
            {
                screen.Pages[i].SetActive(false);
            }
        }

        // ---------------------------------------------------------------- Art

        public static ResultsAssets PrepareArt()
        {
            Directory.CreateDirectory(ArtFolder);
            CopyIfMissing("Assets/Textures/9sprite_8bit_circle.png", BoxFillPath);
            CopyIfMissing("Assets/Textures/9sprite_8bit_circle_outline.png", BoxOutlinePath);
            CopyIfMissing("Assets/Textures/meteorshower-cover_q5gt.jpg", BackgroundPath);
            CopyIfMissing("Assets/Materials/Earth tecture.jpg", WorldMapPath);
            AssetDatabase.Refresh();

            ConfigureSprite(BoxFillPath, true);
            ConfigureSprite(BoxOutlinePath, true);
            ConfigureSprite(BackgroundPath, false);
            ConfigureMap(WorldMapPath);

            return new ResultsAssets
            {
                BodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath),
                PixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontPath),
                BoxFill = AssetDatabase.LoadAssetAtPath<Sprite>(BoxFillPath),
                BoxOutline = AssetDatabase.LoadAssetAtPath<Sprite>(BoxOutlinePath),
                Background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath),
                WorldMap = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldMapPath),
            };
        }

        private static void CopyIfMissing(string from, string to)
        {
            if (!File.Exists(to))
            {
                File.Copy(from, to);
            }
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

        private static void ConfigureMap(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
