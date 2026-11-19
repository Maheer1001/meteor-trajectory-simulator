using System.Collections.Generic;
using Cinemachine;
using MTS.Core;
using UnityEngine;

namespace MTS.Visuals
{
    /// <summary>
    /// Atmosphere-stage visuals in a local East-Up-North frame (metres) anchored at the predicted end point, so the
    /// end sequence happens near the origin where floats are most precise. A far camera draws the curved ground,
    /// cloud deck and altitude-driven sky; the near camera (with the Cinemachine brain and post-processing) draws the
    /// meteor and its effects. Three virtual cameras (chase, ground observer, side view) are switchable through
    /// <see cref="SimulationSignals.CameraRequested"/>. Reads the session only; never writes simulation state.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class AtmosphereVisuals : MonoBehaviour
    {
        public VisualsLibrary library;
        public Camera farCamera;
        public Camera mainCamera;
        public Light sun;
        public EndSequence endSequence;
        public Color sunColor = new Color(1f, 0.96f, 0.9f);
        public float sunIntensity = 1.2f;
        [Tooltip("Ground observer's distance from the predicted end point (m), to the side of the ground track.")]
        public float observerSideDistance = 24000f;
        public float observerBackDistance = 9000f;
        [Tooltip("Height of the cloud deck (m).")]
        public float cloudHeight = 3500f;

        public static readonly string[] CameraNames = { "Chase", "Ground observer", "Side view" };

        private SimulationRunner runner;
        private LocalFrame frame;
        private PathPredictor predictor;
        private MeteorVisual meteor;
        private Transform ground;
        private Transform clouds;
        private Material cloudMat;
        private LineRenderer predictedLine;
        // three user cameras plus an internal one for the end sequence
        private readonly CinemachineVirtualCamera[] vcams = new CinemachineVirtualCamera[4];
        private int mode;
        private Vector3 observerPos;
        private Vector3 trackDir = Vector3.forward;
        private Vector3 sunLocal;
        private bool initialised;

        /// <summary>True once the rig (meteor, cameras, lines) has been built for the current run.</summary>
        public bool IsReady => initialised && meteor != null;
        private bool ended;
        private bool wasBroken;
        private double lastSimTime = double.NaN;
        private float lastStamp;
        private float simRate = 1f;
        private float predictTimer;
        private Vector3 meteorPos;
        private Vector3 velocityDir = Vector3.down;
        private Vector3 smoothedLook;
        private Vector3 endFocus;

        public Vector3 MeteorWorldPosition => meteorPos;
        public Vector3 CameraPosition => mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        public LocalFrame Frame => frame;
        public int Mode => mode;

        private void OnEnable()
        {
            SimulationSignals.CameraRequested += SetMode;
            SimulationSignals.PublishCameras(CameraNames);
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        }

        private void OnDisable()
        {
            SimulationSignals.CameraRequested -= SetMode;
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        }

        /// <summary>Switches virtual camera (0 chase, 1 ground observer, 2 side view).</summary>
        public void SetMode(int index)
        {
            if (index < 0 || index >= CameraNames.Length)
            {
                return;
            }

            mode = index;
            for (int i = 0; i < vcams.Length; i++)
            {
                if (vcams[i] != null) vcams[i].Priority = i == mode ? 20 : (i == 3 && ended ? 30 : 10);
            }

            SimulationSignals.ReportCameraChanged(mode, CameraNames[mode]);
        }

        /// <summary>Surface point (world) directly below a world position.</summary>
        public Vector3 GroundPointBelow(Vector3 world)
        {
            Vector3 centre = new Vector3(0f, -(float)PhysicalConstants.EarthRadius, 0f);
            Vector3 up = (world - centre).normalized;
            return centre + up * (float)PhysicalConstants.EarthRadius;
        }

        // A new run can start while the scene is open (restart, tooling): rebuild from scratch.
        private void Teardown()
        {
            if (ground != null) Destroy(ground.gameObject);
            if (clouds != null) Destroy(clouds.gameObject);
            if (predictedLine != null) Destroy(predictedLine.gameObject);
            if (meteor != null) Destroy(meteor.gameObject);
            for (int i = 0; i < vcams.Length; i++)
            {
                if (vcams[i] != null) Destroy(vcams[i].gameObject);
                vcams[i] = null;
            }

            initialised = false;
            ended = false;
            wasBroken = false;
            lastSimTime = double.NaN;
        }

        private void Initialise(SimulationRunner r)
        {
            runner = r;
            initialised = true;
            SimulationSession session = SimulationSession.Instance;
            MeteorProperties props = session.Properties ?? r.Properties;

            predictor = new PathPredictor(props, r.State, session.WindSpeed, session.WindDirection, 250.0, 3000);
            predictor.RunToEnd();
            Vec3d end = predictor.Points[predictor.Points.Count - 1];
            frame = new LocalFrame(end);
            VisualsGlobals.SetFrame(frame);
            Vec3d sunInertial = VisualsRunCache.SunFor(r, end);
            sunLocal = VisualsGlobals.ToVector3(frame.DirectionToLocal(sunInertial)).normalized;

            // ground track direction at the end and the observer's spot beside it
            Vec3d endVel = predictor.Result != null ? predictor.Result.FinalState.Velocity : r.State.Velocity;
            Vector3 v = VisualsGlobals.ToVector3(frame.DirectionToLocal(endVel));
            Vector3 h = new Vector3(v.x, 0f, v.z);
            trackDir = h.sqrMagnitude > 1e-3f * v.sqrMagnitude ? h.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, trackDir).normalized;
            Vector3 obs = side * observerSideDistance - trackDir * observerBackDistance;
            observerPos = GroundPointBelow(obs) + Vector3.up * 1.8f;

            BuildWorld();
            float radius = (float)r.State.EquivalentRadius(props.Material.Density);
            meteor = MeteorVisual.Create(library, null, VisualLayers.Near, true, radius, 7);
            BuildCameras();
            UpdateMeteorPosition(r.State, 0f);
            smoothedLook = meteorPos;
            UpdatePredictedLine(r.State);
            SetMode(mode);
        }

        private void BuildWorld()
        {
            Mesh patch = ProceduralMeshes.CurvedPatch(2.4e6f);
            var g = new GameObject("Ground (visual)") { layer = VisualLayers.Far };
            g.AddComponent<MeshFilter>().sharedMesh = patch;
            g.AddComponent<MeshRenderer>().sharedMaterial = library.ground;
            ground = g.transform;

            var c = new GameObject("Cloud deck (visual)") { layer = VisualLayers.Far };
            c.transform.position = Vector3.up * cloudHeight;
            c.AddComponent<MeshFilter>().sharedMesh = patch;
            cloudMat = new Material(library.cloudDeck);
            cloudMat.SetVector("_ClearCentre", new Vector4(observerPos.x, 0, observerPos.z, 0));
            c.AddComponent<MeshRenderer>().sharedMaterial = cloudMat;
            clouds = c.transform;

            var lineGo = new GameObject("Predicted path") { layer = VisualLayers.Near };
            predictedLine = lineGo.AddComponent<LineRenderer>();
            predictedLine.sharedMaterial = library.pathFuture;
            predictedLine.textureMode = LineTextureMode.Tile;
            predictedLine.useWorldSpace = true;
            predictedLine.alignment = LineAlignment.View;
            predictedLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            predictedLine.numCapVertices = 2;
            predictedLine.colorGradient = FadeGradient();

            RenderSettings.skybox = library.atmosphereSky;
        }

        private static Gradient FadeGradient()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.05f), new GradientAlphaKey(0.35f, 1f) });
            return g;
        }

        private void BuildCameras()
        {
            for (int i = 0; i < vcams.Length; i++)
            {
                // the brain only sees virtual cameras on layers its camera renders
                var go = new GameObject("VCam " + (i < CameraNames.Length ? CameraNames[i] : "End sequence")) { layer = VisualLayers.Near };
                go.transform.SetParent(transform, false);
                CinemachineVirtualCamera vc = go.AddComponent<CinemachineVirtualCamera>();
                vc.m_Lens.FieldOfView = i == 1 ? 50f : 60f;
                vc.m_Lens.NearClipPlane = 0.1f;
                vc.m_Lens.FarClipPlane = 1.5e6f;
                vc.Priority = 10;
                var listener = go.AddComponent<CinemachineImpulseListener>();
                listener.m_Gain = 1f;
                listener.m_Use2DDistance = false;
                vcams[i] = vc;
            }

            var brain = mainCamera.GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 1.4f);
                brain.m_IgnoreTimeScale = true;
            }
        }

        private void Update()
        {
            SimulationRunner r = SimulationSession.Instance.Runner;
            if (r == null)
            {
                return;
            }

            if (!initialised || !ReferenceEquals(r, runner))
            {
                Teardown();
                Initialise(r);
            }

            MeteorState s = r.State;
            float dt = Time.deltaTime;
            UpdateMeteorPosition(s, dt);
            VisualsGlobals.SetSun(sunLocal, sunColor, sunIntensity);
            VisualsGlobals.SetEarthRotation(s.Time);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunLocal);
                sun.intensity = Mathf.Clamp01(sunLocal.y * 4f + 0.2f) * 1.1f;
            }

            if (!ended)
            {
                FlightTelemetry t = r.Telemetry;
                meteor.CameraDistance = Vector3.Distance(CameraPosition, meteorPos);
                meteor.Apply(t, velocityDir, (float)s.Speed, s.PancakeDiameter, dt);
                if (t.Broken && !wasBroken)
                {
                    wasBroken = true;
                    SimulationSignals.RaiseToast("Break-up at " + (t.Altitude / 1000.0).ToString("0.0") + " km");
                }

                predictTimer += Time.unscaledDeltaTime;
                if (predictTimer > 1.0f && !r.HasEnded)
                {
                    predictTimer = 0f;
                    SimulationSession session = SimulationSession.Instance;
                    predictor = new PathPredictor(session.Properties ?? r.Properties, s, session.WindSpeed, session.WindDirection, 250.0, 3000);
                    predictor.RunToEnd(200000);
                }

                UpdatePredictedLine(s);

                if (r.HasEnded && endSequence != null)
                {
                    endSequence.Play();
                }
            }

            PositionCameras(dt);
        }

        /// <summary>Called by the end sequence when the run ends: hide the body, let trails linger, cut to the observer.</summary>
        public void OnRunEnded(EndReason reason, Vector3 focus, float viewRadius)
        {
            ended = true;
            endFocus = focus;
            meteor.Flash(1.5f);
            meteor.Extinguish();
            predictedLine.enabled = false;

            // Elevated view over the ground-zero area from the observer's side, wide enough for the blast rings.
            Vector3 groundZero = GroundPointBelow(focus);
            Vector3 horiz = observerPos - groundZero;
            horiz.y = 0f;
            horiz = horiz.sqrMagnitude > 1f ? horiz.normalized : -trackDir;
            float burstHeight = Mathf.Max(0f, (focus - groundZero).magnitude);
            float dist = Mathf.Clamp(Mathf.Max(viewRadius * 1.5f, burstHeight * 1.4f), 2500f, 140000f);
            Vector3 pos = groundZero + horiz * dist + Vector3.up * dist * 0.22f;
            Vector3 lookAt = groundZero + Vector3.up * Mathf.Min(burstHeight * 0.45f, dist * 0.3f) - horiz * dist * 0.05f;
            vcams[3].transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos, Vector3.up));
            vcams[3].m_Lens.FieldOfView = 55f;
            vcams[3].Priority = 30;
        }

        private void UpdateMeteorPosition(in MeteorState s, float dt)
        {
            if (s.Time != lastSimTime)
            {
                if (!double.IsNaN(lastSimTime) && Time.time > lastStamp)
                {
                    float rate = (float)((s.Time - lastSimTime) / (Time.time - lastStamp));
                    simRate = Mathf.Lerp(simRate, Mathf.Clamp(rate, 0f, 50f), 0.3f);
                }

                lastSimTime = s.Time;
                lastStamp = Time.time;
            }

            Vec3d local = frame.ToLocal(s.Position);
            Vec3d vel = frame.DirectionToLocal(s.Velocity);
            float ahead = ended ? 0f : Mathf.Min(Time.time - lastStamp, 0.06f) * simRate;
            Vector3 p = VisualsGlobals.ToVector3(local + vel * ahead);
            Vector3 g = GroundPointBelow(p);
            if ((p - new Vector3(0, -(float)PhysicalConstants.EarthRadius, 0)).sqrMagnitude < (g - new Vector3(0, -(float)PhysicalConstants.EarthRadius, 0)).sqrMagnitude)
            {
                p = g;
            }

            meteorPos = p;
            if (vel.Magnitude > 1e-3)
            {
                velocityDir = VisualsGlobals.ToVector3(vel.Normalized);
            }

            if (meteor != null)
            {
                meteor.transform.position = meteorPos;
            }
        }

        private readonly List<Vector3> lineBuffer = new List<Vector3>();

        private void UpdatePredictedLine(in MeteorState s)
        {
            if (predictedLine == null) return;
            lineBuffer.Clear();
            lineBuffer.Add(meteorPos + velocityDir * meteor.Radius * 30f);
            for (int i = 0; i < predictor.Points.Count; i++)
            {
                if (predictor.Times[i] > s.Time + 0.3)
                {
                    lineBuffer.Add(VisualsGlobals.ToVector3(frame.ToLocal(predictor.Points[i])));
                }
            }

            predictedLine.positionCount = lineBuffer.Count;
            predictedLine.SetPositions(lineBuffer.ToArray());
            float camDist = Vector3.Distance(CameraPosition, meteorPos);
            predictedLine.widthMultiplier = Mathf.Max(2f, camDist * 0.004f);
            predictedLine.enabled = lineBuffer.Count > 1 && mode != 1 && !ended;
        }

        private void PositionCameras(float dt)
        {
            float r = Mathf.Max(meteor.Radius, meteor.SheathRadius * 0.8f);
            float b = meteor.Brightness;
            Vector3 up = (meteorPos - new Vector3(0, -(float)PhysicalConstants.EarthRadius, 0)).normalized;
            Vector3 fwd = velocityDir;
            Vector3 side = Vector3.Cross(up, fwd);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(Vector3.forward, fwd);
            side.Normalize();
            Vector3 camUp = Vector3.Cross(fwd, side).normalized;

            // chase: just ahead of and beside the fireball, looking back along its train
            float chaseDist = 16f * r + 12f;
            Vector3 chasePos = meteorPos + fwd * chaseDist * 0.35f + side * chaseDist + camUp * chaseDist * 0.3f;
            vcams[0].transform.SetPositionAndRotation(chasePos, Quaternion.LookRotation(meteorPos - fwd * chaseDist * 0.8f - chasePos, up));

            // ground observer: fixed spot, tracking the fireball but keeping some horizon in view
            smoothedLook = Vector3.Lerp(smoothedLook, ended ? endFocus : meteorPos, 1f - Mathf.Exp(-dt * (ended ? 2f : 6f)));
            Vector3 toMeteor = (smoothedLook - observerPos).normalized;
            Vector3 horizon = new Vector3(toMeteor.x, 0f, toMeteor.z).normalized;
            Vector3 look = Vector3.Slerp(toMeteor, horizon, ended ? 0.5f : 0.22f);
            vcams[1].transform.SetPositionAndRotation(observerPos, Quaternion.LookRotation(look, Vector3.up));

            // side view: alongside at a distance that grows with the fireball
            float d = Mathf.Max(150f, 60f * r) * (1f + 0.5f * b);
            Vector3 sidePos = meteorPos + side * d + up * d * 0.12f - fwd * d * 0.35f;
            vcams[2].transform.SetPositionAndRotation(sidePos, Quaternion.LookRotation(meteorPos + fwd * d * 0.25f - sidePos, up));

            // keep the far camera's near plane sensible for its height
            float camAlt = (float)(CameraPosition - new Vector3(0, -(float)PhysicalConstants.EarthRadius, 0)).magnitude - (float)PhysicalConstants.EarthRadius;
            VisualsGlobals.SetCameraAltitude(Mathf.Max(0f, camAlt));
            float near = Mathf.Clamp(r * 0.2f, 0.05f, 2f);
            foreach (CinemachineVirtualCamera vc in vcams)
            {
                vc.m_Lens.NearClipPlane = vc == vcams[1] ? Mathf.Max(near, 1f) : near;
                vc.m_Lens.FarClipPlane = 1.5e6f;
            }
        }

        private void OnCameraUpdated(CinemachineBrain brain)
        {
            if (farCamera == null || mainCamera == null || brain == null || brain.OutputCamera != mainCamera)
            {
                return;
            }

            SyncFarCamera();
        }

        private void LateUpdate()
        {
            SyncFarCamera();
        }

        private void SyncFarCamera()
        {
            Transform m = mainCamera.transform;
            farCamera.transform.SetPositionAndRotation(m.position, m.rotation);
            farCamera.fieldOfView = mainCamera.fieldOfView;
            float camAlt = (float)(m.position - new Vector3(0, -(float)PhysicalConstants.EarthRadius, 0)).magnitude - (float)PhysicalConstants.EarthRadius;
            farCamera.nearClipPlane = Mathf.Clamp(camAlt * 0.02f, 0.5f, 400f);
            farCamera.farClipPlane = 4.0e6f;
        }
    }
}
