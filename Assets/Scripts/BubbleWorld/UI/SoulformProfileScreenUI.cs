using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    public sealed class SoulformProfileScreenUI : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text soulformNameLabel;
        [SerializeField] private TMP_Text temperamentLabel;

        [Header("Status")]
        [SerializeField] private TMP_Text moodLabel;
        [SerializeField] private TMP_Text activityLabel;
        [SerializeField] private TMP_Text thoughtLabel;
        [SerializeField] private TMP_Text locationLabel;

        [Header("Needs")]
        [SerializeField] private Slider hungerBar;
        [SerializeField] private TMP_Text hungerValueLabel;
        [SerializeField] private Slider comfortBar;
        [SerializeField] private TMP_Text comfortValueLabel;
        [SerializeField] private Slider curiosityBar;
        [SerializeField] private TMP_Text curiosityValueLabel;

        [Header("Memories")]
        [SerializeField] private Transform memoryListContainer;
        [SerializeField] private GameObject memoryEntryPrefab;
        [SerializeField] private int maxMemoryEntries = 20;

        [Header("Skills")]
        [SerializeField] private Transform skillListContainer;
        [SerializeField] private GameObject skillEntryPrefab;

        private readonly List<GameObject> memoryEntries = new List<GameObject>();
        private readonly List<GameObject> skillEntries = new List<GameObject>();
        private WatchState boundState;

        public event Action OnClose;

        public void Build(WatchState state)
        {
            boundState = state;
            if (state?.Soulform == null) return;
            var sf = state.Soulform;

            if (soulformNameLabel != null)
                soulformNameLabel.text = sf.Name;
            if (temperamentLabel != null)
                temperamentLabel.text = sf.Temperament.ToString();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => OnClose?.Invoke());
            }

            Refresh();
        }

        public void Refresh()
        {
            if (boundState?.Soulform == null) return;
            var sf = boundState.Soulform;

            if (moodLabel != null)
                moodLabel.text = sf.Mood.ToString();
            if (activityLabel != null)
                activityLabel.text = sf.CurrentActivity;
            if (thoughtLabel != null)
                thoughtLabel.text = sf.CurrentThought;
            if (locationLabel != null)
                locationLabel.text = "(" + sf.GridX + ", " + sf.GridY + ", " + sf.GridZ + ")";

            if (hungerBar != null)
                hungerBar.value = sf.Hunger / 100f;
            if (hungerValueLabel != null)
                hungerValueLabel.text = sf.Hunger + "/100";

            if (comfortBar != null)
                comfortBar.value = sf.Comfort / 100f;
            if (comfortValueLabel != null)
                comfortValueLabel.text = sf.Comfort + "/100";

            if (curiosityBar != null)
                curiosityBar.value = sf.Curiosity / 100f;
            if (curiosityValueLabel != null)
                curiosityValueLabel.text = sf.Curiosity + "/100";

            RefreshMemories();
            RefreshSkills();
        }

        private void RefreshMemories()
        {
            if (boundState?.MemoryBank == null) return;
            foreach (var entry in memoryEntries)
                Destroy(entry);
            memoryEntries.Clear();

            var chapters = boundState.MemoryBank.Chapters;
            int total = 0;
            for (int i = chapters.Count - 1; i >= 0 && total < maxMemoryEntries; i--)
            {
                var ch = chapters[i];
                var entries = ch.Entries;
                for (int j = entries.Count - 1; j >= 0 && total < maxMemoryEntries; j--, total++)
                {
                    var e = entries[j];
                    var go = Instantiate(memoryEntryPrefab, memoryListContainer);
                    var label = go.GetComponentInChildren<TMP_Text>();
                    if (label != null)
                        label.text = "[" + e.Category + "] " + e.Description;
                    memoryEntries.Add(go);
                }
            }
        }

        private void RefreshSkills()
        {
            if (boundState?.MemoryBank == null) return;
            foreach (var entry in skillEntries)
                Destroy(entry);
            skillEntries.Clear();

            var learned = new HashSet<string>();
            foreach (var ch in boundState.MemoryBank.Chapters)
                foreach (var s in ch.SkillsLearned)
                    if (learned.Add(s))
                    {
                        var go = Instantiate(skillEntryPrefab, skillListContainer);
                        var label = go.GetComponentInChildren<TMP_Text>();
                        if (label != null) label.text = s;
                        skillEntries.Add(go);
                    }
        }
    }
}
