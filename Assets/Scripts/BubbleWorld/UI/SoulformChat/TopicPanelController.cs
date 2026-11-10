using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    public enum TopicTab
    {
        Topics,
        Memory,
        Relationship,
        World
    }

    /// <summary>
    /// Left sidebar conversation archive. Four tabs (Topics / Memory /
    /// Relationship / World), each backed by its own ScrollRect with pooled
    /// list items. Selecting an entry invokes the bound selection callback.
    /// </summary>
    public sealed class TopicPanelController : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private ScrollRect[] tabScrollRects;
        [SerializeField] private GameObject topicListItemPrefab;

        [Header("Layout")]
        [SerializeField] private float itemHeight = 130f;
        [SerializeField] private float itemSpacing = 10f;
        [SerializeField] private float tabFadeDuration = 0.18f;

        private readonly Dictionary<TopicTab, ScrollRect> scrolls = new Dictionary<TopicTab, ScrollRect>();
        private readonly Dictionary<TopicTab, UIObjectPool> pools = new Dictionary<TopicTab, UIObjectPool>();
        private readonly Dictionary<TopicTab, List<GameObject>> rendered = new Dictionary<TopicTab, List<GameObject>>();
        private readonly Dictionary<TopicTab, float> yOffsets = new Dictionary<TopicTab, float>();
        private int tabCount;
        private int currentIndex;
        private Action<Topic> onTopicSelected;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            var tabs = (TopicTab[])Enum.GetValues(typeof(TopicTab));
            int scrollCount = tabScrollRects != null ? tabScrollRects.Length : 0;
            int buttonCount = tabButtons != null ? tabButtons.Length : 0;
            tabCount = Mathf.Max(4, Mathf.Max(scrollCount, buttonCount));

            for (int i = 0; i < tabCount; i++)
            {
                TopicTab tab = i < tabs.Length ? tabs[i] : (TopicTab)i;
                scrolls[tab] = i < scrollCount ? tabScrollRects[i] : null;
                pools[tab] = null;
                rendered[tab] = new List<GameObject>();
                yOffsets[tab] = -10f;

                ScrollRect sr = scrolls[tab];
                if (sr != null && topicListItemPrefab != null)
                {
                    if (sr.content != null)
                    {
                        var vlg = sr.content.GetComponent<VerticalLayoutGroup>();
                        if (vlg != null) vlg.enabled = false;
                        var csf = sr.content.GetComponent<ContentSizeFitter>();
                        if (csf != null) csf.enabled = false;
                    }
                    pools[tab] = new UIObjectPool(topicListItemPrefab, sr.content, 2);
                }

                if (i < buttonCount && tabButtons[i] != null)
                {
                    int captured = i;
                    tabButtons[i].onClick.RemoveAllListeners();
                    tabButtons[i].onClick.AddListener(() => ShowTabIndex(captured));
                }
            }
            ShowTabIndex(0);
        }

        /// <summary>Wires the selection callback fired when a topic is clicked.</summary>
        public void Bind(Action<Topic> onSelect)
        {
            onTopicSelected = onSelect;
        }

        public TopicTab CurrentTab => (TopicTab)currentIndex;

        /// <summary>Switches tabs with an animated fade when CanvasGroups are present.</summary>
        public void ShowTab(TopicTab tab)
        {
            ShowTabIndex((int)tab);
        }

        /// <summary>Switches tabs by array index (matches the scene's button/scroll wiring).</summary>
        public void ShowTabIndex(int index)
        {
            currentIndex = index;
            int scrollCount = tabScrollRects != null ? tabScrollRects.Length : 0;
            for (int i = 0; i < tabCount; i++)
            {
                ScrollRect sr = i < scrollCount ? tabScrollRects[i] : null;
                if (sr == null) continue;
                bool active = i == index;
                CanvasGroup cg = sr.GetComponent<CanvasGroup>();
                if (cg != null && active)
                {
                    sr.gameObject.SetActive(true);
                    FadeTo(cg, 1f, tabFadeDuration);
                }
                else if (cg != null)
                {
                    FadeTo(cg, 0f, tabFadeDuration, () => sr.gameObject.SetActive(false));
                }
                else
                {
                    sr.gameObject.SetActive(active);
                }
            }
        }

        /// <summary>Replaces the entries shown for a tab with pooled list items.</summary>
        public void SetTopics(TopicTab tab, IList<Topic> topics)
        {
            if (!pools.TryGetValue(tab, out UIObjectPool pool) || pool == null || !pool.IsValid) return;

            pool.ReleaseAll();
            rendered[tab].Clear();
            yOffsets[tab] = -10f;
            if (topics == null) return;

            ScrollRect sr = scrolls[tab];
            for (int i = 0; i < topics.Count; i++)
            {
                Topic topic = topics[i];
                GameObject entry = pool.Get();
                rendered[tab].Add(entry);
                ApplyTopic(tab, entry, topic, sr);
            }
            UpdateContentSize(tab);
        }

        public void ClearTab(TopicTab tab)
        {
            if (!pools.TryGetValue(tab, out UIObjectPool pool)) return;
            pool.ReleaseAll();
            rendered[tab].Clear();
            yOffsets[tab] = -10f;
            UpdateContentSize(tab);
        }

        private void ApplyTopic(TopicTab tab, GameObject entry, Topic topic, ScrollRect sr)
        {
            RectTransform viewport = sr != null ? (RectTransform)sr.transform : null;
            float viewportWidth = viewport != null && viewport.rect.width > 0
                ? viewport.rect.width
                : 400f;

            var tmps = entry.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in tmps)
            {
                switch (tmp.name)
                {
                    case "Title": tmp.text = topic.Name; break;
                    case "Preview": tmp.text = topic.Preview; break;
                    case "Timestamp": tmp.text = topic.Timestamp.ToString("h:mm tt"); break;
                    case "Text (TMP)":
                        tmp.text = string.IsNullOrEmpty(topic.Preview)
                            ? topic.Name
                            : topic.Name + "\n" + topic.Preview;
                        break;
                }
            }

            var btn = entry.GetComponentInChildren<Button>(true);
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                Topic captured = topic;
                btn.onClick.AddListener(() => onTopicSelected?.Invoke(captured));
            }

            var rt = (RectTransform)entry.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(viewportWidth, itemHeight);
            rt.anchoredPosition = new Vector2(0, yOffsets[tab]);
            yOffsets[tab] -= itemHeight + itemSpacing;
        }

        private void UpdateContentSize(TopicTab tab)
        {
            if (!scrolls.TryGetValue(tab, out ScrollRect sr) || sr == null) return;
            float total = Mathf.Abs(yOffsets[tab]) + 10f;
            if (sr.content != null)
                sr.content.sizeDelta = new Vector2(0, total);
        }

        private void FadeTo(CanvasGroup cg, float target, float duration, Action onComplete = null)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(cg, target, duration, onComplete));
        }

        private static IEnumerator FadeRoutine(CanvasGroup cg, float target, float duration, Action onComplete)
        {
            float start = cg.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.001f, duration);
                cg.alpha = Mathf.Lerp(start, target, t);
                yield return null;
            }
            cg.alpha = target;
            onComplete?.Invoke();
        }
    }
}
