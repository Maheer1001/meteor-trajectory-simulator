using System.IO;
using Cinemachine;
using MTS.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace MTS.Visuals.EditorTools
{
    /// <summary>
    /// Builds the visuals reproducibly: generated textures, texture import settings, materials, post-processing
    /// profiles, the shared <see cref="VisualsLibrary"/>, and the camera/visual rigs in SpaceSimulation and
    /// AtmosphereReached. Safe to run repeatedly (the "Visuals" rig is replaced). Menu: Tools/MTS/Build Visuals.
    /// Batch: -executeMethod MTS.Visuals.EditorTools.VisualsBuilder.BuildAll
    /// </summary>
    public static class VisualsBuilder
    {
        private const string ArtDir = "Assets/Art";
        private const string MatDir = "Assets/Art/Materials";
        private const string GenDir = "Assets/Art/Generated";
        private const string PpDir = "Assets/Settings/PostProcessing";
        private const string LibraryPath = "Assets/Art/VisualsLibrary.asset";
        private const string RigName = "Visuals";

        [MenuItem("Tools/MTS/Build Visuals")]
        public static void BuildAll()
        {
            VisualsLibrary lib = BuildAssets();
            BuildSpaceScene(lib);
            BuildAtmosphereScene(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Visuals] Build complete.");
        }

        public static VisualsLibrary BuildAssets()
        {
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(GenDir);
            Directory.CreateDirectory(PpDir);
            GenerateTextures();
            ConfigureEarthTextures();

            Texture2D day = Tex("Assets/Art/Earth/earth_day_4k.jpg");
            Texture2D night = Tex("Assets/Art/Earth/earth_night_3600.jpg");
            Texture2D cloudsTex = Tex("Assets/Art/Earth/earth_clouds_2k.jpg");
            Texture2D ocean = Tex("Assets/Art/Earth/earth_ocean_mask_2k.png");
            Texture2D puff = Tex(GenDir + "/smoke_puff.png");
            Texture2D spark = Tex(GenDir + "/spark.png");

            var lib = AssetDatabase.LoadAssetAtPath<VisualsLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<VisualsLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            lib.earth = Mat("Earth", "MTS/Earth", m =>
            {
                m.SetTexture("_DayTex", day);
                m.SetTexture("_NightTex", night);
                m.SetTexture("_CloudTex", cloudsTex);
                m.SetTexture("_OceanMask", ocean);
            });
            lib.atmosphereShell = Mat("AtmosphereShell", "MTS/AtmosphereShell", null);
            lib.starfield = Mat("Starfield", "MTS/Starfield", null);
            lib.atmosphereSky = Mat("AtmosphereSky", "MTS/AtmosphereSky", null);
            lib.ground = Mat("Ground", "MTS/Ground", m =>
            {
                m.SetTexture("_DayTex", day);
                m.SetTexture("_NightTex", night);
                m.SetTexture("_OceanMask", ocean);
                m.SetTexture("_CloudTex", cloudsTex);
            });
            lib.cloudDeck = Mat("CloudDeck", "MTS/CloudDeck", m => m.SetTexture("_CloudTex", cloudsTex));
            lib.meteorRock = Mat("MeteorRock", "MTS/MeteorRock", null);
            lib.plasma = Mat("PlasmaSheath", "MTS/PlasmaSheath", null);
            lib.glow = Mat("Glow", "MTS/Glow", null);
            lib.marker = Mat("Marker", "MTS/Glow", m =>
            {
                m.SetFloat("_Core", 25f);
                m.SetFloat("_Halo", 7f);
                m.SetFloat("_Spikes", 0.6f);
            });
            lib.ionTrail = Mat("IonTrail", "MTS/TrajectoryLine", null);
            lib.sparks = Mat("Sparks", "MTS/ParticleAdditive", m =>
            {
                m.SetTexture("_MainTex", spark);
                m.SetFloat("_Intensity", 3f);
            });
            lib.debris = Mat("Debris", "MTS/ParticleAdditive", m =>
            {
                m.SetTexture("_MainTex", spark);
                m.SetFloat("_Intensity", 4f);
            });
            lib.smoke = Mat("Smoke", "MTS/ParticleSmoke", m =>
            {
                m.SetTexture("_MainTex", puff);
                m.SetColor("_Color", new Color(0.62f, 0.6f, 0.58f, 0.85f));
                m.SetFloat("_NearFade", 60f);
            });
            lib.dust = Mat("Dust", "MTS/ParticleSmoke", m =>
            {
                m.SetTexture("_MainTex", puff);
                m.SetColor("_Color", new Color(0.6f, 0.52f, 0.44f, 1f));
                m.SetFloat("_NearFade", 1500f);
                m.SetFloat("_Ambient", 0.45f);
            });
            lib.pathPast = Mat("PathPast", "MTS/TrajectoryLine", m => m.SetColor("_Color", new Color(1.4f, 0.7f, 0.3f, 1f)));
            lib.pathFuture = Mat("PathFuture", "MTS/TrajectoryLine", m =>
            {
                m.SetColor("_Color", new Color(0.45f, 0.8f, 1.3f, 1f));
                m.SetFloat("_Dash", 1f / 5f);
                m.SetFloat("_DashFill", 0.55f);
            });
            lib.shockwave = Mat("Shockwave", "MTS/Shockwave", null);
            lib.crater = Mat("Crater", "MTS/Crater", null);
            lib.fireball = Mat("Fireball", "MTS/Fireball", null);
            lib.ejecta = Mat("Ejecta", "MTS/DustSheet", null);
            lib.ashCloud = Mat("AshCloud", "MTS/AshCloud", null);
            lib.groundScar = Mat("GroundScar", "MTS/GroundScar", null);

            lib.spaceProfile = Profile("SpaceProfile", bloom: 2.2f, threshold: 1.0f, exposure: 0.15f, vignette: 0.32f, motionBlur: false);
            lib.atmosphereProfile = Profile("AtmosphereProfile", bloom: 2.8f, threshold: 1.05f, exposure: 0.1f, vignette: 0.26f, motionBlur: false);
            lib.flashProfile = FlashProfile();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        // ---------- scenes ----------

        public static void BuildSpaceScene(VisualsLibrary lib)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/SpaceSimulation.unity", OpenSceneMode.Single);
            HideLegacyVisuals(scene);
            GameObject rig = NewRig(scene);
            var visuals = rig.AddComponent<SpaceVisuals>();
            visuals.library = lib;
            visuals.farCamera = NewCamera(rig.transform, "Far Camera (km)", -3, 1 << VisualLayers.Far, CameraClearFlags.Skybox, 1f, 2.0e6f);
            visuals.nearCamera = NewCamera(rig.transform, "Near Camera (m)", -2, 1 << VisualLayers.Near, CameraClearFlags.Depth, 0.05f, 2.0e5f);
            visuals.nearCamera.fieldOfView = 50f;
            visuals.farCamera.fieldOfView = 50f;
            AddMainCameraBits(visuals.nearCamera, lib.spaceProfile, rig.transform);
            visuals.sun = NewSun(rig.transform, 1.2f);

            RenderSettings.skybox = lib.starfield;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.08f);
            RenderSettings.fog = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void BuildAtmosphereScene(VisualsLibrary lib)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/AtmosphereReached.unity", OpenSceneMode.Single);
            HideLegacyVisuals(scene);
            GameObject rig = NewRig(scene);
            var visuals = rig.AddComponent<AtmosphereVisuals>();
            visuals.library = lib;
            visuals.farCamera = NewCamera(rig.transform, "Far Camera (ground, sky)", -3, 1 << VisualLayers.Far, CameraClearFlags.Skybox, 1f, 4.0e6f);
            visuals.mainCamera = NewCamera(rig.transform, "Main Camera (meteor)", -2, 1 << VisualLayers.Near, CameraClearFlags.Depth, 0.1f, 1.5e6f);
            AddMainCameraBits(visuals.mainCamera, lib.atmosphereProfile, rig.transform);
            var brain = visuals.mainCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.m_IgnoreTimeScale = true;
            visuals.sun = NewSun(rig.transform, 1.1f);

            var endGo = new GameObject("End Sequence");
            endGo.transform.SetParent(rig.transform, false);
            var end = endGo.AddComponent<EndSequence>();
            end.visuals = visuals;
            end.impulse = endGo.AddComponent<CinemachineImpulseSource>();
            end.impulse.m_ImpulseDefinition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            end.impulse.m_ImpulseDefinition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion;
            end.impulse.m_ImpulseDefinition.m_ImpulseDuration = 1.6f;
            end.impulse.m_DefaultVelocity = new Vector3(0.6f, 1f, 0.3f);
            var flashGo = new GameObject("Flash Volume") { layer = VisualLayers.Near };
            flashGo.transform.SetParent(rig.transform, false);
            var flashVol = flashGo.AddComponent<PostProcessVolume>();
            flashVol.isGlobal = true;
            flashVol.priority = 10;
            flashVol.weight = 0f;
            flashVol.sharedProfile = lib.flashProfile;
            end.flashVolume = flashVol;
            visuals.endSequence = end;

            RenderSettings.skybox = lib.atmosphereSky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.42f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.38f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.13f);
            RenderSettings.fog = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Hides the old visual objects without deleting anything the adapters reference: the adapters' own renderers
        /// are disabled (their transforms keep moving), the old cameras and the old Earth are switched off.
        /// </summary>
        private static void HideLegacyVisuals(Scene scene)
        {
            GameObject rig = FindRig(scene);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == rig) continue;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;
                    if (go.name == "Earth" || go.name == "Meteor1" || go.name == "Meteor2")
                    {
                        var mr = go.GetComponent<MeshRenderer>();
                        if (mr != null) mr.enabled = false;
                        var col = go.GetComponent<Collider>();
                        if (col != null) col.enabled = false;
                        continue;
                    }

                    if (go.GetComponent<RectTransform>() != null) continue;

                    Camera cam = go.GetComponent<Camera>();
                    if (cam != null)
                    {
                        go.SetActive(false); // "Main Camera" under the meteor, "Earth POV" under the Earth
                        continue;
                    }

                    if (go.name == "Directional Light")
                    {
                        var l = go.GetComponent<Light>();
                        if (l != null) l.enabled = false;
                    }
                }
            }
        }

        private static GameObject FindRig(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == RigName) return root;
            }

            return null;
        }

        private static GameObject NewRig(Scene scene)
        {
            GameObject old = FindRig(scene);
            if (old != null) Object.DestroyImmediate(old);
            var rig = new GameObject(RigName);
            SceneManager.MoveGameObjectToScene(rig, scene);
            return rig;
        }

        private static Camera NewCamera(Transform parent, string name, float depth, int mask, CameraClearFlags clear, float near, float far)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.depth = depth;
            cam.cullingMask = mask;
            cam.clearFlags = clear;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = near;
            cam.farClipPlane = far;
            cam.fieldOfView = 55f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.useOcclusionCulling = false;
            return cam;
        }

        private static void AddMainCameraBits(Camera cam, PostProcessProfile profile, Transform rig)
        {
            cam.gameObject.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();
            var layer = cam.gameObject.AddComponent<PostProcessLayer>();
            var resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
            layer.Init(resources);
            layer.volumeTrigger = cam.transform;
            layer.volumeLayer = 1 << VisualLayers.Near;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.None;
            layer.stopNaNPropagation = true;

            var volGo = new GameObject("Post Volume") { layer = VisualLayers.Near };
            volGo.transform.SetParent(rig, false);
            var vol = volGo.AddComponent<PostProcessVolume>();
            vol.isGlobal = true;
            vol.priority = 1;
            vol.sharedProfile = profile;
        }

        private static Light NewSun(Transform parent, float intensity)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = new Color(1f, 0.96f, 0.9f);
            l.shadows = LightShadows.Soft;
            l.cullingMask = 1 << VisualLayers.Near;
            return l;
        }

        // ---------- assets ----------

        private static Texture2D Tex(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogError("[Visuals] Missing texture " + path);
            return t;
        }

        private static Material Mat(string name, string shaderName, System.Action<Material> setup)
        {
            string path = MatDir + "/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) Debug.LogError("[Visuals] Missing shader " + shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }

            m.shader = shader;
            var fresh = new Material(shader);
            m.CopyPropertiesFromMaterial(fresh); // reset to the shader's defaults, then apply the setup
            Object.DestroyImmediate(fresh);
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static PostProcessProfile Profile(string name, float bloom, float threshold, float exposure, float vignette, bool motionBlur)
        {
            PostProcessProfile p = LoadOrCreateProfile(name);
            var b = AddOrGet<Bloom>(p);
            b.intensity.Override(bloom);
            b.threshold.Override(threshold);
            b.softKnee.Override(0.6f);
            b.diffusion.Override(7.5f);
            b.anamorphicRatio.Override(0f);

            var cg = AddOrGet<ColorGrading>(p);
            cg.gradingMode.Override(GradingMode.HighDefinitionRange);
            cg.tonemapper.Override(Tonemapper.ACES);
            cg.postExposure.Override(exposure);
            cg.contrast.Override(8f);
            cg.saturation.Override(6f);
            cg.temperature.Override(2f);

            var v = AddOrGet<Vignette>(p);
            v.intensity.Override(vignette);
            v.smoothness.Override(0.45f);
            v.roundness.Override(1f);

            var mb = AddOrGet<MotionBlur>(p);
            mb.enabled.Override(motionBlur);
            mb.shutterAngle.Override(110f);
            mb.sampleCount.Override(8);

            var ca = AddOrGet<ChromaticAberration>(p);
            ca.intensity.Override(0.03f);

            var grain = AddOrGet<Grain>(p);
            grain.intensity.Override(0.06f);
            grain.size.Override(0.8f);
            grain.lumContrib.Override(0.7f);

            EditorUtility.SetDirty(p);
            return p;
        }

        private static PostProcessProfile FlashProfile()
        {
            PostProcessProfile p = LoadOrCreateProfile("FlashProfile");
            var b = AddOrGet<Bloom>(p);
            b.intensity.Override(10f);
            b.threshold.Override(0.7f);
            b.diffusion.Override(9f);
            var cg = AddOrGet<ColorGrading>(p);
            cg.gradingMode.Override(GradingMode.HighDefinitionRange);
            cg.tonemapper.Override(Tonemapper.ACES);
            cg.postExposure.Override(0.8f);
            cg.temperature.Override(18f);
            var ca = AddOrGet<ChromaticAberration>(p);
            ca.intensity.Override(0.5f);
            EditorUtility.SetDirty(p);
            return p;
        }

        private static PostProcessProfile LoadOrCreateProfile(string name)
        {
            string path = PpDir + "/" + name + ".asset";
            var p = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<PostProcessProfile>();
                AssetDatabase.CreateAsset(p, path);
            }

            return p;
        }

        private static T AddOrGet<T>(PostProcessProfile p) where T : PostProcessEffectSettings
        {
            if (p.TryGetSettings(out T existing))
            {
                return existing;
            }

            T s = p.AddSettings<T>();
            s.name = typeof(T).Name;
            s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(s, p);
            return s;
        }

        private static void ConfigureEarthTextures()
        {
            string[] paths =
            {
                "Assets/Art/Earth/earth_day_4k.jpg", "Assets/Art/Earth/earth_night_3600.jpg",
                "Assets/Art/Earth/earth_clouds_2k.jpg", "Assets/Art/Earth/earth_ocean_mask_2k.png",
            };
            foreach (string path in paths)
            {
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Default;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.mipmapEnabled = true;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.anisoLevel = 8;
                ti.filterMode = FilterMode.Trilinear;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
        }

        private static void GenerateTextures()
        {
            WriteTexture(GenDir + "/smoke_puff.png", 128, (x, y) =>
            {
                float u = x * 2f - 1f, v = y * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float n = ProceduralMeshes.Fbm(new Vector3(x * 6f, y * 6f, 0.5f), 5);
                float a = Mathf.Clamp01((1f - r) * 1.6f) * Mathf.Clamp01(n * 1.8f - 0.25f);
                a = Mathf.SmoothStep(0f, 1f, a);
                float light = Mathf.Clamp01(0.5f + (v * 0.6f - u * 0.2f) * 0.5f + (n - 0.5f) * 0.6f);
                return new Color(1f, light, 1f, a);
            });
            WriteTexture(GenDir + "/spark.png", 64, (x, y) =>
            {
                float u = x * 2f - 1f, v = y * 2f - 1f;
                float r2 = u * u + v * v;
                float a = Mathf.Exp(-r2 * 9f) + Mathf.Exp(-r2 * 40f) * 0.8f;
                a *= Mathf.Clamp01(1f - r2);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            });
        }

        private static void WriteTexture(string path, int size, System.Func<float, float, Color> f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, f((x + 0.5f) / size, (y + 0.5f) / size));
                }
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }
    }
}
