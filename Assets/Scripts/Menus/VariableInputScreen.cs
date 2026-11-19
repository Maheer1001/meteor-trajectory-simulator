using System;
using System.Globalization;
using MTS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Menus
{
    /// <summary>
    /// The input screen: scenario presets, the composition mixer (one to four materials), mass (1,000 kg to 1e16 kg),
    /// start mode and start position. Every edit is written straight away: the mass and coordinates to the existing
    /// input statics (<see cref="MassRangeInput"/>, <see cref="Xcord"/>, <see cref="Ycord"/>, <see cref="Zcord"/>) and the
    /// composition, start mode, entry speed and angle to <see cref="SimulationSession"/>, so coming back to this screen
    /// shows the current values. The layout is built by MTS.Menus.Editor.MenusSceneBuilder.
    /// </summary>
    public sealed class VariableInputScreen : MonoBehaviour
    {
        [Serializable]
        public sealed class MaterialRowView
        {
            public GameObject root;
            public Image swatch;
            public Button prev;
            public Button next;
            public TMP_Text name;
            public Slider amount;
            public TMP_Text share;
            public Button remove;
        }

        [Header("Scenario")]
        public Button[] scenarioButtons; // [0] = Custom, then ImpactScenarios.All in order
        public TMP_Text scenarioDescription;

        [Header("Composition")]
        public MaterialRowView[] rows;
        public Button addButton;
        public TMP_Text addLabel;
        public TMP_Text totalText;
        public Image swatchBase;
        public Image swatchAccent;
        public TMP_Text compositionName;
        public TMP_Text densityValue;
        public TMP_Text strengthValue;
        public TMP_Text diameterValue;

        [Header("Mass")]
        public TMP_InputField massField;
        public Slider massSlider; // log10(kg)
        public TMP_Text massReadout;
        public TMP_Text massHint;

        [Header("Start")]
        public Button fromRestButton;
        public Button inboundButton;
        public GameObject entryRows;
        public TMP_Text restHint;
        public Slider speedSlider; // km/s
        public TMP_Text speedValue;
        public Slider angleSlider; // degrees
        public TMP_Text angleValue;
        public TMP_Text energyValue;

        [Header("Position")]
        public TMP_InputField xField;
        public TMP_InputField yField;
        public TMP_InputField zField;
        public TMP_Text positionReadout;

        [Header("Navigation")]
        public Button startButton;
        public Button backButton;
        public MainMenuButton navigation;
        public MassRangeInput massInput;
        public Xcord xcord;
        public Ycord ycord;
        public Zcord zcord;

        /// <summary>Composition rows: top of the first row and the row pitch inside the card (px at 1080p).</summary>
        public const float RowTop = 104f;
        public const float RowPitch = 68f;

        public const string MassHintText = "Type 30000, 1.2e7 or 12 kt. Range 1 t to 10,000 Gt.";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private CompositionMixer mixer = new CompositionMixer();
        private bool wired;

        private static SimulationSession Session => SimulationSession.Instance;

        private void Awake() => Wire();

        private void Start() => LoadFromSession();

        /// <summary>Hooks up the listeners (also called by the capture tool in edit mode).</summary>
        public void Wire()
        {
            if (wired) return;
            wired = true;
            for (int i = 0; i < scenarioButtons.Length; i++)
            {
                int index = i;
                scenarioButtons[i].onClick.AddListener(() => PickScenario(index - 1));
            }

            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                rows[i].prev.onClick.AddListener(() => CycleMaterial(index, -1));
                rows[i].next.onClick.AddListener(() => CycleMaterial(index, 1));
                rows[i].remove.onClick.AddListener(() => RemoveMaterial(index));
                rows[i].amount.onValueChanged.AddListener(v => SetAmount(index, v));
            }

            addButton.onClick.AddListener(AddMaterial);
            massField.onValueChanged.AddListener(PreviewMass);
            massField.onEndEdit.AddListener(CommitMass);
            massSlider.onValueChanged.AddListener(SlideMass);
            fromRestButton.onClick.AddListener(() => SetMode(EntryMode.FromRest));
            inboundButton.onClick.AddListener(() => SetMode(EntryMode.Inbound));
            speedSlider.onValueChanged.AddListener(v =>
            {
                Session.EntrySpeed = v * 1000.0;
                Refresh();
            });
            angleSlider.onValueChanged.AddListener(v =>
            {
                Session.EntryAngleDegrees = v;
                Refresh();
            });
            xField.onEndEdit.AddListener(s => CommitCoordinate(xField, s, 0));
            yField.onEndEdit.AddListener(s => CommitCoordinate(yField, s, 1));
            zField.onEndEdit.AddListener(s => CommitCoordinate(zField, s, 2));
            startButton.onClick.AddListener(Commence);
            backButton.onClick.AddListener(() => navigation.MainMenu("MainMenu"));
        }

        /// <summary>Shows the session's current values (used on entry, so the screen survives a round trip).</summary>
        public void LoadFromSession()
        {
            mixer = CompositionMixer.From(Session.Composition);
            massField.SetTextWithoutNotify(MassInput.ToFieldText(MassRangeInput.Mass));
            SetHint(MassHintText, false);
            xField.SetTextWithoutNotify(Coordinate(Xcord.X));
            yField.SetTextWithoutNotify(Coordinate(Ycord.Y));
            zField.SetTextWithoutNotify(Coordinate(Zcord.Z));
            Refresh();
        }

        // ---- Scenarios ----

        /// <summary>
        /// Loads a scenario (index into <see cref="ImpactScenarios.All"/>), or -1 for Custom, which brings back the
        /// setup you had before picking a scenario (or a default custom meteoroid).
        /// </summary>
        public void PickScenario(int index)
        {
            MassRangeInput.Mass = (float)ScenarioSelection.Shared.Pick(index, MassRangeInput.Mass, Session);
            mixer = CompositionMixer.From(Session.Composition);
            massField.SetTextWithoutNotify(MassInput.ToFieldText(MassRangeInput.Mass));
            SetHint(MassHintText, false);
            Refresh();
        }

        // ---- Composition ----

        public void AddMaterial()
        {
            if (mixer.Add() >= 0) ApplyComposition();
        }

        public void RemoveMaterial(int index)
        {
            if (mixer.Remove(index)) ApplyComposition();
        }

        public void CycleMaterial(int index, int direction)
        {
            mixer.Cycle(index, direction);
            ApplyComposition();
        }

        public void SetMaterial(int index, string id)
        {
            mixer.SetMaterial(index, id);
            ApplyComposition();
        }

        public void SetAmount(int index, float value)
        {
            mixer.SetAmount(index, value);
            ApplyComposition();
        }

        /// <summary>The mixer's rows (for tools and tests of the screen).</summary>
        public CompositionMixer Mixer => mixer;

        private void ApplyComposition()
        {
            MeteorComposition composition = mixer.Build();
            if (composition != null) Session.Composition = composition;
            Refresh();
        }

        // ---- Mass ----

        private void PreviewMass(string text)
        {
            if (MassInput.TryParse(text, out double kg))
            {
                MassRangeInput.Mass = (float)MassInput.Clamp(kg);
                SetHint(kg < MassInput.MinMass ? "Below the minimum: 1,000 kg will be used." :
                    kg > MassInput.MaxMass ? "Above the maximum: 1e16 kg will be used." : MassHintText, kg < MassInput.MinMass || kg > MassInput.MaxMass);
                Refresh();
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                SetHint("Not a number yet. Try 30000, 1.2e7 or 12 kt.", true);
            }
        }

        public void CommitMass(string text)
        {
            bool ok = MassRangeInput.TrySet(text, out string message);
            massField.SetTextWithoutNotify(MassInput.ToFieldText(MassRangeInput.Mass));
            SetHint(ok ? MassHintText : message.Replace("1e16", "10<sup>16</sup>"), !ok);
            Refresh();
        }

        private void SlideMass(float log10)
        {
            double kg = MassInput.Clamp(Snap(Math.Pow(10, log10)));
            MassRangeInput.Mass = (float)kg;
            massField.SetTextWithoutNotify(MassInput.ToFieldText(kg));
            SetHint(MassHintText, false);
            Refresh();
        }

        /// <summary>Two significant figures (the slider is for rough choices; the field takes exact values).</summary>
        private static double Snap(double v)
        {
            if (!(v > 0)) return v;
            double step = Math.Pow(10, Math.Floor(Math.Log10(v)) - 1);
            return Math.Round(v / step) * step;
        }

        private void SetHint(string text, bool problem)
        {
            if (massHint == null) return;
            massHint.text = text;
            massHint.color = problem ? MenuTheme.Error : MenuTheme.Muted;
        }

        // ---- Start mode and position ----

        public void SetMode(EntryMode mode)
        {
            Session.Mode = mode;
            Refresh();
        }

        private void CommitCoordinate(TMP_InputField field, string text, int axis)
        {
            switch (axis)
            {
                case 0:
                    xcord.StringReader(text);
                    field.SetTextWithoutNotify(Coordinate(Xcord.X));
                    break;
                case 1:
                    ycord.StringReader(text);
                    field.SetTextWithoutNotify(Coordinate(Ycord.Y));
                    break;
                default:
                    zcord.StringReader(text);
                    field.SetTextWithoutNotify(Coordinate(Zcord.Z));
                    break;
            }

            Refresh();
        }

        private static string Coordinate(float v) => v.ToString("0.##", Inv);

        /// <summary>Commits any field still being edited and starts the simulation (SpaceSimulation).</summary>
        public void Commence()
        {
            if (massField.text != MassInput.ToFieldText(MassRangeInput.Mass)) CommitMass(massField.text);
            if (xField.text != Coordinate(Xcord.X)) CommitCoordinate(xField, xField.text, 0);
            if (yField.text != Coordinate(Ycord.Y)) CommitCoordinate(yField, yField.text, 1);
            if (zField.text != Coordinate(Zcord.Z)) CommitCoordinate(zField, zField.text, 2);
            ApplyComposition();
            navigation.MainMenu("SpaceSimulation");
        }

        // ---- Display ----

        public void Refresh()
        {
            SimulationSession session = Session;
            double mass = MassRangeInput.Mass;
            MeteorComposition composition = session.Composition;

            // Scenario chips.
            int selected = ScenarioSelection.Shared.Selected(mass, session);
            ImpactScenario match = selected > 0 ? ImpactScenarios.All[selected - 1] : null;
            for (int i = 0; i < scenarioButtons.Length; i++) MenuUi.SetSelected(scenarioButtons[i], i == selected);
            if (scenarioDescription != null)
            {
                scenarioDescription.text = match == null
                    ? "<color=#FFCC47>Custom</color>  Your own mass, mix and start. Pick a scenario above to load a famous impact."
                    : "<color=#FFCC47>" + match.Name + "</color>  " + match.Description + ".";
            }

            // Composition rows.
            int[] percents = mixer.DisplayPercents();
            for (int i = 0; i < rows.Length; i++)
            {
                MaterialRowView row = rows[i];
                bool used = i < mixer.Count;
                row.root.SetActive(used);
                if (!used) continue;
                MeteorMaterial m = mixer.Rows[i].Material;
                row.name.text = m.Name;
                row.swatch.color = BaseColour(m.Appearance);
                row.amount.SetValueWithoutNotify((float)mixer.Rows[i].Amount);
                row.share.text = percents[i].ToString(Inv) + "%";
                row.remove.interactable = mixer.CanRemove;
            }

            addButton.interactable = mixer.CanAdd;
            MenuUi.SetSelected(addButton, mixer.CanAdd);
            var addRect = (RectTransform)addButton.transform;
            addRect.anchoredPosition = new Vector2(addRect.anchoredPosition.x, -(RowTop + Math.Min(mixer.Count, rows.Length) * RowPitch));
            if (addLabel != null) addLabel.text = mixer.CanAdd ? "+ ADD MATERIAL" : "4 MATERIALS MAX";
            if (totalText != null)
            {
                totalText.text = mixer.IsValid ? "TOTAL 100%" : "TOTAL 0%";
                totalText.color = mixer.IsValid ? MenuTheme.Muted : MenuTheme.Error;
            }

            MeteorMaterial bulk = composition.Material;
            double diameter = MassInput.SphereDiameter(mass, bulk.Density);
            if (compositionName != null)
            {
                compositionName.text = mixer.IsValid ? composition.Describe() : "Give at least one material an amount above 0%.";
                compositionName.color = mixer.IsValid ? MenuTheme.Text : MenuTheme.Error;
            }

            if (swatchBase != null) swatchBase.color = BaseColour(bulk.Appearance);
            if (swatchAccent != null) swatchAccent.color = AccentColour(bulk.Appearance);
            if (densityValue != null) densityValue.text = SetupLogic.FormatDensity(bulk.Density);
            if (strengthValue != null) strengthValue.text = SetupLogic.FormatStrength(bulk.Strength);
            if (diameterValue != null) diameterValue.text = "about " + MassInput.FormatLength(diameter);

            // Mass.
            massSlider.SetValueWithoutNotify((float)Math.Log10(mass));
            if (massReadout != null)
            {
                massReadout.text = MassInput.Format(mass) + "   <color=#B8B8CC>" + MassInput.FormatKg(mass) +
                                   "   about " + MassInput.FormatLength(diameter) + " across</color>";
            }

            // Start mode.
            bool inbound = session.Mode == EntryMode.Inbound;
            MenuUi.SetSelected(fromRestButton, !inbound);
            MenuUi.SetSelected(inboundButton, inbound);
            if (entryRows != null) entryRows.SetActive(inbound);
            if (restHint != null) restHint.gameObject.SetActive(!inbound);
            double speedKms = session.EntrySpeed / 1000.0;
            speedSlider.SetValueWithoutNotify((float)speedKms);
            angleSlider.SetValueWithoutNotify((float)session.EntryAngleDegrees);
            if (speedValue != null) speedValue.text = speedKms.ToString("0.#", Inv) + " km/s";
            if (angleValue != null) angleValue.text = session.EntryAngleDegrees.ToString("0", Inv) + "°";
            if (energyValue != null)
            {
                energyValue.text = "Energy at entry  <color=#FFCC47>" +
                                   SetupLogic.FormatEnergy(SetupLogic.EnergyKilotons(mass, session.EntrySpeed)) + "</color>";
            }

            // Position.
            if (positionReadout != null)
            {
                double distance = SetupLogic.StartDistanceKm(SetupLogic.Coordinate(Xcord.X), SetupLogic.Coordinate(Ycord.Y), SetupLogic.Coordinate(Zcord.Z));
                double altitude = distance - PhysicalConstants.EarthRadius / 1000.0;
                positionReadout.text = "About " + (Math.Round(altitude / 100.0) * 100.0).ToString("#,0", Inv) +
                                       " km above the ground (1 unit = 4,050 km).\nEach axis: -50 to -1 or 1 to 50.";
            }
        }

        private static int IndexOf(ImpactScenario s)
        {
            for (int i = 0; i < ImpactScenarios.All.Count; i++)
            {
                if (ReferenceEquals(ImpactScenarios.All[i], s)) return i;
            }

            return -1;
        }

        public static Color BaseColour(MaterialAppearance a) => new Color(a.BaseR, a.BaseG, a.BaseB, 1f);

        public static Color AccentColour(MaterialAppearance a) => new Color(a.AccentR, a.AccentG, a.AccentB, 1f);

        /// <summary>Short label for a scenario chip: the name without its parenthesis ("Chicxulub", "Meteor Crater").</summary>
        public static string ShortName(ImpactScenario s)
        {
            int at = s.Name.IndexOf(" (", StringComparison.Ordinal);
            return (at > 0 ? s.Name.Substring(0, at) : s.Name).ToUpperInvariant();
        }
    }
}
