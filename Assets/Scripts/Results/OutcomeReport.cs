using System;
using System.Collections.Generic;
using MTS.Core;

namespace MTS.Results
{
    /// <summary>One labelled value on the results screen.</summary>
    public sealed class ReportStat
    {
        public string Label { get; }
        public string Value { get; }
        /// <summary>Secondary line (may contain TMP rich text); empty if none.</summary>
        public string Note { get; }

        public ReportStat(string label, string value, string note = "")
        {
            Label = label;
            Value = value;
            Note = note ?? "";
        }

        public override string ToString() => Label + ": " + Value + (Note.Length > 0 ? " (" + Note + ")" : "");
    }

    public sealed class ReportSection
    {
        public string Title { get; }
        public List<ReportStat> Rows { get; } = new List<ReportStat>();

        public ReportSection(string title)
        {
            Title = title;
        }

        public ReportSection Add(string label, string value, string note = "")
        {
            Rows.Add(new ReportStat(label, value, note));
            return this;
        }
    }

    /// <summary>One of the nine overpressure rings.</summary>
    public sealed class RingRow
    {
        public int Index { get; }
        public double Overpressure { get; }
        public double Radius { get; }
        public string Label { get; }
        public string Effect { get; }
        public bool Reached => Radius > 0;

        public RingRow(int index, double overpressure, double radius)
        {
            Index = index;
            Overpressure = overpressure;
            Radius = radius;
            Label = ResultsFormat.Psi(overpressure) + "  (" + ResultsFormat.Pressure(overpressure) + ")";
            Effect = BlastEffects.Describe(index);
        }
    }

    /// <summary>Plain-language effects of the nine standard overpressure levels.</summary>
    public static class BlastEffects
    {
        // Typical damage at each peak overpressure, after Glasstone and Dolan (1977) ch. 5 and the EIEP damage tables
        // (Collins et al. 2005, Table 5). Approximate: real damage depends on construction, orientation and duration.
        private static readonly string[] Effects =
        {
            "Loud boom; occasional window breakage",          // 0.04 psi
            "Typical window glass shatters",                  // 0.15 psi
            "Minor damage to houses; flying glass",           // 0.7 psi
            "Roofs and walls of houses partly collapse",      // 2 psi
            "Houses seriously damaged; trees blown down",     // 3 psi
            "Most houses collapse; widespread injuries",      // 5 psi
            "Reinforced-concrete buildings badly damaged",    // 7 psi
            "Concrete buildings collapse; most people killed", // 10 psi
            "Total destruction of all structures",            // 29 psi
        };

        public static string Describe(int index) =>
            index >= 0 && index < Effects.Length ? Effects[index] : "";

        public static int Count => Effects.Length;
    }

    /// <summary>Kinds of effect drawn as rings on the map (each can be toggled in the legend).</summary>
    public enum EffectGroup
    {
        Blast,
        Thermal,
        Seismic,
        Ejecta,
        Tsunami,
    }

    /// <summary>A non-blast ring for the map: thermal, seismic, ejecta or tsunami reach.</summary>
    public sealed class EffectRing
    {
        public EffectGroup Group { get; }
        public string Label { get; }
        public double Radius { get; }
        /// <summary>0 for the strongest ring of the group, rising outwards (used for shading).</summary>
        public int Level { get; }

        public EffectRing(EffectGroup group, string label, double radius, int level)
        {
            Group = group;
            Label = label;
            Radius = radius;
            Level = level;
        }
    }

    /// <summary>What happens at one distance from the impact (the Effects tab's table).</summary>
    public sealed class DistanceRow
    {
        public double Distance { get; set; }
        /// <summary>Modified Mercalli intensity range, e.g. "VII-VIII", or "-".</summary>
        public string Intensity { get; set; } = "-";
        public string Shaking { get; set; } = "-";
        public string Heat { get; set; } = "-";
        public string Debris { get; set; } = "-";
        public string Wave { get; set; } = "-";
        public string Arrival { get; set; } = "-";
    }

    /// <summary>One card of the Effects tab: a title, a one-line summary and labelled values (or why it does not apply).</summary>
    public sealed class EffectCard
    {
        public EffectGroup Group { get; }
        public string Title { get; }
        public bool Applies { get; set; }
        public string Summary { get; set; } = "";
        public List<ReportStat> Rows { get; } = new List<ReportStat>();

        public EffectCard(EffectGroup group, string title)
        {
            Group = group;
            Title = title;
        }

        public EffectCard Add(string label, string value, string note = "")
        {
            Rows.Add(new ReportStat(label, value, note));
            return this;
        }
    }

    public enum OutcomeKind
    {
        NoRun,
        GroundImpact,
        MeteoriteFall,
        Airburst,
        Disintegrated,
        Timeout,
        Error,
    }

    /// <summary>
    /// Everything the results screen shows, built from the session's result, impact assessment and inputs.
    /// Pure data (no Unity types) so every outcome, including Airburst, Disintegrated, Timeout and Error, can be tested.
    /// </summary>
    public sealed class OutcomeReport
    {
        public OutcomeKind Kind { get; private set; }
        /// <summary>Short upper-case title for the header, e.g. "GROUND IMPACT".</summary>
        public string Title { get; private set; } = "";
        /// <summary>The big number, e.g. "12.4 kt TNT".</summary>
        public string Headline { get; private set; } = "";
        /// <summary>One-line explanation under the headline.</summary>
        public string Subtitle { get; private set; } = "";
        /// <summary>Comparison with reference events (empty if no energy).</summary>
        public string Comparison { get; private set; } = "";
        public double Energy { get; private set; }
        public double EnergyKilotons => Energy / PhysicalConstants.JoulesPerKilotonTnt;
        public bool HasLocation { get; private set; }
        public GeoPoint Location { get; private set; }
        /// <summary>Altitude of the end event (m): 0 for an impact, burst or ablation altitude otherwise.</summary>
        public double EventAltitude { get; private set; }
        public List<ReportStat> Stats { get; } = new List<ReportStat>();
        public List<RingRow> Rings { get; } = new List<RingRow>();
        /// <summary>Statement about effects on the ground (shown above the ring table).</summary>
        public string GroundEffects { get; private set; } = "";
        public List<ReportSection> Details { get; } = new List<ReportSection>();
        public double GroundZeroOverpressure { get; private set; }

        /// <summary>Thermal, seismic, ejecta, tsunami and global effects (never null).</summary>
        public ImpactEffects Effects { get; private set; } = new ImpactEffects();
        /// <summary>True if a ground impact hit the ocean (by the coarse <see cref="LandMask"/>).</summary>
        public bool IsOcean { get; private set; }
        /// <summary>"From rest" or "Inbound entry" (empty without inputs).</summary>
        public string ModeText { get; private set; } = "";
        /// <summary>Materials and mass fractions, largest first.</summary>
        public List<(string Name, double Fraction)> Composition { get; } = new List<(string, double)>();
        /// <summary>Bulk density (kg/m^3) and strength (Pa) of the blended composition; 0 if unknown.</summary>
        public double BulkDensity { get; private set; }
        public double BulkStrength { get; private set; }
        public string GlobalTitle { get; private set; } = "";
        public string GlobalText { get; private set; } = "";
        /// <summary>True when the event is big enough for the global-effects banner (regional scale and above).</summary>
        public bool ShowGlobalBanner => Effects.Global >= GlobalScale.Regional;
        public List<EffectCard> EffectCards { get; } = new List<EffectCard>();
        public List<DistanceRow> DistanceRows { get; } = new List<DistanceRow>();
        public List<EffectRing> EffectRings { get; } = new List<EffectRing>();

        /// <summary>Largest radius of any ring (m), including the crater rim.</summary>
        public double OuterRadius
        {
            get
            {
                double outer = 0;
                foreach (RingRow r in Rings) outer = Math.Max(outer, r.Radius);
                foreach (EffectRing r in EffectRings) outer = Math.Max(outer, r.Radius);
                return outer;
            }
        }

        public bool HasRings
        {
            get
            {
                foreach (RingRow r in Rings)
                {
                    if (r.Reached) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Builds the report. <paramref name="composition"/> lists the materials (defaults to the properties' single
        /// material); <paramref name="isOcean"/> decides whether a ground impact at (lat, lon) hit the sea (defaults to
        /// <see cref="LandMask.IsOcean"/>).
        /// </summary>
        public static OutcomeReport Build(SimulationResult result, ImpactAssessment impact, MeteorProperties properties,
            MeteorComposition composition = null, Func<double, double, bool> isOcean = null)
        {
            var report = new OutcomeReport();
            report.FillInputs(properties, composition);
            if (result == null)
            {
                report.Kind = OutcomeKind.NoRun;
                report.Title = "NO RESULTS";
                report.Headline = "No finished run";
                report.Subtitle = "Start a simulation from the input screen to see its results here.";
                return report;
            }

            MeteorState end = result.FinalState;
            Geo.LatitudeLongitude(end.Position, end.Time, out double lat, out double lon);
            report.Location = new GeoPoint(lat, lon);
            report.HasLocation = end.Position.IsFinite && end.Position.Magnitude > 0;
            report.EventAltitude = Math.Max(0.0, end.Altitude);

            if (impact != null)
            {
                double[] levels = impact.OverpressureLevels ?? ImpactModel.StandardOverpressureLevels;
                for (int i = 0; i < levels.Length; i++)
                {
                    double radius = impact.BlastRadii != null && i < impact.BlastRadii.Length ? impact.BlastRadii[i] : 0.0;
                    report.Rings.Add(new RingRow(i, levels[i], radius));
                }
            }

            bool ground = impact != null && (impact.Regime == ImpactRegime.Crater || impact.Regime == ImpactRegime.PenetrationPit);
            report.IsOcean = ground && report.HasLocation && (isOcean ?? LandMask.IsOcean)(lat, lon);
            if (impact != null)
            {
                report.Effects = ImpactEffects.Compute(impact, properties?.Material?.Density ?? report.BulkDensity, report.IsOcean);
            }

            (report.GlobalTitle, report.GlobalText) = ImpactEffects.DescribeGlobal(report.Effects.Global);

            switch (result.Reason)
            {
                case EndReason.Impact:
                    report.FillImpact(result, impact, properties);
                    break;
                case EndReason.Airburst:
                    report.FillAirburst(result, impact, properties);
                    break;
                case EndReason.Disintegrated:
                    report.FillDisintegrated(result, properties);
                    break;
                case EndReason.Timeout:
                    report.FillTimeout(result);
                    break;
                default:
                    report.FillError(result);
                    break;
            }

            report.FillDetails(result, impact, properties);
            report.FillEffects();
            return report;
        }

        private void FillInputs(MeteorProperties p, MeteorComposition composition)
        {
            if (p == null)
            {
                return;
            }

            ModeText = ModeName(p.Mode);
            if (composition == null && p.Material != null)
            {
                composition = MeteorComposition.FromMaterial(p.Material);
            }

            if (composition != null)
            {
                foreach (CompositionPart part in composition.Parts)
                {
                    Composition.Add((part.Material.Name, part.Fraction));
                }
            }

            BulkDensity = p.Material?.Density ?? 0.0;
            BulkStrength = p.Material?.Strength ?? 0.0;
        }

        /// <summary>UI wording of the start modes.</summary>
        public static string ModeName(EntryMode mode) => mode == EntryMode.Inbound ? "Inbound entry" : "From rest";

        /// <summary>"70% Iron (Fe-Ni) + 30% Stony (ordinary chondrite)", or just the name for a single material.</summary>
        public string CompositionText()
        {
            if (Composition.Count == 0) return "";
            if (Composition.Count == 1) return Composition[0].Name;
            var parts = new List<string>();
            foreach ((string name, double fraction) in Composition)
            {
                parts.Add(Math.Round(fraction * 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture) + "% " + name);
            }

            return string.Join(" + ", parts);
        }

        // ---------------------------------------------------------------- effects

        /// <summary>Distances for the "at a distance" table: round values from just outside the crater outwards.</summary>
        public static readonly double[] SampleDistances = { 3e3, 1e4, 3e4, 1e5, 3e5, 1e6, 3e6, 1e7 };

        private void FillEffects()
        {
            ImpactEffects fx = Effects;
            bool ground = fx.Regime == ImpactRegime.Crater || fx.Regime == ImpactRegime.PenetrationPit;
            string why = Kind == OutcomeKind.Airburst ? "The meteor exploded in the air, so"
                : ground ? "" : "Nothing reached the ground, so";

            // Thermal radiation
            var thermal = new EffectCard(EffectGroup.Thermal, "FIREBALL HEAT") { Applies = fx.HasThermal };
            if (fx.HasThermal)
            {
                double burns = ThermalRadius("Third-degree burns");
                thermal.Summary = "Brightest after " + ResultsFormat.ShortDuration(fx.TimeOfMaxRadiation) +
                                  ", it radiates heat for " + ResultsFormat.ShortDuration(fx.IrradiationDuration) + ".";
                thermal.Add("Fireball radius", ResultsFormat.Distance(fx.FireballRadius));
                thermal.Add("Clothing ignites", ResultsFormat.Reach(ThermalRadius("Clothing ignites"), false));
                thermal.Add("Third-degree burns", ResultsFormat.Reach(burns, false));
                thermal.Add("Second-degree burns", ResultsFormat.Reach(ThermalRadius("Second-degree burns"), false));
                thermal.Add("Grass and trees ignite", ResultsFormat.Reach(ThermalRadius("Deciduous trees ignite"), false));
                EffectRings.Add(new EffectRing(EffectGroup.Thermal, "Fireball", fx.FireballRadius, 0));
                EffectRings.Add(new EffectRing(EffectGroup.Thermal, "3rd-degree burns", burns, 1));
                EffectRings.Add(new EffectRing(EffectGroup.Thermal, "Trees ignite", ThermalRadius("Deciduous trees ignite"), 2));
            }
            else
            {
                thermal.Summary = ground
                    ? "Too slow for a fireball: below about 15 km/s an impact makes little vapour, so it radiates little heat."
                    : why + " no fireball touched the ground. (Heat from airbursts is not modelled.)";
            }

            EffectCards.Add(thermal);

            // Seismic shaking
            var seismic = new EffectCard(EffectGroup.Seismic, "GROUND SHAKING") { Applies = fx.HasSeismic };
            if (fx.HasSeismic)
            {
                seismic.Summary = "Like a magnitude " + ResultsFormat.Sig(fx.Magnitude, 2) + " earthquake" +
                                  (fx.IsOcean ? " (from the energy reaching the sea floor)." : ".");
                seismic.Add("Richter magnitude", fx.Magnitude.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                foreach (EffectRadius r in fx.Seismic)
                {
                    seismic.Add(r.Name + " (" + RomanPart(r.Effect) + ")", r.Radius > 0 ? ResultsFormat.Reach(r.Radius, false) : "-");
                }

                for (int i = 0; i < fx.Seismic.Count; i++)
                {
                    EffectRings.Add(new EffectRing(EffectGroup.Seismic, "Shaking: " + fx.Seismic[fx.Seismic.Count - 1 - i].Name.ToLowerInvariant(),
                        fx.Seismic[fx.Seismic.Count - 1 - i].Radius, i));
                }
            }
            else
            {
                seismic.Summary = why + " the ground was not struck and there is no seismic shaking.";
            }

            EffectCards.Add(seismic);

            // Ejecta
            var ejecta = new EffectCard(EffectGroup.Ejecta, "FALLING DEBRIS") { Applies = fx.HasEjecta };
            if (fx.HasEjecta)
            {
                ejecta.Summary = fx.EnergyMegatons < ImpactEffects.EjectaFireballLimitMegatons
                    ? "Rock thrown from the crater; the air stops it within " + ResultsFormat.Distance(fx.EjectaMaxRange) + "."
                    : "Rock thrown from the crater on ballistic arcs, some of it out of the atmosphere.";
                bool clipped = false;
                foreach (EffectRadius r in fx.Ejecta)
                {
                    if (r.Radius >= fx.EjectaMaxRange * 0.999 && fx.EjectaMaxRange < ImpactEffects.MaxGroundRange)
                    {
                        clipped = true; // thicker than this all the way to where the deposit stops
                        continue;
                    }

                    ejecta.Add(r.Name, r.Radius > 0 ? ResultsFormat.Reach(r.Radius, false) : "-");
                }

                if (clipped)
                {
                    ejecta.Add("Deposit ends at", ResultsFormat.Distance(fx.EjectaMaxRange));
                    ejecta.Add("Thickness there", ResultsFormat.Distance(ImpactEffects.EjectaThickness(fx.TransientCraterDiameter, fx.EjectaMaxRange)));
                }

                EffectRings.Add(new EffectRing(EffectGroup.Ejecta, "1 m of debris", fx.Ejecta[0].Radius, 0));
                EffectRings.Add(new EffectRing(EffectGroup.Ejecta, "1 cm of debris", fx.Ejecta[2].Radius, 1));
            }
            else
            {
                ejecta.Summary = ground
                    ? "The impact was too slow to excavate a crater, so it threw out little debris."
                    : why + " no crater formed and no rock was thrown out.";
            }

            EffectCards.Add(ejecta);

            // Tsunami
            var tsunami = new EffectCard(EffectGroup.Tsunami, "TSUNAMI") { Applies = fx.HasTsunami };
            if (fx.HasTsunami)
            {
                tsunami.Summary = "A " + ResultsFormat.Distance(fx.WaterCraterDiameter) + " cavity in the " +
                                  ResultsFormat.Distance(fx.WaterDepth) + "-deep ocean collapses into waves.";
                tsunami.Add("Wave at the cavity rim", ResultsFormat.Distance(fx.RimWaveAmplitude));
                foreach (EffectRadius r in fx.Tsunami)
                {
                    tsunami.Add(r.Name, r.Radius > 0 ? ResultsFormat.Reach(r.Radius, false) : "-");
                }

                tsunami.Add("Reaches 1,000 km after", ResultsFormat.ShortDuration(ImpactEffects.TsunamiArrival(1e6, fx.WaterDepth)));
                EffectRings.Add(new EffectRing(EffectGroup.Tsunami, "10 m waves", fx.Tsunami[1].Radius, 0));
                EffectRings.Add(new EffectRing(EffectGroup.Tsunami, "1 m waves", fx.Tsunami[2].Radius, 1));
            }
            else
            {
                tsunami.Summary = fx.IsOcean || !ground
                    ? (ground ? "The impact was too small to raise a wave." : why + " there is no impact tsunami.")
                    : "It struck land, so there is no tsunami.";
            }

            EffectCards.Add(tsunami);
            EffectRings.RemoveAll(r => !(r.Radius > 0));

            if (ground)
            {
                FillDistanceRows();
            }
        }

        private static string RomanPart(string effect)
        {
            int colon = effect.IndexOf(':');
            return colon > 0 ? effect.Substring(0, colon) : effect;
        }

        private double ThermalRadius(string name)
        {
            foreach (ThermalThreshold t in Effects.Thermal)
            {
                if (t.Name == name) return t.Radius;
            }

            return 0.0;
        }

        private void FillDistanceRows()
        {
            ImpactEffects fx = Effects;
            double rim = fx.FinalCraterDiameter / 2.0;
            foreach (double d in SampleDistances)
            {
                if (d < 1.5 * rim || d > ImpactModel.MaxGroundRange) continue;
                if (DistanceRows.Count == 5) break;
                var row = new DistanceRow { Distance = d };
                var arrivals = new List<string>();
                if (fx.HasSeismic)
                {
                    double m = ImpactEffects.EffectiveMagnitude(fx.Magnitude, d);
                    row.Intensity = ImpactEffects.MercalliRange(m);
                    row.Shaking = ImpactEffects.MercalliDescription(m);
                    if (m >= 1.0) arrivals.Add("shaking " + ResultsFormat.ShortDuration(ImpactEffects.SeismicArrival(d)));
                }

                if (fx.HasThermal)
                {
                    if (d <= fx.FireballRadius)
                    {
                        row.Heat = "Inside the fireball";
                    }
                    else
                    {
                        row.Heat = "None";
                        foreach (ThermalThreshold t in fx.Thermal)
                        {
                            if (t.Radius >= d)
                            {
                                row.Heat = t.Name;
                                break;
                            }
                        }
                    }
                }

                if (fx.HasEjecta && d > rim && d <= fx.EjectaMaxRange)
                {
                    double t = ImpactEffects.EjectaThickness(fx.TransientCraterDiameter, d);
                    if (t >= 1e-4)
                    {
                        row.Debris = ResultsFormat.Distance(t) + " deep";
                        double frag = ImpactEffects.MeanFragmentSize(fx.FinalCraterDiameter, d);
                        if (!double.IsNaN(frag)) row.Debris += ", bits " + ResultsFormat.Distance(frag);
                        double te = ImpactEffects.EjectaArrivalTime(d);
                        if (!double.IsNaN(te)) arrivals.Add("debris " + ResultsFormat.ShortDuration(te));
                    }
                }
                else if (fx.HasEjecta)
                {
                    row.Debris = "None";
                }

                if (fx.HasTsunami)
                {
                    double a = ImpactEffects.TsunamiAmplitude(fx.WaterCraterDiameter, fx.WaterDepth, d);
                    row.Wave = a >= 0.1 ? ResultsFormat.Distance(a) + " wave" : "Under 10 cm";
                    arrivals.Add("wave " + ResultsFormat.ShortDuration(ImpactEffects.TsunamiArrival(d, fx.WaterDepth)));
                }

                row.Arrival = arrivals.Count > 0 ? string.Join(" · ", arrivals) : "-";
                DistanceRows.Add(row);
            }
        }

        private void FillImpact(SimulationResult r, ImpactAssessment a, MeteorProperties p)
        {
            bool crater = a != null && a.Regime == ImpactRegime.Crater;
            Kind = crater ? OutcomeKind.GroundImpact : OutcomeKind.MeteoriteFall;
            Title = crater ? "GROUND IMPACT" : "METEORITE FALL";
            Energy = a?.Energy ?? ImpactModel.KineticEnergy(r.MassAtEnd, r.ImpactSpeed);
            Headline = ResultsFormat.Tnt(Energy);
            Comparison = ResultsFormat.Comparison(Energy);
            Subtitle = crater
                ? (IsOcean
                    ? "The meteor plunged into the ocean at " + ResultsFormat.Speed(r.ImpactSpeed) + ", blasting a cavity in the sea and a crater in the sea floor."
                    : "The meteor reached the ground at " + ResultsFormat.Speed(r.ImpactSpeed) + " and blasted out a crater.")
                : "The meteor was slowed to " + ResultsFormat.Speed(r.ImpactSpeed) +
                  " before landing: a penetration pit rather than a hypervelocity crater.";

            Stats.Add(new ReportStat("Impact energy", ResultsFormat.Tnt(Energy), ResultsFormat.Joules(Energy, true)));
            Stats.Add(new ReportStat("Impact speed", ResultsFormat.Speed(r.ImpactSpeed),
                Mach(r.ImpactSpeed, 0) + ", " + ResultsFormat.Angle(r.ImpactAngleDegrees) + " to the horizontal"));
            Stats.Add(new ReportStat("Mass at impact", ResultsFormat.Mass(r.MassAtEnd), MassNote(r.MassAtEnd, p)));
            if (a != null)
            {
                string type = crater ? (a.IsComplexCrater ? "complex crater" : "simple bowl-shaped crater") : "indicative only (slow impact)";
                if (IsOcean && crater)
                {
                    Stats.Add(new ReportStat("Seabed crater", ResultsFormat.Distance(Effects.FinalCraterDiameter),
                        "under " + ResultsFormat.Distance(Effects.WaterDepth) + " of water (assumed)"));
                    Stats.Add(new ReportStat("Water cavity", ResultsFormat.Distance(Effects.WaterCraterDiameter),
                        "transient crater in the water"));
                }
                else
                {
                    Stats.Add(new ReportStat("Crater size", ResultsFormat.Distance(a.FinalCraterDiameter),
                        type + ", " + ResultsFormat.Distance(a.FinalCraterDepth) + " deep"));
                }

                Stats.Add(new ReportStat("Impactor size", ResultsFormat.Distance(a.ImpactorDiameter), "equivalent sphere at impact"));
            }

            if (Effects.HasSeismic)
            {
                Stats.Add(new ReportStat("Earthquake", "Magnitude " + Effects.Magnitude.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                    "felt out to " + ResultsFormat.Reach(Effects.Seismic[0].Radius)));
            }

            Stats.Add(new ReportStat("Flight time", ResultsFormat.Duration(r.EndTime), AtmosphereNote(r)));
            GroundEffects = HasRings
                ? (Rings.Count > 0 && Rings[0].Radius >= ImpactModel.MaxGroundRange * 0.999
                    ? "The blast wave circles the whole planet (peak overpressure, ground range from the crater):"
                    : "Blast-wave reach on the ground (peak overpressure, ground range from the crater):")
                : "The impact is too small for a damaging blast wave.";
        }

        private void FillAirburst(SimulationResult r, ImpactAssessment a, MeteorProperties p)
        {
            Kind = OutcomeKind.Airburst;
            Title = "AIRBURST";
            Energy = a?.Energy ?? r.BreakupEnergy;
            double burst = a?.BurstAltitude ?? Math.Max(0.0, r.EndAltitude);
            Headline = ResultsFormat.Tnt(Energy);
            Comparison = ResultsFormat.Comparison(Energy);
            Subtitle = "The meteor broke apart and exploded " + ResultsFormat.Distance(burst) +
                       " above the ground. No crater formed.";
            GroundZeroOverpressure = Energy > 0 ? ImpactModel.PeakOverpressure(0.0, Energy, burst) : 0.0;

            Stats.Add(new ReportStat("Burst altitude", ResultsFormat.Distance(burst), "where the fragment cloud exploded"));
            Stats.Add(new ReportStat("Energy", ResultsFormat.Tnt(Energy), ResultsFormat.Joules(Energy, true)));
            Stats.Add(new ReportStat("Break-up at", double.IsNaN(r.BreakupAltitude) ? "-" : ResultsFormat.Distance(r.BreakupAltitude),
                "air pressure exceeded the body's strength"));
            Stats.Add(new ReportStat("Speed at burst", ResultsFormat.Speed(r.ImpactSpeed), Mach(r.ImpactSpeed, burst)));
            Stats.Add(new ReportStat("Ground zero", ResultsFormat.Pressure(GroundZeroOverpressure),
                "peak overpressure, " + ResultsFormat.Psi(GroundZeroOverpressure)));
            Stats.Add(new ReportStat("Path angle", ResultsFormat.Angle(r.ImpactAngleDegrees), "from the horizontal at burst"));
            Stats.Add(new ReportStat("Mass at burst", ResultsFormat.Mass(r.MassAtEnd), MassNote(r.MassAtEnd, p)));
            Stats.Add(new ReportStat("Flight time", ResultsFormat.Duration(r.EndTime), AtmosphereNote(r)));

            if (HasRings)
            {
                double windows = Rings.Count > 1 ? Rings[1].Radius : 0.0;
                GroundEffects = windows > 0
                    ? "The shock wave reaches the ground: windows break out to " + ResultsFormat.Distance(windows) + " from ground zero."
                    : "The shock wave reaches the ground as a loud boom; damage is light.";
            }
            else
            {
                GroundEffects = "The shock wave reaches the ground too weak to cause damage (" +
                                ResultsFormat.Pressure(GroundZeroOverpressure) + " peak).";
            }
        }

        private void FillDisintegrated(SimulationResult r, MeteorProperties p)
        {
            Kind = OutcomeKind.Disintegrated;
            Title = "BURNED UP";
            double altitude = Math.Max(0.0, r.EndAltitude);
            EntryConditions(r, p, out double entryMass, out double entrySpeed);
            double endEnergy = ImpactModel.KineticEnergy(r.MassAtEnd, r.EndSpeed);
            Energy = Math.Max(0.0, ImpactModel.KineticEnergy(entryMass, entrySpeed) - endEnergy);
            Headline = "Ablated away at " + ResultsFormat.Distance(altitude);
            Comparison = ResultsFormat.Comparison(Energy);
            Subtitle = "The meteor vaporised in the upper atmosphere as a bright fireball. Nothing reached the ground.";

            Stats.Add(new ReportStat("Gone at", ResultsFormat.Distance(altitude), "under 1 kg or 10 cm left"));
            Stats.Add(new ReportStat("Energy", ResultsFormat.Tnt(Energy), ResultsFormat.Joules(Energy, true) + " into the air"));
            Stats.Add(new ReportStat("Mass at entry", ResultsFormat.Mass(entryMass), "when it reached the atmosphere"));
            Stats.Add(new ReportStat("Mass lost", ResultsFormat.Percent(entryMass > 0 ? 1.0 - r.MassAtEnd / entryMass : 1.0),
                ResultsFormat.Mass(r.MassAtEnd) + " left at the end"));
            Stats.Add(new ReportStat("Entry speed", ResultsFormat.Speed(entrySpeed), "at the 120 km entry interface"));
            Stats.Add(new ReportStat("Peak heat", ResultsFormat.Temperature(r.PeakTemperature), "surface temperature"));
            Stats.Add(new ReportStat("Peak braking", ResultsFormat.Acceleration(r.PeakDeceleration), "deceleration by drag"));
            Stats.Add(new ReportStat("Flight time", ResultsFormat.Duration(r.EndTime), AtmosphereNote(r)));
            GroundEffects = "No ground effects: the energy was released as light and heat high in the atmosphere.";
        }

        private void FillTimeout(SimulationResult r)
        {
            Kind = OutcomeKind.Timeout;
            Title = "STILL IN FLIGHT";
            Headline = "No end event";
            Subtitle = "The simulated time limit (" + ResultsFormat.Duration(r.EndTime) +
                       ") was reached before the meteor hit the ground, exploded or burned up.";
            Stats.Add(new ReportStat("Altitude", ResultsFormat.Distance(r.EndAltitude), "when the run stopped"));
            Stats.Add(new ReportStat("Speed", ResultsFormat.Speed(r.EndSpeed), ""));
            Stats.Add(new ReportStat("Mass", ResultsFormat.Mass(r.MassAtEnd), ""));
            Stats.Add(new ReportStat("Simulated time", ResultsFormat.Duration(r.EndTime), ""));
            GroundEffects = "No ground effects.";
        }

        private void FillError(SimulationResult r)
        {
            Kind = OutcomeKind.Error;
            Title = "SIMULATION ERROR";
            Headline = "The run stopped early";
            Subtitle = string.IsNullOrEmpty(r.Message) ? "A numerical problem ended the run." : Shorten(r.Message, 180);
            Stats.Add(new ReportStat("Altitude", ResultsFormat.Distance(r.EndAltitude), "last good state"));
            Stats.Add(new ReportStat("Speed", ResultsFormat.Speed(r.EndSpeed), ""));
            Stats.Add(new ReportStat("Mass", ResultsFormat.Mass(r.MassAtEnd), ""));
            Stats.Add(new ReportStat("Simulated time", ResultsFormat.Duration(r.EndTime), ""));
            GroundEffects = "No ground effects were computed.";
        }

        private void FillDetails(SimulationResult r, ImpactAssessment a, MeteorProperties p)
        {
            var entry = new ReportSection("Start and entry");
            if (p != null)
            {
                entry.Add("Start mode", ModeText);
                entry.Add("Initial mass", ResultsFormat.Mass(p.Mass));
                for (int i = 0; i < Composition.Count; i++)
                {
                    entry.Add(i == 0 ? "Composition" : "", Math.Round(Composition[i].Fraction * 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%  " + Composition[i].Name);
                }

                if (p.Material != null)
                {
                    entry.Add("Bulk density", ResultsFormat.Sig(p.Material.Density, 3) + " kg/m³", Composition.Count > 1 ? "blended" : "");
                    entry.Add("Bulk strength", ResultsFormat.Pressure(p.Material.Strength), "breaks up when ρv² exceeds it");
                }

                entry.Add("Start distance", ResultsFormat.Distance(p.StartPosition.Magnitude - PhysicalConstants.EarthRadius), "above the surface");
            }

            entry.Add("Reached 120 km after", double.IsNaN(r.AtmosphereEntryTime) ? "-" : ResultsFormat.Duration(r.AtmosphereEntryTime));
            entry.Add("Entry speed", double.IsNaN(r.AtmosphereEntrySpeed) ? "-" : ResultsFormat.Speed(r.AtmosphereEntrySpeed));
            Details.Add(entry);

            var flight = new ReportSection("Atmospheric flight");
            flight.Add("Peak temperature", ResultsFormat.Temperature(r.PeakTemperature), "surface layer");
            flight.Add("Peak deceleration", ResultsFormat.Acceleration(r.PeakDeceleration), "drag only");
            SeriesPeaks(r.Series, out double maxQ, out double maxQAltitude, out double maxMach);
            flight.Add("Peak ram pressure", maxQ > 0 ? ResultsFormat.Pressure(maxQ) : "-",
                maxQ > 0 ? (maxQAltitude < 1.0 ? "at ground level" : "at " + ResultsFormat.Distance(maxQAltitude)) : "");
            flight.Add("Peak Mach number", maxMach > 0 ? ResultsFormat.Sig(maxMach, 3) : "-");
            flight.Add("Break-up", double.IsNaN(r.BreakupAltitude) ? "Did not break up" : "at " + ResultsFormat.Distance(r.BreakupAltitude),
                r.BreakupEnergy > 0 ? ResultsFormat.Tnt(r.BreakupEnergy) + " at break-up" : "");
            Details.Add(flight);

            var end = new ReportSection("End of the run");
            end.Add("Outcome", Title);
            end.Add("Reason", Shorten(r.Message, 90));
            end.Add("Time", ResultsFormat.Duration(r.EndTime));
            end.Add("Altitude", ResultsFormat.Distance(Math.Max(0.0, r.EndAltitude)));
            end.Add("Speed", ResultsFormat.Speed(r.ImpactSpeed), "relative to the air");
            end.Add("Path angle", ResultsFormat.Angle(r.ImpactAngleDegrees), "below the horizontal");
            end.Add("Mass", ResultsFormat.Mass(r.MassAtEnd));
            if (HasLocation)
            {
                end.Add("Location", ResultsFormat.LatLon(Location.Lat, Location.Lon));
            }

            if (a != null && a.Regime == ImpactRegime.Airburst)
            {
                end.Add("Ground-zero overpressure", ResultsFormat.Pressure(GroundZeroOverpressure));
            }

            Details.Add(end);

            if (Effects.Regime == ImpactRegime.Crater || Effects.Regime == ImpactRegime.PenetrationPit)
            {
                var global = new ReportSection("Planet-wide");
                global.Add("Scale", GlobalTitle.Length > 0 ? char.ToUpperInvariant(GlobalTitle[0]) + GlobalTitle.Substring(1).ToLowerInvariant() : "-");
                global.Add("Earth's orbit", ImpactEffects.OrbitChange(Effects.MomentumRatio),
                    "momentum ratio " + ResultsFormat.Scientific(Effects.MomentumRatio, 2));
                global.Add("Earth's spin", ImpactEffects.RotationChange(Effects.AngularMomentumRatio),
                    "ratio " + ResultsFormat.Scientific(Effects.AngularMomentumRatio, 2));
                if (Effects.IsOcean)
                {
                    global.Add("Ocean depth", ResultsFormat.Distance(Effects.WaterDepth), "assumed open ocean");
                    global.Add("Speed at sea floor", ResultsFormat.Speed(Effects.SeafloorSpeed), "slowed by the water");
                }

                Details.Add(global);
            }

            var models = new ReportSection("Models");
            models.Add("Atmosphere", "U.S. Standard Atmosphere 1976");
            models.Add("Break-up and airburst", "Pancake model", "Collins, Melosh & Marcus 2005 (EIEP)");
            models.Add("Craters", "Pi-group scaling", "EIEP Eqs. 21-28");
            models.Add("Blast", "EIEP Eqs. 54-58", "plus a point-source model for high bursts");
            models.Add("Heat, shaking, debris", "EIEP Eqs. 32-53", "fireball, Richter magnitude, ejecta");
            models.Add("Ocean impacts", "EIEP Eq. 65", "waves decay as 1/r (Ward & Asphaug 2000)");
            Details.Add(models);
        }

        private static void SeriesPeaks(TimeSeries series, out double maxQ, out double maxQAltitude, out double maxMach)
        {
            maxQ = 0;
            maxQAltitude = 0;
            maxMach = 0;
            TimeSeries s = series?.Entry != null && series.Entry.Count > 1 ? series.Entry : series;
            if (s == null)
            {
                return;
            }

            for (int i = 0; i < s.Count; i++)
            {
                if (s.DynamicPressure[i] > maxQ)
                {
                    maxQ = s.DynamicPressure[i];
                    maxQAltitude = s.Altitude[i];
                }

                maxMach = Math.Max(maxMach, s.Mach[i]);
            }
        }

        /// <summary>Mass and speed when the meteor crossed the entry interface (falls back to the start values).</summary>
        public static void EntryConditions(SimulationResult r, MeteorProperties p, out double mass, out double speed)
        {
            TimeSeries entry = r.Series?.Entry;
            if (entry != null && entry.Count > 0)
            {
                mass = entry.Mass[0];
                speed = entry.Speed[0];
                return;
            }

            mass = p?.Mass ?? r.MassAtEnd;
            speed = double.IsNaN(r.AtmosphereEntrySpeed) ? r.EndSpeed : r.AtmosphereEntrySpeed;
        }

        private static string MassNote(double massAtEnd, MeteorProperties p)
        {
            if (p == null || !(p.Mass > 0))
            {
                return "";
            }

            return ResultsFormat.Percent(massAtEnd / p.Mass) + " of the starting " + ResultsFormat.Mass(p.Mass);
        }

        private static string Mach(double speed, double altitude)
        {
            double c = Atmosphere.Sample(Math.Max(0.0, Math.Min(altitude, Atmosphere.TopOfLayeredModel))).SpeedOfSound;
            return c > 0 && speed > 0 ? "Mach " + ResultsFormat.Sig(speed / c, 3) : "";
        }

        private static string AtmosphereNote(SimulationResult r)
        {
            if (double.IsNaN(r.AtmosphereEntryTime))
            {
                return "";
            }

            return ResultsFormat.Duration(r.EndTime - r.AtmosphereEntryTime) + " in the atmosphere";
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? "";
            }

            return text.Substring(0, max - 3) + "...";
        }
    }
}
