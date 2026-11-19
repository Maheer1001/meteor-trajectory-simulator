using MTS.Core;

namespace MTS.Hud
{
    /// <summary>
    /// Bridge between the HUD and the scene adapters (<c>Meteor_Force</c>, <c>Atmosphere_Force</c>).
    /// While a HUD is attached it owns the time warp, the launch hold and the wind; the adapters then read the
    /// warp from here instead of their legacy sliders and report each advance back. Without a HUD (flags off)
    /// the adapters behave exactly as before. Static so it needs no scene references; the HUD clears it on destroy.
    /// </summary>
    public static class SimulationClock
    {
        /// <summary>True while a HUD controls the time warp (the legacy time slider is then ignored).</summary>
        public static bool ExternalControl { get; private set; }

        /// <summary>True while a HUD controls the wind (the legacy wind sliders are then ignored).</summary>
        public static bool ExternalWind { get; set; }

        /// <summary>Simulated seconds the adapters request per physics tick while <see cref="ExternalControl"/> is on.</summary>
        public static double SimSecondsPerTick { get; private set; }

        /// <summary>Pre-launch hold: the run exists but the adapters must not advance it yet.</summary>
        public static bool HoldForLaunch { get; set; }

        /// <summary>Most recent advance (steps taken, whether the per-tick step cap was hit).</summary>
        public static AdvanceReport LastReport { get; private set; }

        /// <summary>Number of physics ticks in a row that hit the step cap (0 when the warp is keeping up).</summary>
        public static int CappedTicks { get; private set; }

        public static void TakeControl(double simSecondsPerTick)
        {
            ExternalControl = true;
            SimSecondsPerTick = simSecondsPerTick;
        }

        public static void Release()
        {
            ExternalControl = false;
            ExternalWind = false;
            HoldForLaunch = false;
            CappedTicks = 0;
        }

        /// <summary>Called by the adapters after every advance.</summary>
        public static void Report(AdvanceReport report)
        {
            LastReport = report;
            CappedTicks = report.Capped ? CappedTicks + 1 : 0;
        }

        /// <summary>Simulated seconds for this tick: the HUD's warp when it is in control, else the legacy value.</summary>
        public static double TickSeconds(double legacySimSeconds) => ExternalControl ? SimSecondsPerTick : legacySimSeconds;
    }
}
