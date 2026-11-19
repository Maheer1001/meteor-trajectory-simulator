using System;
using System.Collections.Generic;

namespace MTS.Core
{
    /// <summary>A ready-made scenario modelled on a real (or textbook) event: mass, composition and inbound entry.</summary>
    public sealed class ImpactScenario
    {
        public string Id { get; }
        public string Name { get; }
        /// <summary>One line for the UI.</summary>
        public string Description { get; }
        /// <summary>Mass (kg).</summary>
        public double Mass { get; }
        public MeteorComposition Composition { get; }
        public EntryMode Mode { get; }
        /// <summary>Speed at the 120 km entry interface (m/s).</summary>
        public double EntrySpeed { get; }
        /// <summary>Path angle below the horizontal at entry (degrees).</summary>
        public double EntryAngleDegrees { get; }

        public ImpactScenario(string id, string name, string description, double mass, MeteorComposition composition,
            double entrySpeed, double entryAngleDegrees, EntryMode mode = EntryMode.Inbound)
        {
            Id = id;
            Name = name;
            Description = description;
            Mass = mass;
            Composition = composition ?? throw new ArgumentNullException(nameof(composition));
            EntrySpeed = entrySpeed;
            EntryAngleDegrees = entryAngleDegrees;
            Mode = mode;
        }

        /// <summary>Equal-mass sphere diameter of the impactor (m).</summary>
        public double Diameter => 2.0 * Math.Pow(3.0 * Mass / (4.0 * Math.PI * Composition.Material.Density), 1.0 / 3.0);

        /// <summary>Kinetic energy at entry (kt TNT).</summary>
        public double EntryEnergyKilotons => 0.5 * Mass * EntrySpeed * EntrySpeed / PhysicalConstants.JoulesPerKilotonTnt;

        /// <summary>Applies the scenario's composition, mode, speed and angle to the session (mass is an input).</summary>
        public void ApplyTo(SimulationSession session)
        {
            session.Composition = Composition;
            session.Mode = Mode;
            session.EntrySpeed = EntrySpeed;
            session.EntryAngleDegrees = EntryAngleDegrees;
        }
    }

    /// <summary>
    /// Famous and textbook impact scenarios, from a house-sized fireball to the dinosaur killer. Sizes, speeds and angles
    /// are commonly quoted estimates: Chelyabinsk (Brown et al. 2013; Popova et al. 2013), Tunguska (Chyba et al. 1993;
    /// Boslough and Crawford 2008), Barringer / Meteor Crater (Melosh and Collins 2005), Chicxulub (Collins et al. 2020).
    /// </summary>
    public static class ImpactScenarios
    {
        public static readonly ImpactScenario Chelyabinsk = new ImpactScenario("chelyabinsk", "Chelyabinsk 2013",
            "~19 m stony asteroid; ~500 kt airburst that shattered windows across a city",
            1.2e7, MeteorComposition.Single(MeteorMaterialPresets.OrdinaryChondriteId), 19000, 18);

        public static readonly ImpactScenario Tunguska = new ImpactScenario("tunguska", "Tunguska 1908",
            "~60 m stony body; 10-15 Mt airburst that flattened 2,000 square km of forest",
            3.5e8, MeteorComposition.Single(MeteorMaterialPresets.OrdinaryChondriteId), 15000, 45);

        public static readonly ImpactScenario Barringer = new ImpactScenario("barringer", "Meteor Crater (Barringer)",
            "~50 m iron asteroid; made the 1.2 km Arizona crater 50,000 years ago",
            5.1e8, MeteorComposition.Single(MeteorMaterialPresets.IronId), 12800, 45);

        public static readonly ImpactScenario RegionalKiller = new ImpactScenario("one-km", "1 km asteroid",
            "A 1 km stony asteroid: regional devastation and global climate effects",
            1.7e12, MeteorComposition.Single(MeteorMaterialPresets.OrdinaryChondriteId), 20000, 45);

        public static readonly ImpactScenario Chicxulub = new ImpactScenario("chicxulub", "Chicxulub (dinosaur killer)",
            "~10 km carbonaceous asteroid; 180 km crater and a mass extinction 66 million years ago",
            1.25e15, MeteorComposition.Single(MeteorMaterialPresets.CarbonaceousId), 20000, 60);

        private static readonly ImpactScenario[] all = { Chelyabinsk, Tunguska, Barringer, RegionalKiller, Chicxulub };

        /// <summary>All scenarios, smallest first.</summary>
        public static IReadOnlyList<ImpactScenario> All => all;

        public static ImpactScenario ById(string id)
        {
            foreach (ImpactScenario s in all)
            {
                if (string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) return s;
            }

            return null;
        }
    }
}
