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

        public static OutcomeReport Build(SimulationResult result, ImpactAssessment impact, MeteorProperties properties)
        {
            var report = new OutcomeReport();
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
            return report;
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
                ? "The meteor reached the ground at " + ResultsFormat.Speed(r.ImpactSpeed) + " and blasted out a crater."
                : "The meteor was slowed to " + ResultsFormat.Speed(r.ImpactSpeed) +
                  " before landing: a penetration pit rather than a hypervelocity crater.";

            Stats.Add(new ReportStat("Impact energy", ResultsFormat.Tnt(Energy), ResultsFormat.Joules(Energy, true)));
            Stats.Add(new ReportStat("Impact speed", ResultsFormat.Speed(r.ImpactSpeed), Mach(r.ImpactSpeed, 0)));
            Stats.Add(new ReportStat("Impact angle", ResultsFormat.Angle(r.ImpactAngleDegrees), "from the horizontal"));
            Stats.Add(new ReportStat("Mass at impact", ResultsFormat.Mass(r.MassAtEnd), MassNote(r.MassAtEnd, p)));
            if (a != null)
            {
                string type = crater ? (a.IsComplexCrater ? "complex crater" : "simple bowl-shaped crater") : "indicative only (slow impact)";
                Stats.Add(new ReportStat("Crater diameter", ResultsFormat.Distance(a.FinalCraterDiameter), type));
                Stats.Add(new ReportStat("Crater depth", ResultsFormat.Distance(a.FinalCraterDepth),
                    "transient crater " + ResultsFormat.Distance(a.TransientCraterDiameter) + " wide"));
                Stats.Add(new ReportStat("Impactor size", ResultsFormat.Distance(a.ImpactorDiameter), "equivalent sphere at impact"));
            }

            Stats.Add(new ReportStat("Flight time", ResultsFormat.Duration(r.EndTime), AtmosphereNote(r)));
            GroundEffects = HasRings
                ? "Blast-wave reach on the ground (peak overpressure, ground range from the crater):"
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

            Stats.Add(new ReportStat("Burst altitude", ResultsFormat.Distance(burst), "fragment cloud reached 7× its break-up width"));
            Stats.Add(new ReportStat("Energy deposited", ResultsFormat.Tnt(Energy), ResultsFormat.Joules(Energy, true)));
            Stats.Add(new ReportStat("Break-up altitude", double.IsNaN(r.BreakupAltitude) ? "-" : ResultsFormat.Distance(r.BreakupAltitude),
                "air pressure exceeded the body's strength"));
            Stats.Add(new ReportStat("Speed at burst", ResultsFormat.Speed(r.ImpactSpeed), Mach(r.ImpactSpeed, burst)));
            Stats.Add(new ReportStat("Blast at ground zero", ResultsFormat.Pressure(GroundZeroOverpressure),
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

            Stats.Add(new ReportStat("Ablated away at", ResultsFormat.Distance(altitude), "too small to track: under 1 kg or 10 cm"));
            Stats.Add(new ReportStat("Energy deposited", ResultsFormat.Tnt(Energy), ResultsFormat.Joules(Energy, true) + " into the air"));
            Stats.Add(new ReportStat("Mass at entry", ResultsFormat.Mass(entryMass), "when it reached the atmosphere"));
            Stats.Add(new ReportStat("Mass lost", ResultsFormat.Percent(entryMass > 0 ? 1.0 - r.MassAtEnd / entryMass : 1.0),
                ResultsFormat.Mass(r.MassAtEnd) + " left at the end"));
            Stats.Add(new ReportStat("Entry speed", ResultsFormat.Speed(entrySpeed), "at the 120 km entry interface"));
            Stats.Add(new ReportStat("Peak temperature", ResultsFormat.Temperature(r.PeakTemperature), "surface layer"));
            Stats.Add(new ReportStat("Peak deceleration", ResultsFormat.Acceleration(r.PeakDeceleration), "drag only"));
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
                entry.Add("Start mode", p.Mode == EntryMode.Inbound ? "Realistic entry" : "Classic NEA (released from rest)");
                entry.Add("Initial mass", ResultsFormat.Mass(p.Mass));
                if (p.Material != null)
                {
                    entry.Add("Material", p.Material.Name);
                    entry.Add("Density", ResultsFormat.Sig(p.Material.Density, 4) + " kg/m³");
                    entry.Add("Bulk strength", ResultsFormat.Pressure(p.Material.Strength), "break-up when ρv² exceeds this");
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
                maxQ > 0 ? "at " + ResultsFormat.Distance(maxQAltitude) : "");
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

            var models = new ReportSection("Models");
            models.Add("Atmosphere", "U.S. Standard Atmosphere 1976");
            models.Add("Break-up and airburst", "Pancake model", "Collins, Melosh & Marcus 2005 (EIEP)");
            models.Add("Craters", "Pi-group scaling", "EIEP Eqs. 21-28");
            models.Add("Blast", "EIEP Eqs. 54-58", "plus a point-source model for high bursts");
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
