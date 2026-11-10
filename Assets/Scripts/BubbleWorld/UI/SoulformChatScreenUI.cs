using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    /// <summary>
    /// Full communication interface. Composes five independent controllers
    /// (header, topic archive, conversation, character panel, footer). All
    /// existing public API is preserved; controllers are auto-created from
    /// the legacy scene wiring when they are not assigned explicitly.
    /// </summary>
    public sealed class SoulformChatScreenUI : MonoBehaviour
    {
        [Header("Controllers (optional - auto-created from legacy wiring)")]
        [SerializeField] private HeaderController headerController;
        [SerializeField] private TopicPanelController topicPanelController;
        [SerializeField] private ConversationController conversationController;
        [SerializeField] private CharacterPanelController characterPanelController;
        [SerializeField] private FooterController footerController;

        [Header("Header (legacy)")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text soulformNameLabel;

        [Header("Conversation (legacy)")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private GameObject playerMessagePrefab;
        [SerializeField] private GameObject soulformMessagePrefab;
        [SerializeField] private int maxVisibleMessages = 100;

        [Header("Typing Indicator")]
        [SerializeField] private GameObject typingIndicator;

        [Header("Input")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;

        [Header("Voice")]
        [Tooltip("Optional. Auto-created next to the input field when not assigned.")]
        [SerializeField] private Button voiceButton;

        [Header("3D Model (legacy)")]
        [SerializeField] private GameObject modelParent;
        [SerializeField] private SoulformExpressionDriver expressionDriver;
        [SerializeField] private SoulformLipSync lipSync;

        private AudioManager audio;
        private SoulformStatus status = new SoulformStatus();

        private bool voiceListening;
        private bool placeholderCached;
        private string placeholderOriginal;

        public event Action<string> OnSendMessage;
        public event Action OnClose;
        public event Action<Topic> OnTopicSelected;

        private void Awake()
        {
            audio = FindObjectOfType<AudioManager>();
            typingIndicator?.SetActive(false);
            EnsureControllers();
            WireFooter();
            VoiceService.OnDictationResult += HandleDictationResult;
            VoiceService.OnDictationStatus += HandleDictationStatus;
        }

        private void OnDestroy()
        {
            VoiceService.OnDictationResult -= HandleDictationResult;
            VoiceService.OnDictationStatus -= HandleDictationStatus;
        }

        private void OnEnable()
        {
            if (footerController != null) footerController.SetInputEnabled(true);
        }

        private void OnDisable()
        {
            if (footerController != null) footerController.SetInputEnabled(false);
            if (characterPanelController != null) characterPanelController.CleanupModel();
        }

        private void EnsureControllers()
        {
            if (conversationController == null && chatScrollRect != null)
            {
                conversationController = gameObject.AddComponent<ConversationController>();
                conversationController.Configure(chatScrollRect, playerMessagePrefab,
                    soulformMessagePrefab, maxVisibleMessages);
            }
            if (conversationController != null)
                conversationController.SetTypingIndicator(typingIndicator);

            if (characterPanelController == null && modelParent != null)
            {
                characterPanelController = gameObject.AddComponent<CharacterPanelController>();
                characterPanelController.Configure(modelParent, expressionDriver, lipSync);
            }

            if (headerController == null)
            {
                headerController = gameObject.AddComponent<HeaderController>();
                headerController.Configure(soulformNameLabel);
            }

            if (topicPanelController == null)
            {
                topicPanelController = gameObject.AddComponent<TopicPanelController>();
            }

            if (footerController == null)
            {
                footerController = gameObject.AddComponent<FooterController>();
            }

            EnsureVoiceButton();
        }

        /// <summary>Creates the mic toggle button beside the input field when the scene has none.</summary>
        private void EnsureVoiceButton()
        {
            if (voiceButton != null || inputField == null) return;

            var go = new GameObject("VoiceButton", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(inputField.transform, false);
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-6f, 0f);
            rt.sizeDelta = new Vector2(64f, 32f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.2f, 0.26f, 0.9f);

            voiceButton = go.AddComponent<Button>();
            voiceButton.targetGraphic = bg;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "MIC";
            label.fontSize = 16f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            if (inputField.textComponent != null)
                label.font = inputField.textComponent.font;

            voiceButton.onClick.AddListener(ToggleVoiceInput);
        }

        private void ToggleVoiceInput()
        {
            if (voiceListening) StopVoiceInput();
            else StartVoiceInput();
        }

        private void StartVoiceInput()
        {
            if (inputField == null) return;
            if (!VoiceService.StartDictation())
            {
                voiceListening = false;
                SetVoiceLabel("N/A");
                SetPlaceholderText("Voice input unavailable");
                return;
            }
            voiceListening = true;
            SetVoiceLabel("...");
            SetPlaceholderText("Listening...");
        }

        private void StopVoiceInput()
        {
            if (!voiceListening) return;
            voiceListening = false;
            VoiceService.StopDictation();
            SetVoiceLabel("MIC");
            RestorePlaceholder();
        }

        private void HandleDictationResult(string text)
        {
            // Single-phrase capture: transcribe the phrase, then stop listening.
            VoiceService.StopDictation();
            voiceListening = false;
            SetVoiceLabel("MIC");
            RestorePlaceholder();

            if (inputField == null || string.IsNullOrEmpty(text)) return;
            inputField.text = string.IsNullOrEmpty(inputField.text) ? text : inputField.text + " " + text;
            if (gameObject.activeInHierarchy) inputField.ActivateInputField();
        }

        private void HandleDictationStatus(string statusText)
        {
            if (string.IsNullOrEmpty(statusText)) return;
            if (statusText.StartsWith("Listening", StringComparison.Ordinal))
            {
                SetPlaceholderText(statusText);
                return;
            }

            voiceListening = false;
            SetVoiceLabel("MIC");
            SetPlaceholderText(statusText);
        }

        private void SetVoiceLabel(string text)
        {
            if (voiceButton == null) return;
            var label = voiceButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = text;
        }

        private void SetPlaceholderText(string text)
        {
            var ph = inputField != null ? inputField.placeholder as TextMeshProUGUI : null;
            if (ph == null) return;
            if (!placeholderCached)
            {
                placeholderOriginal = ph.text;
                placeholderCached = true;
            }
            ph.text = text;
        }

        private void RestorePlaceholder()
        {
            if (!placeholderCached) return;
            var ph = inputField != null ? inputField.placeholder as TextMeshProUGUI : null;
            if (ph != null) ph.text = placeholderOriginal;
        }

        private void WireFooter()
        {
            if (footerController == null) return;
            footerController.OnBack += () => OnClose?.Invoke();
            footerController.OnTabShortcut += tabIndex =>
            {
                if (topicPanelController != null && tabIndex >= 0 && tabIndex < 4)
                    topicPanelController.ShowTab((TopicTab)tabIndex);
            };
            // OnMute fires before the footer toggles its own flag, so the new
            // mute state is the inverse of the current one.
            footerController.OnMute += () => VoiceService.IsMuted = !footerController.IsMuted;
        }

        /// <summary>Builds the screen for a soulform. Wires input and binds status.</summary>
        public void Build(string soulformName)
        {
            status.Name = soulformName;
            headerController?.Bind(status);

            if (conversationController != null) conversationController.ClearMessages();

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(() =>
                {
                    if (audio != null) audio.PlayUiBack();
                    OnClose?.Invoke();
                });
            }

            if (sendButton != null)
            {
                sendButton.onClick.RemoveAllListeners();
                sendButton.onClick.AddListener(() => SubmitMessage());
            }

            if (inputField != null)
            {
                inputField.onSubmit.RemoveAllListeners();
                inputField.onSubmit.AddListener(_ => SubmitMessage());
                inputField.text = "";
                RestorePlaceholder();
                if (gameObject.activeInHierarchy)
                    inputField.ActivateInputField();
            }

            if (topicPanelController != null)
            {
                topicPanelController.Bind(topic => OnTopicSelected?.Invoke(topic));
            }
        }

        private void SubmitMessage()
        {
            if (inputField == null) return;
            string text = inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (audio != null) audio.PlaySelect();
            inputField.text = "";
            inputField.ActivateInputField();
            OnSendMessage?.Invoke(text);
        }

        // ---------------------------------------------------------------
        // Conversation API (backwards compatible)
        // ---------------------------------------------------------------

        public void AddMessage(ChatMessage msg)
        {
            if (conversationController != null)
            {
                conversationController.AddMessage(msg);
                return;
            }
            Debug.LogWarning("Chat: no conversation controller wired");
        }

        public void AddMessages(IEnumerable<ChatMessage> messages)
        {
            if (conversationController != null)
            {
                conversationController.AddMessages(messages);
                return;
            }
            Debug.LogWarning("Chat: no conversation controller wired");
        }

        public void SetTyping(bool visible)
        {
            if (conversationController != null) conversationController.SetTyping(visible);
        }

        public void ClearMessages()
        {
            if (conversationController != null) conversationController.ClearMessages();
        }

        public void LoadConversation(Conversation conversation)
        {
            if (conversationController != null) conversationController.LoadConversation(conversation);
        }

        // ---------------------------------------------------------------
        // Archive API
        // ---------------------------------------------------------------

        public void SetStatus(SoulformStatus value)
        {
            if (value == null) return;
            status = value;
            headerController?.Bind(status);
        }

        /// <summary>Partial refresh of the header AP widget when the balance changes.</summary>
        public void UpdateAP(int current, int max)
        {
            if (status == null) status = new SoulformStatus();
            status.CurrentAP = current;
            status.MaxAP = max;
            headerController?.SetAp(current, max);
        }

        public void SetTopics(TopicTab tab, IList<Topic> topics)
        {
            if (topicPanelController != null) topicPanelController.SetTopics(tab, topics);
        }

        public void SetConversations(IList<Conversation> conversations)
        {
            if (topicPanelController == null || conversations == null) return;
            var topics = new List<Topic>(conversations.Count);
            foreach (Conversation conv in conversations)
            {
                string preview = string.Empty;
                if (conv.Messages != null && conv.Messages.Count > 0)
                    preview = conv.Messages[conv.Messages.Count - 1].Text;
                var topic = new Topic(conv.Id, conv.Title, preview)
                {
                    Timestamp = conv.LastUpdated,
                    Unread = conv.HasUnread
                };
                topics.Add(topic);
            }
            topicPanelController.SetTopics(TopicTab.Topics, topics);
        }

        public void ShowTopicTab(TopicTab tab)
        {
            if (topicPanelController != null) topicPanelController.ShowTab(tab);
        }

        // ---------------------------------------------------------------
        // Character / 3D model API (backwards compatible)
        // ---------------------------------------------------------------

        public void SetupModel(SoulformModel model, string expression, string animation)
        {
            if (characterPanelController != null)
            {
                characterPanelController.SetupModel(model, expression, animation);
                return;
            }
            if (modelParent != null) modelParent.SetActive(true);
            if (lipSync != null && model != null) lipSync.SetSoulform(model);
            UpdateEmotion(expression, animation);
        }

        public void UpdateEmotion(string expression, string animation)
        {
            if (characterPanelController != null)
            {
                characterPanelController.UpdateEmotion(expression, animation);
                return;
            }
            if (expressionDriver != null && !string.IsNullOrEmpty(expression))
                expressionDriver.SetExpression(expression);
        }

        public void Speak(string text)
        {
            if (characterPanelController != null)
            {
                characterPanelController.Speak(text);
                return;
            }
            if (lipSync != null) lipSync.Speak(text);
        }

        public void CleanupModel()
        {
            if (characterPanelController != null)
            {
                characterPanelController.CleanupModel();
                return;
            }
            if (lipSync != null) lipSync.StopSpeaking();
            if (modelParent != null) modelParent.SetActive(false);
        }
    }
}
