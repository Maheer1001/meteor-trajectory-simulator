using System;

namespace MTS.Core
{
    /// <summary>
    /// Bulk material/composition of a meteoroid. Everything the heating, ablation and fragmentation models need
    /// about "what the rock is made of" lives here, so presets or composition mixes can be added later.
    /// For now <see cref="CreateCustom"/> builds a material from the user's inputs (density, SHC, SCF) and fills
    /// the parameters the user does not enter with stony or iron defaults, chosen by density.
    /// </summary>
    public sealed class MeteorMaterial
    {
        /// <summary>Density at or above which the "iron" defaults are used (kg/m^3).</summary>
        public const double IronDensityThreshold = 6000.0;

        public string Name { get; }
        /// <summary>Bulk density rho_i (kg/m^3).</summary>
        public double Density { get; }
        /// <summary>Specific heat capacity c (J/(kg K)).</summary>
        public double SpecificHeat { get; }
        /// <summary>Effective heat of ablation Q* (J/kg).</summary>
        public double HeatOfAblation { get; }
        /// <summary>Surface temperature at which ablation starts and is held (K).</summary>
        public double AblationTemperature { get; }
        /// <summary>Thermal emissivity epsilon (0..1), also used as the solar absorptivity (grey body).</summary>
        public double Emissivity { get; }
        /// <summary>Heat-transfer coefficient Lambda (fraction of the air's kinetic energy flux that reaches the body).</summary>
        public double HeatTransferCoefficient { get; }
        /// <summary>Bulk strength Y_i (Pa) used for aerodynamic break-up.</summary>
        public double Strength { get; }

        /// <summary>How the material looks (for the visuals only; never used by the physics).</summary>
        public MaterialAppearance Appearance { get; }

        public MeteorMaterial(string name, double density, double specificHeat, double heatOfAblation,
            double ablationTemperature, double emissivity, double heatTransferCoefficient, double strength,
            MaterialAppearance? appearance = null)
        {
            Appearance = appearance ?? MaterialAppearance.ForDensity(density);
            Name = name;
            Density = density;
            SpecificHeat = specificHeat;
            HeatOfAblation = heatOfAblation;
            AblationTemperature = ablationTemperature;
            Emissivity = Math.Max(0.0, Math.Min(1.0, emissivity));
            HeatTransferCoefficient = heatTransferCoefficient;
            Strength = strength;
        }

        public bool IsIronLike => Density >= IronDensityThreshold;

        /// <summary>
        /// Empirical strength-density relation of Collins, Melosh and Marcus (2005), Eq. 9:
        /// log10 Y_i = 2.107 + 0.0624 sqrt(rho_i), valid for 1000-8000 kg/m^3.
        /// </summary>
        public static double EiepStrength(double density) => Math.Pow(10.0, 2.107 + 0.0624 * Math.Sqrt(Math.Max(0.0, density)));

        /// <summary>Emissivity from the shape-complexity factor: 0.7 + 0.3 SCF, clamped to [0, 1].</summary>
        public static double EmissivityFromScf(double scf) => Math.Max(0.0, Math.Min(1.0, 0.7 + 0.3 * scf));

        /// <summary>
        /// Material built from the user's inputs. Stony defaults: Q* = 8 MJ/kg, T_abl = 2000 K. Iron defaults
        /// (density at least 6000 kg/m^3): Q* = 6 MJ/kg, T_abl = 1811 K (melting point of iron). Lambda = 0.1 for both
        /// (with Cd near 1 this gives an ablation coefficient sigma = Lambda/(Cd Q*) of about 0.0125 s^2/km^2,
        /// typical of ordinary-chondrite fireballs).
        /// </summary>
        public static MeteorMaterial CreateCustom(double density, double specificHeat, double scf)
        {
            bool iron = density >= IronDensityThreshold;
            return new MeteorMaterial(
                iron ? "Custom (iron defaults)" : "Custom (stony defaults)",
                density,
                specificHeat,
                iron ? 6.0e6 : 8.0e6,
                iron ? 1811.0 : 2000.0,
                EmissivityFromScf(scf),
                0.1,
                EiepStrength(density));
        }
    }
}
