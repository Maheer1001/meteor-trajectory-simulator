using System;

namespace MTS.Core
{
    /// <summary>Result of one heating/ablation update.</summary>
    public readonly struct HeatingStep
    {
        public readonly double Temperature;
        public readonly double MassLost;
        public readonly double HeatingPower;

        public HeatingStep(double temperature, double massLost, double heatingPower)
        {
            Temperature = temperature;
            MassLost = massLost;
            HeatingPower = heatingPower;
        }
    }

    /// <summary>
    /// Heating and ablation (single-body meteor theory with a lumped surface layer).
    ///
    /// Power in:  P_air = 1/2 Lambda A rho_a v^3 (aerodynamic heating) + epsilon S0 A (sunlight, grey body).
    /// Power out: epsilon sigma S (T^4 - T_env^4) (radiation, S = 4 A).
    /// Below the ablation temperature the energy heats a surface layer of mass
    /// m_h = min(m, rho_i S delta) with heat capacity c: m_h c dT/dt = P_in - P_rad. This is solved with a backward-Euler
    /// (implicit) step, which is unconditionally stable for the stiff T^4 term and can never go below 0 K.
    /// Once T reaches T_abl the surface is held there and the surplus energy ablates material:
    /// dm/dt = -(P_in - P_rad(T_abl)) / Q*, which is the classical dm/dt = -Lambda A rho_a v^3 / (2 Q*) when
    /// radiation is small.
    /// </summary>
    public static class Heating
    {
        /// <summary>Thermal skin depth delta (m) of the heated surface layer.</summary>
        public const double SkinDepth = 0.01;

        /// <summary>
        /// Radiative-equilibrium temperature of a fast-rotating grey sphere at 1 AU (absorptivity = emissivity):
        /// T = (S0 / (4 sigma) + T_cmb^4)^(1/4), about 278 K. Used as the starting temperature in space.
        /// </summary>
        public static double SpaceEquilibriumTemperature =>
            Math.Pow(PhysicalConstants.SolarConstant / (4.0 * PhysicalConstants.StefanBoltzmann) +
                     Math.Pow(PhysicalConstants.CmbTemperature, 4), 0.25);

        /// <summary>Aerodynamic heating power 1/2 Lambda A rho v^3 (W).</summary>
        public static double AerodynamicHeating(double lambda, double area, double airDensity, double relativeSpeed) =>
            0.5 * lambda * area * Math.Max(0.0, airDensity) * relativeSpeed * relativeSpeed * relativeSpeed;

        /// <summary>Advances temperature and ablation by dt.</summary>
        public static HeatingStep Step(MeteorMaterial material, double mass, double temperature, double area,
            double airDensity, double relativeSpeed, double environmentTemperature, double dt)
        {
            if (mass <= 0 || area <= 0 || dt <= 0)
            {
                return new HeatingStep(Math.Max(0.0, temperature), 0.0, 0.0);
            }

            double eps = material.Emissivity;
            double sigma = PhysicalConstants.StefanBoltzmann;
            double surface = 4.0 * area;
            double pAir = AerodynamicHeating(material.HeatTransferCoefficient, area, airDensity, relativeSpeed);
            double pIn = pAir + eps * PhysicalConstants.SolarConstant * area;
            double envT4 = Math.Pow(Math.Max(0.0, environmentTemperature), 4);
            double heatedMass = Math.Min(mass, material.Density * surface * SkinDepth);
            double heatCapacity = heatedMass * material.SpecificHeat; // J/K
            double t0 = Math.Max(0.0, temperature);

            double tNew = SolveImplicit(t0, heatCapacity, dt, pIn, eps * sigma * surface, envT4);
            double massLost = 0.0;
            double tAbl = material.AblationTemperature;
            if (tNew > tAbl)
            {
                // Energy left after heating the layer to T_abl and radiating at T_abl goes into ablation.
                double surplus = dt * (pIn - eps * sigma * surface * (Math.Pow(tAbl, 4) - envT4)) -
                                 heatCapacity * (tAbl - t0);
                if (surplus > 0)
                {
                    massLost = Math.Min(mass, surplus / material.HeatOfAblation);
                }

                tNew = tAbl;
            }

            return new HeatingStep(tNew, massLost, pAir);
        }

        // Solves C (T' - T) = dt (P - k (T'^4 - Te4)) for T' >= 0. The left minus right side is strictly increasing
        // in T', so the root is unique; Newton iteration is safeguarded by a bisection bracket.
        private static double SolveImplicit(double t, double c, double dt, double p, double k, double te4)
        {
            if (c <= 0)
            {
                return t;
            }

            double lo = 0.0;
            double hi = t + dt * (p + k * te4) / c; // f(hi) >= 0 and f(0) <= 0
            double x = Math.Min(Math.Max(t, lo), hi);
            for (int i = 0; i < 60; i++)
            {
                double x3 = x * x * x;
                double f = c * (x - t) - dt * (p - k * (x3 * x - te4));
                if (Math.Abs(f) <= 1e-12 * (c * Math.Max(1.0, x)))
                {
                    break;
                }

                if (f > 0)
                {
                    hi = x;
                }
                else
                {
                    lo = x;
                }

                double df = c + 4.0 * dt * k * x3;
                double next = x - f / df;
                if (!(next > lo && next < hi))
                {
                    next = 0.5 * (lo + hi);
                }

                if (Math.Abs(next - x) <= 1e-9 * Math.Max(1.0, x))
                {
                    x = next;
                    break;
                }

                x = next;
            }

            return Math.Max(0.0, x);
        }
    }
}
