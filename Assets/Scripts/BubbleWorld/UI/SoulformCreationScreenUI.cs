using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    public sealed class SoulformCreationScreenUI : MonoBehaviour, IManageSelectionHighlights
    {
        private static readonly string[] RoleNames =
        {
            "Not", "Explorer", "Hero", "Social", "Sage",
            "Ruler", "CareGiver", "Rabel", "Creater"
        };

        private static readonly string[] RoleDescriptions =
        {
            "A free spirit with no fixed role yet.",
            "Learns skills faster. Drawn to distant lands and hidden paths.",
            "Protects the world and everyone inside it.",
            "Thrives among others. Brings the bubble to life.",
            "Wisdom grows with time. Sees the shape of things to come.",
            "Guides the world with a steady, patient hand.",
            "Nurtures plants and creatures with endless care.",
            "Turns wild ideas into wonderful things.",
            "Builds new worlds out of nothing."
        };

        private static readonly SoulformTemperament[] RoleTemperaments =
        {
            SoulformTemperament.Inquisitive,
            SoulformTemperament.Inquisitive,
            SoulformTemperament.Assertive,
            SoulformTemperament.Harmonious,
            SoulformTemperament.Resilient,
            SoulformTemperament.Assertive,
            SoulformTemperament.Harmonious,
            SoulformTemperament.Creative,
            SoulformTemperament.Creative
        };

        [Header("Left Category Tabs")]
        [SerializeField] private Button identityTab;
        [SerializeField] private Button formTab;
        [SerializeField] private Button clothesTab;

        [Header("Role Picker")]
        [SerializeField] private Transform roleCardsContainer;
        [SerializeField] private ScrollRect roleScrollRect;
        [SerializeField] private Button previousRoleButton;
        [SerializeField] private Button nextRoleButton;

        [Header("Summary")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text natureLabel;
        [SerializeField] private TMP_Text detailsLabel;
        [SerializeField] private TMP_Text voiceLabel;

        [Header("Actions")]
        [SerializeField] private Button saveContinueButton;
        [SerializeField] private Button networkButton;

        [Header("Screen Active Object")]
        [Tooltip("Activated whenever this screen UI is active, deactivated when it is hidden.")]
        [SerializeField] private GameObject screenActiveObject;

        private SoulformCreationController controller;
        private AudioManager audio;
        private readonly List<Button> roleCards = new List<Button>();
        private readonly List<int> roleCardIndexes = new List<int>();
        private int currentRoleIndex = 1;

        private void Awake()
        {
            audio = FindObjectOfType<AudioManager>();
        }

        private void OnEnable()
        {
            if (screenActiveObject != null) screenActiveObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (screenActiveObject != null) screenActiveObject.SetActive(false);
        }

        private void Update()
        {
            if (controller == null) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (audio != null) audio.PlayUiBack();
                controller.GoBack();
            }
        }

        public void Bind(SoulformCreationController c)
        {
            controller = c;

            if (controller.RestoreProgress())
                controller.Initialize(controller.Data);

            DiscoverRoleCards();
            BindTabs();
            BindRoleCards();
            BindScroll();
            BindActions();

            int saved = Array.IndexOf(RoleNames, controller.Data.Nature);
            currentRoleIndex = saved >= 0 ? saved : 1;
            SelectRole(currentRoleIndex);

            if (nameLabel != null && !string.IsNullOrWhiteSpace(controller.Data.Name))
                nameLabel.text = controller.Data.Name;
        }

        private void DiscoverRoleCards()
        {
            roleCards.Clear();
            roleCardIndexes.Clear();
            if (roleCardsContainer == null) return;
            foreach (Button card in roleCardsContainer.GetComponentsInChildren<Button>(true))
            {
                if (card.transform.parent != roleCardsContainer) continue;
                int idx = Array.IndexOf(RoleNames, card.name);
                if (idx < 0) continue;
                roleCards.Add(card);
                roleCardIndexes.Add(idx);
            }
        }

        private void BindTabs()
        {
            BindTab(identityTab, 0);
            BindTab(formTab, 1);
            BindTab(clothesTab, 2);
            HighlightTab(0);
        }

        private void BindTab(Button tab, int category)
        {
            if (tab == null) return;
            tab.onClick.RemoveAllListeners();
            tab.onClick.AddListener(() =>
            {
                if (audio != null) audio.PlaySwitch();
                controller?.SetCategory(category);
                HighlightTab(category);
            });
        }

        private void HighlightTab(int category)
        {
            SetTabSelected(identityTab, category == 0);
            SetTabSelected(formTab, category == 1);
            SetTabSelected(clothesTab, category == 2);
        }

        private static void SetTabSelected(Button tab, bool selected)
        {
            if (tab == null || tab.transform.childCount == 0) return;
            tab.transform.GetChild(0).gameObject.SetActive(selected);
        }

        private void BindRoleCards()
        {
            for (int i = 0; i < roleCards.Count; i++)
            {
                int cardIndex = roleCardIndexes[i];
                Button card = roleCards[i];
                card.onClick.RemoveAllListeners();
                card.onClick.AddListener(() =>
                {
                    if (audio != null) audio.PlaySelect();
                    SelectRole(cardIndex);
                });
            }
        }

        private void SelectRole(int roleIndex)
        {
            if (roleIndex < 0 || roleIndex >= RoleNames.Length) return;
            currentRoleIndex = roleIndex;

            for (int i = 0; i < roleCards.Count; i++)
            {
                int cardIndex = roleCardIndexes[i];
                SetCardSelected(roleCards[i], cardIndex == roleIndex);
            }

            if (natureLabel != null) natureLabel.text = RoleNames[roleIndex];
            if (detailsLabel != null) detailsLabel.text = RoleDescriptions[roleIndex];
            if (voiceLabel != null && string.IsNullOrWhiteSpace(voiceLabel.text))
                voiceLabel.text = "Voice M" + ((roleIndex % 5) + 1);
        }

        private static void SetCardSelected(Button card, bool selected)
        {
            if (card == null) return;
            foreach (Transform child in card.transform)
            {
                if (child.name == "Selected" || child.name == "Selection")
                    child.gameObject.SetActive(selected);
            }
        }

        private void BindScroll()
        {
            if (previousRoleButton != null)
            {
                previousRoleButton.onClick.RemoveAllListeners();
                previousRoleButton.onClick.AddListener(() =>
                {
                    if (audio != null) audio.PlaySwitch();
                    ScrollRoles(-0.25f);
                });
            }
            if (nextRoleButton != null)
            {
                nextRoleButton.onClick.RemoveAllListeners();
                nextRoleButton.onClick.AddListener(() =>
                {
                    if (audio != null) audio.PlaySwitch();
                    ScrollRoles(0.25f);
                });
            }
        }

        private void ScrollRoles(float delta)
        {
            if (roleScrollRect == null) return;
            roleScrollRect.horizontalNormalizedPosition =
                Mathf.Clamp01(roleScrollRect.horizontalNormalizedPosition + delta);
        }

        private void BindActions()
        {
            if (networkButton != null)
            {
                networkButton.onClick.RemoveAllListeners();
                networkButton.onClick.AddListener(() =>
                {
                    if (audio != null) audio.PlaySwitch();
                });
            }

            if (saveContinueButton != null)
            {
                saveContinueButton.onClick.RemoveAllListeners();
                saveContinueButton.onClick.AddListener(SaveAndContinue);
            }
        }

        private void SaveAndContinue()
        {
            if (audio != null) audio.PlaySelect();

            if (controller == null) return;
            controller.SetName(nameLabel != null ? nameLabel.text.Trim() : "");
            controller.SetTemperament(RoleTemperaments[currentRoleIndex]);
            controller.Data.Nature = RoleNames[currentRoleIndex];
            controller.SetVoicePreset(voiceLabel != null ? voiceLabel.text.Trim() : "default");

            if (controller.Validate())
            {
                controller.SaveAndContinue();
            }
            else
            {
                Debug.LogWarning("Soulform creation blocked: " + controller.Data.Validate().ErrorMessage);
            }
        }
    }
}
