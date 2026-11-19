using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using MTS.Core;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace MTS.Visuals
{
    /// <summary>
    /// Cinematic when the run ends in the atmosphere scene (impact, airburst or ablation), scaled by the real event:
    /// a flash (blinding for big events), the fireball growing to its real radius (R ~ 0.002 E^1/3) and rising into a
    /// mushroom cloud, the ejecta curtain and the crater at <see cref="ImpactAssessment.FinalCraterDiameter"/>, blast
    /// rings expanding to the real overpressure radii (thousands of km for Chicxulub, drawn on the curved Earth), a
    /// flattened-forest scar under airbursts, dust and ash spreading and darkening the sky, and a camera shake. The
    /// camera starts close to the event and pulls back to its full extent, up to an orbital view.
    ///
    /// Integration hook: the atmosphere adapter calls <see cref="HoldSceneChange"/> before loading the results scene
    /// and skips the load while it returns true. It starts the sequence on the first call and returns false once it
    /// has finished (or when no sequence is present in the scene), so it is safe to call every physics tick.
    /// </summary>
    public sealed class EndSequence : MonoBehaviour
    {
        /// <summary>Longest real-time length of the sequence (s); small events are shorter.</summary>
        public const float Duration = 9f;
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
            if (active == null || !active.isActiveAndEnabled || active.finished || active.visuals == null)
            {
                return false;
            }

            float now = Time.unscaledTime;
            if (active.holdSince < 0f)
            {
                active.holdSince = now;
            }

            // Failsafe: never hold the results scene longer than the sequence (plus slack), even if it threw.
            if (now - active.holdSince > ReadyTimeout + Duration + 2f)
            {
                active.finished = true;
                Time.timeScale = SimulationSession.Instance.IsPaused ? 0f : 1f;
                Debug.LogWarning("End sequence did not finish in time; continuing to the results.");
                return false;
            }

            if (!active.playing)
            {
                // The run can end in the scene's first frames (high time warp) before the rig is built: wait briefly.
                if (!active.visuals.IsReady)
                {
                    return now - active.holdSince < ReadyTimeout;
                }

                active.Play();
            }

            return !active.finished;
        }

        /// <summary>Longest real time (s) to wait for the visuals rig before skipping the sequence.</summary>
        public const float ReadyTimeout = 1f;

        private float holdSince = -1f;

        public AtmosphereVisuals visuals;
        public PostProcessVolume flashVolume;
        public CinemachineImpulseSource impulse;

        private bool playing;
        private bool finished;
        private readonly List<Ring> rings = new List<Ring>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private Material craterMat;
        private Transform fireball;
        private Material fireballMat;
        private Transform curtain;
        private Material curtainMat;
        private Material scarMat;
        private Material ashMat;
        private Transform flashGlow;
        private Material flashGlowMat;

        private struct Ring
        {
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

                VisualsGlobals.SetAsh(0f);
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
            Vector3 upAtZero = (groundZero - EarthCentre).normalized;
            double kt = impact != null ? impact.EnergyKilotons : 0.0;
            double joules = impact != null ? impact.Energy : 0.0;
            float severity = ImpactVisualScale.Severity(kt);
            bool groundEffects = impact != null && impact.Regime != ImpactRegime.None;
            bool groundImpact = reason == EndReason.Impact && groundEffects;
            bool airburst = reason == EndReason.Airburst;
            float fireR = (float)ImpactVisualScale.FireballRadius(joules);
            float craterR = impact != null && groundImpact ? (float)impact.FinalCraterRadius : 0f;
            float length = Mathf.Lerp(6f, Duration, Mathf.InverseLerp(0.25f, 0.85f, severity));

            // what to frame: the event itself first, then everything it reaches
            float nearR = groundImpact ? Mathf.Max(fireR, craterR * 1.8f, 60f) : Mathf.Max(fireR * 1.6f, visuals.Meteor != null ? visuals.Meteor.SheathRadius * 12f : 0f, 120f);
            float farR = nearR * 4f;
            if (groundEffects && impact.BlastRadii != null)
            {
                if (impact.BlastRadii.Length > 3) farR = Mathf.Max(farR, (float)impact.BlastRadii[3] * 1.1f);
                if (impact.BlastRadii.Length > 2) farR = Mathf.Max(farR, (float)impact.BlastRadii[2] * 0.6f);
            }

            farR = Mathf.Min(farR, 2.6e6f);
            Vector3 focus = airburst || !groundEffects ? endPoint : groundZero + upAtZero * Mathf.Min(fireR * 0.3f, 2000f);
            // (no toast here: the HUD announces the airburst / impact itself)
            visuals.OnRunEnded(reason, focus, nearR, farR);

            Time.timeScale = SlowMotion;
            Vector3 flashAt = airburst || !groundEffects ? endPoint : groundZero;
            BuildFlash(flashAt);
            if (fireR > 1f && (groundEffects || kt > 0.01))
            {
                BuildFireball(flashAt, severity);
            }

            if (groundEffects)
            {
                BuildRings(groundZero, upAtZero, impact);
                if (groundImpact || impact.BurstAltitude < 12000)
                {
                    BuildPlume(groundZero, upAtZero, Mathf.Max(groundImpact ? craterR * 1.5f : 0f, fireR * 0.8f), groundImpact);
                }

                if (groundImpact && craterR > 0f)
                {
                    BuildCrater(groundZero, upAtZero, craterR);
                    BuildCurtain(groundZero, upAtZero, craterR);
                }

                double scarR = 0;
                if (impact.BlastRadii.Length > 4) scarR = impact.BlastRadii[airburst ? 3 : 5];
                if (scarR > 200)
                {
                    BuildScar(groundZero, upAtZero, (float)scarR, airburst ? 1f : 0.35f);
                }

                if (severity > 0.45f)
                {
                    BuildAsh(groundZero, upAtZero, Mathf.Min(farR * 1.3f, 3.0e6f), Mathf.Max(3000f, Mathf.Min(fireR * 0.25f, 30000f)));
                }
            }

            float camDistance = Vector3.Distance(visuals.CameraPosition, flashAt);
            if (impulse != null)
            {
                // the shake is in metres: scale it with the viewing distance so it reads at every scale
                float strength = Mathf.Lerp(0.25f, 1.6f, severity) * Mathf.Max(1f, camDistance * 0.0035f);
                impulse.GenerateImpulseWithForce(strength);
            }

            float t = 0f;
            float flashRadius = Mathf.Max(3000f, fireR * 25f, nearR * 6f);
            float ashTarget = groundEffects ? Mathf.Clamp01((severity - 0.45f) / 0.4f) * 0.85f : 0f;
            float blinding = Mathf.InverseLerp(0.3f, 0.7f, severity);
            bool secondShake = false;
            while (t < length)
            {
                // real time, or the fixed capture step when recording (Time.captureFramerate)
                float dt = Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Time.unscaledDeltaTime;
                t += dt;
                float k = t / length;
                visuals.SetEndProgress(Mathf.InverseLerp(0.12f, 0.85f, k));

                // flash: very fast rise, slow decay; big events white the screen out
                float f = t < 0.08f ? t / 0.08f : Mathf.Exp(-(t - 0.08f) * Mathf.Lerp(2.6f, 1.1f, blinding));
                float flashI = f * Mathf.Lerp(0.8f, 6f, severity);
                VisualsGlobals.SetFlash(new Color(1f, 0.88f, 0.72f) * flashI, flashAt, flashRadius);
                if (flashVolume != null) flashVolume.weight = Mathf.Clamp01(f * Mathf.Lerp(0.9f, 1.6f, blinding));
                if (flashGlow != null)
                {
                    float viewDist = Vector3.Distance(visuals.CameraPosition, flashAt);
                    float size = Mathf.Max(fireR * 5f, viewDist * Mathf.Lerp(0.15f, 2.5f, blinding)) * (0.3f + 0.7f * Mathf.Sqrt(Mathf.Clamp01(t * 2f)));
                    size = Mathf.Min(size, viewDist * Mathf.Lerp(0.6f, 3f, blinding));
                    flashGlow.localScale = Vector3.one * size;
                    flashGlowMat.SetFloat("_Intensity", f * Mathf.Lerp(2.5f, 5f, blinding));
                }

                if (fireball != null) UpdateFireball(t, length, fireR, groundImpact, severity);

                foreach (Ring ring in rings)
                {
                    float rk = Mathf.Clamp01(t / ring.Duration);
                    float front = 1f - (1f - rk) * (1f - rk); // decelerating front
                    ring.Material.SetFloat("_Radius", front);
                    ring.Material.SetFloat("_Intensity", ring.Strength * (1f - rk * 0.8f) * Mathf.Clamp01(t * 6f));
                }

                if (scarMat != null)
                {
                    float sk = Mathf.Clamp01(t / (length * 0.55f));
                    scarMat.SetFloat("_Reveal", 1f - (1f - sk) * (1f - sk));
                    scarMat.SetFloat("_Glow", Mathf.Clamp01(1f - t * 0.6f));
                }

                if (craterMat != null)
                {
                    craterMat.SetFloat("_Reveal", Mathf.Clamp01(t * 1.2f / Mathf.Max(1f, length * 0.3f)));
                    craterMat.SetFloat("_Heat", Mathf.Lerp(1f, 0.2f, k));
                }

                if (curtain != null)
                {
                    float ck = Mathf.Clamp01(t / (length * 0.45f));
                    float grow = 1f - (1f - ck) * (1f - ck);
                    curtain.localScale = new Vector3(craterR * (0.4f + 0.6f * grow), craterR * 1.6f * grow + 1f, craterR * (0.4f + 0.6f * grow));
                    curtainMat.SetFloat("_Opacity", 0.85f * Mathf.Clamp01(1.6f - k * 1.6f));
                    curtainMat.SetFloat("_Heat", Mathf.Clamp01(1f - k * 1.5f));
                }

                if (ashMat != null)
                {
                    float ak = Mathf.Clamp01((t - length * 0.1f) / (length * 0.85f));
                    ashMat.SetFloat("_Radius", Mathf.Lerp(0.05f, 0.92f, 1f - (1f - ak) * (1f - ak)));
                    ashMat.SetFloat("_Opacity", Mathf.Lerp(0.2f, 0.9f, ak));
                }

                visuals.Ash = ashTarget * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.8f, k));

                // the ground shock arrives a moment later for big impacts: a second, heavier jolt
                if (!secondShake && impulse != null && severity > 0.5f && t > 0.9f)
                {
                    secondShake = true;
                    impulse.GenerateImpulseWithForce(Mathf.Lerp(0.5f, 2.5f, severity) * Mathf.Max(1f, Vector3.Distance(visuals.CameraPosition, groundZero) * 0.004f));
                }

                if (t > length * 0.5f && Time.timeScale < 1f)
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

        private static readonly Vector3 EarthCentre = new Vector3(0f, -(float)PhysicalConstants.EarthRadius, 0f);

        private GameObject Spawn(string name, int layer, Mesh mesh, Material material)
        {
            var go = new GameObject(name) { layer = layer };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            spawned.Add(go);
            return go;
        }

        private void BuildFlash(Vector3 at)
        {
            flashGlowMat = new Material(visuals.library.glow);
            flashGlowMat.SetColor("_Color", new Color(1f, 0.85f, 0.65f));
            flashGlowMat.SetFloat("_Halo", 2.2f);
            flashGlowMat.SetFloat("_Core", 8f);
            GameObject go = Spawn("End flash", VisualLayers.Near, ProceduralMeshes.Quad(), flashGlowMat);
            go.transform.position = at;
            flashGlow = go.transform;
        }

        private void BuildFireball(Vector3 at, float severity)
        {
            fireballMat = new Material(visuals.library.fireball);
            fireballMat.SetFloat("_Seed", Random.value * 50f);
            fireballMat.SetFloat("_Intensity", Mathf.Lerp(2f, 4f, severity));
            GameObject go = Spawn("Fireball", VisualLayers.Near, ProceduralMeshes.Icosphere(4), fireballMat);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * 0.01f;
            fireball = go.transform;
            fireballStart = at;
        }

        private Vector3 fireballStart;

        private void UpdateFireball(float t, float length, float fireR, bool groundImpact, float severity)
        {
            // grows fast to its full radius, cools, then (big events) rises and spreads into a mushroom cap
            float grow = 1f - Mathf.Exp(-t / Mathf.Max(0.15f, length * 0.07f));
            float cool = Mathf.Clamp01((t - length * 0.08f) / (length * 0.55f));
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - length * 0.18f) / (length * 0.75f)));
            Vector3 up = (fireballStart - EarthCentre).normalized;
            float riseHeight = fireR * Mathf.Lerp(0.8f, 2.6f, severity) * rise;
            float spread = 1f + rise * Mathf.Lerp(0.2f, 0.9f, severity);
            fireball.position = fireballStart + up * (riseHeight + (groundImpact ? fireR * 0.25f * grow : 0f));
            fireball.rotation = Quaternion.FromToRotation(Vector3.up, up);
            fireball.localScale = new Vector3(fireR * grow * spread, fireR * grow * Mathf.Lerp(1f, 0.62f, rise), fireR * grow * spread);
            fireballMat.SetFloat("_Heat", 1f - cool * 0.95f);
            fireballMat.SetFloat("_Opacity", Mathf.Clamp01(1.4f - Mathf.Clamp01((t - length * 0.75f) / (length * 0.25f)) * 0.6f));
        }

        private void BuildRings(Vector3 groundZero, Vector3 up, ImpactAssessment impact)
        {
            int[] levels = { 0, 2, 4, 6, 8 };
            double rmax = 1;
            foreach (int i in levels)
            {
                if (i < impact.BlastRadii.Length) rmax = System.Math.Max(rmax, impact.BlastRadii[i]);
            }

            foreach (int i in levels)
            {
                if (i >= impact.BlastRadii.Length) continue;
                double radius = System.Math.Min(impact.BlastRadii[i], 6.0e6);
                if (!(radius > 30.0)) continue;

                var mat = new Material(visuals.library.shockwave);
                float strength = Mathf.Lerp(0.35f, 1.2f, (float)i / 8f);
                mat.SetFloat("_Width", Mathf.Clamp(0.02f * (float)(rmax / radius), 0.012f, 0.12f));
                mat.SetColor("_Color", Color.Lerp(new Color(0.7f, 0.85f, 1f), new Color(1f, 0.7f, 0.4f), (float)i / 8f) * 1.5f);
                mat.SetFloat("_Dust", Mathf.Lerp(0.15f, 0.65f, (float)i / 8f));
                // big rings live in the far (km-scale) camera with the ground so they follow its curvature and depth
                float lift = Mathf.Max(15f, (float)radius * 0.0008f);
                GameObject go = Spawn("Blast ring " + (impact.OverpressureLevels[i] / 6894.76).ToString("0.##") + " psi",
                    VisualLayers.Far, ProceduralMeshes.CurvedDisc((float)radius, lift), mat);
                Place(go.transform, groundZero, up);
                float duration = Mathf.Lerp(1.2f, Duration * 0.8f, Mathf.Sqrt((float)(radius / rmax)));
                rings.Add(new Ring { Material = mat, Duration = duration, Strength = strength });
            }
        }

        // Puts a mesh built around the local origin (sphere centre at (0, -R, 0)) at a ground point.
        private static void Place(Transform t, Vector3 groundPoint, Vector3 up)
        {
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, up);
            t.rotation = rot;
            // the mesh's sphere centre must land on the Earth's centre
            t.position = EarthCentre + rot * new Vector3(0f, (float)PhysicalConstants.EarthRadius, 0f) + (groundPoint - (EarthCentre + up * (float)PhysicalConstants.EarthRadius));
        }

        private void BuildScar(Vector3 groundZero, Vector3 up, float radius, float radial)
        {
            scarMat = new Material(visuals.library.groundScar);
            scarMat.SetFloat("_Radial", radial);
            scarMat.SetFloat("_Reveal", 0f);
            GameObject go = Spawn("Ground scar", VisualLayers.Far, ProceduralMeshes.CurvedDisc(radius, Mathf.Max(6f, radius * 0.0004f), 192, 40), scarMat);
            Place(go.transform, groundZero, up);
        }

        private void BuildAsh(Vector3 groundZero, Vector3 up, float radius, float height)
        {
            ashMat = new Material(visuals.library.ashCloud);
            ashMat.SetFloat("_Radius", 0.05f);
            ashMat.SetFloat("_Scale", Mathf.Clamp(radius / 40000f, 4f, 40f));
            GameObject go = Spawn("Ash pall", VisualLayers.Far, ProceduralMeshes.CurvedDisc(radius, height, 160, 40), ashMat);
            Place(go.transform, groundZero, up);
        }

        private void BuildCrater(Vector3 groundZero, Vector3 up, float radius)
        {
            craterMat = new Material(visuals.library.crater);
            GameObject go = Spawn("Crater", VisualLayers.Far, ProceduralMeshes.CurvedDisc(radius * 2.5f, Mathf.Max(3f, radius * 0.002f), 128, 32), craterMat);
            Place(go.transform, groundZero, up);
        }

        private void BuildCurtain(Vector3 groundZero, Vector3 up, float craterR)
        {
            curtainMat = new Material(visuals.library.ejecta);
            curtainMat.SetFloat("_Seed", Random.value * 20f);
            GameObject go = Spawn("Ejecta curtain", VisualLayers.Near, ProceduralMeshes.Frustum(2.4f), curtainMat);
            go.transform.position = groundZero;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
            go.transform.localScale = new Vector3(craterR * 0.4f, 1f, craterR * 0.4f);
            curtain = go.transform;
        }

        private void BuildPlume(Vector3 groundZero, Vector3 up, float scale, bool column)
        {
            scale = Mathf.Clamp(scale, 120f, 400000f);
            Quaternion toUp = Quaternion.FromToRotation(Vector3.up, up);

            if (column)
            {
                var go = new GameObject("Dust column") { layer = VisualLayers.Near };
                spawned.Add(go);
                go.transform.position = groundZero;
                go.transform.rotation = toUp;
                ParticleSystem plume = go.AddComponent<ParticleSystem>();
                plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = plume.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.2f, scale * 0.55f);
                main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.2f, scale * 0.4f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.48f, 0.42f, 0.9f), new Color(0.42f, 0.36f, 0.32f, 0.9f));
                main.maxParticles = 600;
                main.gravityModifier = 0f;
                var emission = plume.emission;
                emission.rateOverTime = 0;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 160), new ParticleSystem.Burst(0.4f, 120), new ParticleSystem.Burst(1.0f, 80) });
                var shape = plume.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 24f;
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
                plume.Play();
            }

            // radial ground-hugging dust ring
            var go2 = new GameObject("Ground dust") { layer = VisualLayers.Near };
            spawned.Add(go2);
            go2.transform.position = groundZero + up * scale * 0.1f;
            go2.transform.rotation = toUp;
            ParticleSystem groundDust = go2.AddComponent<ParticleSystem>();
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
    }
}
