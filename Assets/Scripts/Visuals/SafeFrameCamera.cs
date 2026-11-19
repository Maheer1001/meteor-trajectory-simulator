using UnityEngine;

namespace MTS.Visuals
{
    /// <summary>
    /// Off-centre projection that fits the camera's view into the screen area the HUD leaves free
    /// (<see cref="SimulationSignals.SafeViewport"/>): what the camera framing aims at with its field of view lands
    /// inside that area instead of under the panels, and the rest of the frame fills the screen behind them.
    /// Added at runtime to every world camera of the simulation scenes. Changes are eased so hiding the HUD (H)
    /// or a panel resizing does not jump the view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SafeFrameCamera : MonoBehaviour
    {
        private const float EaseRate = 6f;

        private Camera cam;
        private Rect current = new Rect(0f, 0f, 1f, 1f);

        /// <summary>Height of the framed area as a fraction of the screen (1 = whole screen); for pixel-size maths.</summary>
        public static float HeightFraction { get; private set; } = 1f;

        public static void Attach(Camera camera)
        {
            if (camera != null && camera.GetComponent<SafeFrameCamera>() == null)
            {
                camera.gameObject.AddComponent<SafeFrameCamera>();
            }
        }

        private void Awake() => cam = GetComponent<Camera>();

        private void OnDisable()
        {
            if (cam != null) cam.ResetProjectionMatrix();
            HeightFraction = 1f;
        }

        private void OnPreCull()
        {
            Rect target = SimulationSignals.SafeViewport;
            float k = Application.isPlaying ? 1f - Mathf.Exp(-EaseRate * Time.unscaledDeltaTime) : 1f;
            current = new Rect(Mathf.Lerp(current.x, target.x, k), Mathf.Lerp(current.y, target.y, k),
                Mathf.Lerp(current.width, target.width, k), Mathf.Lerp(current.height, target.height, k));
            HeightFraction = current.height;

            if (current.width > 0.995f && current.height > 0.995f)
            {
                cam.ResetProjectionMatrix();
                return;
            }

            cam.projectionMatrix = Projection(cam.fieldOfView, cam.aspect, cam.nearClipPlane, cam.farClipPlane, current);
        }

        /// <summary>
        /// Perspective projection whose symmetric frustum of vertical angle <paramref name="fov"/> fills the viewport
        /// sub-rectangle <paramref name="safe"/> of a screen with aspect <paramref name="screenAspect"/>.
        /// </summary>
        public static Matrix4x4 Projection(float fov, float screenAspect, float near, float far, Rect safe)
        {
            float t = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float r = t * screenAspect * safe.width / safe.height;
            float fullW = 2f * r / safe.width;
            float fullH = 2f * t / safe.height;
            float left = -r - safe.x * fullW;
            float bottom = -t - safe.y * fullH;
            return Matrix4x4.Frustum(left, left + fullW, bottom, bottom + fullH, near, far);
        }
    }
}
