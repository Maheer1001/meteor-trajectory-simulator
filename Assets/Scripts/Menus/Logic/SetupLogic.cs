using System;
using System.Globalization;
using MTS.Core;

namespace MTS.Menus
{
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
