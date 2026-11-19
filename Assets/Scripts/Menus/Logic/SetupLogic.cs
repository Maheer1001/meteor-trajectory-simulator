using System;
using System.Globalization;
using MTS.Core;

namespace MTS.Menus
{
    /// <summary>A full setup: mass, composition and start mode/entry (what a scenario sets).</summary>
    public readonly struct SetupState
    {
        public readonly double Mass;
        public readonly MeteorComposition Composition;
        public readonly EntryMode Mode;
        public readonly double EntrySpeed;
        public readonly double EntryAngle;

        public SetupState(double mass, MeteorComposition composition, EntryMode mode, double entrySpeed, double entryAngle)
        {
            Mass = mass;
            Composition = composition ?? MeteorComposition.Default;
            Mode = mode;
            EntrySpeed = entrySpeed;
            EntryAngle = entryAngle;
        }

        /// <summary>A fresh custom meteoroid: the default mass, 100% stony, released from rest.</summary>
        public static SetupState DefaultCustom => new SetupState(MeteorProperties.DefaultMass, MeteorComposition.Default,
            EntryMode.FromRest, MeteorProperties.DefaultEntrySpeed, MeteorProperties.DefaultEntryAngleDegrees);

        public static SetupState Capture(double mass, SimulationSession session) =>
            new SetupState(mass, session.Composition, session.Mode, session.EntrySpeed, session.EntryAngleDegrees);

        /// <summary>Writes composition, mode and entry to the session and returns the mass for the mass field.</summary>
        public double ApplyTo(SimulationSession session)
        {
            session.Composition = Composition;
            session.Mode = Mode;
            session.EntrySpeed = EntrySpeed;
            session.EntryAngleDegrees = EntryAngle;
            return Mass;
        }
    }

    /// <summary>
    /// Which scenario chip is selected. Picking a scenario remembers the user's own (custom) setup first, and picking
    /// Custom brings it back (or a default custom meteoroid), so Custom always works as a way back from a preset.
    /// Static so the choice survives leaving and re-entering the setup screen.
    /// </summary>
    public sealed class ScenarioSelection
    {
        public static ScenarioSelection Shared { get; } = new ScenarioSelection();

        private SetupState? savedCustom;
        private bool customChosen;

        /// <summary>Picks scenario <paramref name="index"/> (into <see cref="ImpactScenarios.All"/>; negative = Custom). Returns the new mass.</summary>
        public double Pick(int index, double mass, SimulationSession session)
        {
            ImpactScenario current = SetupLogic.Match(mass, session.Composition, session.Mode, session.EntrySpeed, session.EntryAngleDegrees);
            if (index < 0 || index >= ImpactScenarios.All.Count)
            {
                customChosen = true;
                if (current == null) return mass; // already custom: keep the values
                return (savedCustom ?? SetupState.DefaultCustom).ApplyTo(session);
            }

            // Leaving the user's own setup for a preset: remember it so Custom can bring it back.
            if (current == null)
            {
                savedCustom = SetupState.Capture(mass, session);
            }

            customChosen = false;
            return SetupLogic.Apply(ImpactScenarios.All[index], session);
        }

        /// <summary>Chip to highlight: 0 = Custom, otherwise 1 + index of the matching scenario.</summary>
        public int Selected(double mass, SimulationSession session)
        {
            if (customChosen) return 0;
            ImpactScenario match = SetupLogic.Match(mass, session.Composition, session.Mode, session.EntrySpeed, session.EntryAngleDegrees);
            if (match == null) return 0;
            for (int i = 0; i < ImpactScenarios.All.Count; i++)
            {
                if (ReferenceEquals(ImpactScenarios.All[i], match)) return i + 1;
            }

            return 0;
        }

        /// <summary>Forgets the remembered custom setup (tests).</summary>
        public void Reset()
        {
            savedCustom = null;
            customChosen = false;
        }
    }

    /// <summary>Scenario selection and the derived summary lines of the input screen (pure logic).</summary>
    public static class SetupLogic
    {
        public const double MinEntrySpeedKms = 11.0;
        public const double MaxEntrySpeedKms = 72.0;
        public const double MinEntryAngle = 5.0;
        public const double MaxEntryAngle = 90.0;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Fills the session and returns the mass (kg) to put in the mass field.</summary>
        public static double Apply(ImpactScenario scenario, SimulationSession session)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (session == null) throw new ArgumentNullException(nameof(session));
            scenario.ApplyTo(session);
            return scenario.Mass;
        }

        /// <summary>The scenario whose mass, composition and entry these values equal, or null ("Custom").</summary>
        public static ImpactScenario Match(double mass, MeteorComposition composition, EntryMode mode, double entrySpeed, double entryAngle)
        {
            foreach (ImpactScenario s in ImpactScenarios.All)
            {
                if (Close(mass, s.Mass, 1e-4) && mode == s.Mode && Close(entrySpeed, s.EntrySpeed, 1e-6) &&
                    Close(entryAngle, s.EntryAngleDegrees, 1e-6) && SameComposition(composition, s.Composition))
                {
                    return s;
                }
            }

            return null;
        }

        public static bool SameComposition(MeteorComposition a, MeteorComposition b)
        {
            if (a == null || b == null || a.Parts.Count != b.Parts.Count) return false;
            for (int i = 0; i < a.Parts.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < b.Parts.Count; j++)
                {
                    if (a.Parts[i].Id != null && a.Parts[i].Id == b.Parts[j].Id && Math.Abs(a.Parts[i].Fraction - b.Parts[j].Fraction) < 1e-4)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found) return false;
            }

            return true;
        }

        private static bool Close(double a, double b, double relative) =>
            Math.Abs(a - b) <= relative * Math.Max(Math.Abs(a), Math.Abs(b));

        /// <summary>Strength in kPa or MPa, e.g. "200 kPa", "8 MPa", "50 MPa".</summary>
        public static string FormatStrength(double pascals)
        {
            if (pascals < 1e6) return MassInput.Number(pascals / 1e3) + " kPa";
            return MassInput.Number(pascals / 1e6) + " MPa";
        }

        public static string FormatDensity(double density) => Math.Round(density).ToString("#,0", Inv) + " kg/m³";

        /// <summary>Kinetic energy (kt TNT) of a mass at a speed.</summary>
        public static double EnergyKilotons(double mass, double speed) => 0.5 * mass * speed * speed / PhysicalConstants.JoulesPerKilotonTnt;

        /// <summary>TNT equivalent, e.g. "500 kt TNT", "12 Mt TNT", "1.2 × 10<sup>8</sup> Mt TNT".</summary>
        public static string FormatEnergy(double kilotons)
        {
            if (kilotons < 1) return MassInput.Number(kilotons * 1000) + " t TNT";
            if (kilotons < 1000) return MassInput.Number(kilotons) + " kt TNT";
            double mt = kilotons / 1000;
            if (mt < 1e5) return MassInput.Number(mt) + " Mt TNT";
            int exp = (int)Math.Floor(Math.Log10(mt));
            return (mt / Math.Pow(10, exp)).ToString("0.#", Inv) + " × 10<sup>" + exp.ToString(Inv) + "</sup> Mt TNT";
        }

        /// <summary>Distance from the Earth's centre (km) of start coordinates (1 unit = 4,050 km).</summary>
        public static double StartDistanceKm(double x, double y, double z) =>
            Math.Sqrt(x * x + y * y + z * z) * MeteorProperties.MetresPerCoordinateUnit / 1000.0;

        /// <summary>Clamps and snaps a start coordinate the way the simulation reads it (|c| &lt; 1 means 25).</summary>
        public static double Coordinate(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) < 1.0) return MeteorProperties.DefaultCoordinate;
            return Math.Max(-50.0, Math.Min(50.0, value));
        }
    }
}
