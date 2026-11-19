using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Hud
{
    /// <summary>Shows the recent entries of <see cref="HudToasts"/> as a stack of fading cards.</summary>
    public sealed class ToastFeed : MonoBehaviour
    {
        public const int MaxVisible = 3;
        private const float FadeTime = 0.6f;

        public RectTransform container;
        /// <summary>Inactive card used as a template: Image (accent bar child "Accent"), TMP children "Title" and "Message".</summary>
        public GameObject template;

        private readonly List<(GameObject Card, CanvasGroup Group, float PostedAt)> cards = new List<(GameObject, CanvasGroup, float)>();
        private int shownVersion = -1;

        private void Update()
        {
            if (shownVersion != HudToasts.Version)
            {
                Rebuild();
            }

            float now = Time.unscaledTime;
            foreach ((GameObject card, CanvasGroup group, float postedAt) in cards)
            {
                float age = now - postedAt;
                float alpha = age < 0.15f ? age / 0.15f : Mathf.Clamp01((HudToasts.Lifetime - age) / FadeTime);
                group.alpha = alpha;
                if (card.activeSelf != alpha > 0f) card.SetActive(alpha > 0f);
            }
        }

        private void Rebuild()
        {
            shownVersion = HudToasts.Version;
            foreach ((GameObject card, CanvasGroup _, float _) in cards)
            {
                Destroy(card);
            }

            cards.Clear();
            if (template == null || container == null) return;

            float now = Time.unscaledTime;
            IReadOnlyList<HudToast> recent = HudToasts.Recent;
            int shown = 0;
            for (int i = recent.Count - 1; i >= 0 && shown < MaxVisible; i--)
            {
                HudToast toast = recent[i];
                if (now - toast.PostedAt > HudToasts.Lifetime) continue;
                GameObject card = Instantiate(template, container);
                card.name = "Toast";
                card.SetActive(true);
                card.transform.SetSiblingIndex(shown);
                TMP_Text title = card.transform.Find("Title")?.GetComponent<TMP_Text>();
                TMP_Text message = card.transform.Find("Message")?.GetComponent<TMP_Text>();
                Image accent = card.transform.Find("Accent")?.GetComponent<Image>();
                if (title != null) { title.text = toast.Title; title.color = toast.Accent; }
                if (message != null)
                {
                    message.text = toast.Message;
                    message.gameObject.SetActive(!string.IsNullOrEmpty(toast.Message));
                }

                if (accent != null) accent.color = toast.Accent;
                CanvasGroup group = card.GetComponent<CanvasGroup>();
                if (group == null) group = card.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                cards.Add((card, group, toast.PostedAt));
                shown++;
            }
        }
    }
}
