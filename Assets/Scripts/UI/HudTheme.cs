using MTS.Core.Display;
using UnityEngine;

namespace MTS.Hud
{
    /// <summary>
    /// Colours, text sizes and spacing of the HUD (the red pixel style of the menus, made translucent).
    /// Sizes are canvas pixels on the 1920x1080 reference canvas. Alignment system: labels left, values right on a
    /// shared column, headings left; centred text only on buttons and the stage bar.
    /// </summary>
    public static class HudTheme
    {
        public static readonly Color PanelFill = new Color(0.26f, 0.015f, 0.015f, 0.82f);
        public static readonly Color PanelOutline = new Color(1f, 0.16f, 0.08f, 0.95f);
        public static readonly Color Header = new Color(0.9f, 0.06f, 0.03f, 0.92f);
        public static readonly Color RowShade = new Color(0f, 0f, 0f, 0.28f);
        public static readonly Color Label = new Color(1f, 0.86f, 0.82f, 1f);
        public static readonly Color Value = Color.white;
        /// <summary>Secondary text (captions, hints): still readable on the panel fill.</summary>
        public static readonly Color Dim = new Color(1f, 0.74f, 0.69f, 0.9f);
        public static readonly Color Button = new Color(0.85f, 0.05f, 0.03f, 1f);
        public static readonly Color ButtonOff = new Color(0.35f, 0.04f, 0.04f, 0.9f);
        public static readonly Color ButtonHighlight = new Color(1f, 0.35f, 0.25f, 1f);
        public static readonly Color Launch = new Color(1f, 0.45f, 0.05f, 1f);
        public static readonly Color BarBack = new Color(0.12f, 0f, 0f, 0.85f);
        public static readonly Color BarFill = new Color(1f, 0.3f, 0.12f, 1f);
        public static readonly Color Warning = new Color(1f, 0.72f, 0.1f, 1f);
        public static readonly Color Info = new Color(0.55f, 0.8f, 1f, 1f);
        public static readonly Color Danger = new Color(1f, 0.4f, 0.32f, 1f);

        // ---- text sizes (reference canvas px)
        public const float TitleSize = 30f;       // panel headings
        public const float BigValueSize = 36f;    // altitude
        public const float ValueSize = 28f;       // readout values
        public const float LabelSize = 22f;       // row labels, captions, hints, composition lines
        public const float ButtonSize = 26f;      // button captions (LED font)
        public const float ChipSize = 26f;        // stage chips
        public const float ToastTitleSize = 32f;
        public const float ToastBodySize = 24f;
        public const float LegendSize = 20f;      // keyboard shortcut legend
        public const float TooltipSize = 22f;
        /// <summary>Smallest size any auto-sized text may shrink to (last resort for very long values).</summary>
        public const float MinAutoSize = 20f;

        // ---- spacing (reference canvas px)
        public const int PanelPadding = 16;       // panel edge to content
        public const float RowInset = 12f;        // row edge to label / value text (headings use the same inset)
        public const float RowHeight = 40f;
        public const float RowSpacing = 3f;
        public const float HeaderHeight = 48f;
        public const float ScreenMargin = 24f;
        public const float SidePanelWidth = 480f;

        public const string DisplayFontPath = "Assets/Resources/UI/Fonts/HudDisplay SDF.asset"; // copy of TMP Examples "Electronic Highway Sign SDF" (the original is frozen)
        public const string LabelFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        public const string PanelSpritePath = "Assets/Textures/UI/hud_panel.png";
        public const string OutlineSpritePath = "Assets/Textures/UI/hud_panel_outline.png";
        public const string DiscSpritePath = "Assets/Textures/UI/hud_disc.png";
        public const string RingSpritePath = "Assets/Textures/UI/hud_ring.png";

        public static Color AccentFor(FlightEvent e)
        {
            switch (e)
            {
                case FlightEvent.AtmosphericEntry: return Info;
                case FlightEvent.PeakHeating:
                case FlightEvent.PeakBrightness: return Warning;
                default: return Danger;
            }
        }
    }
}
