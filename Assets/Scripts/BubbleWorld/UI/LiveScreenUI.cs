using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BubbleWorld
{
    public sealed class LiveScreenUI : MonoBehaviour
    {
        [Header("Top Bar")]
        [SerializeField] private TextMeshProUGUI bubbleNameLabel;
        [SerializeField] private TextMeshProUGUI liveModeIndicator;
        [SerializeField] private TextMeshProUGUI timeLabel;
        [SerializeField] private TextMeshProUGUI dayLabel;
        [SerializeField] private TextMeshProUGUI dateLabel;
        [Tooltip("Optional. Auto-created below the time label when not assigned.")]
        [SerializeField] private TextMeshProUGUI apLabel;

        [SerializeField] private TextMeshProUGUI weatherLabel;
        [SerializeField] private TextMeshProUGUI temperatureLabel;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button speed1xButton;
        [SerializeField] private Button speed2xButton;
        [SerializeField] private Button speed4xButton;
        [SerializeField] private Button navigationHubButton;

        [Header("Event Log")]
        [SerializeField] private GameObject eventLogPanel;
        [SerializeField] private Transform eventLogContainer;
        [SerializeField] private GameObject eventLogEntryPrefab;
        [SerializeField] private Button viewFullLogButton;
        [SerializeField] private int maxEventHistory = 50;

        [Header("Minimap")]
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private RectTransform creatureMarkerPrefab;
        [SerializeField] private Transform creatureMarkerContainer;
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private Button searchButton;

        [Header("Camera Controls")]
        [SerializeField] private Button followCreatureButton;
        [SerializeField] private Button freeCameraButton;
        [SerializeField] private Button topViewButton;
        [SerializeField] private Button angledViewButton;

        [Header("Intervention Toolbar")]
        [SerializeField] private Button[] interventionButtons;

        [Header("Soulform Info")]
        [SerializeField] private Image soulformPortrait;
        [SerializeField] private TextMeshProUGUI soulformNameLabel;
        [SerializeField] private TextMeshProUGUI soulformSpeciesLabel;
        [SerializeField] private TextMeshProUGUI soulformEmotionLabel;
        [SerializeField] private TextMeshProUGUI soulformActivityLabel;
        [SerializeField] private TextMeshProUGUI soulformLocationLabel;
        [SerializeField] private Slider comfortBar;
        [SerializeField] private Slider hungerBar;
        [SerializeField] private Slider curiosityBar;
        [SerializeField] private TextMeshProUGUI soulformThoughtLabel;
        [SerializeField] private Button openProfileButton;
        [SerializeField] private Button talkObserveButton;

        [Header("World Status")]
        [SerializeField] private TextMeshProUGUI livabilityPercentLabel;
        [SerializeField] private TextMeshProUGUI livabilityRatingLabel;
        [SerializeField] private TextMeshProUGUI livabilityDescriptionLabel;
        [SerializeField] private Image livabilityFillBar;
        [SerializeField] private Button openLivabilityDetailsButton;
        [SerializeField] private Button closeLivabilityDetailsButton;
        [SerializeField] private GameObject livabilityDetailsPanel;
        [SerializeField] private TextMeshProUGUI livabilityDetailsLabel;
        [SerializeField] private Color ratingGoodColor = new Color(0.3f, 0.85f, 0.5f);
        [SerializeField] private Color ratingFairColor = new Color(0.63f, 0.83f, 0.41f);
        [SerializeField] private Color ratingMildColor = new Color(1f, 0.7f, 0.28f);
        [SerializeField] private Color ratingPoorColor = new Color(1f, 0.42f, 0.42f);

        private AudioManager audio;
        private readonly Dictionary<string, TextMeshProUGUI> labels = new Dictionary<string, TextMeshProUGUI>();
        private readonly List<GameObject> eventEntries = new List<GameObject>();
        private readonly List<RectTransform> creatureMarkers = new List<RectTransform>();
        private int creatureMarkerCount;

        public event Action<WatchSpeed> OnSpeedChanged;
        public event Action OnPauseToggled;
        public event Action<Camera3DView> OnCameraModeChanged;
        public event Action OnNavigationHub;
        public event Action<int> OnInterventionClicked;
        public event Action OnOpenProfile;
        public event Action OnTalkObserve;
        public event Action OnViewFullLog;
        public event Action OnOpenLivabilityDetails;
        public event Action OnCloseLivabilityDetails;
        public event Action<float> OnZoomIn;
        public event Action<float> OnZoomOut;
        public event Action OnSearch;

        private void Awake()
        {
            audio = FindObjectOfType<AudioManager>();
        }

        public void Build(WatchState watchState)
        {
            RegisterLabel("BubbleName", bubbleNameLabel);
            RegisterLabel("LiveModeIndicator", liveModeIndicator);
            RegisterLabel("Time", timeLabel);
            RegisterLabel("Day", dayLabel);
            RegisterLabel("Date", dateLabel);
            RegisterLabel("Weather", weatherLabel);
            RegisterLabel("Temperature", temperatureLabel);
            EnsureApLabel();
            RegisterLabel("AP", apLabel);
            if (livabilityFillBar != null)
            {
                livabilityFillBar.type = Image.Type.Filled;
                livabilityFillBar.fillMethod = Image.FillMethod.Horizontal;
            }

            BindButton(pauseButton, () => { if (audio != null) audio.PlaySelect(); OnPauseToggled?.Invoke(); });
            BindButton(speed1xButton, () => { if (audio != null) audio.PlaySwitch(); OnSpeedChanged?.Invoke(WatchSpeed.Normal); });
            BindButton(speed2xButton, () => { if (audio != null) audio.PlaySwitch(); OnSpeedChanged?.Invoke(WatchSpeed.Double); });
            BindButton(speed4xButton, () => { if (audio != null) audio.PlaySwitch(); OnSpeedChanged?.Invoke(WatchSpeed.Quad); });
            BindButton(navigationHubButton, () => { if (audio != null) audio.PlaySelect(); OnNavigationHub?.Invoke(); });

            BindCameraButton(followCreatureButton, Camera3DView.FollowSoulform);
            BindCameraButton(freeCameraButton, Camera3DView.Free);
            BindCameraButton(topViewButton, Camera3DView.Top);
            BindCameraButton(angledViewButton, Camera3DView.Angle45);

            if (viewFullLogButton != null)
                BindButton(viewFullLogButton, () => { if (audio != null) audio.PlaySelect(); OnViewFullLog?.Invoke(); });

            BindButton(openLivabilityDetailsButton, () => { if (audio != null) audio.PlaySelect(); OnOpenLivabilityDetails?.Invoke(); });
            BindButton(closeLivabilityDetailsButton, () => { if (audio != null) audio.PlaySelect(); OnCloseLivabilityDetails?.Invoke(); });

            if (livabilityDetailsPanel != null)
                livabilityDetailsPanel.SetActive(false);

            BindButton(zoomInButton, () => { if (audio != null) audio.PlaySelect(); OnZoomIn?.Invoke(1f); });
            BindButton(zoomOutButton, () => { if (audio != null) audio.PlaySelect(); OnZoomOut?.Invoke(1f); });
            BindButton(searchButton, () => { if (audio != null) audio.PlaySelect(); OnSearch?.Invoke(); });

            if (interventionButtons != null)
            {
                for (int i = 0; i < interventionButtons.Length; i++)
                {
                    int idx = i;
                    BindButton(interventionButtons[i], () =>
                    {
                        if (audio != null) audio.PlaySelect();
                        OnInterventionClicked?.Invoke(idx);
                    });
                }
            }

            BindButton(openProfileButton, () => { if (audio != null) audio.PlaySelect(); OnOpenProfile?.Invoke(); });
            BindButton(talkObserveButton, () => { if (audio != null) audio.PlaySelect(); OnTalkObserve?.Invoke(); });

            UpdateCameraHighlights(watchState.CurrentCameraView);
            UpdateSpeedVisuals(watchState);
            ClearEventLog();
        }

        public void UpdateTime(float elapsed, int dayNumber, string dayName, int season, int year)
        {
            UpdateLabel("Time", FormatTime(elapsed));
            UpdateLabel("Day", dayName);
            UpdateLabel("Date", SeasonName(season) + " " + (season + 1) + ", Year " + year + ", Day " + dayNumber);
        }

        public void UpdateWeather(string condition, string temperature)
        {
            UpdateLabel("Weather", condition);
            UpdateLabel("Temperature", temperature);
        }

        public void UpdateSpeed(WatchState state)
        {
            UpdateSpeedVisuals(state);
        }

        public void UpdateBubbleName(string name)
        {
            UpdateLabel("BubbleName", name);
        }

        public void UpdateLivability(int percent, LivabilityRating rating, string description)
        {
            if (livabilityPercentLabel != null)
                livabilityPercentLabel.text = percent + "%";
            if (livabilityRatingLabel != null)
                livabilityRatingLabel.text = RatingLabel(rating);
            if (livabilityDescriptionLabel != null)
                livabilityDescriptionLabel.text = description;
            if (livabilityFillBar != null)
                livabilityFillBar.fillAmount = percent / 100f;

            Color color = rating switch
            {
                LivabilityRating.Good => ratingGoodColor,
                LivabilityRating.Fair => ratingFairColor,
                LivabilityRating.Mild => ratingMildColor,
                _ => ratingPoorColor
            };
            if (livabilityPercentLabel != null) livabilityPercentLabel.color = color;
            if (livabilityRatingLabel != null) livabilityRatingLabel.color = color;
            if (livabilityFillBar != null) livabilityFillBar.color = color;
        }

        public void UpdateLivabilityDetails(string details)
        {
            if (livabilityDetailsLabel != null)
                livabilityDetailsLabel.text = details;
        }

        public void ShowLivabilityDetails()
        {
            if (livabilityDetailsPanel != null)
                livabilityDetailsPanel.SetActive(true);
        }

        public void HideLivabilityDetails()
        {
            if (livabilityDetailsPanel != null)
                livabilityDetailsPanel.SetActive(false);
        }

        public void UpdateSoulform(SoulformModel soulform)
        {
            if (soulform == null)
            {
                ClearSoulformInfo();
                return;
            }
            if (soulformNameLabel != null) soulformNameLabel.text = soulform.Name;
            if (soulformSpeciesLabel != null) soulformSpeciesLabel.text = soulform.Temperament.ToString();
            if (soulformEmotionLabel != null) soulformEmotionLabel.text = soulform.Mood.ToString();
            if (soulformActivityLabel != null) soulformActivityLabel.text = soulform.CurrentActivity;
            if (soulformLocationLabel != null) soulformLocationLabel.text = "(" + soulform.GridX + ", " + soulform.GridY + ", " + soulform.GridZ + ")";
            if (soulformThoughtLabel != null) soulformThoughtLabel.text = "\"" + soulform.CurrentThought + "\"";
            if (comfortBar != null) comfortBar.value = soulform.Comfort / 100f;
            if (hungerBar != null) hungerBar.value = soulform.Hunger / 100f;
            if (curiosityBar != null) curiosityBar.value = soulform.Curiosity / 100f;
        }

        public void AddEvent(string message)
        {
            if (eventLogContainer == null || eventLogEntryPrefab == null) return;
            GameObject entry = Instantiate(eventLogEntryPrefab, eventLogContainer);
            TextMeshProUGUI text = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = message;
            eventEntries.Add(entry);
            while (eventEntries.Count > maxEventHistory)
            {
                Destroy(eventEntries[0]);
                eventEntries.RemoveAt(0);
            }
        }

        public void ClearEventLog()
        {
            foreach (GameObject e in eventEntries)
                Destroy(e);
            eventEntries.Clear();
        }

        public void UpdateMinimap(Texture texture)
        {
            if (minimapImage != null) minimapImage.texture = texture;
        }

        public void SetPlayerMarker(Vector2 uv)
        {
            if (playerMarker != null)
                playerMarker.anchoredPosition = uv;
        }

        public void AddCreatureMarker(Vector2 uv)
        {
            if (creatureMarkerPrefab == null || creatureMarkerContainer == null) return;
            RectTransform marker = Instantiate(creatureMarkerPrefab, creatureMarkerContainer);
            marker.anchoredPosition = uv;
            creatureMarkers.Add(marker);
        }

        public void ClearCreatureMarkers()
        {
            foreach (RectTransform m in creatureMarkers)
                Destroy(m.gameObject);
            creatureMarkers.Clear();
        }

        public void UpdateCameraHighlights(Camera3DView current)
        {
            SetButtonHighlight(followCreatureButton, current == Camera3DView.FollowSoulform);
            SetButtonHighlight(freeCameraButton, current == Camera3DView.Free);
            SetButtonHighlight(topViewButton, current == Camera3DView.Top);
            SetButtonHighlight(angledViewButton, current == Camera3DView.Angle45);
        }

        public void SetInterventionCooldown(int index, bool onCooldown)
        {
            if (interventionButtons == null || index < 0 || index >= interventionButtons.Length) return;
            interventionButtons[index].interactable = !onCooldown;
        }

        public void SetInterventionEnabled(int index, bool enabled)
        {
            if (interventionButtons == null || index < 0 || index >= interventionButtons.Length) return;
            interventionButtons[index].interactable = enabled;
        }

        public void UpdateLabel(string key, string text)
        {
            if (labels.TryGetValue(key, out TextMeshProUGUI t) && t != null)
                t.text = text;
        }

        /// <summary>Refreshes the Architect Points balance display.</summary>
        public void UpdateAP(int balance)
        {
            UpdateLabel("AP", "AP  " + balance);
        }

        private void EnsureApLabel()
        {
            if (apLabel != null || timeLabel == null) return;

            var go = new GameObject("APLabel", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(timeLabel.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, -4f);
            rt.sizeDelta = new Vector2(260f, 30f);

            apLabel = go.AddComponent<TextMeshProUGUI>();
            apLabel.font = timeLabel.font;
            apLabel.fontSize = Mathf.Max(14f, timeLabel.fontSize * 0.8f);
            apLabel.alignment = TextAlignmentOptions.Center;
            apLabel.color = new Color(1f, 0.84f, 0.3f, 1f);
            apLabel.raycastTarget = false;
        }

        private void RegisterLabel(string key, TextMeshProUGUI text)
        {
            if (text != null) labels[key] = text;
        }

        private void UpdateSpeedVisuals(WatchState state)
        {
            SetButtonHighlight(pauseButton, state.IsPaused);
            SetButtonHighlight(speed1xButton, state.Speed == WatchSpeed.Normal);
            SetButtonHighlight(speed2xButton, state.Speed == WatchSpeed.Double);
            SetButtonHighlight(speed4xButton, state.Speed == WatchSpeed.Quad);
        }

        private void ClearSoulformInfo()
        {
            if (soulformNameLabel != null) soulformNameLabel.text = "-";
            if (soulformSpeciesLabel != null) soulformSpeciesLabel.text = "-";
            if (soulformEmotionLabel != null) soulformEmotionLabel.text = "-";
            if (soulformActivityLabel != null) soulformActivityLabel.text = "-";
            if (soulformLocationLabel != null) soulformLocationLabel.text = "-";
            if (soulformThoughtLabel != null) soulformThoughtLabel.text = "-";
            if (comfortBar != null) comfortBar.value = 0;
            if (hungerBar != null) hungerBar.value = 0;
            if (curiosityBar != null) curiosityBar.value = 0;
        }

        private static string FormatTime(float elapsed)
        {
            int h = Mathf.FloorToInt(elapsed / 3600f) % 24;
            int m = Mathf.FloorToInt((elapsed % 3600f) / 60f);
            return h.ToString("00") + ":" + m.ToString("00");
        }

        private static string SeasonName(int season)
        {
            string[] names = { "Spring", "Summer", "Autumn", "Winter" };
            return names[Mathf.Clamp(season, 0, names.Length - 1)];
        }

        private static string RatingLabel(LivabilityRating rating)
        {
            switch (rating)
            {
                case LivabilityRating.Good: return "<color=#4DFF8A>Good</color>";
                case LivabilityRating.Fair: return "<color=#A0D468>Fair</color>";
                case LivabilityRating.Mild: return "<color=#FFB347>Mild</color>";
                default: return "<color=#FF6B6B>Poor</color>";
            }
        }

        private void BindCameraButton(Button button, Camera3DView view)
        {
            if (button == null) return;
            BindButton(button, () =>
            {
                if (audio != null) audio.PlaySwitch();
                OnCameraModeChanged?.Invoke(view);
            });
        }

        private static void BindButton(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }

        private static void SetButtonHighlight(Button button, bool highlight)
        {
            if (button == null) return;
            for (int i = 0; i < button.transform.childCount; i++)
            {
                GameObject child = button.transform.GetChild(i).gameObject;
                if (child.name == "Selected" || child.name == "Selection")
                {
                    child.SetActive(highlight);
                    return;
                }
            }
            if (button.image != null)
                button.image.color = highlight
                    ? new Color(0.3f, 0.85f, 0.5f, 0.3f)
                    : Color.white;
        }
    }
}
