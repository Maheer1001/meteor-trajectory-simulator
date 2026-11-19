using System;
using System.Collections.Generic;

namespace MTS.Core
{
    /// <summary>How the meteor is started off.</summary>
    public enum EntryMode
    {
        /// <summary>The original NEA set-up: released from rest at the chosen coordinates, falling under gravity.</summary>
        ClassicNea,
        /// <summary>Released at the chosen coordinates on an inbound path that reaches the entry interface at a set speed and angle.</summary>
        RealisticEntry,
    }

    /// <summary>
    /// Validated simulation inputs. Built once when a run is committed; the runner never reads UI statics.
    /// </summary>
    public sealed class MeteorProperties
    {
        // Mid-range defaults from the NEA design, used when an input is missing or invalid.
        public const double DefaultMass = 30000.0;
        public const double DefaultDensity = 4500.0;
        public const double DefaultSpecificHeat = 800.0;
        public const double DefaultScf = 0.5;
        public const double DefaultCoordinate = 25.0;

        /// <summary>One coordinate unit on the input screen is 4050 km.</summary>
        public const double MetresPerCoordinateUnit = 4.05e6;

        public const double DefaultEntrySpeed = 17000.0;
        public const double DefaultEntryAngleDegrees = 45.0;

        /// <summary>Initial mass (kg).</summary>
        public double Mass { get; private set; }
        public MeteorMaterial Material { get; private set; }
        /// <summary>Shape-complexity factor 0..1 (0 = sphere).</summary>
        public double Scf { get; private set; }
        /// <summary>Cross-section multiplier relative to a sphere of equal volume: 1 + 0.24 SCF (a cube averages about 1.24).</summary>
        public double ShapeFactor => 1.0 + 0.24 * Scf;
        /// <summary>Start position (m), Earth-centred.</summary>
        public Vec3d StartPosition { get; private set; }
        public EntryMode Mode { get; private set; }
        /// <summary>Realistic mode: speed at the entry interface (m/s).</summary>
        public double EntrySpeed { get; private set; }
        /// <summary>Realistic mode: flight-path angle below the local horizontal at the entry interface (degrees).</summary>
        public double EntryAngleDegrees { get; private set; }
        /// <summary>Aerodynamic break-up and pancake spreading (Collins et al. 2005) enabled.</summary>
        public bool EnableFragmentation { get; private set; } = true;
        /// <summary>Pancake factor f_p: airburst when the spreading diameter reaches f_p times the break-up diameter.</summary>
        public double PancakeFactor { get; private set; } = 7.0;

        /// <summary>Optional explicit initial velocity (m/s) overriding the mode's set-up (tests and tools).</summary>
        public Vec3d? InitialVelocity { get; private set; }

        /// <summary>Inputs that were missing/invalid and replaced by defaults.</summary>
        public IReadOnlyList<string> Substitutions => substitutions;
        private readonly List<string> substitutions = new List<string>();

        private MeteorProperties()
        {
        }

        /// <summary>
        /// Builds properties from the NEA input screen values. Missing (0), non-finite or out-of-range values are
        /// replaced with the documented defaults and listed in <see cref="Substitutions"/>.
        /// </summary>
        public static MeteorProperties FromInputs(double mass, double density, double specificHeat, double scf,
            double x, double y, double z, EntryMode mode = EntryMode.ClassicNea,
            double entrySpeed = DefaultEntrySpeed, double entryAngleDegrees = DefaultEntryAngleDegrees,
            bool enableFragmentation = true)
        {
            var p = new MeteorProperties();
            p.Mass = p.Positive(mass, DefaultMass, "mass");
            double rho = p.Positive(density, DefaultDensity, "density");
            double c = p.Positive(specificHeat, DefaultSpecificHeat, "SHC");
            p.Scf = p.InRange(scf, 0.0, 1.0, DefaultScf, "SCF");
            p.Material = MeteorMaterial.CreateCustom(rho, c, p.Scf);
            p.StartPosition = new Vec3d(p.Coordinate(x, "X"), p.Coordinate(y, "Y"), p.Coordinate(z, "Z")) * MetresPerCoordinateUnit;
            p.Mode = mode;
            p.EntrySpeed = p.Positive(entrySpeed, DefaultEntrySpeed, "entry speed");
            p.EntryAngleDegrees = p.InRange(entryAngleDegrees, 1.0, 90.0, DefaultEntryAngleDegrees, "entry angle");
            p.EnableFragmentation = enableFragmentation;
            return p;
        }

        /// <summary>Builds properties directly (used by tests and tools).</summary>
        public static MeteorProperties Create(double mass, MeteorMaterial material, double scf, Vec3d startPosition,
            EntryMode mode = EntryMode.ClassicNea, double entrySpeed = DefaultEntrySpeed,
            double entryAngleDegrees = DefaultEntryAngleDegrees, bool enableFragmentation = true, double pancakeFactor = 7.0,
            Vec3d? initialVelocity = null)
        {
            if (!(mass > 0) || double.IsInfinity(mass))
            {
                throw new ArgumentOutOfRangeException(nameof(mass));
            }

            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            if (!startPosition.IsFinite || startPosition.Magnitude <= PhysicalConstants.EarthRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(startPosition), "Start position must be above the Earth's surface.");
            }

            return new MeteorProperties
            {
                Mass = mass,
                Material = material,
                Scf = Math.Max(0, Math.Min(1, scf)),
                StartPosition = startPosition,
                Mode = mode,
                EntrySpeed = entrySpeed,
                EntryAngleDegrees = entryAngleDegrees,
                EnableFragmentation = enableFragmentation,
                PancakeFactor = pancakeFactor,
                InitialVelocity = initialVelocity,
            };
        }

        private double Positive(double value, double fallback, string name)
        {
            if (value > 0 && !double.IsInfinity(value))
            {
                return value;
            }

            substitutions.Add(name + " -> " + fallback);
            return fallback;
        }

        private double InRange(double value, double min, double max, double fallback, string name)
        {
            if (value >= min && value <= max)
            {
                return value;
            }

            substitutions.Add(name + " -> " + fallback);
            return fallback;
        }

        // Mirrors the input screen's rule: |c| < 1 (including a blank field) means "use 25".
        private double Coordinate(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) < 1.0)
            {
                substitutions.Add(name + " -> " + DefaultCoordinate);
                return DefaultCoordinate;
            }

            return Math.Max(-50.0, Math.Min(50.0, value));
        }
    }
}
