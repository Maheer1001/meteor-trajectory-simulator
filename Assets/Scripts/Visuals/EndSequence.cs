using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using MTS.Core;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace MTS.Visuals
{
    /// <summary>
    /// Short cinematic when the run ends in the atmosphere scene (impact, airburst or ablation): slow motion, a bloom
    /// flash, blast rings that expand to the real overpressure radii from <see cref="SimulationSession.Impact"/>,
    /// a dust plume, a crater decal (ground impacts only) and a Cinemachine impulse shake.
    ///
    /// Integration hook: the atmosphere adapter loads the results scene as soon as the run ends. To let the
    /// sequence play, the adapter should call <see cref="HoldSceneChange"/> before loading and skip the load while it
    /// returns true. It starts the sequence on the first call and returns false once it has finished (or when no
    /// sequence is present in the scene), so it is safe to call every physics tick.
    /// </summary>
    public sealed class EndSequence : MonoBehaviour
    {
        /// <summary>Real-time length of the sequence (s).</summary>
        public const float Duration = 6.5f;
        /// <summary>Time scale while the sequence plays.</summary>
        public const float SlowMotion = 0.35f;

        private static EndSequence active;

        /// <summary>True while the sequence is playing.</summary>
        public static bool IsPlaying => active != null && active.playing;

        /// <summary>True once the sequence has played to the end.</summary>
        public static bool IsFinished => active != null && active.finished;

        /// <summary>
        /// For the adapter: returns true while the results scene should wait. Starts the sequence if the run has ended
        /// and it has not started yet. Returns false when there is no sequence in the scene or it has finished.
        /// </summary>
        public static bool HoldSceneChange()
        {
            if (active == null || !active.isActiveAndEnabled || active.finished)
            {
                return false;
            }

            if (!active.playing)
            {
                active.Play();
            }

            return !active.finished;
        }

        public AtmosphereVisuals visuals;
        public PostProcessVolume flashVolume;
        public CinemachineImpulseSource impulse;

        private bool playing;
        private bool finished;
        private readonly List<Ring> rings = new List<Ring>();
        private Transform crater;
        private Material craterMat;
        private ParticleSystem plume;
        private ParticleSystem groundDust;
        private Transform flashGlow;
        private Material flashGlowMat;

        private struct Ring
        {
            public Transform Transform;
            public Material Material;
            public float Duration;
            public float Strength;
        }

        private void OnEnable() => active = this;

        private void OnDisable()
        {
            if (active == this)
            {
                if (playing && !finished)
                {
                    Time.timeScale = SimulationSession.Instance.IsPaused ? 0f : 1f;
                }

                active = null;
            }
        }

        /// <summary>Starts the sequence (idempotent).</summary>
        public void Play()
        {
            if (playing || finished || visuals == null)
            {
                return;
            }

            playing = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            SimulationSession session = SimulationSession.Instance;
            SimulationResult result = session.Result;
            ImpactAssessment impact = session.Impact;
            EndReason reason = result != null ? result.Reason : EndReason.Disintegrated;

            Vector3 endPoint = visuals.MeteorWorldPosition;
            Vector3 groundZero = visuals.GroundPointBelow(endPoint);
            double kt = impact != null ? impact.EnergyKilotons : 0.0;
            float energyScale = Mathf.Clamp01((float)(System.Math.Log10(kt + 1e-3) + 3.0) / 7.0f); // 1 t .. 10 Mt
            bool groundEffects = impact != null && impact.Regime != ImpactRegime.None;

            float viewRadius = 3000f;
            if (impact != null && impact.BlastRadii != null)
            {
                // frame the 3 psi ring with room to spare, without letting the faint outer rings push the camera away
                if (impact.BlastRadii.Length > 4) viewRadius = Mathf.Max(viewRadius, (float)impact.BlastRadii[4] * 1.4f);
                if (impact.BlastRadii.Length > 2) viewRadius = Mathf.Max(viewRadius, (float)impact.BlastRadii[2] * 0.45f);
            }

            visuals.OnRunEnded(reason, reason == EndReason.Airburst ? endPoint : groundZero + Vector3.up * 800f, viewRadius);
            SimulationSignals.RaiseToast(Describe(reason, impact));

            Time.timeScale = SlowMotion;
            Vector3 flashAt = reason == EndReason.Airburst || !groundEffects ? endPoint : groundZero;
            BuildFlash(flashAt, energyScale);
            if (groundEffects)
            {
                BuildRings(groundZero, impact);
                // dust only where the blast actually reaches the ground hard: impacts and low airbursts
                if (reason == EndReason.Impact || impact.BurstAltitude < 8000)
                {
                    BuildPlume(groundZero, impact, energyScale, reason == EndReason.Impact, reason == EndReason.Impact);
                }
                if (reason == EndReason.Impact && impact.FinalCraterDiameter > 0)
                {
                    BuildCrater(groundZero, (float)impact.FinalCraterRadius);
                }
            }

            if (impulse != null)
            {
                float distance = Vector3.Distance(visuals.CameraPosition, groundZero);
                float attenuation = Mathf.Clamp01(1.5f - distance / 80000f);
                impulse.GenerateImpulseWithForce(Mathf.Lerp(0.3f, 3f, energyScale) * Mathf.Max(0.25f, attenuation));
            }

            float t = 0f;
            float flashRadius = Mathf.Lerp(3000f, 60000f, energyScale);
            while (t < Duration)
            {
                // real time, or the fixed capture step when recording (Time.captureFramerate)
                float dt = Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Time.unscaledDeltaTime;
                t += dt;

                // flash: very fast rise, slow decay
                float f = t < 0.08f ? t / 0.08f : Mathf.Exp(-(t - 0.08f) * 2.6f);
                float flashI = f * Mathf.Lerp(0.8f, 3f, energyScale);
                VisualsGlobals.SetFlash(new Color(1f, 0.88f, 0.72f) * flashI, flashAt, flashRadius);
                if (flashVolume != null) flashVolume.weight = Mathf.Clamp01(f * 0.9f);
                if (flashGlow != null)
                {
                    float size = flashRadius * 0.35f * (0.3f + 0.7f * Mathf.Sqrt(Mathf.Clamp01(t * 2f))) * (groundEffects ? 1f : 0.4f);
                    size = Mathf.Min(size, Vector3.Distance(visuals.CameraPosition, flashAt) * 0.6f); // never swallow the view
                    flashGlow.localScale = Vector3.one * size;
                    flashGlowMat.SetFloat("_Intensity", f * 2.5f);
                }

                foreach (Ring ring in rings)
                {
                    float k = Mathf.Clamp01(t / ring.Duration);
                    float front = 1f - (1f - k) * (1f - k); // decelerating front
                    ring.Material.SetFloat("_Radius", front);
                    ring.Material.SetFloat("_Intensity", ring.Strength * (1f - k * 0.85f) * Mathf.Clamp01(t * 6f));
                }

                if (craterMat != null)
                {
                    craterMat.SetFloat("_Reveal", Mathf.Clamp01(t * 1.5f));
                    craterMat.SetFloat("_Heat", Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(t / Duration)));
                }

                if (t > 3.2f && Time.timeScale < 1f)
                {
                    Time.timeScale = Mathf.MoveTowards(Time.timeScale, 1f, dt * 0.5f);
                }

                yield return null;
            }

            VisualsGlobals.SetFlash(Color.black, flashAt, 1f);
            if (flashVolume != null) flashVolume.weight = 0f;
            Time.timeScale = session.IsPaused ? 0f : 1f;
            playing = false;
            finished = true;
        }

        private static string Describe(EndReason reason, ImpactAssessment impact)
        {
            string energy = impact != null && impact.EnergyKilotons > 0
                ? (impact.EnergyKilotons >= 1 ? impact.EnergyKilotons.ToString("0.#") + " kt" : (impact.EnergyKilotons * 1000).ToString("0") + " t") + " TNT"
                : "";
            switch (reason)
            {
                case EndReason.Impact: return "Impact" + (energy.Length > 0 ? " - " + energy : "");
                case EndReason.Airburst: return "Airburst at " + (impact.BurstAltitude / 1000.0).ToString("0.0") + " km" + (energy.Length > 0 ? " - " + energy : "");
                case EndReason.Disintegrated: return "Ablated away";
                default: return "Run ended";
            }
        }

        private void BuildFlash(Vector3 at, float energyScale)
        {
            var go = new GameObject("End flash") { layer = VisualLayers.Near };
            go.transform.position = at;
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Quad();
            flashGlowMat = new Material(visuals.library.glow);
            flashGlowMat.SetColor("_Color", new Color(1f, 0.85f, 0.65f));
            flashGlowMat.SetFloat("_Halo", 2.2f);
            flashGlowMat.SetFloat("_Core", 8f);
            go.AddComponent<MeshRenderer>().sharedMaterial = flashGlowMat;
            flashGlow = go.transform;
        }

        private void BuildRings(Vector3 groundZero, ImpactAssessment impact)
        {
            int[] levels = { 0, 2, 4, 6, 8 };
            double rmax = 1;
            foreach (int i in levels)
            {
                if (i < impact.BlastRadii.Length) rmax = System.Math.Max(rmax, impact.BlastRadii[i]);
            }

            Mesh disc = ProceduralMeshes.Disc(128, 32);
            foreach (int i in levels)
            {
                if (i >= impact.BlastRadii.Length) continue;
                double radius = impact.BlastRadii[i];
                if (!(radius > 30.0)) continue;

                var go = new GameObject("Blast ring " + (impact.OverpressureLevels[i] / 6894.76).ToString("0.##") + " psi") { layer = VisualLayers.Near };
                go.transform.position = groundZero + Vector3.up * 15f;
                go.transform.localScale = Vector3.one * (float)radius;
                go.AddComponent<MeshFilter>().sharedMesh = disc;
                var mat = new Material(visuals.library.shockwave);
                float strength = Mathf.Lerp(0.35f, 1.2f, (float)i / 8f);
                mat.SetFloat("_Width", Mathf.Clamp(0.02f * (float)(rmax / radius), 0.015f, 0.12f));
                mat.SetColor("_Color", Color.Lerp(new Color(0.7f, 0.85f, 1f), new Color(1f, 0.7f, 0.4f), (float)i / 8f) * 1.5f);
                mat.SetFloat("_Dust", Mathf.Lerp(0.15f, 0.65f, (float)i / 8f));
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                float duration = Mathf.Lerp(1.2f, Duration * 0.85f, Mathf.Sqrt((float)(radius / rmax)));
                rings.Add(new Ring { Transform = go.transform, Material = mat, Duration = duration, Strength = strength });
            }
        }

        private void BuildPlume(Vector3 groundZero, ImpactAssessment impact, float energyScale, bool groundImpact, bool column)
        {
            double r = 0;
            foreach (double b in impact.BlastRadii) r = System.Math.Max(r, b);
            // plume size follows the crater (or the blast for low airbursts), kept well inside the end camera's view
            float scale = Mathf.Clamp((float)(groundImpact ? impact.FinalCraterDiameter * 1.5 : r * 0.02), 120f, 3000f);

            var go = new GameObject("Dust plume") { layer = VisualLayers.Near };
            go.transform.position = groundZero;
            plume = go.AddComponent<ParticleSystem>();
            plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = plume.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.2f, scale * 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.2f, scale * 0.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.48f, 0.42f, 0.9f), new Color(0.42f, 0.36f, 0.32f, 0.9f));
            main.maxParticles = 600;
            main.gravityModifier = -0.02f;
            var emission = plume.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 160), new ParticleSystem.Burst(0.4f, 120), new ParticleSystem.Burst(1.0f, 80) });
            var shape = plume.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = groundImpact ? 28f : 50f;
            shape.radius = scale * 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var limit = plume.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.12f;
            limit.limit = scale * 0.05f;
            var size = plume.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(0.3f, 1.2f), new Keyframe(1, 2.4f)));
            var col = plume.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.6f, 0.3f), 0f), new GradientColorKey(new Color(0.75f, 0.68f, 0.6f), 0.15f), new GradientColorKey(new Color(0.6f, 0.56f, 0.52f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = visuals.library.dust;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 3f;
            if (column) plume.Play(); else Destroy(go);

            // radial ground-hugging dust ring
            var go2 = new GameObject("Ground dust") { layer = VisualLayers.Near };
            go2.transform.position = groundZero + Vector3.up * scale * 0.1f;
            groundDust = go2.AddComponent<ParticleSystem>();
            groundDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m2 = groundDust.main;
            m2.simulationSpace = ParticleSystemSimulationSpace.World;
            m2.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            m2.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.5f, scale * 1.1f);
            m2.startSize = new ParticleSystem.MinMaxCurve(scale * 0.2f, scale * 0.4f);
            m2.startColor = new Color(0.55f, 0.47f, 0.4f, 0.6f);
            m2.maxParticles = 400;
            var e2 = groundDust.emission;
            e2.rateOverTime = 0;
            e2.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 220) });
            var s2 = groundDust.shape;
            s2.shapeType = ParticleSystemShapeType.Circle;
            s2.radius = scale * 0.2f;
            s2.rotation = new Vector3(-90f, 0f, 0f);
            var l2 = groundDust.limitVelocityOverLifetime;
            l2.enabled = true;
            l2.dampen = 0.2f;
            l2.limit = 0f;
            var sz2 = groundDust.sizeOverLifetime;
            sz2.enabled = true;
            sz2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2f));
            var c2 = groundDust.colorOverLifetime;
            c2.enabled = true;
            var g2 = new Gradient();
            g2.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 0.7f, 0.6f), 0f), new GradientColorKey(new Color(0.65f, 0.6f, 0.55f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.1f), new GradientAlphaKey(0f, 1f) });
            c2.color = g2;
            var r2 = go2.GetComponent<ParticleSystemRenderer>();
            r2.sharedMaterial = visuals.library.dust;
            r2.sortMode = ParticleSystemSortMode.Distance;
            r2.maxParticleSize = 3f;
            groundDust.Play();
        }

        private void BuildCrater(Vector3 groundZero, float radius)
        {
            var go = new GameObject("Crater") { layer = VisualLayers.Near };
            go.transform.position = groundZero + Vector3.up * 3f;
            go.transform.localScale = Vector3.one * radius * 2.5f;
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Disc(96, 24);
            craterMat = new Material(visuals.library.crater);
            go.AddComponent<MeshRenderer>().sharedMaterial = craterMat;
            crater = go.transform;
        }
    }
}
