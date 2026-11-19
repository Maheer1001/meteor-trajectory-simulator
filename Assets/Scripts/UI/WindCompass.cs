using MTS.Core;
using MTS.Core.Display;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MTS.Hud
{
    /// <summary>
    /// 2D wind control (north up, east right): drag from the centre to set the direction the wind blows towards
    /// and its speed (distance from the centre). Writes to <see cref="SimulationSession.SetWind"/> using the
    /// azimuth-fraction convention of <see cref="Geo.WindVector"/> (see <see cref="WindCompassMath"/>).
    /// </summary>
    public sealed class WindCompass : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public const float MaxSpeed = 100f;

        public RectTransform area;
        public RectTransform needle;
        public RectTransform knob;
        public TMP_Text readout;
        public Button calmButton;
        public Button resetButton;

        private void Start()
        {
            if (calmButton != null) calmButton.onClick.AddListener(() => Apply(0, 0));
            if (resetButton != null)
            {
                resetButton.onClick.AddListener(() => Apply(SimulationSession.DefaultWindSpeed, SimulationSession.DefaultWindDirection));
            }

            Refresh();
        }

        private float Radius => area != null ? Mathf.Min(area.rect.width, area.rect.height) * 0.5f : 1f;

        public void OnPointerDown(PointerEventData eventData) => SetFromPointer(eventData);

        public void OnDrag(PointerEventData eventData) => SetFromPointer(eventData);

        private void SetFromPointer(PointerEventData eventData)
        {
            if (area == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            Vector2 centre = area.rect.center;
            Vector2 offset = local - centre;
            WindCompassMath.FromOffset(offset.x, offset.y, Radius, MaxSpeed, out double speed, out double fraction);
            speed = System.Math.Round(speed); // whole m/s, like the old slider
            Apply(speed, fraction);
        }

        public void Apply(double speed, double azimuthFraction)
        {
            SimulationSession.Instance.SetWind(speed, azimuthFraction);
            Refresh();
        }

        /// <summary>Redraws the needle and readout from the session's wind.</summary>
        public void Refresh()
        {
            SimulationSession session = SimulationSession.Instance;
            double speed = session.WindSpeed;
            double fraction = session.WindDirection;
            WindCompassMath.ToOffset(speed, fraction, Radius, MaxSpeed, out double east, out double north);
            var tip = new Vector2((float)east, (float)north);
            float length = tip.magnitude;

            if (needle != null)
            {
                needle.sizeDelta = new Vector2(length, needle.sizeDelta.y);
                needle.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(tip.y, tip.x) * Mathf.Rad2Deg);
                needle.gameObject.SetActive(length > 0.5f);
            }

            if (knob != null) knob.anchoredPosition = tip;

            if (readout != null)
            {
                if (speed <= 0)
                {
                    readout.text = "Calm";
                }
                else
                {
                    double bearing = WindCompassMath.BearingTowards(fraction);
                    readout.text = UnitFormat.Speed(speed) + " to " + bearing.ToString("000", System.Globalization.CultureInfo.InvariantCulture) +
                                   "° " + WindCompassMath.CardinalName(bearing);
                }
            }
        }
    }
}
