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

        private GameObject globe;
        private float chaseFov = 60f, observerFov = 50f, sideFov = 60f;
        private Vector3 endGroundZero, endHoriz;
        private float endNearDist, endFarDist, endElevNear, endElevFar, endLookHeight;
        private float endProgress;
        private bool endGroundMatters;

        /// <summary>Dust and ash after a large impact (0..1): dims the Sun and browns the sky. Set by the end sequence.</summary>
        public float Ash { get; set; }

        /// <summary>The fireball (with its effects) as currently drawn.</summary>
        public MeteorVisual Meteor => meteor;

        public Vector3 MeteorWorldPosition => meteorPos;
        public Vector3 CameraPosition => mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        public LocalFrame Frame => frame;
        public int Mode => mode;

        private void OnEnable()
        {
            SimulationSignals.CameraRequested += SetMode;
            SimulationSignals.PublishCameras(CameraNames);
            SafeFrameCamera.Attach(farCamera);
            SafeFrameCamera.Attach(mainCamera);
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
            float radius = (float)r.State.EquivalentRadius(props.Material.Density);
            // a city-sized body needs a far more distant observer to fit in the sky (and to survive the view)
            float observerScale = Mathf.Max(1f, radius * 40f / observerSideDistance);
            Vector3 obs = side * observerSideDistance * observerScale - trackDir * observerBackDistance * observerScale;
            // on a rise a few hundred metres up: from eye height the horizon is only ~5 km away and the view is all
            // foreground; from here it shows tens of km of hazy landscape under the fireball
            observerPos = GroundPointBelow(obs) + (GroundPointBelow(obs) - EarthCentre).normalized * 300f;
            Ash = 0f;
            VisualsGlobals.SetAsh(0f);

            BuildWorld();
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

            // The rest of the planet, for the orbital end views of continent-scale events: the same ground shader
            // on a whole sphere, sunk a little below the detailed patch so the patch always wins where both exist.
            var gl = new GameObject("Globe (visual, orbital views)") { layer = VisualLayers.Far };
            gl.transform.SetParent(g.transform, false);
            gl.transform.localPosition = new Vector3(0f, -(float)PhysicalConstants.EarthRadius, 0f);
            gl.transform.localScale = Vector3.one * ((float)PhysicalConstants.EarthRadius - 1500f);
            gl.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Icosphere(6);
            gl.AddComponent<MeshRenderer>().sharedMaterial = library.ground;
            globe = gl;
            globe.SetActive(false);

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
            float dim = 1f - 0.8f * Mathf.Clamp01(Ash);
            VisualsGlobals.SetSun(sunLocal, sunColor * Mathf.Lerp(1f, 0.8f, Ash), sunIntensity * dim);
            VisualsGlobals.SetAsh(Ash);
            VisualsGlobals.SetEarthRotation(s.Time);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunLocal);
                sun.intensity = Mathf.Clamp01(sunLocal.y * 4f + 0.2f) * 1.1f * dim;
            }

            if (!ended)
            {
                FlightTelemetry t = r.Telemetry;
                meteor.CameraDistance = Vector3.Distance(CameraPosition, meteorPos);
                meteor.Apply(t, velocityDir, (float)s.Speed, s.PancakeDiameter, dt);
                // (the HUD announces break-up itself from the telemetry)
                wasBroken |= t.Broken;

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
        /// <param name="focus">The event point: the burst point for airbursts, ground zero (plus a little) for impacts.</param>
        /// <param name="nearRadius">Radius (m) of what to frame first (fireball, crater, glow).</param>
        /// <param name="farRadius">Radius (m) to pull back to by the end of the sequence (blast rings, dust).</param>
        public void OnRunEnded(EndReason reason, Vector3 focus, float nearRadius, float farRadius)
        {
            ended = true;
            endFocus = focus;
            meteor.Flash(1.5f);
            meteor.Extinguish();
            predictedLine.enabled = false;

            // From the observer's side of ground zero, elevated; small events close, continent-scale ones from orbit.
            endGroundZero = GroundPointBelow(focus);
            Vector3 horiz = observerPos - endGroundZero;
            horiz.y = 0f;
            endHoriz = horiz.sqrMagnitude > 1f ? horiz.normalized : -trackDir;
            float burstHeight = Mathf.Max(0f, (focus - endGroundZero).magnitude);
            const float fov = 55f;
            endNearDist = (float)ImpactVisualScale.EndCameraDistance(nearRadius, 0.0, fov, 60.0, 3.0e6);
            endFarDist = Mathf.Max(endNearDist, (float)ImpactVisualScale.EndCameraDistance(farRadius, burstHeight * 0.6, fov, 60.0, 5.0e6));
            endElevNear = Mathf.Lerp(12f, 30f, Mathf.InverseLerp(2000f, 200000f, endNearDist));
            endElevFar = Mathf.Lerp(18f, 62f, Mathf.InverseLerp(20000f, 2.5e6f, endFarDist));
            endLookHeight = burstHeight;
            // only drop the aim to the ground when something there (rings, scar) is bigger than the burst height
            endGroundMatters = farRadius > burstHeight * 0.5f;
            endProgress = 0f;
            vcams[3].m_Lens.FieldOfView = fov;
            vcams[3].Priority = 30;
            PlaceEndCamera();
        }

        /// <summary>Moves the end camera from the close framing (0) to the wide one (1).</summary>
        public void SetEndProgress(float k)
        {
            endProgress = Mathf.Clamp01(k);
            if (ended) PlaceEndCamera();
        }

        private void PlaceEndCamera()
        {
            float k = Mathf.SmoothStep(0f, 1f, endProgress);
            float dist = Mathf.Exp(Mathf.Lerp(Mathf.Log(endNearDist), Mathf.Log(endFarDist), k));
            float elev = Mathf.Lerp(endElevNear, endElevFar, k) * Mathf.Deg2Rad;
            // the point of interest: the burst high up when close, ground zero when wide
            float lookHeight = endGroundMatters ? Mathf.Lerp(endLookHeight, Mathf.Min(endLookHeight, dist * 0.08f), k) : endLookHeight;
            Vector3 target = endGroundZero + Vector3.up * lookHeight;
            Vector3 pos = target + (endHoriz * Mathf.Cos(elev) + Vector3.up * Mathf.Sin(elev)) * dist;
            pos = KeepAboveGround(pos, 40f + dist * 0.01f);
            Vector3 up = (pos - EarthCentre).normalized;
            vcams[3].transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, up));
            vcams[3].m_Lens.NearClipPlane = Mathf.Clamp(dist * 0.002f, 0.1f, 500f);
            vcams[3].m_Lens.FarClipPlane = Mathf.Max(1.5e6f, dist * 4f);
        }

        private static readonly Vector3 EarthCentre = new Vector3(0f, -(float)PhysicalConstants.EarthRadius, 0f);

        private static float AltitudeOf(Vector3 p) => (p - EarthCentre).magnitude - (float)PhysicalConstants.EarthRadius;

        private static Vector3 KeepAboveGround(Vector3 p, float minAltitude)
        {
            float alt = AltitudeOf(p);
            if (alt >= minAltitude) return p;
            return p + (p - EarthCentre).normalized * (minAltitude - alt);
        }

        // Keeps a camera out of the cloud deck: it flies above it when the target is above, below it otherwise.
        private Vector3 AvoidClouds(Vector3 p, Vector3 target)
        {
            float alt = AltitudeOf(p);
            float lo = cloudHeight - 250f, hi = cloudHeight + 450f;
            if (alt <= lo || alt >= hi) return p;
            float want = AltitudeOf(target) > cloudHeight ? hi : lo;
            return p + (p - EarthCentre).normalized * (want - alt);
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

            float smooth = 1f - Mathf.Exp(-dt * 3f);

            // Chase: beside and a little above the fireball, looking slightly down across it so the ground and the
            // horizon fill the background (not black space) and the train streams away diagonally. The distance
            // follows the body and its glow, so the rock reads at any size.
            float chaseDist = (float)ImpactVisualScale.ChaseDistance(meteor.Radius, meteor.SheathRadius, b);
            Vector3 chaseOffset = (side * 1.0f + up * 0.42f + fwd * 0.22f).normalized;
            Vector3 chasePos = meteorPos + chaseOffset * chaseDist;
            Vector3 chaseTarget = meteorPos - fwd * chaseDist * 0.18f;
            chasePos = AvoidClouds(KeepAboveGround(chasePos, 30f), meteorPos);
            Vector3 chaseUp = Vector3.ProjectOnPlane(up, (chaseTarget - chasePos).normalized);
            if (chaseUp.sqrMagnitude < 1e-4f) chaseUp = camUp;
            vcams[0].transform.SetPositionAndRotation(chasePos, Quaternion.LookRotation(chaseTarget - chasePos, chaseUp));
            chaseFov = Mathf.Lerp(chaseFov, 58f, smooth);
            vcams[0].m_Lens.FieldOfView = chaseFov;

            // Ground observer: fixed spot, leading the fireball a little; zooms with distance so the fireball stays
            // a readable size, and keeps the horizon low in the frame whenever the fireball allows.
            Vector3 lead = ended ? endFocus : meteorPos + velocityDir * Mathf.Min((float)runner.State.Speed * 0.35f, 6000f);
            smoothedLook = Vector3.Lerp(smoothedLook, lead, 1f - Mathf.Exp(-dt * (ended ? 2f : 5f)));
            Vector3 obsUp = (observerPos - EarthCentre).normalized;
            Vector3 toLook = smoothedLook - observerPos;
            float range = Mathf.Max(1f, toLook.magnitude);
            Vector3 flat = Vector3.ProjectOnPlane(toLook, obsUp);
            if (flat.sqrMagnitude < 1f) flat = Vector3.ProjectOnPlane(-observerPos, obsUp);
            if (flat.sqrMagnitude < 1f) flat = trackDir;
            flat.Normalize();
            float elevation = Mathf.Asin(Mathf.Clamp(Vector3.Dot(toLook / range, obsUp), -1f, 1f)) * Mathf.Rad2Deg;
            float span = Mathf.Max(4000f, meteor.SheathRadius * 60f, (float)runner.State.Speed * 1.2f);
            float wantFov = Mathf.Clamp(2f * Mathf.Atan(span / range) * Mathf.Rad2Deg, 22f, 62f);
            observerFov = Mathf.Lerp(observerFov, wantFov, smooth);
            // aim so the fireball sits a little above centre, but never tip the horizon out of the bottom of the view
            float aimElev = Mathf.Clamp(elevation - observerFov * 0.15f, -2f, 85f);
            if (elevation < observerFov * 0.75f) aimElev = Mathf.Min(aimElev, observerFov * 0.38f);
            Vector3 obsLook = flat * Mathf.Cos(aimElev * Mathf.Deg2Rad) + obsUp * Mathf.Sin(aimElev * Mathf.Deg2Rad);
            vcams[1].transform.SetPositionAndRotation(observerPos, Quaternion.LookRotation(obsLook, obsUp));
            vcams[1].m_Lens.FieldOfView = observerFov;

            // Side view: alongside at the fireball's height, far enough to show the glow and a stretch of train,
            // aiming a little ahead so the fireball leads into the frame and the horizon stays level.
            float d = Mathf.Max(32f * meteor.Radius + 60f, 7f * meteor.SheathRadius) * (1f + 1.6f * b);
            Vector3 flatSide = Vector3.ProjectOnPlane(side, up).normalized;
            if (flatSide.sqrMagnitude < 1e-4f) flatSide = side;
            Vector3 sidePos = meteorPos + flatSide * d + up * d * 0.1f - fwd * d * 0.25f;
            sidePos = AvoidClouds(KeepAboveGround(sidePos, 30f), meteorPos);
            Vector3 sideTarget = meteorPos + fwd * d * 0.22f;
            vcams[2].transform.SetPositionAndRotation(sidePos, Quaternion.LookRotation(sideTarget - sidePos, up));
            sideFov = Mathf.Lerp(sideFov, 55f, smooth);
            vcams[2].m_Lens.FieldOfView = sideFov;

            // keep the far camera's near plane sensible for its height
            float camAlt = AltitudeOf(CameraPosition);
            VisualsGlobals.SetCameraAltitude(Mathf.Max(0f, camAlt));
            if (globe != null) globe.SetActive(camAlt > 90000f);
            float near = Mathf.Clamp(meteor.Radius * 0.2f, 0.05f, 2f);
            for (int i = 0; i < 3; i++)
            {
                CinemachineVirtualCamera vc = vcams[i];
                vc.m_Lens.NearClipPlane = i == 1 ? Mathf.Clamp(range * 0.0005f, 1f, 50f) : Mathf.Clamp(Mathf.Min(near, chaseDist * 0.05f), 0.05f, 500f);
                vc.m_Lens.FarClipPlane = Mathf.Max(1.5e6f, d * 20f);
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
            farCamera.nearClipPlane = Mathf.Clamp(camAlt * 0.02f, 0.5f, 2000f);
            farCamera.farClipPlane = camAlt > 90000f ? 1.6e7f : 4.0e6f;
        }
    }
}
