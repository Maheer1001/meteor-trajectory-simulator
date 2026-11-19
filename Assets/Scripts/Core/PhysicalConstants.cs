namespace MTS.Core
{
    /// <summary>Physical constants and Earth model parameters (SI units).</summary>
    public static class PhysicalConstants
    {
        /// <summary>Earth's gravitational parameter G*M (m^3 s^-2), WGS-84 value.</summary>
        public const double EarthMu = 3.986004418e14;

        /// <summary>Mean Earth radius (m). Altitude is measured above a sphere of this radius.</summary>
        public const double EarthRadius = 6371000.0;

        /// <summary>Standard gravity g0 (m s^-2), used by USSA76 and crater scaling.</summary>
        public const double StandardGravity = 9.80665;

        /// <summary>Earth's sidereal rotation rate (rad/s).</summary>
        public const double EarthRotationRate = 7.2921159e-5;

        /// <summary>Stefan-Boltzmann constant sigma (W m^-2 K^-4).</summary>
        public const double StefanBoltzmann = 5.670374419e-8;

        /// <summary>Solar constant at 1 AU (W m^-2).</summary>
        public const double SolarConstant = 1361.0;

        /// <summary>Cosmic microwave background temperature (K).</summary>
        public const double CmbTemperature = 2.725;

        /// <summary>Energy of one kiloton of TNT (J).</summary>
        public const double JoulesPerKilotonTnt = 4.184e12;

        /// <summary>Altitude (m) at which the simulation hands over from the space stage to the atmosphere stage.</summary>
        public const double AtmosphereEntryAltitude = 120000.0;
    }
}
