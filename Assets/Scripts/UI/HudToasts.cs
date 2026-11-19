using System.Collections.Generic;
using MTS.Core;
using MTS.Core.Display;
using UnityEngine;

namespace MTS.Hud
{
    /// <summary>One toast message (title + detail) posted at a real time.</summary>
    public readonly struct HudToast
    {
        public readonly string Title;
        public readonly string Message;
        public readonly Color Accent;
        public readonly float PostedAt;

        public HudToast(string title, string message, Color accent, float postedAt)
        {
            Title = title;
            Message = message;
            Accent = accent;
            PostedAt = postedAt;
        }
    }

    /// <summary>
    /// Scene-independent toast queue. Events are posted here (flight events, <see cref="SimulationSignals.Toast"/>)
    /// and every HUD shows the recent ones, so a toast posted just before a scene change still appears afterwards.
    /// </summary>
    public static class HudToasts
    {
        public const float Lifetime = 5f;
        private const int MaxKept = 8;

        private static readonly List<HudToast> recent = new List<HudToast>();

        public static IReadOnlyList<HudToast> Recent => recent;

        /// <summary>Incremented on every post so views can tell when to rebuild.</summary>
        public static int Version { get; private set; }

        public static void Post(string title, string message, Color accent)
        {
            recent.Add(new HudToast(title, message ?? "", accent, Time.unscaledTime));
            if (recent.Count > MaxKept)
            {
                recent.RemoveAt(0);
            }

            Version++;
        }

        /// <summary>Message from <see cref="SimulationSignals.Toast"/>; neutral accent.</summary>
        public static void PostSignal(string message) => Post("NOTICE", message, HudTheme.Info);

        public static void Clear()
        {
            recent.Clear();
            Version++;
        }
    }

    /// <summary>
    /// Per-run HUD state that must survive the Space -> Atmosphere scene change: the flight-event detector and
    /// the run's start altitude. Reset automatically when the session starts a new run.
    /// </summary>
    public static class HudRunTracker
    {
        private static SimulationRunner trackedRunner;
        private static FlightEventDetector detector;

        public static double StartAltitude { get; private set; }
        public static double StartMass { get; private set; }

        /// <summary>Feeds the current telemetry to the detector and posts toasts for new events.</summary>
        public static void Track(SimulationRunner runner)
        {
            if (runner == null)
            {
                return;
            }

            if (!ReferenceEquals(runner, trackedRunner))
            {
                trackedRunner = runner;
                detector = new FlightEventDetector();
                StartAltitude = Geo.Altitude(runner.Properties.StartPosition);
                StartMass = runner.Properties.Mass;
            }

            foreach ((FlightEvent e, string message) in detector.Update(runner.Telemetry, runner.Result))
            {
                HudToasts.Post(FlightEventDetector.Title(e), message, HudTheme.AccentFor(e));
            }
        }
    }
}
