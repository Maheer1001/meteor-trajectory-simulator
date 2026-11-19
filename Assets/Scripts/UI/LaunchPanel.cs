using System.Collections.Generic;
using System.Globalization;
using MTS.Core;
using MTS.Core.Display;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Hud
{
    /// <summary>
    /// Space-stage launch settings: start mode (classic NEA / realistic entry), entry speed and angle, and the
    /// meteoroid material (custom from the input screen, a preset, or a two-component mix).
    /// Every change is written straight to <see cref="SimulationSession"/>; it takes effect when the run is
    /// (re)launched, because the session only reads these settings when a run begins.
    /// </summary>
    public sealed class LaunchPanel : MonoBehaviour
    {
        public const float MinEntrySpeedKms = 11f;
        public const float MaxEntrySpeedKms = 72f;
        public const float MinEntryAngle = 5f;
        public const float MaxEntryAngle = 90f;

        [Header("Collapse")]
        public Button collapseButton;
        public TMP_Text collapseLabel;
        public GameObject body;

        [Header("Mode")]
        public Button classicButton;
        public Button realisticButton;
        public GameObject realisticRows;
        public Slider speedSlider;
        public TMP_Text speedValue;
        public Slider angleSlider;
        public TMP_Text angleValue;

        [Header("Material")]
        public Button materialPrev;
        public Button materialNext;
        public TMP_Text materialName;
        public TMP_Text materialInfo;
        public GameObject mixRows;
        public Button mixAPrev;
        public Button mixANext;
        public TMP_Text mixAName;
        public Button mixBPrev;
        public Button mixBNext;
        public TMP_Text mixBName;
        public Slider mixSlider;
        public TMP_Text mixValue;

        [Header("Launch")]
        public Button launchButton;
        public TMP_Text launchLabel;
        public TMP_Text hint;

        /// <summary>Raised when the user presses Launch / Relaunch.</summary>
        public event System.Action LaunchRequested;

        // Material choice: 0 = custom (inputs), 1..n = presets, n+1 = mix. Kept static so it survives scene loads.
        private static int materialChoice = -1;
        private static int mixA;
        private static int mixB = 3;
        private static float mixFractionA = 0.5f;
        private static bool settingsChanged;

        private bool refreshing;

        private static int PresetCount => MeteorMaterialPresets.All.Count;
        private static int MixChoice => PresetCount + 1;

        /// <summary>True when the settings differ from those of the current run (a relaunch would apply them).</summary>
        public static bool SettingsChanged => settingsChanged;

        public bool Expanded => body == null || body.activeSelf;

        private void Start()
        {
            SimulationSession session = SimulationSession.Instance;
            if (materialChoice < 0)
            {
                materialChoice = ChoiceFor(session.MaterialPreset);
            }

            Hook(collapseButton, () => SetExpanded(!Expanded));
            Hook(classicButton, () => SetMode(EntryMode.ClassicNea));
            Hook(realisticButton, () => SetMode(EntryMode.RealisticEntry));
            Hook(materialPrev, () => StepMaterial(-1));
            Hook(materialNext, () => StepMaterial(+1));
            Hook(mixAPrev, () => { mixA = Wrap(mixA - 1, PresetCount); ApplyMaterial(); });
            Hook(mixANext, () => { mixA = Wrap(mixA + 1, PresetCount); ApplyMaterial(); });
            Hook(mixBPrev, () => { mixB = Wrap(mixB - 1, PresetCount); ApplyMaterial(); });
            Hook(mixBNext, () => { mixB = Wrap(mixB + 1, PresetCount); ApplyMaterial(); });
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

            if (mixSlider != null)
            {
                mixSlider.minValue = 0;
                mixSlider.maxValue = 100;
                mixSlider.onValueChanged.AddListener(v =>
                {
                    if (refreshing) return;
                    mixFractionA = v / 100f;
                    ApplyMaterial();
                });
            }

            Refresh();
        }

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

        private void StepMaterial(int delta)
        {
            materialChoice = Wrap(materialChoice + delta, PresetCount + 2);
            ApplyMaterial();
        }

        /// <summary>Selects a material by choice index (0 custom, 1..4 presets, 5 mix). Used by tools and tests.</summary>
        public void SelectMaterial(int choice)
        {
            materialChoice = Wrap(choice, PresetCount + 2);
            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            SimulationSession.Instance.MaterialPreset = BuildMaterial();
            MarkChanged();
        }

        private static MeteorMaterial BuildMaterial()
        {
            if (materialChoice <= 0) return null;
            if (materialChoice <= PresetCount) return MeteorMaterialPresets.All[materialChoice - 1].Material;
            MeteorMaterial a = MeteorMaterialPresets.All[mixA].Material;
            MeteorMaterial b = MeteorMaterialPresets.All[mixB].Material;
            return MeteorMaterialPresets.Mix(new List<(MeteorMaterial, double)> { (a, mixFractionA), (b, 1.0 - mixFractionA) });
        }

        private static int ChoiceFor(MeteorMaterial material)
        {
            if (material == null) return 0;
            for (int i = 0; i < PresetCount; i++)
            {
                if (ReferenceEquals(MeteorMaterialPresets.All[i].Material, material)) return i + 1;
            }

            return MixChoice;
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
            bool realistic = session.Mode == EntryMode.RealisticEntry;
            CultureInfo ci = CultureInfo.InvariantCulture;

            SetSelected(classicButton, !realistic);
            SetSelected(realisticButton, realistic);
            if (realisticRows != null) realisticRows.SetActive(realistic);

            float speedKms = Mathf.Clamp((float)(session.EntrySpeed / 1000.0), MinEntrySpeedKms, MaxEntrySpeedKms);
            if (speedSlider != null) speedSlider.SetValueWithoutNotify(speedKms);
            if (speedValue != null) speedValue.text = UnitFormat.Speed(session.EntrySpeed);
            float angle = Mathf.Clamp((float)session.EntryAngleDegrees, MinEntryAngle, MaxEntryAngle);
            if (angleSlider != null) angleSlider.SetValueWithoutNotify(angle);
            if (angleValue != null) angleValue.text = UnitFormat.Angle(session.EntryAngleDegrees) + (angle >= 89.5f ? " (vertical)" : "");

            if (materialChoice < 0) materialChoice = ChoiceFor(session.MaterialPreset);
            bool mix = materialChoice == MixChoice;
            if (mixRows != null) mixRows.SetActive(mix);
            if (materialName != null)
            {
                materialName.text = materialChoice == 0 ? "Custom (from inputs)"
                    : mix ? "Two-material mix"
                    : MeteorMaterialPresets.All[materialChoice - 1].Material.Name;
            }

            if (mixAName != null) mixAName.text = ShortName(MeteorMaterialPresets.All[mixA].Material);
            if (mixBName != null) mixBName.text = ShortName(MeteorMaterialPresets.All[mixB].Material);
            if (mixSlider != null) mixSlider.SetValueWithoutNotify(mixFractionA * 100f);
            if (mixValue != null)
            {
                mixValue.text = Mathf.RoundToInt(mixFractionA * 100f).ToString(ci) + " / " +
                                Mathf.RoundToInt((1f - mixFractionA) * 100f).ToString(ci) + " %";
            }

            if (materialInfo != null)
            {
                MeteorMaterial m = session.MaterialPreset;
                materialInfo.text = m == null
                    ? "Density, SHC and SCF from the input screen"
                    : "Density " + m.Density.ToString("N0", ci) + " kg/m³   Strength " + UnitFormat.Pressure(m.Strength);
            }

            bool waiting = SimulationClock.HoldForLaunch;
            if (launchLabel != null) launchLabel.text = waiting ? "LAUNCH" : "RELAUNCH";
            if (launchButton != null)
            {
                SetColour(launchButton, waiting || settingsChanged ? HudTheme.Launch : HudTheme.ButtonOff);
            }

            if (hint != null)
            {
                hint.text = waiting ? "Choose settings, then launch (Space)."
                    : settingsChanged ? "Settings changed - relaunch to apply them."
                    : "Relaunch restarts the run from the start point.";
                hint.color = !waiting && settingsChanged ? HudTheme.Warning : HudTheme.Dim;
            }

            if (collapseLabel != null) collapseLabel.text = Expanded ? "HIDE" : "SETTINGS";
            refreshing = false;
        }

        private static string ShortName(MeteorMaterial m)
        {
            int paren = m.Name.IndexOf(" (", System.StringComparison.Ordinal);
            return paren > 0 ? m.Name.Substring(0, paren) : m.Name;
        }

        private static int Wrap(int v, int n) => ((v % n) + n) % n;

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
