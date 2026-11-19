using System;

namespace MTS.Core
{
    public enum ImpactRegime
    {
        /// <summary>No ground effects (ablated away, error or timeout).</summary>
        None,
        /// <summary>Hypervelocity ground impact forming a crater (impact speed at least 3 km/s).</summary>
        Crater,
        /// <summary>
        /// Slow ground impact (below about 3 km/s, e.g. after the body has been slowed to near terminal velocity):
        /// a meteorite fall or penetration pit. Crater scaling is extrapolated outside its range and is only indicative.
        /// </summary>
        PenetrationPit,
        /// <summary>Energy released in the air; blast wave but no crater.</summary>
        Airburst,
    }

    /// <summary>Ground effects of the end event.</summary>
    public sealed class ImpactAssessment
    {
        public ImpactRegime Regime { get; internal set; }
        /// <summary>Kinetic energy released (J).</summary>
        public double Energy { get; internal set; }
        public double EnergyKilotons => Energy / PhysicalConstants.JoulesPerKilotonTnt;
        /// <summary>Burst altitude (m); 0 for a ground impact.</summary>
        public double BurstAltitude { get; internal set; }
        public double ImpactorDiameter { get; internal set; }
        public double ImpactSpeed { get; internal set; }
        public double ImpactAngleDegrees { get; internal set; }
        public double TransientCraterDiameter { get; internal set; }
        public double TransientCraterDepth { get; internal set; }
        public double FinalCraterDiameter { get; internal set; }
        public double FinalCraterDepth { get; internal set; }
        public double FinalCraterRadius => FinalCraterDiameter / 2.0;
        public bool IsComplexCrater { get; internal set; }
        /// <summary>Peak overpressure thresholds (Pa) matching <see cref="BlastRadii"/>.</summary>
        public double[] OverpressureLevels { get; internal set; }
        /// <summary>Ground range (m) out to which each overpressure level is reached.</summary>
        public double[] BlastRadii { get; internal set; }
    }

    /// <summary>
    /// Impact and airburst effects after Collins, Melosh and Marcus (2005), "Earth Impact Effects Program",
    /// Meteoritics and Planetary Science 40, 817-840. All quantities in SI units.
    /// </summary>
    public static class ImpactModel
    {
        /// <summary>Target density (kg/m^3): sedimentary rock, as in EIEP.</summary>
        public const double DefaultTargetDensity = 2500.0;
        public const double SurfaceGravity = 9.81;
        /// <summary>Simple-to-complex transition diameter on Earth (m).</summary>
        public const double ComplexTransitionDiameter = 3200.0;
        /// <summary>Below this impact speed (m/s) the result is reported as a penetration pit / meteorite fall.</summary>
        public const double HypervelocityThreshold = 3000.0;

        /// <summary>
        /// The nine overpressure rings shown on the results screen (Pa): 0.04, 0.15, 0.7, 2, 3, 5, 7, 10 and 29 psi.
        /// </summary>
        public static readonly double[] StandardOverpressureLevels =
            { 275.8, 1034.2, 4826.3, 13789.5, 20684.3, 34473.8, 48263.3, 68947.6, 199948.0 };

        // Surface-burst fit (Eq. 54): crossover pressure and distance for a 1 kt explosion.
        public const double CrossoverPressure = 75000.0;
        public const double CrossoverDistance = 290.0;

        public static double KineticEnergy(double mass, double speed) => 0.5 * mass * speed * speed;

        /// <summary>Transient crater diameter (m), Eq. 21: D_tc = 1.161 (rho_i/rho_t)^(1/3) L^0.78 v^0.44 g^-0.22 sin^(1/3) theta.</summary>
        public static double TransientCraterDiameter(double impactorDiameter, double impactorDensity, double speed,
            double angleDegrees, double targetDensity = DefaultTargetDensity, double gravity = SurfaceGravity)
        {
            if (!(impactorDiameter > 0) || !(speed > 0))
            {
                return 0.0;
            }

            double theta = Math.Max(1.0, Math.Min(90.0, angleDegrees)) * Math.PI / 180.0;
            return 1.161 * Math.Pow(impactorDensity / targetDensity, 1.0 / 3.0) * Math.Pow(impactorDiameter, 0.78) *
                   Math.Pow(speed, 0.44) * Math.Pow(gravity, -0.22) * Math.Pow(Math.Sin(theta), 1.0 / 3.0);
        }

        /// <summary>
        /// Final rim-to-rim diameter (m): simple craters D_fr = 1.25 D_tc (Eq. 22); if D_tc exceeds 2.56 km the crater is
        /// complex and D_fr = 1.17 D_tc^1.13 / D_c^0.13 with diameters in km and D_c = 3.2 km (Eq. 27).
        /// </summary>
        public static double FinalCraterDiameter(double transientDiameter, out bool complex)
        {
            complex = transientDiameter > 2560.0;
            if (!complex)
            {
                return 1.25 * transientDiameter;
            }

            double dtcKm = transientDiameter / 1000.0;
            return 1000.0 * 1.17 * Math.Pow(dtcKm, 1.13) / Math.Pow(ComplexTransitionDiameter / 1000.0, 0.13);
        }

        /// <summary>Peak overpressure (Pa) of a 1 kt surface burst at scaled distance r1 (m), Eq. 54.</summary>
        public static double SurfaceBurstOverpressure1Kt(double r1, double crossoverDistance = CrossoverDistance)
        {
            if (!(r1 > 0))
            {
                return double.PositiveInfinity;
            }

            return CrossoverPressure * crossoverDistance / (4.0 * r1) * (1.0 + 3.0 * Math.Pow(crossoverDistance / r1, 1.3));
        }

        /// <summary>
        /// Peak overpressure (Pa) at ground range r (m) from ground zero for an explosion of energy E (J) at burst
        /// altitude zb (m). Yield scaling r1 = r / E_kt^(1/3), z_b1 = z_b / E_kt^(1/3) (Eq. 57).
        /// Surface burst (zb = 0): Eq. 54. Airburst: inside the regular-reflection region p = p0 exp(-beta r1) with
        /// p0 = 3.14e11 z_b1^-2.6 and beta = 34.87 z_b1^-1.73 (Eqs. 55-56); beyond the Mach-stem radius
        /// r_m1 = 550 z_b1 / (1.2 (550 - z_b1)) (Eq. 58) Eq. 54 applies with r_x = 289 + 0.65 z_b1.
        /// For z_b1 of 550 m or more there is no Mach region. An airburst result is never
        /// below <see cref="HighAltitudeAirburstOverpressure"/>, which keeps high-altitude bursts physical.
        /// </summary>
        public static double PeakOverpressure(double groundRange, double energy, double burstAltitude)
        {
            double ekt = energy / PhysicalConstants.JoulesPerKilotonTnt;
            if (!(ekt > 0))
            {
                return 0.0;
            }

            double scale = Math.Pow(ekt, 1.0 / 3.0);
            double r1 = Math.Max(0.0, groundRange) / scale;
            if (!(burstAltitude > 0))
            {
                return SurfaceBurstOverpressure1Kt(r1);
            }

            // The point-source model takes over where the EIEP fit (calibrated at modest scaled heights) underestimates
            // high, small bursts; for low bursts the fit is the larger and is kept.
            return Math.Max(EiepAirburstOverpressure(r1, burstAltitude / scale),
                HighAltitudeAirburstOverpressure(groundRange, energy, burstAltitude));
        }

        /// <summary>EIEP airburst fit (Eqs. 55-58) at scaled ground range r1 and scaled burst altitude zb1 (m / kt^(1/3)).</summary>
        public static double EiepAirburstOverpressure(double r1, double zb1)
        {
            if (zb1 < 550.0)
            {
                double rm1 = 550.0 * zb1 / (1.2 * (550.0 - zb1));
                if (r1 >= rm1)
                {
                    return SurfaceBurstOverpressure1Kt(r1, 289.0 + 0.65 * zb1);
                }
            }

            double p0 = 3.14e11 * Math.Pow(zb1, -2.6);
            double beta = 34.87 * Math.Pow(zb1, -1.73);
            return p0 * Math.Exp(-beta * r1);
        }

        /// <summary>Peak overpressure reflection factor at the ground for a weak shock (acoustic limit, rigid ground).</summary>
        public const double GroundReflectionFactor = 2.0;

        /// <summary>
        /// Ground overpressure (Pa) of an airburst from a point-source model that stays valid for high, small bursts,
        /// where the EIEP fit (calibrated on nuclear tests at modest scaled heights) extrapolates to almost nothing.
        /// As in EIEP the meteor's energy is treated as an equivalent nuclear yield.
        /// 1. Free-air blast: a surface burst on rigid ground behaves like a free-air burst of twice the yield
        ///    (Glasstone and Dolan 1977, sec. 3.34), so the free-air overpressure of yield E at range R is the Eq. 54
        ///    surface-burst curve for E/2.
        /// 2. Sachs scaling to the ambient air at the burst altitude (Glasstone and Dolan 1977, sec. 3.64-3.66):
        ///    dp_b = (p_b / p_0) P54(R (p_b / p_0)^(1/3) / (E_kt / 2)^(1/3)), with R the slant range from the burst.
        /// 3. Downward propagation as a weak shock through the stratified atmosphere: along a ray the energy flux
        ///    dp^2 / (rho c) times the ray-tube area is conserved, so the amplitude grows by
        ///    sqrt(rho_g c_g / (rho_b c_b)) between the burst altitude and the ground (geometric acoustics, e.g.
        ///    Pierce 1989, Acoustics, ch. 8). Refraction, absorption and nonlinear attenuation are ignored.
        /// 4. Reflection at the ground doubles the incident overpressure (<see cref="GroundReflectionFactor"/>).
        /// Checks: Chelyabinsk (about 500 kt at about 30 km; Brown et al. 2013, Nature 503, 238; Popova et al. 2013,
        /// Science 342, 1069) gives about 1.6 kPa below the burst and 0.04 psi (window damage) out to about 120 km,
        /// matching the observed window breakage up to about 120 km from the track. For low bursts (Tunguska-like,
        /// 15 Mt at 8 km) it is within 10 % of the EIEP fit below the burst, so the EIEP results there are unchanged.
        /// </summary>
        public static double HighAltitudeAirburstOverpressure(double groundRange, double energy, double burstAltitude)
        {
            double ekt = energy / PhysicalConstants.JoulesPerKilotonTnt;
            if (!(ekt > 0) || !(burstAltitude > 0))
            {
                return 0.0;
            }

            AtmosphereSample burst = Atmosphere.Sample(burstAltitude);
            AtmosphereSample ground = Atmosphere.Sample(0.0);
            double pressureRatio = burst.Pressure / ground.Pressure;
            double slant = Math.Sqrt(burstAltitude * burstAltitude + groundRange * groundRange);
            double r1 = slant * Math.Pow(pressureRatio, 1.0 / 3.0) / Math.Pow(0.5 * ekt, 1.0 / 3.0);
            double freeAir = pressureRatio * SurfaceBurstOverpressure1Kt(r1);
            double impedance = Math.Sqrt(ground.Density * ground.SpeedOfSound / (burst.Density * burst.SpeedOfSound));
            return GroundReflectionFactor * freeAir * impedance;
        }

        /// <summary>Largest ground range (m) at which the peak overpressure is at least the given level; 0 if never.</summary>
        public static double BlastRadius(double level, double energy, double burstAltitude)
        {
            if (!(energy > 0) || !(level > 0))
            {
                return 0.0;
            }

            const double rMin = 1e-3;
            const double rMax = 1e8;
            const int samples = 1200;
            double logMin = Math.Log(rMin);
            double logStep = (Math.Log(rMax) - logMin) / samples;

            int last = -1;
            for (int i = 0; i <= samples; i++)
            {
                double r = Math.Exp(logMin + i * logStep);
                if (PeakOverpressure(r, energy, burstAltitude) >= level)
                {
                    last = i;
                }
            }

            if (last < 0)
            {
                return PeakOverpressure(0.0, energy, burstAltitude) >= level ? rMin : 0.0;
            }

            if (last == samples)
            {
                return rMax;
            }

            double lo = Math.Exp(logMin + last * logStep);
            double hi = Math.Exp(logMin + (last + 1) * logStep);
            for (int k = 0; k < 60; k++)
            {
                double mid = 0.5 * (lo + hi);
                if (PeakOverpressure(mid, energy, burstAltitude) >= level)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            return 0.5 * (lo + hi);
        }

        /// <summary>Ground effects for a ground impact of a body with the given properties.</summary>
        public static ImpactAssessment GroundImpact(double mass, double impactorDensity, double speed, double angleDegrees,
            double targetDensity = DefaultTargetDensity)
        {
            double diameter = 2.0 * Math.Pow(3.0 * mass / (4.0 * Math.PI * impactorDensity), 1.0 / 3.0);
            double dtc = TransientCraterDiameter(diameter, impactorDensity, speed, angleDegrees, targetDensity);
            double dfr = FinalCraterDiameter(dtc, out bool complex);
            double energy = KineticEnergy(mass, speed);
            var a = new ImpactAssessment
            {
                Regime = speed >= HypervelocityThreshold ? ImpactRegime.Crater : ImpactRegime.PenetrationPit,
                Energy = energy,
                BurstAltitude = 0.0,
                ImpactorDiameter = diameter,
                ImpactSpeed = speed,
                ImpactAngleDegrees = angleDegrees,
                TransientCraterDiameter = dtc,
                TransientCraterDepth = dtc / (2.0 * Math.Sqrt(2.0)), // Eq. 25
                FinalCraterDiameter = dfr,
                // simple: about 0.2 D_fr; complex: d_fr = 0.4 D_fr^0.3 (km), Eq. 28
                FinalCraterDepth = complex ? 1000.0 * 0.4 * Math.Pow(dfr / 1000.0, 0.3) : 0.2 * dfr,
                IsComplexCrater = complex,
            };
            FillBlast(a, 0.0);
            return a;
        }

        /// <summary>Ground effects of an airburst releasing the given energy (J) at burstAltitude (m).</summary>
        public static ImpactAssessment Airburst(double energy, double speed, double burstAltitude, double angleDegrees)
        {
            var a = new ImpactAssessment
            {
                Regime = ImpactRegime.Airburst,
                Energy = Math.Max(0.0, energy),
                BurstAltitude = Math.Max(0.0, burstAltitude),
                ImpactSpeed = speed,
                ImpactAngleDegrees = angleDegrees,
            };
            FillBlast(a, a.BurstAltitude);
            return a;
        }

        /// <summary>Ground effects for the end of a simulation run.</summary>
        public static ImpactAssessment Assess(SimulationResult result, MeteorProperties properties)
        {
            if (result == null)
            {
                return Empty();
            }

            MeteorState s = result.FinalState;
            switch (result.Reason)
            {
                case EndReason.Impact:
                    return GroundImpact(s.Mass, properties.Material.Density, result.ImpactSpeed, result.ImpactAngleDegrees);
                case EndReason.Airburst:
                    // The fragment cloud deposits essentially all of its kinetic energy between break-up and the
                    // burst point, most of it just before the burst, so the energy at break-up is released at the
                    // burst altitude (as in EIEP, which uses the swarm's energy rather than its residual energy).
                    double energy = result.BreakupEnergy > 0 ? result.BreakupEnergy : KineticEnergy(s.Mass, result.ImpactSpeed);
                    return Airburst(energy, result.ImpactSpeed, s.Altitude, result.ImpactAngleDegrees);
                default:
                    return Empty();
            }
        }

        private static ImpactAssessment Empty()
        {
            var a = new ImpactAssessment { Regime = ImpactRegime.None };
            a.OverpressureLevels = (double[])StandardOverpressureLevels.Clone();
            a.BlastRadii = new double[StandardOverpressureLevels.Length];
            return a;
        }

        private static void FillBlast(ImpactAssessment a, double burstAltitude)
        {
            a.OverpressureLevels = (double[])StandardOverpressureLevels.Clone();
            a.BlastRadii = new double[a.OverpressureLevels.Length];
            for (int i = 0; i < a.OverpressureLevels.Length; i++)
            {
                a.BlastRadii[i] = BlastRadius(a.OverpressureLevels[i], a.Energy, burstAltitude);
            }
        }
    }
}
