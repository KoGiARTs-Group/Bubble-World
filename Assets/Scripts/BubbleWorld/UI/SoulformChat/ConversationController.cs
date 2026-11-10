using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    /// <summary>
    /// Right conversation window. Renders soulform/player messages with
    /// portraits, names, timestamps, voice icons and dynamic heights.
    /// Long histories are virtualized: only the most recent window is
    /// instantiated, older messages remain in the data list and are
    /// re-materialized when scrolled back.
    /// </summary>
    public sealed class ConversationController : MonoBehaviour
    {
        [Header("Scroll")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameObject playerMessagePrefab;
        [SerializeField] private GameObject soulformMessagePrefab;

        [Header("Typing")]
        [SerializeField] private GameObject typingIndicator;

        [Header("Virtualization")]
        [SerializeField] private int maxRenderedMessages = 200;

        [Header("Layout")]
        [SerializeField] private float sidePadding = 50f;
        [SerializeField] private float maxMessageWidth = 865f;
        [SerializeField] private float itemSpacing = 30f;
        [SerializeField] private float topPadding = -50f;
        [Tooltip("Minimum bubble height regardless of content.")]
        [SerializeField] private float minBubbleHeight = 250f;
        [Tooltip("Space reserved at the top of a bubble for the Name/Time/Icon header row.")]
        [SerializeField] private float headerHeight = 88f;
        [Tooltip("Gap between the header row and the message body text.")]
        [SerializeField] private float headerGap = 15f;
        [Tooltip("Bottom padding below the message body text.")]
        [SerializeField] private float bottomPadding = 30f;

        private readonly List<ChatMessage> messages = new List<ChatMessage>();
        private readonly List<GameObject> rendered = new List<GameObject>();
        private UIObjectPool playerPool;
        private UIObjectPool soulformPool;
        private int renderStartIndex;
        private float yOffset;

        public event Action<ChatMessage> OnVoicePlayed;
        public event Action OnMessagesChanged;

        private void Awake()
        {
            BuildPools();
            PrepareContent();
        }

        /// <summary>Programmatic fallback wiring for scenes without a controller.</summary>
        public void Configure(ScrollRect rect, GameObject playerPrefab, GameObject soulformPrefab, int maxRendered)
        {
            if (scrollRect == null && rect != null) scrollRect = rect;
            if (playerMessagePrefab == null && playerPrefab != null) playerMessagePrefab = playerPrefab;
            if (soulformMessagePrefab == null && soulformPrefab != null) soulformMessagePrefab = soulformPrefab;
            if (maxRendered > 0) maxRenderedMessages = maxRendered;
            BuildPools();
            PrepareContent();
        }

        private void BuildPools()
        {
            Transform content = scrollRect != null ? scrollRect.content : null;
            if (content == null) return;
            if (playerPool == null && playerMessagePrefab != null)
                playerPool = new UIObjectPool(playerMessagePrefab, content, 2);
            if (soulformPool == null && soulformMessagePrefab != null)
                soulformPool = new UIObjectPool(soulformMessagePrefab, content, 2);
        }

        private void PrepareContent()
        {
            Transform content = scrollRect != null ? scrollRect.content : null;
            if (content == null) return;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;
            var csf = content.GetComponent<ContentSizeFitter>();
            if (csf != null) csf.enabled = false;
        }

        public IReadOnlyList<ChatMessage> Messages => messages;

        public void AddMessage(ChatMessage msg)
        {
            if (msg == null) return;
            messages.Add(msg);
            renderStartIndex = Mathf.Max(0, messages.Count - maxRenderedMessages);
            if (messages.Count - renderStartIndex > maxRenderedMessages)
                Relayout();
            else
                RenderMessage(msg);
            AutoScrollToBottom();
            OnMessagesChanged?.Invoke();
        }

        public void AddMessages(IEnumerable<ChatMessage> items)
        {
            if (items == null) return;
            foreach (ChatMessage msg in items)
            {
                messages.Add(msg);
            }
            Relayout();
            AutoScrollToBottom();
            OnMessagesChanged?.Invoke();
        }

        public void LoadConversation(Conversation conversation)
        {
            ClearMessages();
            if (conversation != null && conversation.Messages != null)
                AddMessages(conversation.Messages);
        }

        public void ClearMessages()
        {
            messages.Clear();
            renderStartIndex = 0;
            ReleaseAllRendered();
            ResetTyping();
            OnMessagesChanged?.Invoke();
        }

        public void SetTypingIndicator(GameObject indicator)
        {
            typingIndicator = indicator;
        }

        public void SetTyping(bool visible)
        {
            if (typingIndicator != null) typingIndicator.SetActive(visible);
            if (visible) AutoScrollToBottom();
        }

        public void ResetTyping()
        {
            if (typingIndicator != null) typingIndicator.SetActive(false);
        }

        public void AutoScrollToBottom()
        {
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        /// <summary>Drops the oldest rendered window and relayouts the tail.</summary>
        public void Relayout()
        {
            renderStartIndex = Mathf.Max(0, messages.Count - maxRenderedMessages);
            ReleaseAllRendered();
            yOffset = topPadding;
            for (int i = renderStartIndex; i < messages.Count; i++)
                RenderMessage(messages[i]);
            UpdateContentSize();
        }

        private void RenderMessage(ChatMessage msg)
        {
            UIObjectPool pool = msg.Speaker == SpeakerType.Player ? playerPool : soulformPool;
            RectTransform content = scrollRect != null ? scrollRect.content : null;
            if (pool == null || !pool.IsValid || content == null) return;

            float messageWidth = Mathf.Min(content.rect.width - sidePadding * 2f, maxMessageWidth);

            GameObject entry = pool.Get();
            rendered.Add(entry);
            RectTransform entryRect = (RectTransform)entry.transform;

            SetupBubble(entry, msg);
            entryRect.pivot = new Vector2(0, 1);
            entryRect.anchorMin = new Vector2(0, 1);
            entryRect.anchorMax = new Vector2(0, 1);
            entryRect.sizeDelta = new Vector2(messageWidth, 200f);

            Canvas.ForceUpdateCanvases();
            float textH = MeasureTextHeight(entry);
            float bubbleH = Mathf.Max(minBubbleHeight, headerHeight + headerGap + textH + bottomPadding);
            entryRect.sizeDelta = new Vector2(messageWidth, bubbleH);
            ResizeTextToBubble(entry, bubbleH, headerHeight);

            entryRect.anchoredPosition = new Vector2(sidePadding, yOffset);

            yOffset -= entryRect.rect.height + itemSpacing;
        }

        private static float MeasureTextHeight(GameObject entry)
        {
            var tmp = FindBodyText(entry) as TextMeshProUGUI;
            if (tmp == null) return 100f;
            tmp.ForceMeshUpdate(true);
            return tmp.preferredHeight;
        }

        /// <summary>
        /// The message body is authored as the "Text (TMP)" child. Selecting it
        /// by name avoids grabbing the "Name"/"Time" header labels, which sit
        /// earlier in the hierarchy and have constant short content.
        /// </summary>
        private static TMP_Text FindBodyText(GameObject entry)
        {
            if (entry == null) return null;
            Transform body = entry.transform.Find("Text (TMP)");
            if (body == null) return null;
            return body.GetComponent<TMP_Text>();
        }

        /// <summary>
        /// Resizes only the height of the message's "Text (TMP)" child so it
        /// stays inside the authored horizontal layout. Stretching it to the
        /// full bubble made it overlap the Icon/Name/Time header elements.
        /// </summary>
        private static void ResizeTextToBubble(GameObject entry, float bubbleHeight, float headerHeight)
        {
            var tmp = FindBodyText(entry);
            if (tmp == null) return;
            RectTransform textRect = (RectTransform)tmp.transform;
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, -headerHeight);
            textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, bubbleHeight - headerHeight);
        }

        private void SetupBubble(GameObject entry, ChatMessage msg)
        {
            var tmps = entry.GetComponentsInChildren<TMP_Text>(true);
            foreach (var label in tmps)
            {
                switch (label.name)
                {
                    case "Name":
                        label.text = msg.Speaker == SpeakerType.Player ? "You" : "Soulform";
                        break;
                    case "Timestamp":
                    case "Time":
                        label.text = msg.Timestamp;
                        break;
                    case "Text (TMP)":
                        label.margin = new Vector4(20, 15, 20, 15);
                        string text = msg.Text;
                        if (msg.Speaker == SpeakerType.Soulform && !string.IsNullOrEmpty(msg.Emotion))
                            text = "<i>" + msg.Emotion + "</i> " + text;
                        if (!HasTimestampLabel(entry))
                            text += "\n<size=60%>" + msg.Timestamp + "</size>";
                        label.text = text;
                        break;
                }
            }

            bool hasVoice = msg.HasVoice;
            bool hasPortrait = !string.IsNullOrEmpty(msg.PortraitId);
            foreach (Transform child in entry.transform)
            {
                switch (child.name)
                {
                    case "Voice Icon":
                        child.gameObject.SetActive(hasVoice);
                        break;
                    case "Portrait":
                        child.gameObject.SetActive(hasPortrait);
                        break;
                }
            }
        }

        private static bool HasTimestampLabel(GameObject entry)
        {
            return entry.transform.Find("Timestamp") != null || entry.transform.Find("Time") != null;
        }

        private void UpdateContentSize()
        {
            Transform content = scrollRect != null ? scrollRect.content : null;
            if (content == null) return;
            float totalHeight = Mathf.Abs(yOffset) + Mathf.Abs(topPadding) + 10f;
            ((RectTransform)content).sizeDelta = new Vector2(0, totalHeight);
        }

        private void ReleaseAllRendered()
        {
            if (playerPool != null) playerPool.ReleaseAll();
            if (soulformPool != null) soulformPool.ReleaseAll();
            rendered.Clear();
        }

        public void ToggleVoice(ChatMessage msg)
        {
            OnVoicePlayed?.Invoke(msg);
        }
    }
}
