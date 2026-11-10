using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BubbleWorld
{
    /// <summary>
    /// Centered Master Navigation modal. The Panel and all of its buttons are
    /// authored in the scene and assigned through serialized fields; this
    /// component only wires them up (Button handlers, grid focus, controller
    /// navigation) and toggles the panel via SetActive.
    /// </summary>
    public sealed class MasterNavigationScreenUI : MonoBehaviour, IManageSelectionHighlights
    {
        public const string Architect = "Architect";
        public const string World = "World";
        public const string Soulform = "Soulform";
        public const string Settings = "Settings";

        public const string ActionArchitectEdit = "architect-edit";
        public const string ActionArchitectLive = "architect-live";
        public const string ActionArchitectReport = "architect-report";
        public const string ActionWorldOverview = "world-overview";
        public const string ActionWorldNetwork = "world-network";
        public const string ActionWorldCreate = "world-create";
        public const string ActionWorldLinkApp = "world-link-app";
        public const string ActionSoulformProfile = "soulform-profile";
        public const string ActionSoulformCreate = "soulform-create";
        public const string ActionSettingsConfig = "settings-config";
        public const string ActionSettingsSave = "settings-save";
        public const string ActionSettingsExit = "settings-exit";

        [Header("Architect Column")]
        [SerializeField] private Button architectEditButton;
        [SerializeField] private Button architectLiveButton;
        [SerializeField] private Button architectReportButton;

        [Header("World Column")]
        [SerializeField] private Button worldOverviewButton;
        [SerializeField] private Button worldNetworkButton;
        [SerializeField] private Button worldCreateButton;
        [Tooltip("Link Companion App overlay; installed by Assets/Editor/LinkCompanionButtonInstaller.")]
        [SerializeField] private Button worldLinkAppButton;

        [Header("Soulform Column")]
        [SerializeField] private Button soulformProfileButton;
        [SerializeField] private Button soulformCreateButton;

        [Header("Settings Column")]
        [SerializeField] private Button settingsConfigButton;
        [SerializeField] private Button settingsSaveButton;
        [SerializeField] private Button settingsExitButton;

        [Header("Column Selected Highlights")]
        [Tooltip("Column header transforms whose first child is the Selected highlight image.")]
        [SerializeField] private Transform architectColumnHeader;
        [SerializeField] private Transform worldColumnHeader;
        [SerializeField] private Transform soulformColumnHeader;
        [SerializeField] private Transform settingsColumnHeader;

        [Header("Footer")]
        [Tooltip("Optional Back button; also closed by B / Escape.")]
        [SerializeField] private Button backButton;

        public event System.Action<string> ActionSelected;
        public event System.Action Closed;

        private AudioManager audioManager;
        private bool open;
        private bool wired;

        private sealed class NavButton
        {
            public string Id;
            public Button Btn;
            public GameObject Selected;
        }

        private readonly System.Collections.Generic.List<System.Collections.Generic.List<NavButton>> grid =
            new System.Collections.Generic.List<System.Collections.Generic.List<NavButton>>();

        private readonly System.Collections.Generic.List<GameObject> columnSelected =
            new System.Collections.Generic.List<GameObject>();

        private int focusColumn;
        private int focusRow;

        private void Awake()
        {
            audioManager = FindObjectOfType<AudioManager>();
        }

        public bool IsOpen => open;

        public void Open()
        {
            EnsureWired();
            if (!wired) return;
            audioManager?.PlaySelect();
            gameObject.SetActive(true);
            open = true;
            RefreshSelection();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(CurrentCell()?.Btn != null ? CurrentCell().Btn.gameObject : null);
        }

        public void Close()
        {
            if (!wired) { open = false; return; }
            audioManager?.PlayUiBack();
            gameObject.SetActive(false);
            open = false;
            Closed?.Invoke();
        }

        public void Toggle()
        {
            if (open) Close();
            else Open();
        }

        private void EnsureWired()
        {
            if (wired) return;

            grid.Clear();
            columnSelected.Clear();
            columnSelected.Add(FirstChild(architectColumnHeader));
            columnSelected.Add(FirstChild(worldColumnHeader));
            columnSelected.Add(FirstChild(soulformColumnHeader));
            columnSelected.Add(FirstChild(settingsColumnHeader));
            grid.Add(MakeColumn(
                new[] { architectEditButton, architectLiveButton, architectReportButton },
                new[] { ActionArchitectEdit, ActionArchitectLive, ActionArchitectReport }));
            grid.Add(MakeColumn(
                new[] { worldOverviewButton, worldNetworkButton, worldCreateButton, worldLinkAppButton },
                new[] { ActionWorldOverview, ActionWorldNetwork, ActionWorldCreate, ActionWorldLinkApp }));
            grid.Add(MakeColumn(
                new[] { soulformProfileButton, soulformCreateButton },
                new[] { ActionSoulformProfile, ActionSoulformCreate }));
            grid.Add(MakeColumn(
                new[] { settingsConfigButton, settingsSaveButton, settingsExitButton },
                new[] { ActionSettingsConfig, ActionSettingsSave, ActionSettingsExit }));

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(() =>
                {
                    if (audioManager != null) audioManager.PlayUiBack();
                    Close();
                });
            }

            wired = true;
            gameObject.SetActive(false);
        }

        private System.Collections.Generic.List<NavButton> MakeColumn(Button[] buttons, string[] ids)
        {
            var column = new System.Collections.Generic.List<NavButton>();
            for (int i = 0; i < ids.Length; i++)
            {
                Button b = buttons[i];
                if (b == null) continue;
                string id = ids[i];
                int colIndex = grid.Count;
                int rowIndex = column.Count;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() =>
                {
                    SetFocus(colIndex, rowIndex);
                    HandleAction(id);
                });
                column.Add(new NavButton { Id = id, Btn = b, Selected = FirstChild(b.transform) });
            }
            return column;
        }

        private void SetFocus(int col, int row)
        {
            if (col < 0 || col >= grid.Count) return;
            if (row < 0 || row >= grid[col].Count) return;
            focusColumn = col;
            focusRow = row;
            RefreshSelection();
        }

        private GameObject FirstChild(Component c)
        {
            if (c == null || c.transform.childCount == 0) return null;
            return c.transform.GetChild(0).gameObject;
        }

        private void RefreshSelection()
        {
            for (int c = 0; c < columnSelected.Count; c++)
            {
                if (columnSelected[c] != null)
                    columnSelected[c].SetActive(c == focusColumn);
            }
            for (int c = 0; c < grid.Count; c++)
            {
                var column = grid[c];
                for (int r = 0; r < column.Count; r++)
                {
                    if (column[r].Selected != null)
                        column[r].Selected.SetActive(c == focusColumn && r == focusRow);
                }
            }
        }

        private NavButton CurrentCell()
        {
            if (focusColumn < 0 || focusColumn >= grid.Count) return null;
            var column = grid[focusColumn];
            if (column == null || focusRow < 0 || focusRow >= column.Count) return null;
            return column[focusRow];
        }

        private void HandleAction(string id)
        {
            if (audioManager != null) audioManager.PlaySelect();
            ActionSelected?.Invoke(id);
        }

        private void Update()
        {
            if (!open || !gameObject.activeSelf) return;

            if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Submit"))
            {
                NavButton cell = CurrentCell();
                if (cell != null && cell.Btn != null) HandleAction(cell.Id);
                return;
            }

            bool moved = false;
            float horiz = Input.GetAxisRaw("Horizontal");
            float vert = Input.GetAxisRaw("Vertical");
            if (horiz > 0.5f) { MoveFocus(1, 0); moved = true; }
            else if (horiz < -0.5f) { MoveFocus(-1, 0); moved = true; }
            else if (vert > 0.5f) { MoveFocus(0, -1); moved = true; }
            else if (vert < -0.5f) { MoveFocus(0, 1); moved = true; }
            else if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveFocus(1, 0); moved = true; }
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveFocus(-1, 0); moved = true; }
            else if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveFocus(0, -1); moved = true; }
            else if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveFocus(0, 1); moved = true; }

            if (moved && EventSystem.current != null)
            {
                NavButton cell = CurrentCell();
                if (cell != null && cell.Btn != null)
                    EventSystem.current.SetSelectedGameObject(cell.Btn.gameObject);
            }
        }

        private void MoveFocus(int dcol, int drow)
        {
            int c = Mathf.Clamp(focusColumn + dcol, 0, grid.Count - 1);
            if (grid[c].Count == 0) return;
            int rowCount = Mathf.Max(1, grid[c].Count);
            int r = drow != 0
                ? Mathf.Clamp(focusRow + drow, 0, rowCount - 1)
                : Mathf.Clamp(focusRow, 0, rowCount - 1);
            focusColumn = c;
            focusRow = r;
            RefreshSelection();
        }
    }
}