using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MTS.Hud
{
    /// <summary>Single floating tooltip panel of a HUD canvas. Appears after a short hover, follows the pointer.</summary>
    public sealed class HudTooltip : MonoBehaviour
    {
        public const float Delay = 0.35f;

        public RectTransform panel;
        public TMP_Text label;
        public Canvas canvas;

        public static HudTooltip Instance { get; private set; }

        private TooltipTarget current;
        private float shownAt;

        private void Awake()
        {
            Instance = this;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(TooltipTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.text)) return;
            current = target;
            shownAt = Time.unscaledTime + Delay;
            label.text = target.text;
        }

        public void Hide(TooltipTarget target)
        {
            if (current != target) return;
            current = null;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (current == null || panel == null || canvas == null) return;
            if (Time.unscaledTime < shownAt) return;
            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);

            var canvasRect = (RectTransform)canvas.transform;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, cam, out Vector2 local))
            {
                return;
            }

            // Keep the tooltip on screen: flip to the left/below of the pointer near the right/top edges.
            Vector2 half = canvasRect.rect.size * 0.5f;
            Vector2 size = panel.rect.size;
            float x = local.x + 18f;
            float y = local.y - 18f;
            if (x + size.x > half.x) x = local.x - 18f - size.x;
            if (y - size.y < -half.y) y = local.y + 18f + size.y;
            panel.anchoredPosition = new Vector2(x, y);
        }
    }
}
