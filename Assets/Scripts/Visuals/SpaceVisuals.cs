using System.Collections.Generic;
using MTS.Core;
using UnityEngine;

namespace MTS.Visuals
{
    /// <summary>
    /// Space-stage visuals at true scale with a two-camera stack and a floating origin.
    /// The far camera sits at the origin of a kilometre-scale world holding the Earth, its atmosphere, the paths and
    /// the meteor marker; the near camera renders the metre-scale meteor, which always stays at the origin of its
    /// own world. Both share one double-precision camera pose, so nothing ever loses float precision.
    /// Reads the session only; never writes simulation state.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class SpaceVisuals : MonoBehaviour
    {
        public VisualsLibrary library;
        public Camera farCamera;
        public Camera nearCamera;
        public Light sun;
        [Tooltip("Sun colour (gamma) and intensity used by the MTS shaders.")]
        public Color sunColor = new Color(1f, 0.97f, 0.92f);
        public float sunIntensity = 1.25f;

        public static readonly string[] CameraNames = { "Meteor close-up", "Earth and meteor", "Trajectory overview" };

        private const double KmPerUnit = 1000.0;
        private const float EarthRadiusKm = (float)(PhysicalConstants.EarthRadius / KmPerUnit);

        private SimulationRunner runner;
        private Transform earthRoot;
        private Transform marker;
        private Material markerMat;
        private Transform impactMarker;
        private LineRenderer pastLine;
        private LineRenderer futureLine;
        private MeteorVisual meteor;
        private PathPredictor predictor;
        private readonly List<Vector3> pastPoints = new List<Vector3>();
        private Vec3d lastPastSample;
        private Vector3 sunDisplay;
        private int mode;
        private float yaw, pitch, zoom = 1f;
        private Vec3d camPos;
        private Quaternion camRot = Quaternion.identity;
        private Vec3d blendFromPos;
        private Quaternion blendFromRot;
        private float blend = 1f;
        private bool initialised;

        /// <summary>Current camera mode index (0..2).</summary>
        public int Mode => mode;

        private void OnEnable()
        {
            SimulationSignals.CameraRequested += OnCameraRequested;
            SimulationSignals.PublishCameras(CameraNames);
            SafeFrameCamera.Attach(farCamera);
            SafeFrameCamera.Attach(nearCamera);
        }

        private void OnDisable()
        {
            SimulationSignals.CameraRequested -= OnCameraRequested;
        }

        private void OnCameraRequested(int index) => SetMode(index);

        /// <summary>Switches camera with a smooth move from the current pose.</summary>
        public void SetMode(int index)
        {
            if (index < 0 || index >= CameraNames.Length || index == mode)
            {
                SimulationSignals.ReportCameraChanged(mode, CameraNames[mode]);
                return;
            }

            blendFromPos = camPos;
            blendFromRot = camRot;
            blend = 0f;
            mode = index;
            SimulationSignals.ReportCameraChanged(mode, CameraNames[mode]);
        }

        /// <summary>Sets the close-up orbit angles and zoom (used by tooling and input).</summary>
        public void SetOrbit(float newYaw, float newPitch, float newZoom)
        {
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, -80f, 80f);
            zoom = Mathf.Clamp(newZoom, 0.3f, 20f);
        }

        /// <summary>Finishes any camera move immediately.</summary>
        public void SnapCamera() => blend = 1f;

        // A new run can start while the scene is open (restart, tooling): rebuild from scratch.
        private void Teardown()
        {
            if (earthRoot != null) Destroy(earthRoot.gameObject);
            if (marker != null) Destroy(marker.gameObject);
            if (meteor != null) Destroy(meteor.gameObject);
            pastPoints.Clear();
            initialised = false;
        }

        private void Initialise(SimulationRunner r)
        {
            runner = r;
            initialised = true;
            MeteorProperties props = SimulationSession.Instance.Properties ?? r.Properties;

            // Full prediction up front (cheap: < 200k steps in vacuum) so the Sun can be placed for the whole run.
            predictor = new PathPredictor(props, r.State, SimulationSession.Instance.WindSpeed, SimulationSession.Instance.WindDirection, 20000.0, 3000);
            predictor.RunToEnd();
            Vec3d end = predictor.Points[predictor.Points.Count - 1];
            Vec3d sunInertial = VisualsRunCache.SunFor(r, end);
            sunDisplay = VisualsGlobals.ToVector3(VisualFrame.ToDisplay(sunInertial));

            BuildFarWorld();
            float radius = (float)r.State.EquivalentRadius(props.Material.Density);
            meteor = MeteorVisual.Create(library, transform, VisualLayers.Near, false, radius, 7);
            meteor.transform.SetParent(null, false);
            meteor.transform.position = Vector3.zero;

            lastPastSample = r.State.Position;
            pastPoints.Add(ToEarthLocal(r.State.Position));
            UpdateFutureLine(r.State);
            ComputeTargetPose(r.State, out camPos, out camRot);
            SimulationSignals.ReportCameraChanged(mode, CameraNames[mode]);
        }

        private void BuildFarWorld()
        {
            earthRoot = new GameObject("Earth (visual, km scale)").transform;
            earthRoot.gameObject.layer = VisualLayers.Far;

            var globe = new GameObject("Globe") { layer = VisualLayers.Far };
            globe.transform.SetParent(earthRoot, false);
            globe.transform.localScale = Vector3.one * EarthRadiusKm;
            globe.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Icosphere(6);
            globe.AddComponent<MeshRenderer>().sharedMaterial = library.earth;

            var shell = new GameObject("Atmosphere") { layer = VisualLayers.Far };
            shell.transform.SetParent(earthRoot, false);
            float atmoTop = EarthRadiusKm + 150f;
            shell.transform.localScale = Vector3.one * (atmoTop + 5f);
            shell.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Icosphere(4);
            var shellMat = new Material(library.atmosphereShell);
            shellMat.SetFloat("_PlanetRadius", EarthRadiusKm);
            shellMat.SetFloat("_AtmosphereRadius", atmoTop);
            shell.AddComponent<MeshRenderer>().sharedMaterial = shellMat;

            pastLine = NewLine("Flown path", library.pathPast);
            futureLine = NewLine("Predicted path", library.pathFuture);
            futureLine.textureMode = LineTextureMode.Tile;

            marker = NewGlow("Meteor marker", out markerMat, new Color(1f, 0.75f, 0.45f), 2.5f);
            impactMarker = NewGlow("Predicted end", out Material endMat, new Color(1f, 0.3f, 0.2f), 1.5f);
            impactMarker.SetParent(earthRoot, false);
            impactMarker.localPosition = ToEarthLocal(predictor.Points[predictor.Points.Count - 1]);
        }

        private LineRenderer NewLine(string name, Material mat)
        {
            var go = new GameObject(name) { layer = VisualLayers.Far };
            go.transform.SetParent(earthRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = mat;
            lr.positionCount = 0;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        private Transform NewGlow(string name, out Material mat, Color color, float intensity)
        {
            var go = new GameObject(name) { layer = VisualLayers.Far };
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Quad();
            mat = new Material(library.marker);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Intensity", intensity);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        // Display-axis position in kilometres relative to the Earth's centre (for children of earthRoot).
        private static Vector3 ToEarthLocal(Vec3d inertial) =>
            VisualsGlobals.ToVector3(VisualFrame.ToDisplay(inertial) / KmPerUnit);

        private void LateUpdate()
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

            HandleInput();
            MeteorState s = r.State;
            VisualsGlobals.SetSun(sunDisplay, sunColor, sunIntensity);
            VisualsGlobals.SetAsh(0f);
            VisualsGlobals.SetEarthRotation(s.Time);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunDisplay);
            }

            RecordPast(s);
            UpdateFutureLine(s);

            ComputeTargetPose(s, out Vec3d targetPos, out Quaternion targetRot);
            if (blend < 1f)
            {
                blend = Mathf.Min(1f, blend + Time.unscaledDeltaTime / 1.6f);
                float k = Mathf.SmoothStep(0f, 1f, blend);
                // move in log-distance from the meteor so zooming out feels even
                Vec3d m = VisualFrame.ToDisplay(s.Position);
                Vec3d a = blendFromPos - m, b = targetPos - m;
                double la = System.Math.Log(System.Math.Max(a.Magnitude, 1.0)), lb = System.Math.Log(System.Math.Max(b.Magnitude, 1.0));
                double len = System.Math.Exp(la + (lb - la) * k);
                Vec3d dir = Vec3d.Lerp(a.Normalized, b.Normalized, k).Normalized;
                camPos = m + dir * len;
                camRot = Quaternion.Slerp(blendFromRot, targetRot, k);
            }
            else
            {
                camPos = targetPos;
                camRot = targetRot;
            }

            ApplyPose(s);
            meteor.EnvironmentUp = VisualsGlobals.ToVector3(VisualFrame.ToDisplay(s.Position)).normalized;
            meteor.Apply(r.Telemetry, VisualsGlobals.ToVector3(VisualFrame.ToDisplay(s.Velocity)).normalized, (float)s.Speed, s.PancakeDiameter, Time.deltaTime);
        }

        private void ApplyPose(in MeteorState s)
        {
            Vec3d m = VisualFrame.ToDisplay(s.Position);
            Vec3d rel = camPos - m;
            nearCamera.transform.SetPositionAndRotation(VisualsGlobals.ToVector3(rel), camRot);
            farCamera.transform.SetPositionAndRotation(Vector3.zero, camRot);
            farCamera.fieldOfView = nearCamera.fieldOfView;
            nearCamera.nearClipPlane = Mathf.Max(0.02f, meteor.Radius * 0.1f);
            nearCamera.farClipPlane = 200000f;

            earthRoot.position = VisualsGlobals.ToVector3(-camPos / KmPerUnit);
            Vector3 meteorKm = VisualsGlobals.ToVector3((m - camPos) / KmPerUnit);
            marker.position = meteorKm;
            float distKm = meteorKm.magnitude;
            float pixelSize = 2f * Mathf.Tan(farCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, farCamera.pixelHeight * SafeFrameCamera.HeightFraction);
            marker.localScale = Vector3.one * Mathf.Max(distKm * pixelSize * 34f, 0.001f);
            marker.gameObject.SetActive(distKm > 50f);
            float endDist = (impactMarker.position).magnitude;
            impactMarker.localScale = Vector3.one * Mathf.Max(endDist * pixelSize * 22f, 0.01f);
            // Constant on-screen width: a line's width is in world units, so a path passing near the camera would
            // otherwise become a huge beam. Width keys follow each part's distance from the camera.
            ScreenWidth(pastLine, pixelSize * 2.4f);
            ScreenWidth(futureLine, pixelSize * 1.9f);
            // In the close-up the camera sits on the path itself, where a screen-sized line would become a wall.
            bool showPaths = mode != 0 || blend < 0.5f;
            pastLine.enabled = showPaths;
            futureLine.enabled = showPaths;
            impactMarker.gameObject.SetActive(showPaths);
        }

        private readonly Keyframe[] widthKeys = new Keyframe[24];
        private readonly AnimationCurve widthCurve = new AnimationCurve();

        /// <summary>Sets a line's width curve so it is about <paramref name="radiansWide"/> wide on screen everywhere.</summary>
        private void ScreenWidth(LineRenderer line, float radiansWide)
        {
            int n = line.positionCount;
            if (n < 2) return;
            Transform t = line.transform;
            float total = 0f;
            Vector3 prev = t.TransformPoint(line.GetPosition(0));
            var cumulative = new float[n];
            var dist = new float[n];
            dist[0] = prev.magnitude; // the far camera sits at the origin
            for (int i = 1; i < n; i++)
            {
                Vector3 p = t.TransformPoint(line.GetPosition(i));
                total += Vector3.Distance(prev, p);
                cumulative[i] = total;
                dist[i] = p.magnitude;
                prev = p;
            }

            if (!(total > 0f)) return;
            int keys = widthKeys.Length;
            int j = 0;
            for (int k = 0; k < keys; k++)
            {
                float u = k / (keys - 1f);
                float target = u * total;
                while (j < n - 2 && cumulative[j + 1] < target) j++;
                float seg = cumulative[j + 1] - cumulative[j];
                float f = seg > 0f ? Mathf.Clamp01((target - cumulative[j]) / seg) : 0f;
                float d = Mathf.Lerp(dist[j], dist[j + 1], f);
                // the nearest stretch decides the key, so a vertex right beside the camera never balloons
                widthKeys[k] = new Keyframe(u, Mathf.Max(d * radiansWide, 1e-4f));
            }

            widthCurve.keys = widthKeys;
            for (int k = 0; k < keys; k++) widthCurve.SmoothTangents(k, 0f);
            line.widthCurve = widthCurve;
            line.widthMultiplier = 1f;
        }

        private void RecordPast(in MeteorState s)
        {
            double spacing = System.Math.Max(5000.0, s.Altitude * 0.004);
            if ((s.Position - lastPastSample).Magnitude > spacing)
            {
                lastPastSample = s.Position;
                pastPoints.Add(ToEarthLocal(s.Position));
                if (pastPoints.Count > 3000)
                {
                    for (int i = pastPoints.Count - 2; i > 0; i -= 2) pastPoints.RemoveAt(i);
                }
            }

            int n = pastPoints.Count + 1;
            if (pastLine.positionCount != n) pastLine.positionCount = n;
            for (int i = 0; i < pastPoints.Count; i++) pastLine.SetPosition(i, pastPoints[i]);
            pastLine.SetPosition(n - 1, ToEarthLocal(s.Position));
        }

        private readonly List<Vector3> futureBuffer = new List<Vector3>();

        private void UpdateFutureLine(in MeteorState s)
        {
            futureBuffer.Clear();
            futureBuffer.Add(ToEarthLocal(s.Position));
            for (int i = 0; i < predictor.Points.Count; i++)
            {
                if (predictor.Times[i] > s.Time)
                {
                    futureBuffer.Add(ToEarthLocal(predictor.Points[i]));
                }
            }

            futureLine.positionCount = futureBuffer.Count;
            futureLine.SetPositions(futureBuffer.ToArray());
        }

        private void HandleInput()
        {
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 4f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 4f, -80f, 80f);
            }

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                zoom = Mathf.Clamp(zoom * Mathf.Pow(0.88f, wheel), 0.3f, 20f);
            }
        }

        /// <summary>Camera pose (display axes, metres, Earth-centred) for the current mode.</summary>
        private void ComputeTargetPose(in MeteorState s, out Vec3d pos, out Quaternion rot)
        {
            Vec3d m = VisualFrame.ToDisplay(s.Position);
            Vec3d toEarth = (-m).Normalized;
            Vector3 toEarthF = VisualsGlobals.ToVector3(toEarth);
            Vector3 side = Vector3.Cross(toEarthF, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(toEarthF, Vector3.right);
            side.Normalize();
            Quaternion basis = Quaternion.LookRotation(toEarthF, Vector3.Cross(side, toEarthF));
            double earthR = PhysicalConstants.EarthRadius;

            switch (mode)
            {
                case 0:
                {
                    // Close-up: look slightly down past the rock so the Earth's limb sits in the upper part of the
                    // frame behind it, side-lit by the Sun; yaw/pitch orbit around that.
                    double dist = System.Math.Max(meteor != null ? meteor.Radius : 1.0, 0.5) * 6.5 * zoom;
                    Vector3 radialUp = -toEarthF;
                    double alt = System.Math.Max(1000.0, m.Magnitude - earthR);
                    float limbDip = (float)(System.Math.Acos(earthR / (earthR + alt)) * 180.0 / System.Math.PI);
                    float vfov = nearCamera != null ? nearCamera.fieldOfView : 50f;
                    float dip = Mathf.Clamp(limbDip + 0.22f * vfov + pitch, -60f, 88f);
                    Vector3 sunH = Vector3.ProjectOnPlane(sunDisplay.normalized, radialUp);
                    if (sunH.sqrMagnitude < 1e-4f) sunH = side;
                    sunH.Normalize();
                    Vector3 horiz = Quaternion.AngleAxis(105f + yaw, radialUp) * sunH;
                    Vector3 view = (horiz * Mathf.Cos(dip * Mathf.Deg2Rad) - radialUp * Mathf.Sin(dip * Mathf.Deg2Rad)).normalized;
                    pos = m - ToVec(view) * dist;
                    rot = Quaternion.LookRotation(view, radialUp);
                    break;
                }
                case 1:
                {
                    // Earth and meteor: from higher up and behind, the meteor marker in the lower-middle of the
                    // frame against the planet, the curved limb across the upper part.
                    double alt = System.Math.Max(1000.0, m.Magnitude - earthR);
                    Vector3 radialUp = -toEarthF;
                    float vfov = farCamera != null ? farCamera.fieldOfView : 50f;
                    Vector3 sunH = Vector3.ProjectOnPlane(sunDisplay.normalized, radialUp);
                    if (sunH.sqrMagnitude < 1e-4f) sunH = side;
                    sunH.Normalize();
                    Vector3 horiz = Quaternion.AngleAxis(105f + yaw * 0.5f, radialUp) * sunH;
                    double d = System.Math.Max(System.Math.Min(alt * 1.3, 1.2e7), 700000.0) * zoom;
                    // the camera's own altitude sets where the limb is; two passes settle it
                    float meteorDip = 40f, viewDip = 30f;
                    for (int it = 0; it < 3; it++)
                    {
                        double camAlt = alt + d * System.Math.Sin(meteorDip * Mathf.Deg2Rad);
                        float limbDip = (float)(System.Math.Acos(earthR / (earthR + camAlt)) * 180.0 / System.Math.PI);
                        viewDip = Mathf.Clamp(limbDip + 0.26f * vfov + pitch * 0.5f, 5f, 85f);
                        meteorDip = Mathf.Clamp(viewDip + 0.14f * vfov, 5f, 88f);
                    }

                    Vector3 toMeteor = (horiz * Mathf.Cos(meteorDip * Mathf.Deg2Rad) - radialUp * Mathf.Sin(meteorDip * Mathf.Deg2Rad)).normalized;
                    Vector3 look = (horiz * Mathf.Cos(viewDip * Mathf.Deg2Rad) - radialUp * Mathf.Sin(viewDip * Mathf.Deg2Rad)).normalized;
                    pos = m - ToVec(toMeteor) * d;
                    rot = Quaternion.LookRotation(look, radialUp);
                    break;
                }
                default:
                {
                    // Fit the Earth and the meteor, looking down on the path's plane from the sunlit side.
                    Vec3d c = m * 0.5;
                    double rad = System.Math.Max(earthR * 1.3, m.Magnitude * 0.5 + earthR * 0.6);
                    Vector3 vel = VisualsGlobals.ToVector3(VisualFrame.ToDisplay(s.Velocity));
                    Vector3 n = Vector3.Cross(toEarthF, vel.normalized);
                    if (n.sqrMagnitude < 1e-4f) n = side;
                    n.Normalize();
                    if (Vector3.Dot(n, sunDisplay) < 0f) n = -n;
                    Vector3 offset = (n + sunDisplay.normalized * 0.45f + (-toEarthF) * 0.2f).normalized;
                    offset = Quaternion.AngleAxis(yaw * 0.3f, n) * offset;
                    double dist = rad / System.Math.Tan(farCamera.fieldOfView * 0.5 * System.Math.PI / 180.0) * 1.22 * zoom;
                    pos = c + ToVec(offset) * dist;
                    Vector3 upHint = Vector3.Cross(offset, Vector3.Cross(-toEarthF, offset));
                    if (upHint.sqrMagnitude < 1e-4f) upHint = Vector3.up;
                    rot = Quaternion.LookRotation(VisualsGlobals.ToVector3((c - pos).Normalized), upHint);
                    break;
                }
            }
        }

        private static Vec3d ToVec(Vector3 v) => new Vec3d(v.x, v.y, v.z);
    }
}
