using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class BubbleWorldApp : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private GridConfig gridConfig3D;
        [SerializeField] private UITheme uiTheme;
        [SerializeField] private PlacementCatalog placementCatalog;

        [Header("Layers")]
        [SerializeField] private WorldLayerConfig layerConfig;

        [Header("Visual Profiles (one per StarterEnvironment, indexed by enum order)")]
        [SerializeField] private EnvironmentVisualProfile[] visualProfiles;

        [Header("Environment")]
        [SerializeField] private LightingManager lightingManager;

        [Header("Audio")]
        [SerializeField] private AudioManager audioManager;

        [Header("UI Screen System (assign in Inspector)")]
        [SerializeField] private UIScreenController screenController;
        [SerializeField] private TitleScreenUI titleUI;
        [SerializeField] private StarterScreenUI starterUI;
        [SerializeField] private EditorScreenUI editorUI;
        [SerializeField] private LiveScreenUI liveUI;
        [SerializeField] private SoulformCreationScreenUI soulformCreationUI;
        [SerializeField] private SoulformChatScreenUI soulformChatUI;
        [SerializeField] private SoulformProfileScreenUI soulformProfileUI;
        [SerializeField] private MasterNavigationScreenUI masterNavigationUI;

        [Header("Soulform Brain")]
        [SerializeField] private SoulformBrainConfig brainConfig;

        [Header("Game Balance Configs")]
        [SerializeField] private GameTimeConfig gameTimeConfig;
        [SerializeField] private WeatherConfig weatherConfig;
        [SerializeField] private SourceLogicConfig sourceLogicConfig;
        [SerializeField] private AIConfig aiConfig;
        [SerializeField] private SpawnConfig spawnConfig;
        [SerializeField] private SkillConfig skillConfig;
        [SerializeField] private EconomyConfig economyConfig;
        [SerializeField] private MemoryConfig memoryConfig;

        public static PlacementCatalog ActiveCatalog;

        private Camera mainCamera;
        private BubbleWorldGrid grid;
        private BubbleModel bubble;
        private EditorState state;
        private EditorMode editorMode;
        private CompanySplashScreenUI splash;
        private UIScreenId soulformBackTarget = UIScreenId.Editor;

        public WatchState WatchState;
        public SoulformModel CurrentSoulform;

        private IAppMode activeMode;
        private SelectionHighlightController selectionHighlights;

        public EventLog WorldEventLog { get; private set; }

        public BubbleModel Bubble => bubble;
        public BubbleWorldGrid Grid => grid;
        public EditorState State => state;
        public EditorScreenUI EditorScreen => editorUI;
        public LiveScreenUI LiveScreen => liveUI;
        public UIScreenController ScreenController => screenController;
        public AudioManager Audio => audioManager;
        public SoulformBrainConfig BrainConfig => brainConfig;
        public GameTimeConfig GameTimeConfig => gameTimeConfig;
        public WeatherConfig WeatherConfig => weatherConfig;
        public SourceLogicConfig SourceLogicConfig => sourceLogicConfig;
        public AIConfig AIConfig => aiConfig;
        public SpawnConfig SpawnConfig => spawnConfig;
        public SkillConfig SkillConfig => skillConfig;
        public EconomyConfig EconomyConfig => economyConfig;
        public MemoryConfig MemoryConfig => memoryConfig;
        public SoulformChatScreenUI SoulformChatScreen => soulformChatUI;
        public SoulformProfileScreenUI SoulformProfileScreen => soulformProfileUI;

        public MasterNavigationScreenUI MasterNavigation => masterNavigationUI;

        private EditorMode EditorModeInstance
        {
            get
            {
                if (editorMode == null) editorMode = new EditorMode();
                return editorMode;
            }
        }

        public IAppMode ActiveMode
        {
            get => activeMode;
            private set => activeMode = value;
        }

        public void SetMode(IAppMode mode)
        {
            activeMode?.OnExit(this);
            activeMode = mode;
            activeMode?.OnEnter(this);
        }

        private void HandleMasterNavAction(string actionId)
        {
            switch (actionId)
            {
                case MasterNavigationScreenUI.ActionArchitectEdit:
                    masterNavigationUI?.Close();
                    SetMode(EditorModeInstance);
                    break;

                case MasterNavigationScreenUI.ActionArchitectLive:
                    masterNavigationUI?.Close();
                    if (Bubble != null)
                    {
                        if (CurrentSoulform == null)
                            CurrentSoulform = SoulformModel.CreateDefault(SoulformTemperament.Inquisitive);
                        SetMode(WatchMode.Instance);
                    }
                    break;

                case MasterNavigationScreenUI.ActionArchitectReport:
                case MasterNavigationScreenUI.ActionWorldOverview:
                // Bubble Network is reserved for its own purpose (status/links);
                // the companion pairing moved to ActionWorldLinkApp below.
                case MasterNavigationScreenUI.ActionWorldNetwork:
                case MasterNavigationScreenUI.ActionSettingsConfig:
                    masterNavigationUI?.Close();
                    Debug.Log("Master Navigation: " + actionId);
                    break;

                case MasterNavigationScreenUI.ActionWorldLinkApp:
                    masterNavigationUI?.Close();
                    ShowPairingScreen();
                    break;

                case MasterNavigationScreenUI.ActionWorldCreate:
                    masterNavigationUI?.Close();
                    ShowStarterSelection();
                    break;

                case MasterNavigationScreenUI.ActionSoulformProfile:
                    masterNavigationUI?.Close();
                    if (screenController != null && soulformProfileUI != null)
                    {
                        ShowScreen(UIScreenId.SoulformProfile);
                        if (WatchState != null) soulformProfileUI.Build(WatchState);
                    }
                    break;

                case MasterNavigationScreenUI.ActionSoulformCreate:
                    masterNavigationUI?.Close();
                    ShowSoulformCreation();
                    break;

                case MasterNavigationScreenUI.ActionSettingsSave:
                    masterNavigationUI?.Close();
                    SaveBubble();
                    break;

                case MasterNavigationScreenUI.ActionSettingsExit:
                    masterNavigationUI?.Close();
                    Application.Quit();
                    break;

                default:
                    masterNavigationUI?.Close();
                    break;
            }
        }

        private PairingScreenUI pairingUI;

        // World → Link Companion App: pair the companion app. Prefers the
        // scene-authored panel (Canvas ▸ Pairing Screen); falls back to a
        // code-built overlay when the scene has none.
        private void ShowPairingScreen()
        {
            if (pairingUI == null)
                pairingUI = FindObjectOfType<PairingScreenUI>(true);
            if (pairingUI == null)
            {
                var go = new GameObject("Pairing Screen");
                go.transform.SetParent(transform, false);
                pairingUI = go.AddComponent<PairingScreenUI>();
            }
            pairingUI.Open();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<BubbleWorldApp>() != null) return;
            GameObject app = new GameObject("Bubble World App");
            app.AddComponent<BubbleWorldApp>();
        }

        private void Awake()
        {
            mainCamera = Camera.main != null ? Camera.main : new GameObject("Main Camera").AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = uiTheme != null ? uiTheme.ScreenBackground : new Color(0.04f, 0.08f, 0.1f);

            EnsureEventSystem();
            ActiveCatalog = placementCatalog;
            WorldEventLog = new EventLog();

            state = new EditorState
            {
                OnSave = SaveBubble,
                OnLoad = () => LoadBubble(state.WorldName),
                OnUndo = UndoAction,
                OnRedo = RedoAction
            };
            GridConfig cfg = gridConfig3D != null ? gridConfig3D : GridConfig.CreateDefault();
            state.Current3DView = cfg.Default3DView;

            InitGrid();
            editorMode = EditorModeInstance;
            EnsureSelectionHighlights();
            if (masterNavigationUI != null)
                masterNavigationUI.ActionSelected += HandleMasterNavAction;
            ShowSplash();

            // Backend links + startup world pull: the backend is the single source
            // of truth when this client has no local save (see StartupServerSync).
            BackendGateway.Configure(brainConfig);
            StartCoroutine(StartupServerSync());
        }

        private void EnsureSelectionHighlights()
        {
            if (screenController == null) return;
            Transform root = screenController.transform;
            while (root.parent != null) root = root.parent;
            selectionHighlights = root.GetComponent<SelectionHighlightController>();
            if (selectionHighlights == null)
                selectionHighlights = root.gameObject.AddComponent<SelectionHighlightController>();
        }

        private void ShowScreen(UIScreenId id)
        {
            screenController.ShowScreen(id);
            if (selectionHighlights != null) selectionHighlights.RebindAll();
        }

        public void ShowSplash()
        {
            ShowScreen(UIScreenId.Title);
            if (splash == null)
            {
                GameObject overlay = new GameObject("Company Splash");
                Canvas canvas = overlay.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                UnityEngine.UI.CanvasScaler scaler = overlay.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                overlay.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                splash = overlay.AddComponent<CompanySplashScreenUI>();
            }
            splash.Show(ShowTitle);
        }

        private void InitGrid()
        {
            if (grid != null) grid.Destroy();
            GridConfig config = gridConfig3D;
            if (config == null) config = GridConfig.CreateDefault();
            grid = new BubbleWorldGrid(new GameObject("Bubble Grid").transform, mainCamera);
            grid.SetConfig(config);
            if (layerConfig != null)
                grid.SetLayerConfig(layerConfig);
            else
                grid.SetLayerConfig(WorldLayerConfig.CreateDefault());
        }

        private void Update()
        {
            if (activeMode != null)
            {
                activeMode.OnUpdate(this);
            }
        }

        public void ShowTitle()
        {
            ShowScreen(UIScreenId.Title);
            titleUI.Bind(ShowStarterSelection, () => ShowLoadWorldList(), null);
            if (audioManager != null) audioManager.StopBgm();
        }

        private void ShowLoadWorldList()
        {
            string[] worlds = BubbleSaveService.ListSavedWorlds();
            if (worlds.Length == 0)
            {
                if (BubbleSaveService.TryLoadLegacy(out var legacy))
                {
                    LoadFromResult(legacy);
                    return;
                }
            }
            titleUI.ShowLoadWorld(worlds, LoadBubble, ShowTitle);
        }

        private void ShowStarterSelection()
        {
            ShowScreen(UIScreenId.StarterSelection);
            starterUI.Bind(StartBubble, ShowTitle);
            if (audioManager != null) audioManager.PlaySelect();
        }

        public void StartVerticalSlice()
        {
            string savedName = state.WorldName;
            StartBubble(StarterEnvironment.MeadowGrassland);
            state.WorldName = !string.IsNullOrEmpty(savedName) ? savedName : "BubbleWorld";
        }

        private void StartBubble(StarterEnvironment environment)
        {
            bubble = BubbleStarterFactory.Create(environment);
            state.SelectedItem = BubblePlacementCatalog.DefaultFor(environment);
            state.SelectedTab = state.SelectedItem.Tab;
            state.WorldName = environment.ToString() + " World";
            CurrentSoulform = null;
            if (grid != null)
            {
                grid.SetEnvironmentVisualProfile(GetVisualProfile(environment));
                grid.Show(bubble);
            }
            if (lightingManager != null) lightingManager.ApplyProfile(environment);
            if (audioManager != null)
            {
                audioManager.StopBgm();
                audioManager.PlayBgmForWorld(environment);
            }
            ShowSoulformCreation(true);
        }

        private EnvironmentVisualProfile GetVisualProfile(StarterEnvironment environment)
        {
            int index = (int)environment;
            if (visualProfiles != null && index >= 0 && index < visualProfiles.Length)
                return visualProfiles[index];
            return null;
        }

        public void ShowEditorUi()
        {
            if (screenController != null && editorUI != null)
            {
                ShowScreen(UIScreenId.Editor);
                editorUI.OnLiveMode = () => SetMode(WatchMode.Instance);
                editorUI.OnCreateSoulform = () => ShowSoulformCreation();
                editorUI.OnAngleViewToggle = () => { };
                editorUI.OnDirectionSelected = (angle) =>
                {
                    state.ShowAnglePanel = false;
                    state.CameraAngle = angle;
                    state.Current3DView = Camera3DView.Angle45;
                    grid?.SetViewAngle(angle);
                    grid?.Set3DView(Camera3DView.Angle45);
                    ShowEditorUi();
                };
                editorUI.Build(state, grid, ShowEditorUi);
            }
            RefreshLivability();
        }

        public void ShowSoulformCreation(bool fromStarter = false)
        {
            soulformBackTarget = fromStarter ? UIScreenId.StarterSelection : UIScreenId.Editor;
            ShowScreen(UIScreenId.SoulformCreation);
            if (soulformCreationUI != null)
            {
                var controller = new SoulformCreationController();
                controller.OnCreationCompleted += OnSoulformCreated;
                controller.OnBackPressed += ReturnFromSoulformCreation;
                soulformCreationUI.Bind(controller);
            }
        }

        private void ReturnFromSoulformCreation()
        {
            if (soulformBackTarget == UIScreenId.StarterSelection)
            {
                SetMode(null);
                ShowScreen(UIScreenId.StarterSelection);
            }
            else
            {
                SetMode(EditorModeInstance);
            }
        }

        private void OnSoulformCreated(SoulformModel soulform)
        {
            CurrentSoulform = soulform;
            WorldEventLog?.Record(GameEventType.SoulformReacted,
                soulform.Name + " has been created!");
            if (audioManager != null) audioManager.PlaySelect();
            if (masterNavigationUI != null) masterNavigationUI.Open();
        }

        public void UndoAction()
        {
            if (editorMode == null || bubble == null) return;
            var undoRedo = editorMode.UndoRedo;
            if (undoRedo.Undo(bubble) && grid != null)
            {
                bubble.RebuildOwnedCells();
                grid.RefreshAll();
                RefreshLivability();
            }
        }

        public void RedoAction()
        {
            if (editorMode == null || bubble == null) return;
            var undoRedo = editorMode.UndoRedo;
            if (undoRedo.Redo(bubble) && grid != null)
            {
                bubble.RebuildOwnedCells();
                grid.RefreshAll();
                RefreshLivability();
            }
        }

        public void RefreshLivability()
        {
            if (bubble == null) return;
            LivabilityReport report = BubbleLivability.Calculate(bubble);

            if (editorUI != null && screenController != null)
            {
                string pct = report.Percentage + "%";
                editorUI.UpdateLabel("PopupScore", pct);
                editorUI.UpdateLabel("LivabilityScore", pct);
                editorUI.UpdateLabel("LivabilityRating", RatingLabel(report.Rating));
                if (state.LivabilityExpanded) editorUI.UpdateLabel("PopupDetails", FormatDetails(report));
                editorUI.UpdateLabel("BrushSize", state.BrushSizeLabel());
                string layerInfo = state.SelectedItem != null ? "Layer: " + state.SelectedItem.Layer : "Layer: Auto (height/depth)";
                editorUI.UpdateLabel("SelLayer", layerInfo);
                string info = state.SelectedItem != null ? "Selected: " + state.SelectedItem.DisplayName : "No item selected";
                editorUI.UpdateLabel("SelName", info);

                bool canCreateSoulform = SoulformUnlockRule.IsUnlocked(bubble);
                string lockReason = SoulformUnlockRule.LockReason(bubble);
                editorUI.UpdateLabel("SoulformStatus", canCreateSoulform ? "Ready to create Soulform!" : lockReason);
            }

            if (editorUI != null)
            {
                float pctVal = report.Percentage / 100f;
                editorUI.UpdateFillBar(pctVal);

                int used = bubble.Cells.Count;
                int capacity = bubble.Width * bubble.Height * bubble.Depth;
                editorUI.UpdateLabel("WorldSize", bubble.Width + " x " + bubble.Height + " x " + bubble.Depth);
                editorUI.UpdateLabel("TilesUsed", used + " / " + capacity);
                editorUI.UpdateGridCapacity(used / (float)capacity);
            }
        }

        private string FormatDetails(LivabilityReport report)
        {
            List<string> lines = new List<string>();
            lines.Add("<b>WORLD CONDITIONS</b>");
            foreach (WorldCondition c in report.WorldConditions)
            {
                string rating = RatingLabel(c.Rating);
                lines.Add("  " + c.Name + ": " + rating + "  " + c.Value + "%");
                lines.Add("    " + BuildBar(c.Value));
            }
            lines.Add("");
            int met = 0;
            foreach (LifeCondition c in report.LifeConditions)
                if (c.Met) met++;
            int total = report.LifeConditions.Count;
            string lifeRating = met == total ? "Good"
                : met >= total * 0.75f ? "Fair"
                : met >= total * 0.5f ? "Mild"
                : "Poor";
            lines.Add("  " + met + "/" + total + " met â€” " + lifeRating);
            lines.Add("");
            if (report.AnimaReady)
            {
                lines.Add("<color=#4DFF8A><b>Ready to create your Primary Anima</b></color>");
            }
            else
            {
                lines.Add("<color=#FFB347><b>Not yet ready for Primary Anima</b></color>");
            }
            return string.Join("\n", lines);
        }

        private string RatingLabel(LivabilityRating rating)
        {
            switch (rating)
            {
                case LivabilityRating.Good: return "<color=#4DFF8A>Good</color>";
                case LivabilityRating.Fair: return "<color=#A0D468>Fair</color>";
                case LivabilityRating.Mild: return "<color=#FFB347>Mild</color>";
                default: return "<color=#FF6B6B>Poor</color>";
            }
        }

        private string BuildBar(int value)
        {
            int filled = value / 5;
            int empty = 20 - filled;
            return "[" + new string('#', filled) + new string('-', empty) + "]";
        }

        private void SaveBubble()
        {
            if (bubble == null) return;
            try
            {
                string savedJson;
                if (WatchState?.GameTime != null)
                {
                    var request = new SaveExtendedRequest
                    {
                        WorldName = state.WorldName,
                        Environment = bubble.Environment,
                        GridWidth = bubble.Width,
                        GridHeight = bubble.Height,
                        GridDepth = bubble.Depth,
                        LivabilityPercent = BubbleLivability.Calculate(bubble).Percentage,
                        WeatherCondition = WatchState.Weather?.WeatherDisplayName ?? "Clear",
                        ElapsedGameTime = WatchState.GameTime.ElapsedGameTime,
                        DayCount = WatchState.GameTime.DayCount,
                        Season = WatchState.GameTime.Season,
                        TutorialDaysRemaining = WatchState.GameTime.TutorialDaysRemaining,
                        ArchitectPoints = WatchState.ArchitectPoints?.Balance ?? 100,
                        LastPassiveAPTime = null,
                        SkillData = WatchState.SkillProgression?.Serialize(),
                        MemoryChapters = WatchState.MemoryBank?.Serialize(),
                        Cells = bubble.Cells,
                        Soulform = CurrentSoulform,
                        Cooldowns = null
                    };
                    savedJson = BubbleSaveService.SaveFullExtended(request);
                }
                else
                {
                    LivabilityReport report = BubbleLivability.Calculate(bubble);
                    string weather = WatchState?.WeatherCondition ?? "Clear";
                    savedJson = BubbleSaveService.SaveFull(bubble, report, state.WorldName,
                        CurrentSoulform, weather);
                }
                PushServerState(savedJson);
                string msg = "Saved \"" + state.WorldName + "\"!";
                if (editorUI != null) editorUI.UpdateLabel("SaveStatus", msg);
            }
            catch (Exception e)
            {
                string msg = "Save failed: " + e.Message;
                if (editorUI != null) editorUI.UpdateLabel("SaveStatus", msg);
            }
        }

        public void LoadBubble(string worldName)
        {
            try
            {
                var result = BubbleSaveService.LoadFullExtended(worldName);

                if (result.SaveVersion == "2.1")
                {
                    LoadFromExtendedResult(result);
                }
                else
                {
                    LoadFromResult(new LoadResult
                    {
                        Bubble = result.Bubble,
                        Soulform = result.Soulform,
                        WorldName = result.WorldName,
                        WeatherCondition = result.WeatherCondition,
                        LivabilityPercent = result.LivabilityPercent
                    });
                }
                string loadedMsg = "Loaded \"" + worldName + "\"!";
                if (editorUI != null) editorUI.UpdateLabel("SaveStatus", loadedMsg);
            }
            catch (Exception e)
            {
                string msg = "Load failed: " + e.Message;
                if (editorUI != null) editorUI.UpdateLabel("SaveStatus", msg);
            }
        }

        private void LoadFromExtendedResult(ExtendedLoadResult result)
        {
            bubble = result.Bubble;
            state.WorldName = result.WorldName;
            CurrentSoulform = result.Soulform;
            state.SelectedItem = BubblePlacementCatalog.DefaultFor(bubble.Environment);
            state.SelectedTab = state.SelectedItem.Tab;
            if (grid != null) grid.Show(bubble);
            ShowEditorUi();

            if (WatchState != null)
            {
                if (WatchState.GameTime != null)
                {
                    WatchState.GameTime.SetTimeDirectly(
                        result.ElapsedGameTime,
                        result.DayCount,
                        result.Season);
                    WatchState.GameTime.TutorialDaysRemaining = result.TutorialDaysRemaining;
                }

                WatchState.ElapsedTime = result.ElapsedGameTime;
                WatchState.DayCount = result.DayCount;
                WatchState.Season = (int)result.Season;
                WatchState.TutorialDaysRemaining = result.TutorialDaysRemaining;
                WatchState.UtcLastSave = result.UtcLastSave;
                WatchState.UtcLastLoad = result.UtcLastLoad;

                if (WatchState.ArchitectPoints != null)
                {
                    WatchState.ArchitectPoints.Initialize(
                        result.LastPassiveAPTime,
                        result.ArchitectPoints);
                }
                if (WatchState.SkillProgression != null && result.SkillData != null)
                {
                    WatchState.SkillProgression.Deserialize(result.SkillData);
                }
                if (WatchState.MemoryBank != null && result.MemoryChapters != null)
                {
                    WatchState.MemoryBank.Deserialize(result.MemoryChapters);
                }
            }
        }

        private void LoadFromResult(LoadResult result)
        {
            bubble = result.Bubble;
            state.WorldName = result.WorldName;
            CurrentSoulform = result.Soulform;
            state.SelectedItem = BubblePlacementCatalog.DefaultFor(bubble.Environment);
            state.SelectedTab = state.SelectedItem.Tab;
            if (grid != null) grid.Show(bubble);
            ShowEditorUi();
        }

        // ── Backend world-state sync (single source of truth) ──

        // Runs at startup: pull the authoritative snapshot from the backend.
        // Applied only when this client has no local save (a local save wins;
        // the next save pushes back up). Same code path as loading a local file.
        private IEnumerator StartupServerSync()
        {
            ServerStateData serverState = null;
            yield return GameStateManager.Instance.CheckStartup(state => serverState = state);

            if (serverState == null) yield break;          // backend down or nothing stored
            if (bubble != null) yield break;               // player already in a world
            if (BubbleSaveService.ListSavedWorlds().Length > 0) yield break; // local save wins

            LoadFromServerState(serverState.SerializedState);
        }

        private void LoadFromServerState(string json)
        {
            try
            {
                var result = BubbleSaveService.LoadExtendedFromJson(json);
                if (result.SaveVersion == "2.1")
                {
                    LoadFromExtendedResult(result);
                }
                else
                {
                    LoadFromResult(new LoadResult
                    {
                        Bubble = result.Bubble,
                        Soulform = result.Soulform,
                        WorldName = result.WorldName,
                        WeatherCondition = result.WeatherCondition,
                        LivabilityPercent = result.LivabilityPercent
                    });
                }
                Debug.Log("[ServerSync] Applied authoritative world snapshot from backend.");
                if (editorUI != null) editorUI.UpdateLabel("SaveStatus", "Loaded world from server");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ServerSync] Failed to apply server state: " + e.Message);
            }
        }

        // Fire-and-forget: upload the snapshot we just saved so the backend stays
        // the source of truth for other/fresh clients. Offline = no-op.
        private void PushServerState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            StartCoroutine(GameStateManager.Instance.SyncService.PushLocalChanges(json, ok =>
            {
                if (ok) Debug.Log("[ServerSync] Snapshot pushed to backend.");
                else Debug.Log("[ServerSync] Backend push skipped (backend offline).");
            }));
        }

        public string DescribeCell(int x, int y, int z)
        {
            if (bubble == null) return "No world loaded";
            BubbleCell cell = bubble.Get(x, y, z);
            if (cell == null || string.IsNullOrEmpty(cell.ItemId))
            {
                return string.Format("({0}, {1}, {2}) â€” Empty", x, y, z);
            }
            BubblePlacementDefinition def = BubblePlacementCatalog.Find(cell.ItemId);
            string name = def != null ? def.DisplayName : cell.ItemId;
            return string.Format("({0}, {1}, {2}) â€” {3} | Layer: {4}", x, y, z, name, cell.Layer);
        }

        public void RotateEntity(int x, int y, int z, int targetRotation)
        {
            if (bubble == null) return;
            BubbleCell cell = bubble.Get(x, y, z);
            if (cell == null) return;
            cell.Rotation = Mathf.Clamp(targetRotation, 0, 3);
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem");
                events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
    }
}
