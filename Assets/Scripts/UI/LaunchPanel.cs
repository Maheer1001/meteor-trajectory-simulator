using System.Globalization;
using MTS.Core;
using MTS.Core.Display;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Hud
{
    /// <summary>
    /// Space-stage launch settings: start mode (from rest / inbound entry) with the entry speed and angle, and a
    /// read-only summary of the meteoroid (composition, mass, diameter) chosen on the input screen.
    /// Every change is written straight to <see cref="SimulationSession"/>; it takes effect when the run is
    /// (re)launched, because the session only reads these settings when a run begins. The panel reads the session
    /// on every refresh, so presets applied elsewhere (the input screen's scenarios) show up as soon as it opens.
    /// </summary>
    public sealed class LaunchPanel : MonoBehaviour
    {
        public const float MinEntrySpeedKms = 11f;
        public const float MaxEntrySpeedKms = 72f;
        public const float MinEntryAngle = 5f;
        public const float MaxEntryAngle = 90f;
        public const int MaxCompositionLines = MeteorComposition.MaxComponents;

        [Header("Collapse")]
        public Button collapseButton;
        public TMP_Text collapseLabel;
        public GameObject body;

        [Header("Mode")]
        public Button fromRestButton;
        public Button inboundButton;
        public GameObject inboundRows;
        public Slider speedSlider;
        public TMP_Text speedValue;
        public Slider angleSlider;
        public TMP_Text angleValue;

        [Header("Meteoroid (read-only, from the input screen)")]
        public GameObject[] compositionLines = new GameObject[MaxCompositionLines];
        public Image[] compositionSwatches = new Image[MaxCompositionLines];
        public TMP_Text[] compositionTexts = new TMP_Text[MaxCompositionLines];
        public TMP_Text massValue;
        public TMP_Text diameterValue;
        public GameObject energyRow;
        public TMP_Text energyValue;

        [Header("Launch")]
        public Button launchButton;
        public TMP_Text launchLabel;
        public TMP_Text hint;

        /// <summary>Raised when the user presses Launch / Relaunch.</summary>
        public event System.Action LaunchRequested;

        private static bool settingsChanged;
        private bool refreshing;

        /// <summary>True when the settings differ from those of the current run (a relaunch would apply them).</summary>
        public static bool SettingsChanged => settingsChanged;

        public bool Expanded => body == null || body.activeSelf;

        /// <summary>Mass the next run will start with (kg): the input screen's value, or the default when unset.</summary>
        public static double InputMass
        {
            get
            {
                double m = MassRangeInput.Mass;
                return m > 0 && !double.IsInfinity(m) ? m : MeteorProperties.DefaultMass;
            }
        }

        /// <summary>Diameter (m) of a sphere of the given mass and bulk density.</summary>
        public static double SphereDiameter(double mass, double density) =>
            density > 0 ? 2.0 * System.Math.Pow(3.0 * mass / (4.0 * System.Math.PI * density), 1.0 / 3.0) : double.NaN;

        /// <summary>One composition line, e.g. "70% Iron (Fe-Ni)".</summary>
        public static string PartLabel(CompositionPart part) =>
            System.Math.Round(part.Fraction * 100.0).ToString(CultureInfo.InvariantCulture) + "% " + part.Material.Name;

        /// <summary>Material name without the parenthesised detail ("Iron (Fe-Ni)" becomes "Iron").</summary>
        public static string ShortName(MeteorMaterial m)
        {
            int paren = m.Name.IndexOf(" (", System.StringComparison.Ordinal);
            return paren > 0 ? m.Name.Substring(0, paren) : m.Name;
        }

        /// <summary>One-line composition for narrow places: the name, "70% Iron + 30% Stony", or "Mix of 4 materials".</summary>
        public static string ShortComposition(MeteorComposition c)
        {
            if (c == null) return "";
            if (c.Parts.Count == 1) return ShortName(c.Parts[0].Material);
            if (c.Parts.Count > 2) return "Mix of " + c.Parts.Count + " materials";
            return System.Math.Round(c.Parts[0].Fraction * 100.0).ToString(CultureInfo.InvariantCulture) + "% " + ShortName(c.Parts[0].Material) + " + " +
                   System.Math.Round(c.Parts[1].Fraction * 100.0).ToString(CultureInfo.InvariantCulture) + "% " + ShortName(c.Parts[1].Material);
        }

        /// <summary>The start mode as the user calls it.</summary>
        public static string ModeName(EntryMode mode) => mode == EntryMode.Inbound ? "Inbound entry" : "From rest";

        private void Start()
        {
            Hook(collapseButton, () => SetExpanded(!Expanded));
            Hook(fromRestButton, () => SetMode(EntryMode.FromRest));
            Hook(inboundButton, () => SetMode(EntryMode.Inbound));
            Hook(launchButton, () => LaunchRequested?.Invoke());

            if (speedSlider != null)
            {
                speedSlider.minValue = MinEntrySpeedKms;
                speedSlider.maxValue = MaxEntrySpeedKms;
                speedSlider.onValueChanged.AddListener(v =>
                {
                    if (refreshing) return;
                    SimulationSession.Instance.EntrySpeed = v * 1000.0;
                    MarkChanged();
                });
            }

            if (angleSlider != null)
            {
                angleSlider.minValue = MinEntryAngle;
                angleSlider.maxValue = MaxEntryAngle;
                angleSlider.onValueChanged.AddListener(v =>
                {
                    if (refreshing) return;
                    SimulationSession.Instance.EntryAngleDegrees = v;
                    MarkChanged();
                });
            }

            Refresh();
        }

        private void OnEnable() => Refresh();

        /// <summary>Call after a launch: the session now runs with the panel's settings.</summary>
        public static void MarkApplied() => settingsChanged = false;

        public void SetExpanded(bool expanded)
        {
            if (body != null) body.SetActive(expanded);
            Refresh();
        }

        public void SetMode(EntryMode mode)
        {
            SimulationSession session = SimulationSession.Instance;
            if (session.Mode == mode) return;
            session.Mode = mode;
            MarkChanged();
        }

        private void MarkChanged()
        {
            settingsChanged = true;
            Refresh();
        }

        /// <summary>Updates every control from the session (does not write back).</summary>
        public void Refresh()
        {
            refreshing = true;
            SimulationSession session = SimulationSession.Instance;
            bool inbound = session.Mode == EntryMode.Inbound;

            SetSelected(fromRestButton, !inbound);
            SetSelected(inboundButton, inbound);
            if (inboundRows != null) inboundRows.SetActive(inbound);

            float speedKms = Mathf.Clamp((float)(session.EntrySpeed / 1000.0), MinEntrySpeedKms, MaxEntrySpeedKms);
            if (speedSlider != null) speedSlider.SetValueWithoutNotify(speedKms);
            if (speedValue != null) speedValue.text = UnitFormat.Speed(session.EntrySpeed);
            float angle = Mathf.Clamp((float)session.EntryAngleDegrees, MinEntryAngle, MaxEntryAngle);
            if (angleSlider != null) angleSlider.SetValueWithoutNotify(angle);
            if (angleValue != null) angleValue.text = UnitFormat.Angle(session.EntryAngleDegrees) + (angle >= 89.5f ? " (vertical)" : "");

            MeteorComposition composition = session.Composition;
            for (int i = 0; i < MaxCompositionLines; i++)
            {
                bool used = composition != null && i < composition.Parts.Count;
                if (compositionLines != null && i < compositionLines.Length && compositionLines[i] != null) compositionLines[i].SetActive(used);
                if (!used) continue;
                CompositionPart part = composition.Parts[i];
                if (compositionTexts != null && i < compositionTexts.Length && compositionTexts[i] != null) compositionTexts[i].text = PartLabel(part);
                if (compositionSwatches != null && i < compositionSwatches.Length && compositionSwatches[i] != null)
                {
                    MaterialAppearance look = part.Material.Appearance;
                    compositionSwatches[i].color = new Color(look.BaseR, look.BaseG, look.BaseB, 1f);
                }
            }

            double mass = InputMass;
            if (massValue != null) massValue.text = UnitFormat.Mass(mass);
            if (diameterValue != null) diameterValue.text = UnitFormat.Length(SphereDiameter(mass, composition != null ? composition.Material.Density : 0));
            if (energyRow != null) energyRow.SetActive(inbound);
            if (energyValue != null) energyValue.text = UnitFormat.Energy(0.5 * mass * session.EntrySpeed * session.EntrySpeed);

            bool waiting = SimulationClock.HoldForLaunch;
            if (launchLabel != null) launchLabel.text = waiting ? "LAUNCH" : "RELAUNCH";
            if (launchButton != null)
            {
                SetColour(launchButton, waiting || settingsChanged ? HudTheme.Launch : HudTheme.ButtonOff);
            }

            if (hint != null)
            {
                hint.text = waiting ? "Choose a mode, then launch (Space)"
                    : settingsChanged ? "Settings changed: relaunch to apply"
                    : "Relaunch restarts from the start point";
                hint.color = !waiting && settingsChanged ? HudTheme.Warning : HudTheme.Dim;
            }

            if (collapseLabel != null) collapseLabel.text = Expanded ? "HIDE" : "SHOW";
            refreshing = false;
        }

        private static void Hook(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        private static void SetSelected(Button b, bool selected) => SetColour(b, selected ? HudTheme.Button : HudTheme.ButtonOff);

        private static void SetColour(Button b, Color c)
        {
            if (b != null && b.targetGraphic != null) b.targetGraphic.color = c;
        }
    }
}
