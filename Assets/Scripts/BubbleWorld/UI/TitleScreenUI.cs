using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BubbleWorld
{
    public sealed class TitleScreenUI : MonoBehaviour
    {
        [SerializeField] private Button newWorldButton;
        [SerializeField] private Button openWorldButton;
        [SerializeField] private Button settingsButton;

        [Header("Load World Panel")]
        [SerializeField] private GameObject loadWorldPanel;
        [SerializeField] private Transform loadWorldListContainer;
        [SerializeField] private GameObject loadWorldEntryPrefab;
        [SerializeField] private Button closeLoadWorldButton;

        private AudioManager audio;

        private void Awake()
        {
            audio = FindObjectOfType<AudioManager>();
        }

        public void Bind(UnityAction onNewWorld, UnityAction onOpenWorld, UnityAction onSettings)
        {
            SetListener(newWorldButton, onNewWorld);
            SetListener(openWorldButton, onOpenWorld);
            SetListener(settingsButton, onSettings);
        }

        public void ShowLoadWorld(string[] worldNames, Action<string> onSelect, Action onClose)
        {
            if (loadWorldPanel != null)
                loadWorldPanel.SetActive(true);

            if (loadWorldListContainer != null)
            {
                foreach (Transform child in loadWorldListContainer)
                    Destroy(child.gameObject);
            }

            if (worldNames == null || worldNames.Length == 0)
            {
                var entry = Instantiate(loadWorldEntryPrefab, loadWorldListContainer);
                var text = entry.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (text != null) text.text = "No saved worlds found";
                var btn = entry.GetComponent<Button>();
                if (btn != null) btn.interactable = false;
            }
            else
            {
                foreach (string name in worldNames)
                {
                    var entry = Instantiate(loadWorldEntryPrefab, loadWorldListContainer);
                    var text = entry.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (text != null) text.text = name;
                    var btn = entry.GetComponent<Button>();
                    if (btn != null)
                    {
                        string captured = name;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() =>
                        {
                            if (audio != null) audio.PlaySelect();
                            if (loadWorldPanel != null) loadWorldPanel.SetActive(false);
                            onSelect?.Invoke(captured);
                        });
                    }
                }
            }

            if (closeLoadWorldButton != null)
            {
                closeLoadWorldButton.onClick.RemoveAllListeners();
                closeLoadWorldButton.onClick.AddListener(() =>
                {
                    if (audio != null) audio.PlayUiBack();
                    if (loadWorldPanel != null) loadWorldPanel.SetActive(false);
                    onClose?.Invoke();
                });
            }
        }

        private static void SetListener(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }
    }
}
