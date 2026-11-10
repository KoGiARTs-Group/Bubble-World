using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BubbleWorld
{
    public sealed class StarterScreenUI : MonoBehaviour
    {
        [SerializeField] private Button meadowButton;
        [SerializeField] private Button forestButton;
        [SerializeField] private Button beachButton;
        [SerializeField] private Button moreButton;
        [SerializeField] private Button customButton;
        [SerializeField] private Button beginButton;
        [SerializeField] private Button backButton;

        private AudioManager audio;
        private StarterEnvironment selectedEnv = StarterEnvironment.MeadowGrassland;

        private void Awake()
        {
            audio = FindObjectOfType<AudioManager>();
        }

        public void Bind(Action<StarterEnvironment> onBegin, UnityAction onBack)
        {
            SetListener(meadowButton, () => { if (audio != null) audio.PlaySwitch(); Select(StarterEnvironment.MeadowGrassland); });
            SetListener(forestButton, () => { if (audio != null) audio.PlaySwitch(); Select(StarterEnvironment.Forest); });
            SetListener(beachButton, () => { if (audio != null) audio.PlaySwitch(); Select(StarterEnvironment.BeachCoastal); });
            SetListener(moreButton, null);
            SetListener(customButton, null);
            SetListener(beginButton, () => { if (audio != null) audio.PlaySelect(); onBegin?.Invoke(selectedEnv); });
            SetListener(backButton, () => { if (audio != null) audio.PlayUiBack(); onBack?.Invoke(); });

            Select(selectedEnv);

            AppendSuffix(moreButton, " (Coming Soon)");
            AppendSuffix(customButton, " (Coming Soon)");
        }

        private static void AppendSuffix(Button button, string suffix)
        {
            if (button == null) return;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null && !label.text.Contains(suffix))
                label.text += suffix;
        }

        private void Select(StarterEnvironment env)
        {
            selectedEnv = env;
            UpdateSelectionVisual();
        }

        private void UpdateSelectionVisual()
        {
            SetButtonSelected(meadowButton, selectedEnv == StarterEnvironment.MeadowGrassland);
            SetButtonSelected(forestButton, selectedEnv == StarterEnvironment.Forest);
            SetButtonSelected(beachButton, selectedEnv == StarterEnvironment.BeachCoastal);
            SetButtonSelected(moreButton, false);
            SetButtonSelected(customButton, false);
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null) return;
            for (int i = 0; i < button.transform.childCount; i++)
            {
                GameObject child = button.transform.GetChild(i).gameObject;
                if (child.name == "Selected" || child.name == "Selection")
                    child.SetActive(selected);
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
