using System;
using System.Collections.Generic;

namespace MTS.Core
{
    /// <summary>One thermal-damage threshold of EIEP Table 1 and how far it reaches.</summary>
    public readonly struct ThermalThreshold
    {
        public readonly string Name;
        /// <summary>Plain-language consequence for the UI.</summary>
        public readonly string Effect;
        /// <summary>Exposure needed for a 1 Mt event (J/m^2), EIEP Table 1.</summary>
        public readonly double ExposureAt1Mt;
        /// <summary>Exposure needed for this event (J/m^2), Eq. 39.</summary>
        public readonly double Exposure;
        /// <summary>Ground range (m) out to which the exposure is reached; 0 if nowhere.</summary>
        public readonly double Radius;

        public ThermalThreshold(string name, string effect, double exposureAt1Mt, double exposure, double radius)
        {
            Name = name;
            Effect = effect;
            ExposureAt1Mt = exposureAt1Mt;
            Exposure = exposure;
            Radius = radius;
        }
    }

    /// <summary>A ground range at which an effect reaches a stated level.</summary>
    public readonly struct EffectRadius
    {
        public readonly string Name;
        public readonly string Effect;
        public readonly double Radius;

        public EffectRadius(string name, string effect, double radius)
        {
            Name = name;
            Effect = effect;
            Radius = radius;
        }
    }

    /// <summary>Scale of the global consequences, by impact energy.</summary>
    public enum GlobalScale
    {
        /// <summary>Local effects only (below about 1,000 Mt).</summary>
        Local,
        /// <summary>Regional devastation (1,000 to 10,000 Mt).</summary>
        Regional,
        /// <summary>Continental devastation with a measurable climate signal (10,000 to 100,000 Mt).</summary>
        Continental,
        /// <summary>Global climate disruption: dust and aerosols, crop failures (100,000 to 1,000,000 Mt).</summary>
        GlobalClimate,
        /// <summary>Global catastrophe threatening civilisation (1e6 to 1e8 Mt).</summary>
        Civilisation,
        /// <summary>Mass extinction scale, like Chicxulub (1e8 Mt and above).</summary>
        MassExtinction,
    }

    /// <summary>
    /// Regional and global consequences of an impact besides the air blast: thermal radiation from the fireball, seismic
    /// shaking, the ejecta deposit, an ocean impact's tsunami, and a global-effects summary. Equations from Collins,
    /// Melosh and Marcus (2005), "Earth Impact Effects Program" (EIEP), Meteoritics and Planetary Science 40, 817-840,
    /// with equation numbers as in the paper. All quantities SI unless noted. Ground ranges r are measured along the
    /// surface from the impact point; they are capped at the antipode (<see cref="MaxGroundRange"/>).
    /// </summary>
    public sealed class ImpactEffects
    {
        // ------------------------------------------------------------------ constants

        /// <summary>EIEP computes thermal radiation only for impacts faster than 15 km/s (slower ones make little vapour).</summary>
        public const double ThermalSpeedThreshold = 15000.0;
        /// <summary>Luminous efficiency eta: fraction of the impact energy radiated as heat (EIEP, after Ortiz et al. 2000).</summary>
        public const double LuminousEfficiency = 3e-3;
        /// <summary>Fireball transparency temperature T* (K) used for the duration of irradiation (EIEP).</summary>
        public const double TransparencyTemperature = 3000.0;
        public const double StefanBoltzmann = 5.67e-8;
        /// <summary>Surface-wave speed of upper-crustal rocks (m/s), Eq. 42.</summary>
        public const double SurfaceWaveSpeed = 5000.0;
        /// <summary>EIEP limits the ejecta deposit to the fireball radius for impacts below 200 Mt.</summary>
        public const double EjectaFireballLimitMegatons = 200.0;
        /// <summary>Representative open-ocean depth (m) for impacts that hit the sea (the mean ocean depth is about 3.7 km).</summary>
        public const double DefaultOceanDepth = 4000.0;
        public const double WaterDensity = 1000.0;
        /// <summary>Sea-floor (crystalline crust) density used by EIEP for marine impacts.</summary>
        public const double SeafloorDensity = 2700.0;
        /// <summary>Drag coefficient of the impactor in water (supersonic regime), Eq. 65.</summary>
        public const double WaterDragCoefficient = 0.877;
        /// <summary>Crater-scaling constant for a water target (replaces 1.161 in Eq. 21; EIEP, after Schmidt and Housen 1987).</summary>
        public const double WaterCraterCoefficient = 1.365;
        public const double JoulesPerMegaton = PhysicalConstants.JoulesPerKilotonTnt * 1000.0;
        /// <summary>Earth's mass (kg) and mean orbital speed (m/s), as used by EIEP for Table 5.</summary>
        public const double EarthMass = 5.97e24;
        public const double EarthOrbitalSpeed = 29780.0;
        /// <summary>Earth's spin angular momentum (kg m^2/s), EIEP.</summary>
        public const double EarthAngularMomentum = 5.86e33;

        /// <summary>Half the Earth's circumference (m): no ground range is larger.</summary>
        public static double MaxGroundRange => Math.PI * PhysicalConstants.EarthRadius;
        /// <summary>A quarter of the Earth's circumference (m): radii beyond this are reported as global.</summary>
        public static double GlobalRange => 0.5 * Math.PI * PhysicalConstants.EarthRadius;

        /// <summary>EIEP Table 1 (data from Glasstone and Dolan 1977): exposure (J/m^2) to ignite or burn in a 1 Mt event.</summary>
        public static readonly (string Name, string Effect, double At1Mt)[] ThermalTable =
        {
            ("Clothing ignites", "Clothes catch fire on people outdoors", 1.0e6),
            ("Plywood ignites", "Wooden buildings catch fire", 0.67e6),
            ("Third-degree burns", "Full-thickness burns to exposed skin", 0.42e6),
            ("Grass ignites", "Grassland fires start", 0.38e6),
            ("Newspaper ignites", "Paper and dry litter catch fire", 0.33e6),
            ("Second-degree burns", "Blistering burns to exposed skin", 0.25e6),
            ("Deciduous trees ignite", "Forest fires start", 0.25e6),
            ("First-degree burns", "Sunburn-like reddening of skin", 0.13e6),
        };

        // ------------------------------------------------------------------ results

        public ImpactRegime Regime { get; private set; }
        /// <summary>Energy at the surface (J): drives the fireball and the air blast.</summary>
        public double Energy { get; private set; }
        public double EnergyMegatons => Energy / JoulesPerMegaton;
        public double ImpactSpeed { get; private set; }
        public double ImpactAngleDegrees { get; private set; }

        public bool IsOcean { get; private set; }
        /// <summary>Water depth assumed at the impact point (m); 0 on land.</summary>
        public double WaterDepth { get; private set; }
        /// <summary>Speed reaching the sea floor (m/s), Eq. 65; the impact speed on land.</summary>
        public double SeafloorSpeed { get; private set; }
        /// <summary>Energy delivered to the ground or sea floor (J): drives the seismic shaking and the ejecta.</summary>
        public double GroundEnergy { get; private set; }
        /// <summary>Transient crater diameter (m) in rock (the sea floor for an ocean impact).</summary>
        public double TransientCraterDiameter { get; private set; }
        /// <summary>Final rim-to-rim crater diameter (m) in rock (the sea floor for an ocean impact).</summary>
        public double FinalCraterDiameter { get; private set; }
        /// <summary>Transient cavity diameter in the water layer (m), Eq. 21 with the water constant; 0 on land.</summary>
        public double WaterCraterDiameter { get; private set; }

        public bool HasThermal { get; private set; }
        /// <summary>Fireball radius at the time of maximum radiation (m), Eq. 32.</summary>
        public double FireballRadius { get; private set; }
        /// <summary>Time of maximum radiation after impact (s), Eq. 33.</summary>
        public double TimeOfMaxRadiation { get; private set; }
        /// <summary>Duration of irradiation (s), Eq. 35.</summary>
        public double IrradiationDuration { get; private set; }
        /// <summary>Ground range (m) beyond which the whole fireball is below the horizon (h = R_f in Eq. 37).</summary>
        public double FireballHorizonRange { get; private set; }
        public List<ThermalThreshold> Thermal { get; } = new List<ThermalThreshold>();

        public bool HasSeismic { get; private set; }
        /// <summary>Richter magnitude, Eq. 40.</summary>
        public double Magnitude { get; private set; }
        /// <summary>Shaking radii: felt, damaging, destructive.</summary>
        public List<EffectRadius> Seismic { get; } = new List<EffectRadius>();

        public bool HasEjecta { get; private set; }
        /// <summary>Range (m) the rocky ejecta deposit is limited to (the fireball radius below 200 Mt; EIEP).</summary>
        public double EjectaMaxRange { get; private set; }
        /// <summary>Ejecta thickness radii: 1 m, 10 cm, 1 cm, 1 mm.</summary>
        public List<EffectRadius> Ejecta { get; } = new List<EffectRadius>();

        public bool HasTsunami { get; private set; }
        /// <summary>Wave amplitude at the rim of the water cavity (m).</summary>
        public double RimWaveAmplitude { get; private set; }
        public List<EffectRadius> Tsunami { get; } = new List<EffectRadius>();

        public GlobalScale Global { get; private set; }
        /// <summary>Impactor momentum over Earth's orbital momentum (EIEP Table 5).</summary>
        public double MomentumRatio { get; private set; }
        /// <summary>Impact angular momentum over Earth's spin angular momentum (EIEP Table 5).</summary>
        public double AngularMomentumRatio { get; private set; }

        private double impactorDensity;
        private double impactorDiameter;

        // ------------------------------------------------------------------ build

        /// <summary>
        /// Effects of an end event. Airbursts get only the global summary (EIEP models thermal, seismic and ejecta
        /// effects for ground impacts only). For an ocean impact (<paramref name="ocean"/>) the water layer of the given
        /// depth slows the impactor before it reaches the sea floor (Eq. 65); as in EIEP the fireball uses the energy at
        /// the water surface and the seismic shaking and ejecta the energy at the sea floor.
        /// </summary>
        public static ImpactEffects Compute(ImpactAssessment impact, double impactorDensity, bool ocean,
            double waterDepth = DefaultOceanDepth)
        {
            var e = new ImpactEffects();
            if (impact == null)
            {
                return e;
            }

            e.Regime = impact.Regime;
            e.Energy = impact.Energy;
            e.ImpactSpeed = impact.ImpactSpeed;
            e.ImpactAngleDegrees = impact.ImpactAngleDegrees;
            e.Global = GlobalScaleFor(impact.Energy);
            bool ground = impact.Regime == ImpactRegime.Crater || impact.Regime == ImpactRegime.PenetrationPit;
            if (!ground || !(impact.Energy > 0) || !(impact.ImpactorDiameter > 0))
            {
                return e;
            }

            e.impactorDensity = impactorDensity > 0 ? impactorDensity : 3000.0;
            e.impactorDiameter = impact.ImpactorDiameter;
            double mass = e.impactorDensity * Math.PI / 6.0 * Math.Pow(e.impactorDiameter, 3);
            double theta = Math.Max(1.0, Math.Min(90.0, impact.ImpactAngleDegrees)) * Math.PI / 180.0;
            e.MomentumRatio = mass * impact.ImpactSpeed / (EarthMass * EarthOrbitalSpeed);
            e.AngularMomentumRatio = mass * impact.ImpactSpeed * PhysicalConstants.EarthRadius * Math.Cos(theta) / EarthAngularMomentum;

            e.IsOcean = ocean && waterDepth > 0;
            e.WaterDepth = e.IsOcean ? waterDepth : 0.0;
            if (e.IsOcean)
            {
                e.SeafloorSpeed = SeafloorImpactSpeed(impact.ImpactSpeed, waterDepth, e.impactorDiameter, e.impactorDensity, impact.ImpactAngleDegrees);
                e.GroundEnergy = ImpactModel.KineticEnergy(mass, e.SeafloorSpeed);
                e.WaterCraterDiameter = ImpactModel.TransientCraterDiameter(e.impactorDiameter, e.impactorDensity, impact.ImpactSpeed,
                    impact.ImpactAngleDegrees, WaterDensity, ImpactModel.SurfaceGravity, WaterCraterCoefficient);
                e.TransientCraterDiameter = ImpactModel.TransientCraterDiameter(e.impactorDiameter, e.impactorDensity, e.SeafloorSpeed,
                    impact.ImpactAngleDegrees, SeafloorDensity);
                e.FinalCraterDiameter = ImpactModel.FinalCraterDiameter(e.TransientCraterDiameter, out _);
            }
            else
            {
                e.SeafloorSpeed = impact.ImpactSpeed;
                e.GroundEnergy = impact.Energy;
                e.TransientCraterDiameter = impact.TransientCraterDiameter;
                e.FinalCraterDiameter = impact.FinalCraterDiameter;
            }

            e.FillThermal();
            e.FillSeismic();
            e.FillEjecta();
            e.FillTsunami();
            return e;
        }

        private void FillThermal()
        {
            HasThermal = ImpactSpeed > ThermalSpeedThreshold && Regime == ImpactRegime.Crater;
            if (!HasThermal)
            {
                return;
            }

            FireballRadius = FireballRadiusFor(Energy);
            TimeOfMaxRadiation = FireballRadius / ImpactSpeed;
            IrradiationDuration = IrradiationDurationFor(Energy, FireballRadius);
            FireballHorizonRange = HorizonRange(FireballRadius);
            foreach (var row in ThermalTable)
            {
                double needed = IgnitionExposure(row.At1Mt, Energy);
                Thermal.Add(new ThermalThreshold(row.Name, row.Effect, row.At1Mt, needed, ThermalRadius(needed, Energy)));
            }
        }

        private void FillSeismic()
        {
            HasSeismic = GroundEnergy > 0;
            if (!HasSeismic)
            {
                return;
            }

            Magnitude = SeismicMagnitude(GroundEnergy);
            // EIEP Table 2: an effective magnitude of 3-4 gives MMI III-IV (felt indoors), 5-6 gives VI-VII (damage in
            // poorly built structures), 7-8 gives IX-X (most masonry and frame structures destroyed).
            Seismic.Add(new EffectRadius("Felt", "MMI III-IV: felt indoors, windows and dishes rattle", SeismicRadius(Magnitude, 3.0)));
            Seismic.Add(new EffectRadius("Damaging", "MMI VI-VII: plaster falls, chimneys break, weak buildings damaged", SeismicRadius(Magnitude, 5.0)));
            Seismic.Add(new EffectRadius("Destructive", "MMI IX-X: most buildings destroyed, ground cracks", SeismicRadius(Magnitude, 7.0)));
        }

        private void FillEjecta()
        {
            HasEjecta = TransientCraterDiameter > 0 && Regime == ImpactRegime.Crater;
            if (!HasEjecta)
            {
                return;
            }

            // EIEP limits the deposit to the fireball for impacts below 200 Mt (the ejecta is stopped by the air).
            EjectaMaxRange = EnergyMegatons < EjectaFireballLimitMegatons ? FireballRadiusFor(Energy) : MaxGroundRange;
            double rim = FinalCraterDiameter / 2.0;
            foreach ((string name, string effect, double t) in new[]
                     {
                         ("1 m of debris", "Buried under a metre of rock debris", 1.0),
                         ("10 cm of debris", "Roofs collapse under the weight of debris", 0.1),
                         ("1 cm of debris", "A layer of gravel and dust, like heavy ashfall", 0.01),
                         ("1 mm of dust", "A fine dusting of rock powder", 0.001),
                     })
            {
                double r = EjectaRadius(TransientCraterDiameter, t);
                if (r <= rim) r = 0;
                Ejecta.Add(new EffectRadius(name, effect, Math.Min(r, EjectaMaxRange)));
            }
        }

        private void FillTsunami()
        {
            HasTsunami = IsOcean && WaterCraterDiameter > 0;
            if (!HasTsunami)
            {
                return;
            }

            RimWaveAmplitude = RimWave(WaterCraterDiameter, WaterDepth);
            foreach ((string name, string effect, double a) in new[]
                     {
                         ("100 m wave", "Overtops any coast: total inundation far inland", 100.0),
                         ("10 m wave", "Devastating coastal flooding", 10.0),
                         ("1 m wave", "Dangerous currents and harbour damage", 1.0),
                     })
            {
                Tsunami.Add(new EffectRadius(name, effect, TsunamiRadius(WaterCraterDiameter, WaterDepth, a)));
            }
        }

        // ------------------------------------------------------------------ thermal radiation

        /// <summary>Fireball radius at the time of maximum radiation (m), Eq. 32: R_f = 0.002 E^(1/3), E in J.</summary>
        public static double FireballRadiusFor(double energy) => energy > 0 ? 0.002 * Math.Pow(energy, 1.0 / 3.0) : 0.0;

        /// <summary>Duration of irradiation (s), Eq. 35: eta E / (2 pi R_f^2 sigma T*^4).</summary>
        public static double IrradiationDurationFor(double energy, double fireballRadius) =>
            fireballRadius > 0
                ? LuminousEfficiency * energy / (2.0 * Math.PI * fireballRadius * fireballRadius * StefanBoltzmann * Math.Pow(TransparencyTemperature, 4))
                : 0.0;

        /// <summary>
        /// Fraction of the fireball above the horizon at ground range r, Eqs. 36-37: h = (1 - cos delta) R_E with delta
        /// the epicentral angle; f = (2/pi)(theta - (h/R_f) sin theta), theta = arccos(h/R_f); 0 once h reaches R_f.
        /// </summary>
        public static double VisibleFraction(double range, double fireballRadius)
        {
            if (!(fireballRadius > 0)) return 0.0;
            double delta = Math.Max(0.0, range) / PhysicalConstants.EarthRadius;
            double h = (1.0 - Math.Cos(delta)) * PhysicalConstants.EarthRadius;
            if (h >= fireballRadius) return 0.0;
            double ratio = h / fireballRadius;
            double theta = Math.Acos(ratio);
            return 2.0 / Math.PI * (theta - ratio * Math.Sin(theta));
        }

        /// <summary>Ground range (m) at which the fireball sinks fully below the horizon (h = R_f in Eq. 37).</summary>
        public static double HorizonRange(double fireballRadius)
        {
            if (!(fireballRadius > 0)) return 0.0;
            double c = 1.0 - fireballRadius / PhysicalConstants.EarthRadius;
            return c <= -1.0 ? MaxGroundRange : Math.Acos(c) * PhysicalConstants.EarthRadius;
        }

        /// <summary>Thermal exposure (J/m^2) at ground range r, Eq. 34 with the horizon factor: f eta E / (2 pi r^2).</summary>
        public static double ThermalExposure(double range, double energy)
        {
            if (!(energy > 0) || !(range > 0)) return double.PositiveInfinity;
            double rf = FireballRadiusFor(energy);
            return VisibleFraction(range, rf) * LuminousEfficiency * energy / (2.0 * Math.PI * range * range);
        }

        /// <summary>Exposure needed to ignite or burn (J/m^2), Eq. 39: Phi(1 Mt) E_Mt^(1/6).</summary>
        public static double IgnitionExposure(double exposureAt1Mt, double energy) =>
            exposureAt1Mt * Math.Pow(Math.Max(energy, 1.0) / JoulesPerMegaton, 1.0 / 6.0);

        /// <summary>Largest ground range (m) where the thermal exposure reaches the given level; exposure falls with range.</summary>
        public static double ThermalRadius(double exposure, double energy)
        {
            if (!(energy > 0) || !(exposure > 0)) return 0.0;
            double hi = HorizonRange(FireballRadiusFor(energy));
            double lo = 1e-3;
            if (ThermalExposure(lo, energy) < exposure) return 0.0;
            for (int k = 0; k < 100; k++)
            {
                double mid = Math.Sqrt(lo * hi);
                if (ThermalExposure(mid, energy) >= exposure) lo = mid;
                else hi = mid;
            }

            return lo;
        }

        // ------------------------------------------------------------------ seismic shaking

        /// <summary>Richter magnitude, Eq. 40: M = 0.67 log10 E - 5.87 (seismic efficiency 1e-4), E in J.</summary>
        public static double SeismicMagnitude(double energy) => energy > 0 ? 0.67 * Math.Log10(energy) - 5.87 : double.NegativeInfinity;

        /// <summary>
        /// Effective magnitude at ground range r, Eqs. 41a-c: M - 0.0238 r_km below 60 km; M - 0.0048 r_km - 1.1644 up
        /// to 700 km; beyond, M - 1.66 log10(delta) - 6.399 with the epicentral angle delta in radians (which makes 41c
        /// continuous with 41b at 700 km to within 0.3 magnitudes, as in the EIEP program, which uses delta = r / R_E).
        /// </summary>
        public static double EffectiveMagnitude(double magnitude, double range)
        {
            double rkm = Math.Max(0.0, range) / 1000.0;
            if (rkm < 60.0) return magnitude - 0.0238 * rkm;
            if (rkm < 700.0) return magnitude - 0.0048 * rkm - 1.1644;
            double delta = Math.Max(range, 1.0) / PhysicalConstants.EarthRadius;
            return magnitude - 1.66 * Math.Log10(delta) - 6.399;
        }

        /// <summary>Arrival time (s) of the strongest shaking (surface waves at 5 km/s), Eq. 42.</summary>
        public static double SeismicArrival(double range) => Math.Max(0.0, range) / SurfaceWaveSpeed;

        /// <summary>Largest ground range (m) at which the effective magnitude is at least the given level; 0 if never.</summary>
        public static double SeismicRadius(double magnitude, double effectiveMagnitude)
        {
            if (double.IsNaN(magnitude) || EffectiveMagnitude(magnitude, 0) < effectiveMagnitude) return 0.0;
            const int samples = 2000;
            double max = MaxGroundRange;
            double last = 0;
            for (int i = 1; i <= samples; i++)
            {
                double r = max * i / samples;
                if (EffectiveMagnitude(magnitude, r) >= effectiveMagnitude) last = r;
            }

            if (last >= max) return max;
            double lo = last, hi = Math.Min(max, last + max / samples);
            for (int k = 0; k < 60; k++)
            {
                double mid = 0.5 * (lo + hi);
                if (EffectiveMagnitude(magnitude, mid) >= effectiveMagnitude) lo = mid;
                else hi = mid;
            }

            return lo;
        }

        /// <summary>Modified Mercalli intensity range for an effective magnitude, EIEP Table 2 (after Richter 1958).</summary>
        public static string MercalliRange(double effectiveMagnitude)
        {
            if (effectiveMagnitude < 1.0) return "-";
            if (effectiveMagnitude < 2.0) return "I";
            if (effectiveMagnitude < 3.0) return "I-II";
            if (effectiveMagnitude < 4.0) return "III-IV";
            if (effectiveMagnitude < 5.0) return "IV-V";
            if (effectiveMagnitude < 6.0) return "VI-VII";
            if (effectiveMagnitude < 7.0) return "VII-VIII";
            if (effectiveMagnitude < 8.0) return "IX-X";
            if (effectiveMagnitude < 9.0) return "X-XI";
            return "XII";
        }

        /// <summary>Short description of the shaking for an effective magnitude (abbreviated EIEP Table 3).</summary>
        public static string MercalliDescription(double effectiveMagnitude)
        {
            if (effectiveMagnitude < 1.0) return "Not felt";
            if (effectiveMagnitude < 3.0) return "Felt by a few people at rest";
            if (effectiveMagnitude < 4.0) return "Felt indoors; dishes and windows rattle";
            if (effectiveMagnitude < 5.0) return "Felt by nearly everyone; some windows break";
            if (effectiveMagnitude < 6.0) return "Heavy furniture moves; weak buildings damaged";
            if (effectiveMagnitude < 7.0) return "Chimneys and walls fall; ordinary buildings damaged";
            if (effectiveMagnitude < 8.0) return "Buildings shift off foundations; ground cracks";
            if (effectiveMagnitude < 9.0) return "Most structures destroyed; rails bent";
            return "Damage nearly total; objects thrown into the air";
        }

        // ------------------------------------------------------------------ ejecta

        /// <summary>Ejecta thickness (m) at ground range r, Eq. 47: t_e = D_tc^4 / (112 r^3).</summary>
        public static double EjectaThickness(double transientDiameter, double range) =>
            range > 0 ? Math.Pow(transientDiameter, 4) / (112.0 * Math.Pow(range, 3)) : double.PositiveInfinity;

        /// <summary>Ground range (m) at which the ejecta is the given thickness (Eq. 47 inverted).</summary>
        public static double EjectaRadius(double transientDiameter, double thickness) =>
            thickness > 0 && transientDiameter > 0 ? Math.Pow(Math.Pow(transientDiameter, 4) / (112.0 * thickness), 1.0 / 3.0) : 0.0;

        /// <summary>
        /// Mean ejecta fragment diameter (m) at ground range r, Eq. 53 (Schaller and Melosh 1998):
        /// d = d_c (D_fr / (2 r_km))^2.65 with d_c = 2400 (D_fr / 2)^-1.62, D_fr and r in km. EIEP reports it only beyond
        /// two crater radii; NaN closer in.
        /// </summary>
        public static double MeanFragmentSize(double finalCraterDiameter, double range)
        {
            double dfr = finalCraterDiameter / 1000.0;
            double rkm = range / 1000.0;
            if (!(dfr > 0) || rkm < dfr) return double.NaN;
            double dc = 2400.0 * Math.Pow(dfr / 2.0, -1.62);
            return dc * Math.Pow(dfr / (2.0 * rkm), 2.65);
        }

        /// <summary>
        /// Mean arrival time (s) of the ejecta at ground range r for a 45 degree ejection angle on a spherical Earth,
        /// Eqs. 49-52 (Ahrens and O'Keefe 1978): v_e^2 = 2 g R tan(d/2) / (1 + tan(d/2)) (Eq. 52);
        /// e^2 = (1/2)((v_e^2 / (g R) - 1)^2 + 1), e negative when v_e^2 / (g R) &lt; 1 (Eq. 50);
        /// a = v_e^2 / (2 g (1 - e^2)) (Eq. 51);
        /// T = 2 a^1.5 / sqrt(g R^2) [2 atan(sqrt((1 - e)/(1 + e)) tan(d/4)) - e sqrt(1 - e^2) sin(d/2) / (1 + e cos(d/2))] (Eq. 49).
        /// Valid while v_e^2 / (g R) &lt; 1 (ranges below about a quarter of the way round the Earth); NaN beyond.
        /// </summary>
        public static double EjectaArrivalTime(double range)
        {
            double g = ImpactModel.SurfaceGravity;
            double R = PhysicalConstants.EarthRadius;
            double d = Math.Max(0.0, range) / R;
            if (!(d > 0)) return 0.0;
            double t2 = Math.Tan(d / 2.0);
            double ve2 = 2.0 * g * R * t2 / (1.0 + t2);
            double x = ve2 / (g * R);
            if (x >= 1.0) return double.NaN;
            double e = -Math.Sqrt(0.5 * ((x - 1.0) * (x - 1.0) + 1.0));
            double a = ve2 / (2.0 * g * (1.0 - e * e));
            double bracket = 2.0 * Math.Atan(Math.Sqrt((1.0 - e) / (1.0 + e)) * Math.Tan(d / 4.0)) -
                             e * Math.Sqrt(1.0 - e * e) * Math.Sin(d / 2.0) / (1.0 + e * Math.Cos(d / 2.0));
            return 2.0 * Math.Pow(a, 1.5) / Math.Sqrt(g * R * R) * bracket;
        }

        // ------------------------------------------------------------------ ocean impacts

        /// <summary>
        /// Speed at the sea floor (m/s), Eq. 65: v_sf = v exp(-3 rho_w C_D d_w / (2 rho_i L sin theta)), integrating the
        /// drag on the impactor (diameter L) through a water column of depth d_w along its inclined path.
        /// </summary>
        public static double SeafloorImpactSpeed(double speed, double waterDepth, double impactorDiameter, double impactorDensity,
            double angleDegrees)
        {
            if (!(impactorDiameter > 0) || !(impactorDensity > 0)) return 0.0;
            double sin = Math.Sin(Math.Max(1.0, Math.Min(90.0, angleDegrees)) * Math.PI / 180.0);
            return speed * Math.Exp(-3.0 * WaterDensity * WaterDragCoefficient * Math.Max(0.0, waterDepth) /
                                    (2.0 * impactorDensity * impactorDiameter * sin));
        }

        /// <summary>
        /// Wave amplitude (m) at the rim of the water cavity: the cavity's rim height D_tc,w / 14.1 (EIEP's volume
        /// argument for Eq. 47, applied to the water crater), limited by the water depth (a wave cannot be higher than
        /// the water that forms it, roughly; the deep-water limit of Ward and Asphaug 2000).
        /// </summary>
        public static double RimWave(double waterCraterDiameter, double waterDepth) =>
            Math.Min(waterCraterDiameter / 14.1, Math.Max(0.0, waterDepth));

        /// <summary>
        /// Deep-water tsunami amplitude (m) at ground range r. The rim wave decays as 1/r from the cavity rim
        /// (R_c = D_tc,w / 2): Ward and Asphaug (2000, Icarus 145, 64) find that impact waves in deep water decay about as
        /// 1/r (geometric spreading plus frequency dispersion), far faster than earthquake tsunamis. This is the open-ocean
        /// amplitude; waves this short may break on the continental shelf (the Van Dorn effect, Melosh 2003), while
        /// run-up on a steep coast can be about twice the amplitude.
        /// </summary>
        public static double TsunamiAmplitude(double waterCraterDiameter, double waterDepth, double range)
        {
            double rc = waterCraterDiameter / 2.0;
            double a0 = RimWave(waterCraterDiameter, waterDepth);
            if (!(rc > 0)) return 0.0;
            return range <= rc ? a0 : a0 * rc / range;
        }

        /// <summary>Ground range (m) out to which the open-ocean wave amplitude is at least the given height.</summary>
        public static double TsunamiRadius(double waterCraterDiameter, double waterDepth, double amplitude)
        {
            double a0 = RimWave(waterCraterDiameter, waterDepth);
            if (!(amplitude > 0) || a0 < amplitude) return 0.0;
            return Math.Min(MaxGroundRange, a0 * (waterCraterDiameter / 2.0) / amplitude);
        }

        /// <summary>Tsunami travel time (s) in deep water at the shallow-water wave speed sqrt(g d).</summary>
        public static double TsunamiArrival(double range, double waterDepth) =>
            waterDepth > 0 ? range / Math.Sqrt(ImpactModel.SurfaceGravity * waterDepth) : double.NaN;

        // ------------------------------------------------------------------ global effects

        /// <summary>
        /// Global scale by energy. EIEP itself does not model global environmental effects; the thresholds follow
        /// Toon et al. (1997, Rev. Geophys. 35, 41): regional devastation from about 1e3 Mt, global climate effects from
        /// dust and sulphate aerosols from about 1e5 Mt, a global catastrophe (Chapman and Morrison 1994, Nature 367, 33:
        /// about 1e6 Mt, a 1.5-2 km asteroid) and mass extinction at the Chicxulub scale (about 1e8 Mt; from 5e7 Mt here,
        /// since estimates of the Chicxulub energy span 3e7 to 3e8 Mt).
        /// </summary>
        public static GlobalScale GlobalScaleFor(double energy)
        {
            double mt = energy / JoulesPerMegaton;
            if (mt >= 1e8 * 0.5) return GlobalScale.MassExtinction;
            if (mt >= 1e6) return GlobalScale.Civilisation;
            if (mt >= 1e5) return GlobalScale.GlobalClimate;
            if (mt >= 1e4) return GlobalScale.Continental;
            if (mt >= 1e3) return GlobalScale.Regional;
            return GlobalScale.Local;
        }

        /// <summary>Headline and explanation of the global consequences.</summary>
        public static (string Title, string Text) DescribeGlobal(GlobalScale scale)
        {
            switch (scale)
            {
                case GlobalScale.MassExtinction:
                    return ("MASS EXTINCTION",
                        "Chicxulub scale: re-entering ejecta heats the sky and ignites fires worldwide, soot and sulphate " +
                        "aerosols block sunlight for years, photosynthesis collapses and most species die out.");
                case GlobalScale.Civilisation:
                    return ("GLOBAL CATASTROPHE",
                        "Dust and aerosols darken the whole planet for a year or more: global harvests fail and " +
                        "civilisation itself is threatened.");
                case GlobalScale.GlobalClimate:
                    return ("GLOBAL CLIMATE SHOCK",
                        "Dust and sulphate aerosols spread round the world: several degrees of cooling and failed " +
                        "harvests for a year or more.");
                case GlobalScale.Continental:
                    return ("CONTINENTAL DISASTER",
                        "A continent-wide disaster zone; stratospheric dust dims the Sun worldwide for months.");
                case GlobalScale.Regional:
                    return ("REGIONAL DISASTER",
                        "Devastation across a region the size of a country; little lasting effect on the global climate.");
                default:
                    return ("LOCAL EVENT", "Effects are confined to the area around the impact.");
            }
        }

        /// <summary>EIEP Table 5: qualitative change to Earth's orbit for a momentum ratio.</summary>
        public static string OrbitChange(double momentumRatio) =>
            momentumRatio < 0.001 ? "No noticeable change in orbit"
            : momentumRatio < 0.01 ? "Noticeable change in orbit"
            : momentumRatio < 0.1 ? "Substantial change in orbit" : "Totally changes orbit";

        /// <summary>EIEP Table 5: qualitative change to Earth's spin for an angular-momentum ratio.</summary>
        public static string RotationChange(double angularMomentumRatio) =>
            angularMomentumRatio < 0.01 ? "No noticeable change in day length or tilt"
            : angularMomentumRatio < 0.1 ? "Noticeable change in day length and tilt"
            : angularMomentumRatio < 1.0 ? "Substantial change in day length and tilt" : "Totally changes day length and tilt";
    }
}
