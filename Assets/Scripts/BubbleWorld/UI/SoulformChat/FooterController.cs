using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BubbleWorld
{
    /// <summary>
    /// Bottom control bar. Provides controller friendly buttons plus
    /// keyboard/gamepad shortcuts. Input handling is opt-in (enabled only
    /// while the chat screen is active and not typing).
    /// </summary>
    public sealed class FooterController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button selectButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button quickChatButton;
        [SerializeField] private Button muteButton;
        [SerializeField] private Button switchWindowButton;
        [SerializeField] private Button scrollButton;
        [SerializeField] private Button navigationWindowButton;

        [Header("Input")]
        [SerializeField] private KeyCode backKey = KeyCode.Escape;
        [SerializeField] private KeyCode quickChatKey = KeyCode.Q;
        [SerializeField] private KeyCode muteKey = KeyCode.M;
        [SerializeField] private KeyCode switchWindowKey = KeyCode.Tab;

        public event Action OnSelect;
        public event Action OnBack;
        public event Action OnQuickChat;
        public event Action OnMute;
        public event Action OnSwitchWindow;
        public event Action OnScroll;
        public event Action OnNavigationWindow;

        private bool inputEnabled;
        private bool muted;
        private readonly KeyCode[] tabKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

        public event Action<int> OnTabShortcut;

        private void Awake()
        {
            Bind(selectButton, () => OnSelect?.Invoke());
            Bind(backButton, () => OnBack?.Invoke());
            Bind(quickChatButton, () => OnQuickChat?.Invoke());
            Bind(muteButton, () => { OnMute?.Invoke(); ToggleMute(); });
            Bind(switchWindowButton, () => OnSwitchWindow?.Invoke());
            Bind(scrollButton, () => OnScroll?.Invoke());
            Bind(navigationWindowButton, () => OnNavigationWindow?.Invoke());
        }

        private void Update()
        {
            if (!inputEnabled) return;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                var input = EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>();
                if (input != null && input.isFocused) return;
            }

            if (Input.GetKeyDown(backKey) || Input.GetButtonDown("Cancel")) OnBack?.Invoke();
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetButtonDown("Submit")) OnSelect?.Invoke();
            if (Input.GetKeyDown(quickChatKey)) OnQuickChat?.Invoke();
            if (Input.GetKeyDown(muteKey)) { OnMute?.Invoke(); ToggleMute(); }
            if (Input.GetKeyDown(switchWindowKey)) OnSwitchWindow?.Invoke();
            if (Input.GetAxisRaw("Mouse ScrollWheel") != 0f) OnScroll?.Invoke();

            for (int i = 0; i < tabKeys.Length; i++)
            {
                if (Input.GetKeyDown(tabKeys[i])) OnTabShortcut?.Invoke(i);
            }
        }

        /// <summary>Toggles input polling. Call from the screen OnEnable/OnDisable.</summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }

        public bool IsMuted => muted;

        public void ToggleMute()
        {
            muted = !muted;
            AudioListener.volume = muted ? 0f : 1f;
        }

        public void SetMuted(bool value)
        {
            muted = value;
            AudioListener.volume = muted ? 0f : 1f;
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
