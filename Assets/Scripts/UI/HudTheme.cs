using MTS.Core.Display;
using UnityEngine;

namespace MTS.Hud
{
    /// <summary>Colours and sizes of the HUD (the red pixel style of the original NEA screens, made translucent).</summary>
    public static class HudTheme
    {
        public static readonly Color PanelFill = new Color(0.45f, 0.02f, 0.02f, 0.62f);
        public static readonly Color PanelOutline = new Color(1f, 0.16f, 0.08f, 0.95f);
        public static readonly Color Header = new Color(0.9f, 0.06f, 0.03f, 0.92f);
        public static readonly Color RowShade = new Color(0f, 0f, 0f, 0.22f);
        public static readonly Color Label = new Color(1f, 0.8f, 0.76f, 1f);
        public static readonly Color Value = Color.white;
        public static readonly Color Dim = new Color(1f, 0.8f, 0.76f, 0.55f);
        public static readonly Color Button = new Color(0.85f, 0.05f, 0.03f, 1f);
        public static readonly Color ButtonOff = new Color(0.35f, 0.04f, 0.04f, 0.9f);
        public static readonly Color ButtonHighlight = new Color(1f, 0.35f, 0.25f, 1f);
        public static readonly Color Launch = new Color(1f, 0.45f, 0.05f, 1f);
        public static readonly Color BarBack = new Color(0.12f, 0f, 0f, 0.85f);
        public static readonly Color BarFill = new Color(1f, 0.3f, 0.12f, 1f);
        public static readonly Color Warning = new Color(1f, 0.72f, 0.1f, 1f);
        public static readonly Color Info = new Color(0.55f, 0.8f, 1f, 1f);
        public static readonly Color Danger = new Color(1f, 0.25f, 0.2f, 1f);

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
