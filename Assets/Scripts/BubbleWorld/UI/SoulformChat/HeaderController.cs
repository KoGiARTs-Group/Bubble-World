using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    /// <summary>
    /// Top status bar. Renders persistent soulform info from a SoulformStatus
    /// data model: icon, name, type, mood, time, location, AP meter,
    /// relationship level. Each widget is optional and independently wired.
    /// </summary>
    public sealed class HeaderController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private RawImage soulformIcon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text typeLabel;

        [Header("Mood Widget")]
        [SerializeField] private TMP_Text moodLabel;
        [SerializeField] private Image moodIcon;

        [Header("Time Widget")]
        [SerializeField] private TMP_Text timeLabel;

        [Header("Location Widget")]
        [SerializeField] private TMP_Text locationLabel;

        [Header("AP Widget")]
        [SerializeField] private Slider apSlider;
        [SerializeField] private TMP_Text apLabel;

        [Header("Relationship Widget")]
        [SerializeField] private TMP_Text relationshipLabel;

        private SoulformStatus boundStatus;

        private void Awake()
        {
            if (apSlider != null)
            {
                apSlider.minValue = 0f;
                apSlider.maxValue = 1f;
                apSlider.value = 0f;
            }
        }

        /// <summary>Programmatic fallback wiring for scenes without a controller.</summary>
        public void Configure(TMP_Text nameText)
        {
            if (nameLabel == null && nameText != null) nameLabel = nameText;
        }

        /// <summary>Binds a status model and renders it immediately.</summary>
        public void Bind(SoulformStatus status)
        {
            boundStatus = status;
            Refresh();
        }

        public SoulformStatus BoundStatus => boundStatus;

        /// <summary>Re-renders all widgets from the bound status.</summary>
        public void Refresh()
        {
            if (boundStatus == null) return;

            if (nameLabel != null) nameLabel.text = boundStatus.Name;
            if (typeLabel != null) typeLabel.text = boundStatus.Type;
            if (moodLabel != null) moodLabel.text = boundStatus.Mood;
            if (timeLabel != null)
                timeLabel.text = string.IsNullOrEmpty(boundStatus.Season)
                    ? boundStatus.TimeOfDay
                    : boundStatus.Season + ", " + boundStatus.TimeOfDay;
            if (locationLabel != null) locationLabel.text = boundStatus.Location;
            if (relationshipLabel != null)
                relationshipLabel.text = "Lv " + boundStatus.RelationshipLevel;

            if (apSlider != null)
            {
                apSlider.minValue = 0f;
                apSlider.maxValue = Mathf.Max(1, boundStatus.MaxAP);
                apSlider.value = Mathf.Clamp(boundStatus.CurrentAP, 0, boundStatus.MaxAP);
            }
            if (apLabel != null)
                apLabel.text = boundStatus.CurrentAP + " / " + boundStatus.MaxAP;
        }

        /// <summary>Applies a partial status update without rebuilding the model.</summary>
        public void SetMood(string mood)
        {
            if (boundStatus == null) return;
            boundStatus.Mood = mood;
            if (moodLabel != null) moodLabel.text = mood;
        }

        public void SetAp(int current, int max)
        {
            if (boundStatus == null) return;
            boundStatus.CurrentAP = current;
            boundStatus.MaxAP = max;
            if (apSlider != null)
            {
                apSlider.minValue = 0f;
                apSlider.maxValue = Mathf.Max(1, max);
                apSlider.value = Mathf.Clamp(current, 0, max);
            }
            if (apLabel != null) apLabel.text = current + " / " + max;
        }
    }
}
