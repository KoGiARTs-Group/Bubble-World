using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    [System.Serializable]
    public sealed class AssetItemEntry
    {
        public Button button;
        public string itemId;
        public Image iconImage;
        public TextMeshProUGUI nameLabel;
    }

    public sealed class EditorScreenUI : MonoBehaviour
    {
        [Header("Global Toolbar")]
        [SerializeField] private Button undoButton;
        [SerializeField] private Button redoButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button liveModeButton;
        [Space]
        [SerializeField] private TextMeshProUGUI worldNameLabel;
        [SerializeField] private TextMeshProUGUI worldSizeLabel;
        [SerializeField] private TextMeshProUGUI tilesUsedLabel;
        [SerializeField] private Image gridCapacityBar;
        [SerializeField] private TextMeshProUGUI activeLayerLabel;
        [SerializeField] private TextMeshProUGUI saveStatusLabel;

        [Header("Tool Sidebar")]
        [SerializeField] private Button[] toolButtons;

        [Header("Asset Library")]
        [SerializeField] private Button[] categoryButtons;
        [Header("Asset Library Items (serialized)")]
        [SerializeField] private AssetItemEntry[] terrainItems;
        [SerializeField] private AssetItemEntry[] waterItems;
        [SerializeField] private AssetItemEntry[] plantsItems;
        [SerializeField] private AssetItemEntry[] resourcesItems;
        [SerializeField] private AssetItemEntry[] climateItems;
        [SerializeField] private AssetItemEntry[] interventionsItems;

        [Header("Inspector Panel")]
        [SerializeField] private Image selIconImage;
        [SerializeField] private Button brushOneButton;
        [SerializeField] private Button brushThreeButton;
        [SerializeField] private Button brushFiveButton;
        [SerializeField] private Button topViewButton;
        [SerializeField] private Button angleViewButton;
        [Space]
        [SerializeField] private TextMeshProUGUI selNameLabel;
        [SerializeField] private TextMeshProUGUI selCategoryLabel;
        [SerializeField] private TextMeshProUGUI selLayerLabel;
        [SerializeField] private TextMeshProUGUI brushSizeLabel;

        [Header("Angle View")]
        [SerializeField] private GameObject angleViewPanel;
        [SerializeField] private Button northViewButton;
        [SerializeField] private Button eastViewButton;
        [SerializeField] private Button southViewButton;
        [SerializeField] private Button westViewButton;

        [Header("World Metrics")]
        [SerializeField] private TextMeshProUGUI livabilityScoreLabel;
        [SerializeField] private TextMeshProUGUI livabilityRatingLabel;
        [SerializeField] private Image livabilityFillBar;
        [SerializeField] private Button openLivabilityButton;
        [SerializeField] private Button closeLivabilityButton;
        [SerializeField] private GameObject livabilityReportPanel;
        [SerializeField] private TextMeshProUGUI popupScoreLabel;
        [SerializeField] private TextMeshProUGUI popupDetailsLabel;

        [Header("Layer Panel")]
        [SerializeField] private GameObject layerPanel;
        [SerializeField] private Button[] layerButtons;

        [Header("Rotation")]
        [SerializeField] private GameObject rotationPanel;
        [SerializeField] private Button rotate0Button;
        [SerializeField] private Button rotate90Button;
        [SerializeField] private Button rotate180Button;
        [SerializeField] private Button rotate270Button;

        [Header("World Effect")]
        [SerializeField] private TextMeshProUGUI worldEffectLabel;

        [Header("World Name")]
        [SerializeField] private Button editWorldNameButton;
        [SerializeField] private GameObject worldNamePopup;
        [SerializeField] private TMP_InputField worldNameInput;
        [SerializeField] private Button worldNameConfirmButton;
        [SerializeField] private Button worldNameCancelButton;

        [Header("Button Sprites")]
        [SerializeField] private Sprite toolButtonNormal;
        [SerializeField] private Sprite toolButtonSelected;
        [SerializeField] private Sprite layerButtonNormal;
        [SerializeField] private Sprite layerButtonSelected;

        [Header("Soulform")]
        [SerializeField] private Button createSoulformButton;
        [SerializeField] private TextMeshProUGUI soulformStatusLabel;

        [Header("Height Controls")]
        [SerializeField] private Button heightUpButton;
        [SerializeField] private Button heightDownButton;
        [SerializeField] private TextMeshProUGUI heightIndicatorLabel;

        [Header("Hints")]
        [SerializeField] private TextMeshProUGUI hintsLabel;

        public Action OnLiveMode;
        public Action OnCreateSoulform;
        public Action OnAngleViewToggle;
        public Action<int> OnDirectionSelected;
        private AudioManager audio;
        private readonly Dictionary<string, TextMeshProUGUI> labels = new Dictionary<string, TextMeshProUGUI>();

        private void Awake()
        {
            audio = FindObjectOfType<AudioManager>();
        }

        public void Build(
            EditorState state,
            BubbleWorldGrid grid,
            Action onRefresh)
        {
            Image img = GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
            BindInspectorUI(state, grid, onRefresh);
        }

        public void UpdateLabel(string key, string text)
        {
            if (labels.TryGetValue(key, out TextMeshProUGUI t) && t != null)
                t.text = text;
        }

        public void UpdateGridCapacity(float amount01)
        {
            if (gridCapacityBar != null)
                gridCapacityBar.fillAmount = amount01;
        }

        public void UpdateFillBar(float amount01)
        {
            if (livabilityFillBar != null)
                livabilityFillBar.fillAmount = amount01;
        }

        private void BindInspectorUI(EditorState state, BubbleWorldGrid grid, Action onRefresh)
        {
            RegisterLabel("WorldName", worldNameLabel);
            RegisterLabel("WorldSize", worldSizeLabel);
            RegisterLabel("TilesUsed", tilesUsedLabel);
            if (gridCapacityBar != null)
            {
                gridCapacityBar.type = Image.Type.Filled;
                gridCapacityBar.fillMethod = Image.FillMethod.Horizontal;
            }
            RegisterLabel("ActiveLayer", activeLayerLabel);
            RegisterLabel("SaveStatus", saveStatusLabel);
            RegisterLabel("SelName", selNameLabel);
            RegisterLabel("SelCategory", selCategoryLabel);
            RegisterLabel("SelLayer", selLayerLabel);
            RegisterLabel("BrushSize", brushSizeLabel);
            RegisterLabel("LivabilityScore", livabilityScoreLabel);
            RegisterLabel("LivabilityRating", livabilityRatingLabel);
            RegisterLabel("Hints", hintsLabel);
            RegisterLabel("SoulformStatus", soulformStatusLabel);
            if (popupScoreLabel != null) labels["PopupScore"] = popupScoreLabel;
            if (popupDetailsLabel != null) labels["PopupDetails"] = popupDetailsLabel;

            BindButton(undoButton, () => { if (audio != null) audio.PlaySelect(); state.OnUndo?.Invoke(); });
            BindButton(createSoulformButton, () =>
            {
                if (audio != null) audio.PlaySelect();
                OnCreateSoulform?.Invoke();
            });
            BindButton(redoButton, () => { if (audio != null) audio.PlaySelect(); state.OnRedo?.Invoke(); });
            BindButton(saveButton, () =>
            {
                if (audio != null) audio.PlaySelect();
                state.OnSave?.Invoke();
                if (saveStatusLabel != null) saveStatusLabel.text = "Saved \"" + state.WorldName + "\"!";
            });
            BindButton(liveModeButton, OnLiveMode);

            if (editWorldNameButton != null)
            {
                BindButton(editWorldNameButton, () =>
                {
                    if (audio != null) audio.PlaySelect();
                    if (worldNamePopup != null) worldNamePopup.SetActive(true);
                    if (worldNameInput != null) worldNameInput.text = state.WorldName;
                });
            }

            if (worldNameConfirmButton != null)
            {
                BindButton(worldNameConfirmButton, () =>
                {
                    if (audio != null) audio.PlaySelect();
                    if (worldNameInput != null && !string.IsNullOrEmpty(worldNameInput.text))
                        state.WorldName = worldNameInput.text;
                    if (worldNamePopup != null) worldNamePopup.SetActive(false);
                    if (worldNameLabel != null) worldNameLabel.text = state.WorldName;
                });
            }

            if (worldNameCancelButton != null)
            {
                BindButton(worldNameCancelButton, () =>
                {
                    if (audio != null) audio.PlayUiBack();
                    if (worldNamePopup != null) worldNamePopup.SetActive(false);
                });
            }

            Array toolValues = Enum.GetValues(typeof(EditorTool));
            if (toolButtons != null)
            {
                for (int i = 0; i < toolButtons.Length && i < toolValues.Length; i++)
                {
                    int idx = i;
                    EditorTool tool = (EditorTool)toolValues.GetValue(idx);
                    Button btn = toolButtons[idx];
                    if (btn != null && btn.image != null)
                        btn.image.sprite = tool == state.ActiveTool ? toolButtonSelected : toolButtonNormal;
                    BindButton(btn, () =>
                    {
                        if (audio != null) audio.PlaySwitch();
                        state.ActiveTool = tool;
                        if (tool == EditorTool.Layers) state.ShowLayerPanel = !state.ShowLayerPanel;
                        onRefresh();
                    });
                }
            }

            if (livabilityFillBar != null)
            {
                livabilityFillBar.type = Image.Type.Filled;
                livabilityFillBar.fillMethod = Image.FillMethod.Radial360;
                livabilityFillBar.fillOrigin = (int)Image.Origin360.Top;
                livabilityFillBar.fillClockwise = true;
            }

            if (categoryButtons != null)
            {
                Array tabValues = Enum.GetValues(typeof(BuildTab));
                for (int i = 0; i < categoryButtons.Length && i < tabValues.Length; i++)
                {
                    int idx = i;
                    BuildTab tab = (BuildTab)tabValues.GetValue(idx);
                    BindButton(categoryButtons[idx], () =>
                    {
                        if (audio != null) audio.PlaySwitch();
                        state.SelectedTab = tab;
                        var items = BubblePlacementCatalog.ForTab(tab);
                        if (items.Count > 0 && state.SelectedItem == null)
                            state.SelectedItem = items[0];
                        ShowAssetGroup(tab);
                        BuildAssetItems(state, onRefresh);
                        onRefresh();
                    });
                }
            }

            BindButton(brushOneButton, () => { if (audio != null) audio.PlaySwitch(); state.SetBrushSize(BrushSize.One, onRefresh); });
            BindButton(brushThreeButton, () => { if (audio != null) audio.PlaySwitch(); state.SetBrushSize(BrushSize.Three, onRefresh); });
            BindButton(brushFiveButton, () => { if (audio != null) audio.PlaySwitch(); state.SetBrushSize(BrushSize.Five, onRefresh); });
            if (heightUpButton != null) heightUpButton.gameObject.SetActive(false);
            if (heightDownButton != null) heightDownButton.gameObject.SetActive(false);
            BindButton(topViewButton, () =>
            {
                if (audio != null) audio.PlaySwitch();
                state.ShowAnglePanel = false;
                state.Current3DView = Camera3DView.Top;
                grid?.Set3DView(Camera3DView.Top);
                onRefresh();
            });
            BindButton(angleViewButton, () =>
            {
                if (audio != null) audio.PlaySwitch();
                state.ShowAnglePanel = !state.ShowAnglePanel;
                OnAngleViewToggle?.Invoke();
                onRefresh();
            });

            BindButton(northViewButton, () => { if (audio != null) audio.PlaySwitch(); OnDirectionSelected?.Invoke(0); });
            BindButton(eastViewButton, () => { if (audio != null) audio.PlaySwitch(); OnDirectionSelected?.Invoke(90); });
            BindButton(southViewButton, () => { if (audio != null) audio.PlaySwitch(); OnDirectionSelected?.Invoke(180); });
            BindButton(westViewButton, () => { if (audio != null) audio.PlaySwitch(); OnDirectionSelected?.Invoke(270); });

            if (heightIndicatorLabel != null) heightIndicatorLabel.gameObject.SetActive(false);
            if (layerPanel != null)
            {
                layerPanel.SetActive(state.ShowLayerPanel);
                if (layerButtons != null)
                {
                    string[] layerNames = { "Underground", "Ground/Water", "Living Env", "Elevated", "Sky/Atmo" };
                    int layerIdx = state.SelectedItem != null ? (int)state.SelectedItem.Layer + 1 : state.ActiveLayerIndex;
                    for (int i = 0; i < layerButtons.Length && i < layerNames.Length; i++)
                    {
                        layerButtons[i].interactable = false;
                        ApplyLayerSprite(layerButtons[i], i == layerIdx);
                    }
                }
            }

            if (angleViewPanel != null)
                angleViewPanel.SetActive(state.ShowAnglePanel);

            if (state.Current3DView == Camera3DView.Angle45)
            {
                SetHighlight(northViewButton, state.CameraAngle == 0);
                SetHighlight(eastViewButton, state.CameraAngle == 90);
                SetHighlight(southViewButton, state.CameraAngle == 180);
                SetHighlight(westViewButton, state.CameraAngle == 270);
            }
            else
            {
                SetHighlight(northViewButton, false);
                SetHighlight(eastViewButton, false);
                SetHighlight(southViewButton, false);
                SetHighlight(westViewButton, false);
            }

            if (rotationPanel != null)
                rotationPanel.SetActive(state.ActiveTool == EditorTool.Rotate);

            BindButton(rotate0Button, () => { if (audio != null) audio.PlaySelect(); state.SelectedRotation = 0; state.ActiveTool = EditorTool.Rotate; onRefresh(); });
            BindButton(rotate90Button, () => { if (audio != null) audio.PlaySelect(); state.SelectedRotation = 1; state.ActiveTool = EditorTool.Rotate; onRefresh(); });
            BindButton(rotate180Button, () => { if (audio != null) audio.PlaySelect(); state.SelectedRotation = 2; state.ActiveTool = EditorTool.Rotate; onRefresh(); });
            BindButton(rotate270Button, () => { if (audio != null) audio.PlaySelect(); state.SelectedRotation = 3; state.ActiveTool = EditorTool.Rotate; onRefresh(); });

            BindButton(openLivabilityButton, () => { if (audio != null) audio.PlaySelect(); state.LivabilityExpanded = true; onRefresh(); });
            BindButton(closeLivabilityButton, () => { if (audio != null) audio.PlayUiBack(); state.LivabilityExpanded = false; onRefresh(); });

            if (livabilityReportPanel != null)
                livabilityReportPanel.SetActive(state.LivabilityExpanded);

            if (worldNamePopup != null)
                worldNamePopup.SetActive(false);

            BuildAssetItems(state, onRefresh);
            UpdateInspectorLabels(state);
            HighlightSelectedItem(state);
        }

        private void HighlightSelectedItem(EditorState state)
        {
            AssetItemEntry[] entries = GetAssetEntries(state.SelectedTab);
            if (entries == null) return;
            foreach (AssetItemEntry e in entries)
            {
                if (e == null || e.button == null) continue;
                bool isSelected = state.SelectedItem != null && state.SelectedItem.Id == e.itemId;
                if (!SetHighlightByChild(e.button, isSelected) && e.button.image != null)
                {
                    e.button.image.color = isSelected
                        ? new Color(0.3f, 0.85f, 0.5f, 0.3f)
                        : Color.white;
                }
            }
        }

        private AssetItemEntry[] GetAssetEntries(BuildTab tab)
        {
            switch (tab)
            {
                case BuildTab.Terrain: return terrainItems;
                case BuildTab.Water: return waterItems;
                case BuildTab.Plants: return plantsItems;
                case BuildTab.Resources: return resourcesItems;
                case BuildTab.Climate: return climateItems;
                case BuildTab.Interventions: return interventionsItems;
                default: return null;
            }
        }

        private void ShowAssetGroup(BuildTab tab)
        {
            foreach (BuildTab t in System.Enum.GetValues(typeof(BuildTab)))
            {
                AssetItemEntry[] entries = GetAssetEntries(t);
                if (entries == null) continue;
                foreach (AssetItemEntry e in entries)
                {
                    if (e == null || e.button == null) continue;
                    e.button.gameObject.SetActive(t == tab);
                }
            }
        }

        private void BuildAssetItems(EditorState state, Action onRefresh)
        {
            AssetItemEntry[] serialized = GetAssetEntries(state.SelectedTab);
            if (serialized == null || serialized.Length == 0) return;
            ShowAssetGroup(state.SelectedTab);
            foreach (AssetItemEntry e in serialized)
            {
                if (e == null || e.button == null) continue;
                PlacementItem asset = BubblePlacementCatalog.FindItemAsset(e.itemId);
                if (asset != null)
                {
                    if (e.iconImage != null)
                    {
                        if (asset.Icon != null)
                        {
                            e.iconImage.sprite = asset.Icon;
                            e.iconImage.color = Color.white;
                        }
                        else
                        {
                            e.iconImage.sprite = null;
                            e.iconImage.color = asset.TintColor;
                        }
                    }
                    if (e.nameLabel != null)
                        e.nameLabel.text = asset.DisplayName;
                }
                BindButton(e.button, () =>
                {
                    BubblePlacementDefinition item = BubblePlacementCatalog.Find(e.itemId);
                    if (item == null)
                        item = BubblePlacementCatalog.All.FirstOrDefault(d =>
                            d.Tab == state.SelectedTab && d.Id.Contains(e.itemId));
                    if (item != null) state.SelectedItem = item;
                    onRefresh();
                });
            }
        }

        private void UpdateInspectorLabels(EditorState state)
        {
            if (worldNameLabel != null)
                worldNameLabel.text = state.WorldName;

            if (selLayerLabel != null)
                selLayerLabel.text = "Layer: " + (state.SelectedItem != null ? state.SelectedItem.Layer.ToString() : "-");
            if (layerButtons != null)
            {
                int layerIdx = state.SelectedItem != null ? (int)state.SelectedItem.Layer + 1 : state.ActiveLayerIndex;
                for (int i = 0; i < layerButtons.Length; i++)
                    ApplyLayerSprite(layerButtons[i], i == layerIdx);
            }

            if (worldEffectLabel != null && state.SelectedItem != null)
                worldEffectLabel.text = "Effects: " + DescribeEffects(state.SelectedItem);
            else if (worldEffectLabel != null)
                worldEffectLabel.text = "Effects: -";

            if (rotationPanel != null)
            {
                rotationPanel.SetActive(state.ActiveTool == EditorTool.Rotate);
                SetHighlight(rotate0Button, state.SelectedRotation == 0);
                SetHighlight(rotate90Button, state.SelectedRotation == 1);
                SetHighlight(rotate180Button, state.SelectedRotation == 2);
                SetHighlight(rotate270Button, state.SelectedRotation == 3);
            }

            string selId = state.SelectedItem?.Id;
            if (selIconImage != null)
            {
                PlacementItem asset = selId != null ? BubblePlacementCatalog.FindItemAsset(selId) : null;
                if (asset != null && asset.Icon != null)
                {
                    selIconImage.sprite = asset.Icon;
                    selIconImage.color = Color.white;
                }
                else
                {
                    selIconImage.sprite = null;
                    selIconImage.color = asset != null ? asset.TintColor : Color.clear;
                }
            }
            if (selNameLabel != null)
                selNameLabel.text = state.SelectedItem != null ? state.SelectedItem.DisplayName : "None";
            if (selCategoryLabel != null)
                selCategoryLabel.text = "Category: " + (state.SelectedItem != null ? state.SelectedItem.Tab.ToString() : "-");
            if (brushSizeLabel != null)
                brushSizeLabel.text = state.BrushSizeLabel();
            if (activeLayerLabel != null)
            {
                string[] names = { "Underground", "Ground/Water", "Living Env", "Elevated", "Sky/Atmo" };
                int idx = state.SelectedItem != null ? (int)state.SelectedItem.Layer + 1 : state.ActiveLayerIndex;
                idx = Mathf.Clamp(idx, 0, names.Length - 1);
                activeLayerLabel.text = names[idx];
            }
            if (toolButtons != null)
            {
                Array toolValues = Enum.GetValues(typeof(EditorTool));
                for (int i = 0; i < toolButtons.Length && i < toolValues.Length; i++)
                {
                    bool isActive = (EditorTool)toolValues.GetValue(i) == state.ActiveTool;
                    if (!SetHighlightByChild(toolButtons[i], isActive) && toolButtons[i] != null && toolButtons[i].image != null)
                        toolButtons[i].image.sprite = isActive ? toolButtonSelected : toolButtonNormal;
                }
            }
            if (hintsLabel != null)
                hintsLabel.text = ToolHint(state.ActiveTool);
        }

        private static string ToolHint(EditorTool tool)
        {
            string raw;
            switch (tool)
            {
                case EditorTool.Select: raw = "LClick: Inspect + select  |  Middle-Drag: Pan  |  Wheel: Zoom  |  Esc: Menu"; break;
                case EditorTool.MoveView: raw = "RClick-Drag: Orbit  |  Middle-Drag: Pan  |  Wheel: Zoom  |  Esc: Menu"; break;
                case EditorTool.AddPlace: raw = "LClick: Place item  |  RClick: Remove top  |  Middle-Drag: Pan  |  Wheel: Zoom  |  Esc: Menu"; break;
                case EditorTool.Delete: raw = "LClick: Remove item  |  Middle-Drag: Pan  |  Wheel: Zoom  |  Esc: Menu"; break;
                case EditorTool.Rotate: raw = "Click a rotation preset then click an object to apply  |  Esc: Menu"; break;
                default: raw = "Middle-Drag: Pan  |  Wheel: Zoom  |  Esc: Menu"; break;
            }
            return FormatHint(raw);
        }

        private static string FormatHint(string raw)
        {
            string[] parts = raw.Split(new[] { "  |  " }, System.StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                int colon = parts[i].IndexOf(": ");
                if (colon > 0)
                    parts[i] = "<color=green>" + parts[i].Substring(0, colon) + "</color><color=white>" + parts[i].Substring(colon) + "</color>";
                else
                    parts[i] = "<color=white>" + parts[i] + "</color>";
            }
            return string.Join("<color=red>  |  </color>", parts);
        }

        private void ApplyLayerSprite(Button btn, bool selected)
        {
            if (btn == null || btn.image == null) return;
            if (layerButtonNormal != null && layerButtonSelected != null)
                btn.image.sprite = selected ? layerButtonSelected : layerButtonNormal;
            else
                btn.image.color = selected ? new Color(0.3f, 0.85f, 0.5f, 0.3f) : Color.white;
        }

        private void RegisterLabel(string key, TextMeshProUGUI text)
        {
            if (text != null) labels[key] = text;
        }

        private static void BindButton(Button button, Action action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }

        private static void SetHighlight(Button button, bool highlight)
        {
            if (button == null) return;
            if (SetHighlightByChild(button, highlight)) return;
            if (button.image == null) return;
            button.image.color = highlight
                ? new Color(0.3f, 0.85f, 0.5f, 0.3f)
                : Color.white;
        }

        private static bool SetHighlightByChild(Button button, bool active)
        {
            if (button == null) return false;
            for (int i = 0; i < button.transform.childCount; i++)
            {
                GameObject child = button.transform.GetChild(i).gameObject;
                if (child.name == "Selected" || child.name == "Selection")
                {
                    child.SetActive(active);
                    return true;
                }
            }
            return false;
        }

        private static string DescribeEffects(BubblePlacementDefinition item)
        {
            if (item == null) return "-";
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            if (item.Food > 0) parts.Add("Food+" + item.Food);
            if (item.Water > 0) parts.Add("Water+" + item.Water);
            if (item.Shelter > 0) parts.Add("Shelter+" + item.Shelter);
            if (item.Light > 0) parts.Add("Light+" + item.Light);
            if (item.Safety > 0) parts.Add("Safety+" + item.Safety);
            if (item.Weather > 0) parts.Add("Weather+" + item.Weather);
            if (item.Stability > 0) parts.Add("Stability+" + item.Stability);
            if (item.Space > 0) parts.Add("Space+" + item.Space);
            if (item.Food < 0) parts.Add("Food" + item.Food);
            if (item.Light < 0) parts.Add("Light" + item.Light);
            if (item.Space < 0) parts.Add("Space" + item.Space);
            return parts.Count > 0 ? string.Join(", ", parts) : "None";
        }
    }
}
