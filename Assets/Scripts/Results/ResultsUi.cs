using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Results
{
    /// <summary>Art used by the results screen (assigned by the scene builder).</summary>
    [Serializable]
    public sealed class ResultsAssets
    {
        public TMP_FontAsset BodyFont;
        public TMP_FontAsset PixelFont;
        /// <summary>9-slice pixel rounded box (white, tinted per use).</summary>
        public Sprite BoxFill;
        /// <summary>9-slice pixel rounded outline (white, tinted per use).</summary>
        public Sprite BoxOutline;
        public Sprite Background;
        /// <summary>Equirectangular world map, centred on Greenwich.</summary>
        public Texture2D WorldMap;
    }

    /// <summary>Colours of the results screen: the menu's pixel red on a dark night-sky panel style.</summary>
    public static class ResultsTheme
    {
        public static readonly Color Red = new Color(1f, 0f, 0f, 1f);
        public static readonly Color DarkRed = new Color(0.42f, 0f, 0f, 1f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.345f);
        public static readonly Color PanelFill = new Color(0.035f, 0.03f, 0.075f, 0.9f);
        public static readonly Color PanelOutline = new Color(0.62f, 0.04f, 0.04f, 1f);
        public static readonly Color TabIdleFill = new Color(0.22f, 0.02f, 0.03f, 0.95f);
        public static readonly Color Text = new Color(1f, 1f, 1f, 1f);
        public static readonly Color Muted = new Color(0.72f, 0.72f, 0.8f, 1f);
        public static readonly Color Label = new Color(1f, 0.48f, 0.44f, 1f);
        public static readonly Color Accent = new Color(1f, 0.8f, 0.28f, 1f);
        public static readonly Color Grid = new Color(1f, 1f, 1f, 0.09f);
        public static readonly Color Axis = new Color(1f, 1f, 1f, 0.55f);

        /// <summary>Ring colours from the weakest (0.04 psi, outermost) to the strongest (29 psi, innermost).</summary>
        public static readonly Color[] Rings =
        {
            new Color(0.45f, 0.68f, 1f),
            new Color(0.35f, 0.88f, 0.95f),
            new Color(0.45f, 0.92f, 0.5f),
            new Color(0.95f, 0.95f, 0.35f),
            new Color(1f, 0.78f, 0.25f),
            new Color(1f, 0.58f, 0.18f),
            new Color(1f, 0.36f, 0.14f),
            new Color(1f, 0.12f, 0.16f),
            new Color(1f, 0.92f, 0.86f),
        };

        public static readonly Color Crater = new Color(0.1f, 0.06f, 0.04f, 1f);

        /// <summary>Hues of the non-blast effect layers (dashed rings on the map, card accents on the Effects tab).</summary>
        public static readonly Color Thermal = new Color(1f, 0.62f, 0.2f);
        public static readonly Color Seismic = new Color(0.78f, 0.55f, 1f);
        public static readonly Color Ejecta = new Color(0.86f, 0.7f, 0.5f);
        public static readonly Color Tsunami = new Color(0.3f, 0.82f, 1f);

        public static Color GroupColor(EffectGroup group)
        {
            switch (group)
            {
                case EffectGroup.Thermal: return Thermal;
                case EffectGroup.Seismic: return Seismic;
                case EffectGroup.Ejecta: return Ejecta;
                case EffectGroup.Tsunami: return Tsunami;
                default: return Rings[0];
            }
        }

        /// <summary>Ring colour of an effect layer: level 0 (strongest, innermost) is the brightest, outer rings fade.</summary>
        public static Color EffectColor(EffectGroup group, int level)
        {
            Color c = GroupColor(group);
            float k = Mathf.Clamp01(1f - 0.22f * level);
            return new Color(Mathf.Lerp(0.55f, c.r, k), Mathf.Lerp(0.55f, c.g, k), Mathf.Lerp(0.6f, c.b, k), 1f);
        }

        public static readonly Color[] Series =
        {
            new Color(1f, 0.32f, 0.28f),
            new Color(0.4f, 0.8f, 1f),
            new Color(1f, 0.8f, 0.28f),
            new Color(1f, 0.55f, 0.95f),
        };
    }

    /// <summary>Small uGUI factory shared by the scene builder (edit time) and the results screen (run time).</summary>
    public static class ResultsUi
    {
        /// <summary>Pixels-per-unit multiplier for the 512 px 9-slice sprites: about 13 px corners at 1080p.</summary>
        public const float BoxPixelScale = 10f;
        /// <summary>Multiplier for small boxes (swatches): about 3 px corners.</summary>
        public const float SmallBoxPixelScale = 40f;

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
                t.fontSharedMaterial = font.material;
            }

            t.text = RichPowers(text);
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.richText = true;
            if (bold)
            {
                t.fontStyle = FontStyles.Bold;
            }

            return t;
        }

        private static readonly System.Text.RegularExpressions.Regex PowerOfTen =
            new System.Text.RegularExpressions.Regex(@"10\^(-?\d+)");

        /// <summary>Turns plain "10^12" (from the culture-invariant formatters) into a TMP superscript.</summary>
        public static string RichPowers(string text) =>
            string.IsNullOrEmpty(text) || text.IndexOf('^') < 0 ? text ?? "" : PowerOfTen.Replace(text, "10<sup>$1</sup>");

        /// <summary>
        /// The menu's pixel box: drop shadow, fill and outline (9-slice sprites). Returns the container; content goes
        /// into it. The fill is the container's first <see cref="UnityEngine.UI.Image"/> child named "Fill".
        /// </summary>
        public static RectTransform PixelBox(Transform parent, string name, ResultsAssets assets, Color fill, Color outline,
            bool shadow = true, float shadowOffset = 6f, float pixelScale = BoxPixelScale)
        {
            RectTransform box = Node(name, parent);
            if (shadow)
            {
                Image s = Image(box, "Shadow", assets.BoxFill, ResultsTheme.Shadow, true, pixelScale);
                Stretch(s.rectTransform, shadowOffset, -shadowOffset, -shadowOffset, shadowOffset);
            }

            Image f = Image(box, "Fill", assets.BoxFill, fill, true, pixelScale);
            Stretch(f.rectTransform);
            Image o = Image(box, "Outline", assets.BoxOutline, outline, true, pixelScale);
            Stretch(o.rectTransform);
            return box;
        }

        /// <summary>Card panel: dark translucent fill with a red pixel outline.</summary>
        public static RectTransform Card(Transform parent, string name, ResultsAssets assets) =>
            PixelBox(parent, name, assets, ResultsTheme.PanelFill, ResultsTheme.PanelOutline, true, 5f);

        /// <summary>Red pixel button with a pixel-font label (the menu's button style).</summary>
        public static Button PixelButton(Transform parent, string name, string label, ResultsAssets assets, float fontSize = 30f)
        {
            RectTransform box = PixelBox(parent, name, assets, ResultsTheme.Red, ResultsTheme.DarkRed);
            Image fill = box.Find("Fill").GetComponent<Image>();
            fill.raycastTarget = true;
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.75f, 0.75f);
            colors.pressedColor = new Color(0.7f, 0.5f, 0.5f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            TextMeshProUGUI text = Text(box, "Label", label, assets.PixelFont, fontSize, Color.white, TextAlignmentOptions.Center);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            Stretch(text.rectTransform, 8, 4, 8, 4);
            return button;
        }

        /// <summary>Upper-case section heading in the pixel font.</summary>
        public static TextMeshProUGUI Heading(Transform parent, string text, ResultsAssets assets, float size = 30f)
        {
            TextMeshProUGUI t = Text(parent, "Heading", text, assets.PixelFont, size, ResultsTheme.Label);
            t.enableWordWrapping = false;
            return t;
        }

        public static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject child = t.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child);
                    child.transform.SetParent(null, false);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child);
                }
            }
        }
    }
}
