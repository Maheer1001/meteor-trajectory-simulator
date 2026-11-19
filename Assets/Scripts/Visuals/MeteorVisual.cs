using System.Collections.Generic;
using MTS.Core;
using UnityEngine;

namespace MTS.Visuals
{
    /// <summary>
    /// The meteoroid and everything it gives off, driven only by read-only flight telemetry:
    /// procedural rock (black-body heat glow from Temperature), plasma sheath (Luminosity / dynamic pressure),
    /// fireball halo, ionisation trail, ablation sparks (MassLossRate), dust train, point light, and break-up
    /// fragments that spread with the pancake diameter. Built entirely at runtime from a <see cref="VisualsLibrary"/>.
    /// </summary>
    public sealed class MeteorVisual : MonoBehaviour
    {
        public float Radius { get; private set; } = 1f;
        public float Brightness { get; private set; }
        public Color HeatColor { get; private set; } = Color.black;
        public bool HasBrokenUp => fragments.Count > 0;
        /// <summary>Radius (m) of the glowing body: the rock, or the fragment cloud after break-up.</summary>
        public float SheathRadius { get; private set; } = 1f;
        /// <summary>Distance (m) from the viewing camera, set by the scene controller before <see cref="Apply"/>.</summary>
        public float CameraDistance { get; set; } = 100f;

        private VisualsLibrary lib;
        private bool atmosphere;
        private Transform rock;
        private Material rockMat;
        private Transform plasma;
        private Material plasmaMat;
        private Transform glow;
        private Material glowMat;
        private Light pointLight;
        private TrailRenderer trail;
        private Material trailMat;
        private ParticleSystem sparks;
        private ParticleSystem smoke;
        private ParticleSystem burst;
        private readonly List<Fragment> fragments = new List<Fragment>();
        private Vector3 tumbleAxis;
        private float tumbleSpeed;
        private float flash;
        private float breakupTime;
        private double breakupDiameter;
        private Vector3 lastVelocityDir = Vector3.forward;
        private float smoothedBrightness;

        private sealed class Fragment
        {
            public Transform Rock;
            public Transform Glow;
            public TrailRenderer Trail;
            public Vector3 Direction; // lateral, unit
            public float Spread;      // fraction of the cloud radius
            public float Lag;
            public float Size;
        }

        public static MeteorVisual Create(VisualsLibrary lib, Transform parent, int layer, bool atmosphere, float radius, int seed, RockLook look = null)
        {
            var go = new GameObject("Meteor (visual)");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var mv = go.AddComponent<MeteorVisual>();
            mv.Build(lib, layer, atmosphere, radius, seed, look);
            return mv;
        }

        /// <summary>The composition-driven look of the rock.</summary>
        public RockLook Look { get; private set; }

        /// <summary>World direction "away from the Earth" for the rock's cheap environment reflection.</summary>
        public Vector3 EnvironmentUp { get; set; } = Vector3.up;

        /// <summary>Look of the body being flown in the current session.</summary>
        public static RockLook CurrentLook()
        {
            SimulationSession s = SimulationSession.Instance;
            MeteorMaterial flown = s.Properties != null ? s.Properties.Material : (s.Runner != null ? s.Runner.Properties.Material : null);
            return RockLook.For(s.Composition, flown);
        }

        private static readonly int LBase = Shader.PropertyToID("_LBase");
        private static readonly int LAccent = Shader.PropertyToID("_LAccent");
        private static readonly int LParam = Shader.PropertyToID("_LParam");

        /// <summary>Sends a look's layers to a MeteorRock material.</summary>
        public static void ApplyLook(Material m, RockLook look)
        {
            var bases = new Vector4[RockLook.MaxLayers];
            var accents = new Vector4[RockLook.MaxLayers];
            var prms = new Vector4[RockLook.MaxLayers];
            var styles = Vector4.zero;
            for (int i = 0; i < RockLook.MaxLayers; i++)
            {
                RockLayer l = look.Layers[Mathf.Min(i, look.Layers.Count - 1)];
                MaterialAppearance a = l.Appearance;
                bases[i] = new Vector4(a.BaseR, a.BaseG, a.BaseB, 1f);
                accents[i] = new Vector4(a.AccentR, a.AccentG, a.AccentB, 1f);
                prms[i] = new Vector4(a.Metallic, a.Roughness, a.Inclusions, a.Ice);
                styles[i] = (float)l.Style;
            }

            float[] e = look.Edges();
            m.SetVectorArray(LBase, bases);
            m.SetVectorArray(LAccent, accents);
            m.SetVectorArray(LParam, prms);
            m.SetVector("_LayerStyles", styles);
            m.SetVector("_LayerEdges", new Vector4(e[0], e[1], e[2], 0f));
            m.SetFloat("_LayerCount", look.Layers.Count);
        }

        private void Build(VisualsLibrary library, int layer, bool inAtmosphere, float radius, int seed, RockLook look)
        {
            lib = library;
            atmosphere = inAtmosphere;
            Radius = Mathf.Max(0.05f, radius);
            SheathRadius = Radius;
            var rng = new System.Random(seed);
            tumbleAxis = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).normalized;
            tumbleSpeed = 4f + (float)rng.NextDouble() * 8f;

            Look = look ?? CurrentLook();
            rock = NewChild("Rock", layer).transform;
            rock.gameObject.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Rock(seed, 5, 0.22f, null, Look.Lumpiness);
            var rr = rock.gameObject.AddComponent<MeshRenderer>();
            rockMat = new Material(lib.meteorRock);
            ApplyLook(rockMat, Look);
            rr.sharedMaterial = rockMat;
            rock.localRotation = Random.rotation;
            rock.localScale = Vector3.one * Radius;

            glow = NewChild("Glow", layer).transform;
            glow.gameObject.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Quad();
            var gr = glow.gameObject.AddComponent<MeshRenderer>();
            glowMat = new Material(lib.glow);
            gr.sharedMaterial = glowMat;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (!atmosphere)
            {
                // icy bodies sublimate in sunlight: a faint vapour coma around the nucleus
                float ice = Look.Ice;
                glow.gameObject.SetActive(ice > 0.15f);
                glow.localScale = Vector3.one * Radius * 9f;
                glowMat.SetColor("_Color", new Color(0.55f, 0.75f, 1f));
                glowMat.SetFloat("_Intensity", 0.09f + ice * 0.12f);
                glowMat.SetFloat("_Core", 600f);
                glowMat.SetFloat("_Halo", 3.2f);
                return;
            }

            plasma = NewChild("Plasma sheath", layer).transform;
            plasma.gameObject.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Icosphere(4);
            var pr = plasma.gameObject.AddComponent<MeshRenderer>();
            plasmaMat = new Material(lib.plasma);
            pr.sharedMaterial = plasmaMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = NewChild("Fireball light", layer);
            pointLight = lightGo.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.shadows = LightShadows.None;
            pointLight.intensity = 0;

            trail = gameObject.AddComponent<TrailRenderer>();
            trailMat = new Material(lib.ionTrail);
            trail.sharedMaterial = trailMat;
            trail.time = 1.6f;
            trail.minVertexDistance = Radius;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.colorGradient = MakeGradient(
                new[] { new GradientColorKey(new Color(1f, 0.92f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.2f), 0.12f), new GradientColorKey(new Color(0.75f, 0.3f, 0.55f), 0.45f), new GradientColorKey(new Color(0.35f, 0.25f, 0.6f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0.15f, 0.6f), new GradientAlphaKey(0f, 1f) });
            trail.emitting = false;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.15f, 0.7f), new Keyframe(1f, 0.15f));

            sparks = CreateSparks(layer);
            smoke = CreateSmoke(layer);
            burst = CreateBurst(layer);
        }

        /// <summary>Updates every effect for this frame.</summary>
        /// <param name="velocityDir">Unit direction of travel in world axes.</param>
        /// <param name="speed">Speed (m/s) used to size trails.</param>
        /// <param name="unitsPerMetre">World units per metre (1 in the atmosphere scene).</param>
        public void Apply(in FlightTelemetry t, Vector3 velocityDir, float speed, double pancakeDiameter, float dt)
        {
            if (velocityDir.sqrMagnitude > 1e-6f)
            {
                lastVelocityDir = velocityDir.normalized;
            }

            rock.Rotate(tumbleAxis, tumbleSpeed * dt, Space.Self);

            double kelvin = t.Temperature;
            float heat = (float)Blackbody.GlowWeight(kelvin);
            HeatColor = VisualsGlobals.BlackbodyColor(kelvin);
            float target = atmosphere ? (float)Blackbody.LuminosityToBrightness(t.Luminosity) : 0f;
            smoothedBrightness = Mathf.Lerp(smoothedBrightness, target, 1f - Mathf.Exp(-dt * 8f));
            Brightness = smoothedBrightness;
            float b = Brightness;
            flash = Mathf.Max(0f, flash - dt * 1.6f);

            rockMat.SetColor("_HeatColor", HeatColor * 2.2f);
            rockMat.SetFloat("_HeatIntensity", heat * 2.2f);
            rockMat.SetFloat("_Char", Mathf.Clamp01(heat * 1.5f));
            rockMat.SetVector("_FlowDir", rock.InverseTransformDirection(lastVelocityDir));
            rockMat.SetVector("_EnvUp", rock.InverseTransformDirection(EnvironmentUp));

            if (!atmosphere)
            {
                return;
            }

            float q = (float)t.DynamicPressure;
            float qGlow = Mathf.Clamp01(Mathf.Log10(1f + q) / 6.5f); // ~1 at 3 MPa
            float plasmaI = Mathf.Clamp01(b * 1.3f) * (0.5f + qGlow) * 1.3f + flash * 2f;
            bool broken = t.Broken;

            plasma.gameObject.SetActive(plasmaI > 0.01f);
            plasma.rotation = Quaternion.LookRotation(lastVelocityDir);
            float sheath = broken ? Mathf.Max(Radius, (float)pancakeDiameter * 0.5f) : Radius;
            plasma.localScale = Vector3.one * sheath * (1.5f + b * 0.6f);
            plasmaMat.SetFloat("_Intensity", plasmaI);
            plasmaMat.SetFloat("_Stretch", 5f + b * 8f);
            Color plasmaCol = Color.Lerp(new Color(1f, 0.45f, 0.18f), HeatColor, 0.35f);
            plasmaMat.SetColor("_Color", plasmaCol * 1.3f);
            plasmaMat.SetColor("_CoreColor", Color.Lerp(new Color(1f, 0.92f, 0.8f), HeatColor, 0.2f) * 2f);

            SheathRadius = sheath;
            // Halo: a few body sizes, but never thinner than a few pixels' worth at the viewing distance (a distant
            // fireball still reads as a bright point) and never larger than a fraction of the view.
            float cam = Mathf.Max(CameraDistance, 1f);
            float glowSize = Mathf.Max(sheath * (broken ? 3f + 5f * b : 5f + 14f * b), cam * 0.03f * b) * (1f + flash * 1.2f);
            glowSize = Mathf.Min(glowSize, cam * 0.9f);
            glow.gameObject.SetActive(b > 0.01f || flash > 0.01f);
            glow.localScale = Vector3.one * glowSize;
            Color glowCol = Color.Lerp(new Color(1f, 0.62f, 0.32f), new Color(0.85f, 0.9f, 1f), Mathf.Clamp01(b - 0.6f));
            glowMat.SetColor("_Color", glowCol);
            glowMat.SetFloat("_Intensity", (b * 1.6f + flash * 3f));

            pointLight.color = Color.Lerp(new Color(1f, 0.65f, 0.35f), HeatColor, 0.5f);
            pointLight.intensity = (b * 6f + flash * 10f);
            pointLight.range = sheath * 300f * (0.3f + b);

            trail.emitting = b > 0.03f;
            trail.widthMultiplier = Mathf.Max((broken ? Radius * 2f : sheath) * (0.6f + b * 1.2f), cam * 0.002f * b);
            trail.minVertexDistance = Mathf.Max(Radius, speed * 0.004f);
            trail.time = 0.8f + b * 1.6f;
            trailMat.SetColor("_Color", Color.white * (0.55f + b * 0.55f));

            float mlr = (float)t.MassLossRate;
            var sparkEmission = sparks.emission;
            sparkEmission.rateOverTime = Mathf.Clamp(Mathf.Log10(1f + mlr) * 70f * (0.3f + b), 0f, 500f);
            var sparkShape = sparks.shape;
            sparkShape.radius = sheath * 0.8f;
            var sparkMain = sparks.main;
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(sheath * 0.15f, sheath * 0.45f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(sheath * 2f, sheath * 12f);

            var smokeEmission = smoke.emission;
            bool smoking = mlr > 0.05f && t.Altitude < 95000;
            smokeEmission.rateOverDistance = smoking ? Mathf.Clamp(120f / Mathf.Max(speed, 1f), 0.002f, 0.2f) * 60f / 60f : 0f;
            var smokeMain = smoke.main;
            float puff = Mathf.Max(sheath * 6f, 25f) * (0.6f + b);
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(puff * 0.7f, puff * 1.3f);
            smokeMain.startColor = new Color(1f, 1f, 1f, Mathf.Clamp01(0.08f + Mathf.Log10(1f + mlr) * 0.12f));

            if (broken && fragments.Count == 0)
            {
                BeginBreakup(t);
            }

            if (fragments.Count > 0)
            {
                UpdateFragments(pancakeDiameter, b, dt);
            }
        }

        /// <summary>Starts a short bright flash (used for break-up and the end of the run).</summary>
        public void Flash(float amount) => flash = Mathf.Max(flash, amount);

        /// <summary>Stops all emission and hides the body (end of the run); trails and smoke linger.</summary>
        public void Extinguish()
        {
            if (rock != null) rock.gameObject.SetActive(false);
            if (plasma != null) plasma.gameObject.SetActive(false);
            if (glow != null) glow.gameObject.SetActive(false);
            if (trail != null) { trail.emitting = false; trail.time = 0.8f; trail.widthMultiplier *= 0.4f; }
            if (sparks != null) { var e = sparks.emission; e.rateOverTime = 0; }
            if (smoke != null) { var e = smoke.emission; e.rateOverDistance = 0; }
            if (pointLight != null) pointLight.intensity = 0;
            foreach (Fragment f in fragments)
            {
                f.Rock.gameObject.SetActive(false);
                f.Glow.gameObject.SetActive(false);
                f.Trail.emitting = false;
            }

            enabled = false;
        }

        private void BeginBreakup(in FlightTelemetry t)
        {
            breakupTime = Time.time;
            breakupDiameter = t.Diameter;
            Flash(1.2f);
            burst.transform.rotation = Quaternion.LookRotation(lastVelocityDir);
            var bm = burst.main;
            bm.startSpeed = new ParticleSystem.MinMaxCurve(Radius * 20f, Radius * 90f);
            bm.startSize = new ParticleSystem.MinMaxCurve(Radius * 0.3f, Radius * 1.2f);
            burst.Emit(350);
            var rng = new System.Random(1234);
            int count = 9;
            for (int i = 0; i < count; i++)
            {
                var f = new Fragment();
                var go = NewChild("Fragment " + i, gameObject.layer);
                go.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Rock(100 + i, 3, 0.3f);
                go.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
                f.Rock = go.transform;
                f.Rock.localRotation = Random.rotation;
                f.Size = Radius * (0.22f + (float)rng.NextDouble() * 0.3f);
                f.Rock.localScale = Vector3.one * f.Size;

                var g = NewChild("Fragment glow " + i, gameObject.layer);
                g.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Quad();
                var gr = g.AddComponent<MeshRenderer>();
                gr.sharedMaterial = glowMat;
                f.Glow = g.transform;

                var tg = NewChild("Fragment trail " + i, gameObject.layer);
                f.Trail = tg.AddComponent<TrailRenderer>();
                f.Trail.sharedMaterial = trailMat;
                f.Trail.time = 1.0f;
                f.Trail.alignment = LineAlignment.View;
                f.Trail.colorGradient = trail.colorGradient;
                f.Trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f.Rock.SetParent(tg.transform, false);
                f.Glow.SetParent(tg.transform, false);
                f.Rock.localPosition = Vector3.zero;
                f.Glow.localPosition = Vector3.zero;
                f.Rock = tg.transform.GetChild(0);
                f.Glow = tg.transform.GetChild(1);

                float a = (float)(i * 2.0 * System.Math.PI / count + rng.NextDouble() * 0.6);
                f.Direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                f.Spread = 0.35f + (float)rng.NextDouble() * 0.65f;
                f.Lag = (float)rng.NextDouble();
                fragments.Add(f);
            }
        }

        private void UpdateFragments(double pancakeDiameter, float b, float dt)
        {
            float age = Time.time - breakupTime;
            float cloudRadius = Mathf.Max(Radius, (float)(pancakeDiameter * 0.5));
            Quaternion frame = Quaternion.LookRotation(lastVelocityDir);
            // main body shrinks away as its mass is shared with the fragment cloud
            float bodyScale = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(age * 2f));
            rock.localScale = Vector3.one * Radius * bodyScale;
            for (int i = 0; i < fragments.Count; i++)
            {
                Fragment f = fragments[i];
                Transform holder = f.Trail.transform;
                Vector3 lateral = frame * f.Direction * (cloudRadius * f.Spread * 1.6f + Radius * age * 3f * f.Spread);
                Vector3 back = -lastVelocityDir * (Radius * (0.5f + f.Lag * 6f) * Mathf.Clamp01(age) + f.Lag * cloudRadius);
                holder.localPosition = lateral + back;
                f.Rock.Rotate(tumbleAxis, tumbleSpeed * 3f * dt, Space.Self);
                f.Glow.localScale = Vector3.one * f.Size * (6f + 20f * b);
                f.Trail.widthMultiplier = f.Size * (1f + b * 3f);
                f.Trail.minVertexDistance = Mathf.Max(f.Size, 1f);
                f.Trail.emitting = b > 0.03f;
            }
        }

        private GameObject NewChild(string name, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(transform, false);
            return go;
        }

        private ParticleSystem NewParticles(string name, int layer, Material material, int max)
        {
            var go = NewChild(name, layer);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private ParticleSystem CreateSparks(int layer)
        {
            ParticleSystem ps = NewParticles("Ablation sparks", layer, lib.sparks, 1200);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.45f, 0.15f));
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGradient(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f), new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) }));
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 2f;
            ps.Play();
            return ps;
        }

        private ParticleSystem CreateSmoke(int layer)
        {
            ParticleSystem ps = NewParticles("Dust train", layer, lib.smoke, 2500);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 24f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.15f, 1.2f), new Keyframe(1, 3.5f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGradient(
                new[] { new GradientColorKey(new Color(1f, 0.75f, 0.5f), 0f), new GradientColorKey(new Color(0.85f, 0.82f, 0.8f), 0.08f), new GradientColorKey(new Color(0.7f, 0.68f, 0.66f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) }));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 6f;
            noise.frequency = 0.05f;
            noise.scrollSpeed = 0.1f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = 4f;
            ps.Play();
            return ps;
        }

        private ParticleSystem CreateBurst(int layer)
        {
            ParticleSystem ps = NewParticles("Break-up burst", layer, lib.debris, 600);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.7f), new Color(1f, 0.5f, 0.2f));
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGradient(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.6f), new GradientColorKey(new Color(0.4f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }));
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            var inherit = ps.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = 0.9f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.15f;
            limit.limit = 0f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.03f;
            r.lengthScale = 1.5f;
            ps.Play();
            return ps;
        }

        private static Gradient MakeGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var g = new Gradient();
            g.SetKeys(colors, alphas);
            return g;
        }
    }
}
