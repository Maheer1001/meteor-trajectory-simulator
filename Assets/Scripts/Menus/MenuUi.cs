using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Menus
{
    /// <summary>Art used by the menus and the input screen (assigned by the scene builder).</summary>
    [Serializable]
    public sealed class MenuAssets
    {
        public TMP_FontAsset BodyFont;
        public TMP_FontAsset PixelFont;
        /// <summary>Body font material with a plain white face, so text colours show (the shared LiberationSans SDF
        /// material has an HDR face colour that turns every colour white).</summary>
        public Material BodyMaterial;
        /// <summary>9-slice pixel rounded box (white, tinted per use).</summary>
        public Sprite BoxFill;
        /// <summary>9-slice pixel rounded outline (white, tinted per use).</summary>
        public Sprite BoxOutline;
        public Sprite Background;
    }

    /// <summary>
    /// The menus' layout system, at the 1920x1080 reference (Canvas Scaler: scale with screen size, match 0.5).
    /// Same red pixel look as the results screen: red pixel buttons, dark night-sky cards with a red outline.
    /// </summary>
    public static class MenuTheme
    {
        public static readonly Color Red = new Color(1f, 0f, 0f, 1f);
        public static readonly Color DarkRed = new Color(0.42f, 0f, 0f, 1f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.345f);
        public static readonly Color PanelFill = new Color(0.035f, 0.03f, 0.075f, 0.9f);
        public static readonly Color PanelOutline = new Color(0.62f, 0.04f, 0.04f, 1f);
        public static readonly Color FieldFill = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color FieldOutline = new Color(0.62f, 0.04f, 0.04f, 1f);
        public static readonly Color IdleFill = new Color(0.22f, 0.02f, 0.03f, 0.95f);
        public static readonly Color Text = Color.white;
        public static readonly Color Muted = new Color(0.72f, 0.72f, 0.8f, 1f);
        public static readonly Color Label = new Color(1f, 0.48f, 0.44f, 1f);
        public static readonly Color Accent = new Color(1f, 0.8f, 0.28f, 1f);
        public static readonly Color Error = new Color(1f, 0.55f, 0.3f, 1f);
        public static readonly Color Divider = new Color(1f, 1f, 1f, 0.1f);

        // Type scale (px at 1080p).
        public const float Title = 48f;
        public const float Heading = 30f;
        public const float Button = 28f;
        public const float SmallButton = 22f;
        public const float Body = 24f;
        public const float BodySmall = 22f;
        public const float LabelSize = 22f;
        public const float Caption = 20f;

        // Spacing.
        public const float Margin = 48f;
        public const float Padding = 24f;
        public const float Gap = 16f;
        public const float RowHeight = 56f;
    }

    /// <summary>Small uGUI factory used by the menu scene builder (edit time) and the input screen (run time).</summary>
    public static class MenuUi
    {
        /// <summary>Pixels-per-unit multiplier for the 512 px 9-slice sprites: about 13 px corners at 1080p.</summary>
        public const float BoxPixelScale = 10f;
        /// <summary>Multiplier for small boxes (fields, swatches): about 6 px corners.</summary>
        public const float SmallBoxPixelScale = 20f;

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Stretches to the parent with insets (left, bottom, right, top).</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Places a fixed-size rect relative to an anchor point of the parent (pivot = anchor).</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Top-left placement: x to the right, y down from the parent's top-left corner.</summary>
        public static RectTransform TopLeft(RectTransform rt, float x, float y, float width, float height) =>
            Place(rt, new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));

        /// <summary>Spans the parent's width (with insets) at a fixed height measured down from the top.</summary>
        public static RectTransform TopBand(RectTransform rt, float top, float height, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = true, float pixelScale = BoxPixelScale)
        {
            RectTransform rt = Node(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null && sliced)
            {
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = pixelScale;
            }

            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool bold = false)
        {
            RectTransform rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                t.font = font;
            }

            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            if (bold)
            {
                t.fontStyle = FontStyles.Bold;
            }

            return t;
        }

        /// <summary>The pixel box: drop shadow, fill and outline (9-slice sprites). Content goes into the returned container.</summary>
        public static RectTransform PixelBox(Transform parent, string name, MenuAssets assets, Color fill, Color outline,
            bool shadow = true, float shadowOffset = 6f, float pixelScale = BoxPixelScale)
        {
            RectTransform box = Node(name, parent);
            if (shadow)
            {
                Image s = Image(box, "Shadow", assets.BoxFill, MenuTheme.Shadow, true, pixelScale);
                Stretch(s.rectTransform, shadowOffset, -shadowOffset, -shadowOffset, shadowOffset);
            }

            Image f = Image(box, "Fill", assets.BoxFill, fill, true, pixelScale);
            Stretch(f.rectTransform);
            Image o = Image(box, "Outline", assets.BoxOutline, outline, true, pixelScale);
            Stretch(o.rectTransform);
            return box;
        }

        /// <summary>Card panel: dark translucent fill with a red pixel outline.</summary>
        public static RectTransform Card(Transform parent, string name, MenuAssets assets) =>
            PixelBox(parent, name, assets, MenuTheme.PanelFill, MenuTheme.PanelOutline, true, 5f);

        /// <summary>Red pixel button with a centred pixel-font label.</summary>
        public static Button PixelButton(Transform parent, string name, string label, MenuAssets assets, float fontSize = MenuTheme.Button,
            float pixelScale = BoxPixelScale)
        {
            RectTransform box = PixelBox(parent, name, assets, MenuTheme.Red, MenuTheme.DarkRed, true, pixelScale > BoxPixelScale ? 4f : 6f, pixelScale);
            Image fill = box.Find("Fill").GetComponent<Image>();
            fill.raycastTarget = true;
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.75f, 0.75f);
            colors.pressedColor = new Color(0.7f, 0.5f, 0.5f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.3f, 0.3f, 0.7f);
            button.colors = colors;
            TextMeshProUGUI text = Text(box, "Label", label, assets.PixelFont, fontSize, Color.white, TextAlignmentOptions.Center);
            text.enableWordWrapping = false;
            Stretch(text.rectTransform, 8, 2, 8, 2);
            return button;
        }

        /// <summary>Section heading in the pixel font.</summary>
        public static TextMeshProUGUI Heading(Transform parent, string text, MenuAssets assets, float size = MenuTheme.Heading)
        {
            TextMeshProUGUI t = Text(parent, "Heading", text, assets.PixelFont, size, MenuTheme.Label);
            t.enableWordWrapping = false;
            return t;
        }

        public static Image Divider(Transform parent, float y, float inset)
        {
            Image d = Image(parent, "Divider", null, MenuTheme.Divider, false);
            TopBand(d.rectTransform, y, 2, inset, inset);
            return d;
        }

        /// <summary>Single-line text field: dark pixel box, body font, left-aligned text.</summary>
        public static TMP_InputField InputField(Transform parent, string name, MenuAssets assets, string placeholder,
            TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard, float fontSize = MenuTheme.Body)
        {
            RectTransform box = PixelBox(parent, name, assets, MenuTheme.FieldFill, MenuTheme.FieldOutline, false, 0, SmallBoxPixelScale);
            Image fill = box.Find("Fill").GetComponent<Image>();
            fill.raycastTarget = true;

            RectTransform area = Stretch(Node("Text Area", box), 14, 4, 14, 4);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI hint = Text(area, "Placeholder", placeholder, assets.BodyFont, fontSize, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.Left);
            hint.enableWordWrapping = false;
            hint.fontStyle = FontStyles.Italic;
            Stretch(hint.rectTransform);
            TextMeshProUGUI value = Text(area, "Text", "", assets.BodyFont, fontSize, MenuTheme.Text, TextAlignmentOptions.Left);
            value.enableWordWrapping = false;
            Stretch(value.rectTransform);

            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = fill;
            field.textViewport = area;
            field.textComponent = value;
            field.placeholder = hint;
            field.fontAsset = assets.BodyFont;
            field.pointSize = fontSize;
            field.contentType = contentType;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.caretColor = MenuTheme.Accent;
            field.customCaretColor = true;
            field.caretWidth = 2;
            field.selectionColor = new Color(1f, 0.3f, 0.2f, 0.45f);
            ColorBlock colors = field.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.85f, 0.85f);
            colors.selectedColor = new Color(1f, 0.9f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.7f);
            field.colors = colors;
            return field;
        }

        /// <summary>Horizontal slider: dark pixel track, red fill, light pixel handle.</summary>
        public static Slider Slider(Transform parent, string name, MenuAssets assets, float min, float max, bool wholeNumbers = false)
        {
            RectTransform root = Node(name, parent);
            Image track = Image(root, "Background", assets.BoxFill, MenuTheme.FieldFill, true, SmallBoxPixelScale * 2);
            Stretch(track.rectTransform, 0, 0, 0, 0);
            track.rectTransform.anchorMin = new Vector2(0, 0.3f);
            track.rectTransform.anchorMax = new Vector2(1, 0.7f);
            track.rectTransform.offsetMin = Vector2.zero;
            track.rectTransform.offsetMax = Vector2.zero;
            track.raycastTarget = true;

            RectTransform fillArea = Node("Fill Area", root);
            fillArea.anchorMin = new Vector2(0, 0.3f);
            fillArea.anchorMax = new Vector2(1, 0.7f);
            fillArea.offsetMin = new Vector2(0, 0);
            fillArea.offsetMax = new Vector2(-12, 0);
            Image fill = Image(fillArea, "Fill", assets.BoxFill, MenuTheme.Red, true, SmallBoxPixelScale * 2);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = new Vector2(12, 0);

            RectTransform handleArea = Node("Handle Slide Area", root);
            Stretch(handleArea, 12, 0, 12, 0);
            Image handle = Image(handleArea, "Handle", assets.BoxFill, new Color(1f, 0.92f, 0.88f), true, SmallBoxPixelScale * 2);
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.sizeDelta = new Vector2(24, 0);
            handle.raycastTarget = true;
            Image handleEdge = Image(handle.transform, "Edge", assets.BoxOutline, MenuTheme.DarkRed, true, SmallBoxPixelScale * 2);
            Stretch(handleEdge.rectTransform);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            ColorBlock colors = slider.colors;
            colors.highlightedColor = new Color(1f, 0.8f, 0.75f);
            colors.pressedColor = new Color(1f, 0.6f, 0.5f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            slider.colors = colors;
            return slider;
        }

        /// <summary>Selected / idle look for toggle-style pixel buttons (scenario chips, start mode).</summary>
        public static void SetSelected(Button button, bool selected)
        {
            if (button == null) return;
            Transform fill = button.transform.Find("Fill");
            Transform outline = button.transform.Find("Outline");
            if (fill != null) fill.GetComponent<Image>().color = selected ? MenuTheme.Red : MenuTheme.IdleFill;
            if (outline != null) outline.GetComponent<Image>().color = selected ? new Color(1f, 0.75f, 0.6f) : MenuTheme.DarkRed;
            Transform label = button.transform.Find("Label");
            if (label != null) label.GetComponent<TMP_Text>().color = selected ? Color.white : new Color(1f, 0.8f, 0.78f);
        }
    }
}
