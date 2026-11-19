using System;
using MTS.Core;
using UnityEngine;

namespace MTS.Visuals
{
    /// <summary>Layers used by the two-camera stack (see ProjectSettings/TagManager).</summary>
    public static class VisualLayers
    {
        public const int Far = 8;   // "VisualsFar": Earth, sky, ground, clouds, paths (far camera)
        public const int Near = 9;  // "VisualsNear": meteor, effects, post volume (near camera)
    }

    /// <summary>Shader globals shared by the MTS shaders (MTSCommon.cginc / MTSLocalFrame.cginc).</summary>
    public static class VisualsGlobals
    {
        private static readonly int SunDir = Shader.PropertyToID("_MTS_SunDir");
        private static readonly int SunColor = Shader.PropertyToID("_MTS_SunColor");
        private static readonly int EarthRotation = Shader.PropertyToID("_MTS_EarthRotation");
        private static readonly int CloudRotation = Shader.PropertyToID("_MTS_CloudRotation");
        private static readonly int Flash = Shader.PropertyToID("_MTS_Flash");
        private static readonly int FlashPos = Shader.PropertyToID("_MTS_FlashPos");
        private static readonly int FrameAnchor = Shader.PropertyToID("_MTS_FrameAnchor");
        private static readonly int FrameEast = Shader.PropertyToID("_MTS_FrameEast");
        private static readonly int FrameUp = Shader.PropertyToID("_MTS_FrameUp");
        private static readonly int FrameNorth = Shader.PropertyToID("_MTS_FrameNorth");
        private static readonly int CamAlt = Shader.PropertyToID("_MTS_CamAlt");

        public static void SetSun(Vector3 directionTowardsSun, Color color, float intensity)
        {
            Shader.SetGlobalVector(SunDir, directionTowardsSun.normalized);
            Shader.SetGlobalVector(SunColor, new Vector4(color.r, color.g, color.b, intensity));
        }

        /// <summary>Earth rotation for a simulation time (s); clouds drift a little faster than the ground.</summary>
        public static void SetEarthRotation(double simTime)
        {
            double angle = PhysicalConstants.EarthRotationRate * simTime;
            Shader.SetGlobalFloat(EarthRotation, (float)(angle % (2 * Math.PI)));
            Shader.SetGlobalFloat(CloudRotation, (float)((angle * 0.04 + 0.35) % (2 * Math.PI)));
        }

        public static void SetFlash(Color colorTimesIntensity, Vector3 position, float radius)
        {
            Shader.SetGlobalVector(Flash, colorTimesIntensity);
            Shader.SetGlobalVector(FlashPos, new Vector4(position.x, position.y, position.z, radius));
        }

        public static void SetFrame(LocalFrame frame)
        {
            Shader.SetGlobalVector(FrameAnchor, ToVector4(frame.Anchor));
            Shader.SetGlobalVector(FrameEast, ToVector4(frame.East));
            Shader.SetGlobalVector(FrameUp, ToVector4(frame.Up));
            Shader.SetGlobalVector(FrameNorth, ToVector4(frame.North));
        }

        public static void SetCameraAltitude(float metres) => Shader.SetGlobalFloat(CamAlt, metres);

        public static Vector3 ToVector3(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        private static Vector4 ToVector4(Vec3d v) => new Vector4((float)v.X, (float)v.Y, (float)v.Z, 0);

        /// <summary>Black-body colour as a Unity colour (gamma space).</summary>
        public static Color BlackbodyColor(double kelvin)
        {
            Blackbody.Rgb(kelvin, out double r, out double g, out double b);
            return new Color((float)r, (float)g, (float)b, 1f);
        }
    }

    /// <summary>
    /// Per-run cache shared by the space and atmosphere visuals: the Sun direction is chosen once per run (from the
    /// predicted end point) so both scenes light the same Earth the same way.
    /// </summary>
    public static class VisualsRunCache
    {
        private static WeakReference<SimulationRunner> owner;
        private static Vec3d sunInertial;

        /// <summary>Sun elevation (deg) at the predicted end point and its azimuth (deg from north towards east).</summary>
        public const double SunElevation = 24.0;
        public const double SunAzimuth = 235.0;

        public static bool TryGetSun(SimulationRunner runner, out Vec3d sun)
        {
            sun = sunInertial;
            return owner != null && owner.TryGetTarget(out SimulationRunner r) && ReferenceEquals(r, runner);
        }

        /// <summary>Picks (and caches) the Sun direction for a run, given its predicted end position (inertial).</summary>
        public static Vec3d SunFor(SimulationRunner runner, Vec3d predictedEnd)
        {
            if (TryGetSun(runner, out Vec3d cached))
            {
                return cached;
            }

            var frame = new LocalFrame(predictedEnd);
            double el = SunElevation * Math.PI / 180.0;
            double az = SunAzimuth * Math.PI / 180.0;
            Vec3d horizontal = frame.North * Math.Cos(az) + frame.East * Math.Sin(az);
            sunInertial = (frame.Up * Math.Sin(el) + horizontal * Math.Cos(el)).Normalized;
            owner = new WeakReference<SimulationRunner>(runner);
            return sunInertial;
        }
    }

    /// <summary>Starts a run when a scene is opened without one (editor preview / screenshots), mirroring the adapters.</summary>
    public static class VisualsPreview
    {
        public static SimulationRunner EnsureRun()
        {
            SimulationSession session = SimulationSession.Instance;
            if (session.Runner == null)
            {
                session.Begin(MeteorProperties.FromInputs(60000, 7800, 800, 0.5, 1, 1, 1));
            }

            return session.Runner;
        }
    }
}
