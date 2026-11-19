using UnityEngine;
using UnityEngine.EventSystems;

namespace MTS.Hud
{
    /// <summary>Marks a HUD element as having a tooltip; <see cref="HudTooltip"/> shows the text on hover.</summary>
    public sealed class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea] public string text;

        public void OnPointerEnter(PointerEventData eventData) => HudTooltip.Instance?.Show(this);

        public void OnPointerExit(PointerEventData eventData) => HudTooltip.Instance?.Hide(this);

        private void OnDisable() => HudTooltip.Instance?.Hide(this);
    }
}
