using UnityEngine;
using UnityEngine.EventSystems;

namespace MTS.Results
{
    /// <summary>Forwards pointer events from the map viewport to its <see cref="WorldMapView"/>.</summary>
    public sealed class WorldMapInput : MonoBehaviour, IDragHandler, IBeginDragHandler, IScrollHandler
    {
        public WorldMapView Target;

        public void OnBeginDrag(PointerEventData eventData) => Target?.OnBeginDrag(eventData);
        public void OnDrag(PointerEventData eventData) => Target?.OnDrag(eventData);
        public void OnScroll(PointerEventData eventData) => Target?.OnScroll(eventData);
    }
}
