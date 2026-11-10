using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BubbleWorld
{
    public sealed class WatchMode : IAppMode
    {
        public static readonly WatchMode Instance = new WatchMode();

        private static readonly string[] WeatherCycle = { "Clear", "Cloudy", "Rain", "Sunny" };
        private static readonly string[] SeasonNames = { "Spring", "Summer", "Autumn", "Winter" };

        private WatchState watchState;
        private BubbleWorldApp app;
        private Coroutine brainCoroutine;
        private Coroutine chatCoroutine;
        private readonly List<ChatMessage> chatHistory = new List<ChatMessage>();
        private bool pendingBrainTick;
        private float eventTickCooldown;
        private const float EventTickDebounce = 3f;
        private string lastUrgentEvent;
        private float autoActionTimer;
        private const float AutoActionCooldown = 8f;
        private List<Conversation> lastConversationArchive = new List<Conversation>();

        private EconomyConfig economyConfig;
        private TutorialSequence tutorial;

        /// <summary>Interventions beyond this count start locked until the first skill is learned.</summary>
        private const int StartingInterventionCount = 5;
        private readonly HashSet<InterventionType> lockedInterventions = new HashSet<InterventionType>();

        public void OnEnter(BubbleWorldApp app)
        {
            this.app = app;
            var soulform = app.CurrentSoulform;

            watchState = new WatchState
            {
                Soulform = soulform,
                EventLog = app.WorldEventLog ?? new EventLog(),
                CurrentCameraView = Camera3DView.FollowSoulform,
                WeatherCondition = WeatherCycle[Random.Range(0, WeatherCycle.Length)]
            };

            InitializeNewSystems(app, soulform);

            app.WatchState = watchState;

            if (watchState.ArchitectPoints != null)
            {
                watchState.ArchitectPoints.OnBalanceChanged += HandleAPBalanceChanged;
                watchState.ArchitectPoints.OnInsufficientAP += HandleAPInsufficient;
            }

            if (app.ScreenController != null)
                app.ScreenController.ShowScreen(UIScreenId.LiveWatch);

            if (app.LiveScreen != null)
            {
                app.LiveScreen.Build(watchState);
                InitializeInterventionLocks();
                app.LiveScreen.OnSpeedChanged += OnSpeedChanged;
                app.LiveScreen.OnPauseToggled += OnPauseToggled;
                app.LiveScreen.OnCameraModeChanged += OnCameraModeChanged;
                app.LiveScreen.OnInterventionClicked += OnInterventionClicked;
                app.LiveScreen.OnOpenProfile += OnOpenProfile;
                app.LiveScreen.OnTalkObserve += OnTalkObserve;
                app.LiveScreen.OnOpenLivabilityDetails += OnOpenLivabilityDetails;
                app.LiveScreen.OnCloseLivabilityDetails += OnCloseLivabilityDetails;
                app.LiveScreen.OnNavigationHub += OnOpenMasterNavigation;
            }

            if (app.SoulformChatScreen != null)
            {
                app.SoulformChatScreen.OnSendMessage += OnChatMessage;
                app.SoulformChatScreen.OnClose += OnChatClose;
            }

            if (app.SoulformProfileScreen != null)
                app.SoulformProfileScreen.OnClose += OnProfileClose;

            chatHistory.Clear();

            SpawnSoulformVisual();
            RefreshUI();

            if (app.Grid != null)
                app.Grid.Set3DView(Camera3DView.FollowSoulform);

            if (watchState.BrainService != null && watchState.BrainService.IsConfigured)
                brainCoroutine = app.StartCoroutine(BrainTickLoop());

            watchState.EventLog.OnEventRecorded += OnGameEvent;
            watchState.EventLog.OnEventRecorded += OnUrgentGameEvent;

            UpdateAPDisplay();
            tutorial?.Begin();
        }

        private void InitializeNewSystems(BubbleWorldApp app, SoulformModel soulform)
        {
            int seed = System.DateTime.UtcNow.GetHashCode();
            var gameTimeConfig = app.GameTimeConfig;
            var weatherConfig = app.WeatherConfig;
            var sourceConfig = app.SourceLogicConfig;
            var aiConfig = app.AIConfig;
            var spawnConfig = app.SpawnConfig;
            var skillConfig = app.SkillConfig;
            economyConfig = app.EconomyConfig;
            var memoryConfig = app.MemoryConfig;

            if (gameTimeConfig != null)
            {
                var gameTime = new GameTimeManager(gameTimeConfig);
                watchState.GameTime = gameTime;
            }

            if (weatherConfig != null && gameTimeConfig != null)
            {
                var weather = new WeatherManager(weatherConfig, seed);
                weather.RollWeather(SeasonType.Spring);
                watchState.Weather = weather;
                watchState.WeatherCondition = weather.WeatherDisplayName;
                watchState.GameTime?.AddReceiver(weather);
            }

            if (sourceConfig != null && watchState.Weather != null && app.Bubble != null)
            {
                var sourceLogic = new SourceLogicSystem(sourceConfig, watchState.Weather, app.Bubble, seed);
                watchState.SourceLogic = sourceLogic;
                watchState.GameTime?.AddReceiver(sourceLogic);
            }

            if (economyConfig != null)
            {
                var ap = new ArchitectPoints(economyConfig);
                watchState.ArchitectPoints = ap;
                watchState.GameTime?.AddReceiver(ap);
            }

            if (skillConfig != null)
            {
                watchState.SkillProgression = new SkillProgression(skillConfig, seed);
            }

            if (memoryConfig != null)
            {
                watchState.MemoryBank = new MemoryBank(memoryConfig);
            }

            if (watchState.SkillProgression != null)
            {
                watchState.SkillProgression.OnSkillAcquired += HandleSkillAcquired;
                watchState.SkillProgression.OnSkillExecuted += HandleSkillExecuted;
                watchState.SkillProgression.OnSkillSetLeveled += HandleSkillSetLeveled;
            }

            tutorial = new TutorialSequence();
            tutorial.OnGuidance += HandleTutorialGuidance;

            watchState.EntityManager = new EntityManager();

            if (app.Bubble != null)
            {
                watchState.EntityMovementSystem = new EntityMovementSystem(watchState.EntityManager, app.Bubble);
                watchState.EntityManager.EntityMovementSystem = watchState.EntityMovementSystem;
                watchState.GameTime?.AddReceiver(watchState.EntityMovementSystem);

                watchState.StormDamage = new StormDamageSystem(app.Bubble, watchState.EntityManager,
                    watchState.EventLog, seed);

                watchState.EntityVisualManager = new EntityVisualManager(watchState.EntityManager);
            }

            var sfEntity = new SoulformEntityData("soulform",
                soulform?.GridX ?? 12, soulform?.GridY ?? 0, soulform?.GridZ ?? 12);
            watchState.EntityManager.Register(sfEntity);

            watchState.ActionExecutor = new SoulformActionExecutor(app.Bubble,
                watchState.EntityManager, watchState.SkillProgression,
                watchState.EventLog, watchState.MemoryBank);
            watchState.ActionExecutor.OnActionCompleted += HandleActionCompleted;

            watchState.DeathHandler = new SoulformDeathHandler();

            if (spawnConfig != null)
            {
                var spawn = new SpawnManager(spawnConfig, seed)
                {
                    EntityManager = watchState.EntityManager,
                    Bubble = app.Bubble,
                    AIConfig = aiConfig,
                    EventLog = watchState.EventLog
                };
                watchState.SpawnManager = spawn;
                watchState.GameTime?.AddReceiver(spawn);
            }

            if (aiConfig != null && app.Bubble != null)
            {
                var animalAI = new AnimalAI(aiConfig, AnimalDiet.Herbivore, seed)
                {
                    BoundAnimalData = null,
                    EntityManager = watchState.EntityManager,
                    Bubble = app.Bubble
                };
                watchState.AnimalAI = animalAI;
                watchState.GameTime?.AddReceiver(animalAI);

                var npcAI = new NPCAI(aiConfig, seed)
                {
                    BoundNPCData = null,
                    EntityManager = watchState.EntityManager,
                    Bubble = app.Bubble
                };
                watchState.NPCAI = npcAI;
                watchState.GameTime?.AddReceiver(npcAI);

                foreach (var kv in watchState.EntityManager.AllAnimalAIs)
                {
                    watchState.GameTime?.AddReceiver(kv.Value);
                }
                foreach (var kv in watchState.EntityManager.AllNPCAIs)
                {
                    watchState.GameTime?.AddReceiver(kv.Value);
                }
            }

            watchState.BrainService = new SoulformBrainService(
                app.BrainConfig,
                watchState.SkillProgression,
                watchState.MemoryBank,
                watchState.ArchitectPoints);

            watchState.DataProvider = new LocalWorldDataProvider(
                app.Bubble, watchState.Weather, watchState.EntityManager,
                soulform, watchState.ArchitectPoints);

            GameStateManager.Instance.InitializeLocal(
                app.Bubble, watchState.Weather, watchState.EntityManager,
                soulform, watchState.ArchitectPoints);

            watchState.WeatherVisuals = new WeatherVisualSystem();
            watchState.WeatherVisuals.Initialize(app.transform);

            watchState.DayNight = new DayNightCycle();
            var mainLight = UnityEngine.Object.FindObjectOfType<Light>();
            if (mainLight != null && mainLight.type == LightType.Directional)
                watchState.DayNight.Initialize(mainLight);

            watchState.BubbleWorldManager = new BubbleWorldManager();
            if (app.Bubble != null)
            {
                var mainInstance = watchState.BubbleWorldManager.AddBubble(
                    "main", app.Bubble, app.Bubble.Environment, 0);
                mainInstance.EntityManager = watchState.EntityManager;
                mainInstance.GameTime = watchState.GameTime;
                mainInstance.Weather = watchState.Weather;
                mainInstance.SourceLogic = watchState.SourceLogic;
                mainInstance.SpawnManager = watchState.SpawnManager;
                mainInstance.MovementSystem = watchState.EntityMovementSystem;
                mainInstance.StormDamage = watchState.StormDamage;
                watchState.BubbleWorldManager.SetActiveBubble("main");
            }

            watchState.WeatherCondition = watchState.Weather?.WeatherDisplayName ?? "Clear";
            watchState.Season = (int)(watchState.Weather?.CurrentSeason ?? SeasonType.Spring);
        }

        public void OnUpdate(BubbleWorldApp app)
        {
            if (watchState == null || watchState.IsPaused) return;

            float speed = (float)watchState.Speed;
            float delta = Time.deltaTime * speed * 60f;

            if (watchState.GameTime != null)
            {
                watchState.GameTime.IsPaused = watchState.IsPaused;
                watchState.GameTime.TimeMultiplier = speed;
                watchState.GameTime.AdvanceTime(delta);

                watchState.ElapsedTime = watchState.GameTime.ElapsedGameTime;
                watchState.DayCount = watchState.GameTime.DayCount;
                watchState.Season = (int)watchState.GameTime.Season;

                if (watchState.Weather != null)
                {
                    watchState.WeatherCondition = watchState.Weather.WeatherDisplayName;

                    if (watchState.WeatherVisuals != null)
                        watchState.WeatherVisuals.UpdateWeather(watchState.Weather.CurrentWeather, watchState.Weather.CurrentSeason);

                    if (watchState.LastProcessedDay != watchState.DayCount)
                    {
                        watchState.LastProcessedDay = watchState.DayCount;
                        ApplyStormDamageIfNeeded();
                    }
                }

                if (watchState.DayNight != null)
                    watchState.DayNight.Update(watchState.GameTime.Hour + watchState.GameTime.Minute / 60f);

                if (watchState.GameTime.Hour == 0 && watchState.GameTime.Minute == 0)
                {
                    watchState.EventLog?.Record(GameEventType.DayBegan,
                        "Day " + watchState.DayCount + " began");
                }
            }
            else
            {
                watchState.ElapsedTime += delta;
                if (watchState.ElapsedTime >= WatchState.DayLength)
                {
                    watchState.ElapsedTime -= WatchState.DayLength;
                    watchState.DayCount++;
                    watchState.Season = (watchState.DayCount / 30) % 4;
                    watchState.EventLog?.Record(GameEventType.DayBegan,
                        "Day " + watchState.DayCount + " began");
                }
                TickWeather();
            }

            TickCooldowns(delta);
            eventTickCooldown = Mathf.Max(0, eventTickCooldown - delta);
            autoActionTimer = Mathf.Max(0, autoActionTimer - delta);
            TickSoulformNeeds(delta);
            SyncMoodFromNeeds();
            TickSoulformMovement(delta * speed);
            CheckAutonomousTriggers();

            tutorial?.Evaluate(delta, watchState.Soulform, app.Bubble);

            TickEntityVisuals();

            if (watchState.DeathHandler != null && watchState.Soulform != null)
                watchState.DeathHandler.CheckForDeath(watchState.Soulform);

            app.Grid?.TickCamera();

            if (watchState.CurrentCameraView == Camera3DView.FollowSoulform && watchState.Soulform != null)
            {
                FollowSoulformCamera();
            }

            if (app.LiveScreen != null && watchState.Soulform != null)
            {
                app.LiveScreen.UpdateSoulform(watchState.Soulform);
            }

            RefreshUI();

            if (app?.SoulformProfileScreen != null && app.ScreenController?.GetPanel(UIScreenId.SoulformProfile)?.activeInHierarchy == true)
                app.SoulformProfileScreen.Refresh();
        }

        public void OnExit(BubbleWorldApp app)
        {
            if (watchState != null)
            {
                watchState.EventLog.OnEventRecorded -= OnGameEvent;
                watchState.EventLog.OnEventRecorded -= OnUrgentGameEvent;

                if (app.LiveScreen != null)
                {
                    app.LiveScreen.OnSpeedChanged -= OnSpeedChanged;
                    app.LiveScreen.OnPauseToggled -= OnPauseToggled;
                    app.LiveScreen.OnCameraModeChanged -= OnCameraModeChanged;
                    app.LiveScreen.OnInterventionClicked -= OnInterventionClicked;
                    app.LiveScreen.OnOpenProfile -= OnOpenProfile;
                    app.LiveScreen.OnTalkObserve -= OnTalkObserve;
                    app.LiveScreen.OnOpenLivabilityDetails -= OnOpenLivabilityDetails;
                    app.LiveScreen.OnCloseLivabilityDetails -= OnCloseLivabilityDetails;
                    app.LiveScreen.OnNavigationHub -= OnOpenMasterNavigation;
                }

                if (app.SoulformChatScreen != null)
                {
                    app.SoulformChatScreen.OnSendMessage -= OnChatMessage;
                    app.SoulformChatScreen.OnClose -= OnChatClose;
                }

                if (app.SoulformProfileScreen != null)
                    app.SoulformProfileScreen.OnClose -= OnProfileClose;

                if (watchState.SkillProgression != null)
                {
                    watchState.SkillProgression.OnSkillAcquired -= HandleSkillAcquired;
                    watchState.SkillProgression.OnSkillExecuted -= HandleSkillExecuted;
                    watchState.SkillProgression.OnSkillSetLeveled -= HandleSkillSetLeveled;
                }

                if (watchState.ActionExecutor != null)
                    watchState.ActionExecutor.OnActionCompleted -= HandleActionCompleted;

                if (watchState.ArchitectPoints != null)
                {
                    watchState.ArchitectPoints.OnBalanceChanged -= HandleAPBalanceChanged;
                    watchState.ArchitectPoints.OnInsufficientAP -= HandleAPInsufficient;
                }

                if (tutorial != null)
                {
                    tutorial.OnGuidance -= HandleTutorialGuidance;
                    tutorial = null;
                }

                if (brainCoroutine != null)
                {
                    app.StopCoroutine(brainCoroutine);
                    brainCoroutine = null;
                }

                if (chatCoroutine != null)
                {
                    app.StopCoroutine(chatCoroutine);
                    chatCoroutine = null;
                }

                if (watchState.GameTime != null)
                {
                    watchState.GameTime.IsPaused = true;
                }

                if (watchState.EntityVisualManager != null)
                {
                    watchState.EntityVisualManager.DestroyAll();
                }

                if (watchState.WeatherVisuals != null)
                {
                    watchState.WeatherVisuals.Destroy();
                }
            }

            DestroySoulformVisual();
            app.WatchState = null;
            watchState = null;
            this.app = null;
        }

        private void SpawnSoulformVisual()
        {
            if (app?.Grid == null || watchState?.Soulform == null) return;
            app.Grid.SpawnSoulformVisual(watchState.Soulform.GridX, watchState.Soulform.GridY, watchState.Soulform.GridZ);

            var lipSync = app.Grid.SoulformVisual?.GetComponent<SoulformLipSync>();
            if (lipSync != null)
                lipSync.SetSoulform(watchState.Soulform);
        }

        private void DestroySoulformVisual()
        {
            app?.Grid?.DestroySoulformVisual();
        }

        private void FollowSoulformCamera()
        {
            if (app?.Grid == null || watchState?.Soulform == null) return;
            app.Grid.SetSoulformPosition(watchState.Soulform.GridX, watchState.Soulform.GridY, watchState.Soulform.GridZ);
        }

        private void TickWeather()
        {
            if (watchState == null) return;
            float dayFraction = watchState.ElapsedTime / WatchState.DayLength;
            if (dayFraction % WatchState.WeatherChangeInterval < 0.01f)
            {
                string oldWeather = watchState.WeatherCondition;
                string newWeather;
                do
                {
                    newWeather = WeatherCycle[Random.Range(0, WeatherCycle.Length)];
                } while (newWeather == oldWeather);
                watchState.WeatherCondition = newWeather;

                if (newWeather == "Rain")
                    watchState.EventLog?.Record(GameEventType.RainStarted, "Rain started");
                else if (oldWeather == "Rain")
                    watchState.EventLog?.Record(GameEventType.RainStarted, "Sky cleared");
            }
        }

        private void TickCooldowns(float delta)
        {
            if (watchState?.Cooldowns == null) return;
            var keys = new List<InterventionType>(watchState.Cooldowns.Keys);
            foreach (var key in keys)
            {
                watchState.Cooldowns[key] -= delta;
                if (watchState.Cooldowns[key] <= 0f)
                    watchState.Cooldowns.Remove(key);
            }
        }

        private void TickSoulformNeeds(float delta)
        {
            if (watchState?.Soulform == null) return;
            float hourFraction = delta / 3600f;

            var report = app?.Bubble != null ? BubbleLivability.Calculate(app.Bubble) : null;
            int foodPct = report != null ? report.Metrics.GetValueOrDefault("Food Potential", 0) : 0;
            int shelterPct = report != null ? report.Metrics.GetValueOrDefault("Shelter", 0) : 0;
            bool isRaining = watchState.Weather?.WeatherDisplayName == "Rain" ||
                             watchState.WeatherCondition == "Rain";

            float hungerMod = foodPct < 40 ? 2f : 1f;
            int hungerLoss = Mathf.RoundToInt(hourFraction * hungerMod);
            watchState.Soulform.Hunger = Mathf.Max(0, watchState.Soulform.Hunger - hungerLoss);

            float comfortRecovery = 1f;
            if (isRaining && shelterPct < 40)
                comfortRecovery = -1.5f;
            else if (isRaining)
                comfortRecovery = -0.5f;
            else if (shelterPct >= 40)
                comfortRecovery = 1.5f;

            watchState.Soulform.Comfort = Mathf.Clamp(watchState.Soulform.Comfort +
                Mathf.RoundToInt(comfortRecovery), 0, 100);

            if (watchState.Soulform.Hunger < 20)
                watchState.Soulform.Comfort = Mathf.Max(0, watchState.Soulform.Comfort - Mathf.RoundToInt(hourFraction));
        }

        private void SyncMoodFromNeeds()
        {
            if (watchState?.Soulform == null) return;
            var s = watchState.Soulform;
            SoulformMood prevMood = s.Mood;
            if (s.Hunger < 30)
                s.Mood = SoulformMood.Hungry;
            else if (s.Comfort < 30)
                s.Mood = SoulformMood.Distressed;
            else if (s.Curiosity > 70)
                s.Mood = SoulformMood.Curious;
            else if (s.Comfort >= 70 && s.Hunger >= 70)
                s.Mood = SoulformMood.Content;

            if (s.Mood != prevMood)
                ApplyMoodToVisuals();
        }

        private void ApplyMoodToVisuals()
        {
            if (watchState?.Soulform == null) return;
            var s = watchState.Soulform;
            switch (s.Mood)
            {
                case SoulformMood.Content:
                    s.CurrentExpression = "Neutral";
                    s.CurrentAnimation = "Idle";
                    break;
                case SoulformMood.Curious:
                    s.CurrentExpression = "Curious";
                    s.CurrentAnimation = "LookAround";
                    break;
                case SoulformMood.Hungry:
                    s.CurrentExpression = "Tired";
                    s.CurrentAnimation = "Walk";
                    break;
                case SoulformMood.Tired:
                    s.CurrentExpression = "Tired";
                    s.CurrentAnimation = "Sit";
                    break;
                case SoulformMood.Inspired:
                    s.CurrentExpression = "Happy";
                    s.CurrentAnimation = "Celebrate";
                    break;
                case SoulformMood.Distressed:
                    s.CurrentExpression = "Sad";
                    s.CurrentAnimation = "Defeated";
                    break;
            }
            app?.Grid?.SetExpression(s.CurrentExpression);
            app?.Grid?.SetAnimation(s.CurrentAnimation);
        }

        private bool IsActionRelatedToNeed(SoulformAction? action, SoulformAction needAction)
        {
            if (action == null) return false;
            return action.Value == needAction;
        }

        private void CheckAutonomousTriggers()
        {
            if (watchState?.Soulform == null || app?.Bubble == null) return;
            if (watchState.BrainService != null && watchState.BrainService.IsConfigured) return;
            if (autoActionTimer > 0f) return;

            var sf = watchState.Soulform;
            SoulformActionCommand trigger = null;

            if (sf.Hunger <= 25 && !IsActionRelatedToNeed(watchState.CurrentAction, SoulformAction.SearchForFood)
                && !IsActionRelatedToNeed(watchState.CurrentAction, SoulformAction.GoToShelter))
            {
                trigger = new SoulformActionCommand(SoulformAction.SearchForFood);
            }
            else if (sf.Comfort <= 25 && !IsActionRelatedToNeed(watchState.CurrentAction, SoulformAction.GoToShelter))
            {
                trigger = new SoulformActionCommand(SoulformAction.GoToShelter);
            }
            else if (sf.Curiosity > 75 && watchState.CurrentPath == null
                     && !IsActionRelatedToNeed(watchState.CurrentAction, SoulformAction.ObserveObject))
            {
                trigger = new SoulformActionCommand(SoulformAction.ObserveObject);
            }

            if (trigger != null)
            {
                var validated = ActionValidator.Validate(trigger, app.Bubble,
                    sf.GridX, sf.GridY, sf.GridZ);
                if (validated.IsValid && validated.TargetX.HasValue)
                {
                    if (trigger.Action == SoulformAction.SearchForFood)
                    {
                        sf.CurrentActivity = "Searching for food";
                        sf.CurrentThought = "I need to find something to eat...";
                        sf.Mood = SoulformMood.Hungry;
                    }
                    else if (trigger.Action == SoulformAction.GoToShelter)
                    {
                        sf.CurrentActivity = "Seeking shelter";
                        sf.CurrentThought = "I need to get out of the weather.";
                        sf.Mood = SoulformMood.Distressed;
                    }
                    else
                    {
                        sf.CurrentActivity = "Exploring";
                        sf.CurrentThought = "I wonder what's over there?";
                        sf.Mood = SoulformMood.Curious;
                    }
                    ApplyMoodToVisuals();
                    ExecuteAction(validated, sf);
                    autoActionTimer = AutoActionCooldown;
                }
            }
        }

        private void TickSoulformMovement(float delta)
        {
            if (watchState?.Soulform == null || watchState.CurrentPath == null) return;
            if (watchState.CurrentPathIndex >= watchState.CurrentPath.Count)
            {
                watchState.CurrentPath = null;
                return;
            }

            watchState.MoveTimer += delta;
            float interval = 0.3f;
            while (watchState.MoveTimer >= interval && watchState.CurrentPath != null)
            {
                watchState.MoveTimer -= interval;
                var (nx, ny, nz) = watchState.CurrentPath[watchState.CurrentPathIndex];
                watchState.Soulform.GridX = nx;
                watchState.Soulform.GridY = ny;
                watchState.Soulform.GridZ = nz;
                app?.Grid?.SetSoulformPosition(nx, ny, nz);
                watchState.CurrentPathIndex++;
                if (watchState.CurrentPathIndex >= watchState.CurrentPath.Count)
                {
                    watchState.CurrentPath = null;
                    CheckArrivalTile(nx, ny, nz);
                    break;
                }
            }
        }

        private void CheckArrivalTile(int x, int y, int z)
        {
            if (app?.Bubble == null || watchState?.Soulform == null) return;
            var cell = app.Bubble.Get(x, y, z);
            var soulform = watchState.Soulform;
            SoulformAction? arrivalAction = watchState.CurrentAction;
            watchState.CurrentAction = null;

            if (arrivalAction == SoulformAction.CollectResource && cell != null && !string.IsNullOrEmpty(cell.ItemId))
            {
                var def = BubblePlacementCatalog.Find(cell.ItemId);
                if (def != null)
                {
                    app.Bubble.Remove(x, y, z);
                    app.Grid?.RefreshCells(new HashSet<(int, int, int)> { (x, y, z) });
                    watchState.EventLog?.Record(GameEventType.SoulformFoundFood,
                        soulform.Name + " collected " + def.DisplayName);
                    RecordMemory("Collected " + def.DisplayName,
                        MemoryCategory.Skill, "collect", def.DisplayName.ToLower());
                }
                return;
            }

            if (arrivalAction == SoulformAction.BuildObject && !string.IsNullOrEmpty(watchState.ActionTargetId))
            {
                var buildDef = BubblePlacementCatalog.Find(watchState.ActionTargetId);
                if (buildDef != null)
                {
                    app.Bubble.Place(x, y, z, buildDef);
                    app.Grid?.RefreshCells(new HashSet<(int, int, int)> { (x, y, z) });
                    watchState.EventLog?.Record(GameEventType.InterventionUsed,
                        soulform.Name + " built " + buildDef.DisplayName);
                    RecordMemory("Built " + buildDef.DisplayName + " at (" + x + "," + z + ")",
                        MemoryCategory.Skill, "build", buildDef.DisplayName.ToLower());
                }
                watchState.ActionTargetId = null;
                return;
            }

            if (cell == null || string.IsNullOrEmpty(cell.ItemId)) return;
            var itemDef = BubblePlacementCatalog.Find(cell.ItemId);
            if (itemDef == null) return;

            if (arrivalAction == SoulformAction.ObserveObject)
            {
                RecordMemory("Observed " + itemDef.DisplayName + " up close",
                    MemoryCategory.Discovery, "observe", itemDef.DisplayName.ToLower());
            }

            if (itemDef.Food > 0)
            {
                int gain = Mathf.Min(itemDef.Food, 100 - soulform.Hunger);
                if (gain > 0)
                {
                    soulform.Hunger = Mathf.Min(100, soulform.Hunger + gain);
                    watchState.EventLog?.Record(GameEventType.SoulformFoundFood,
                        soulform.Name + " ate " + itemDef.DisplayName + " (" + gain + " hunger)");
                    RecordMemory("Ate " + itemDef.DisplayName + " to satisfy hunger",
                        MemoryCategory.Discovery, "food", "eat", itemDef.DisplayName.ToLower());
                }
            }

            if (itemDef.Shelter > 0)
            {
                soulform.Comfort = Mathf.Min(100, soulform.Comfort + 5);
            }
        }

        private IEnumerator BrainTickLoop()
        {
            while (true)
            {
                // Cadence served by the backend wins once its status config has been
                // fetched (single source of truth); the config asset is the fallback.
                float tickSeconds = watchState?.BrainService?.ServerTickSeconds > 0f
                    ? watchState.BrainService.ServerTickSeconds
                    : (app?.BrainConfig?.BrainTickSeconds ?? 5f);
                var wait = new WaitForSeconds(tickSeconds);

                if (pendingBrainTick)
                {
                    pendingBrainTick = false;
                    eventTickCooldown = EventTickDebounce;
                    yield return null;
                }
                else
                {
                    yield return wait;
                }

                if (watchState?.Soulform == null) continue;
                if (watchState.GameTime != null && watchState.GameTime.IsPaused) continue;
                if (watchState.BrainService == null || !watchState.BrainService.IsConfigured) continue;

                string context = BuildWorldContext();
                if (!string.IsNullOrEmpty(lastUrgentEvent))
                {
                    context = "Urgent event: " + lastUrgentEvent + "\n" + context;
                    lastUrgentEvent = null;
                }
                yield return watchState.BrainService.QueryBrain(context,
                    OnBrainResult,
                    error => Debug.LogWarning("Brain: " + error));
            }
        }

        private string BuildWorldContext()
        {
            if (app?.Bubble == null || watchState?.Soulform == null)
                return "No world state available.";

            var soulform = watchState.Soulform;
            var report = BubbleLivability.Calculate(app.Bubble);
            int hours = watchState.GameTime != null
                ? watchState.GameTime.Hour
                : Mathf.FloorToInt(watchState.ElapsedTime / 3600f) % 24;
            int mins = watchState.GameTime != null
                ? watchState.GameTime.Minute
                : Mathf.FloorToInt((watchState.ElapsedTime % 3600f) / 60f);

            var nearby = app.Bubble.Get(soulform.GridX, soulform.GridY, soulform.GridZ);
            string nearbyDesc = nearby != null ? nearby.ItemId : "empty";

            string weatherStr = watchState.Weather?.WeatherDisplayName ?? watchState.WeatherCondition;
            int seasonIdx = watchState.Weather != null ? (int)watchState.Weather.CurrentSeason : watchState.Season;

            return string.Format(
                "Environment: {0}\nTime: {1:D2}:{2:D2} (Day {3}, {4})\nWeather: {5}\nTemperature: {6}\nLivability: {7}%\nFood: {8}\nWater: {9}\nShelter: {10}\nNearby: {11}\nPosition: ({12}, {13}, {14})\nComfort: {15}/100\nHunger: {16}/100\nCuriosity: {17}/100\nActivity: {18}\nAnimals: {19}\nNPCs: {20}",
                app.Bubble.Environment,
                hours, mins,
                watchState.DayCount,
                SeasonNames[seasonIdx],
                weatherStr,
                watchState.Weather?.TemperatureDisplayName ?? "Mild",
                report.Percentage,
                report.Metrics.GetValueOrDefault("Food Potential", 0) >= 40 ? "present" : "missing",
                report.Metrics.GetValueOrDefault("Water Access", 0) >= 40 ? "present" : "missing",
                report.Metrics.GetValueOrDefault("Shelter", 0) >= 40 ? "available" : "unavailable",
                nearbyDesc,
                soulform.GridX, soulform.GridY, soulform.GridZ,
                soulform.Comfort,
                soulform.Hunger,
                soulform.Curiosity,
                soulform.CurrentActivity,
                watchState.EntityManager?.GetAllAnimals().Count ?? 0,
                watchState.EntityManager?.GetAllNPCs().Count ?? 0);
        }

        private void OnBrainResult(SoulformBrainResponse response)
        {
            if (watchState?.Soulform == null || response == null) return;
            var soulform = watchState.Soulform;

            soulform.CurrentThought = response.CurrentThought;
            soulform.Mood = response.Mood;
            soulform.CurrentActivity = response.CurrentActivity;
            soulform.CurrentExpression = response.Expression;
            soulform.CurrentAnimation = response.Animation;

            app?.Grid?.SetExpression(response.Expression);
            app?.Grid?.SetAnimation(response.Animation);

            if (response.ParsedCommand != null)
            {
                var validated = ActionValidator.Validate(response.ParsedCommand,
                    app?.Bubble, soulform.GridX, soulform.GridY, soulform.GridZ);

                if (validated.IsValid && validated.TargetX.HasValue)
                    ExecuteAction(validated, soulform);
                else
                    Debug.Log("Action rejected: " + validated.ValidationError);

                string actionDesc = validated.IsValid
                    ? validated.Action.ToString()
                    : "nothing (" + validated.ValidationError + ")";
                watchState.EventLog?.Record(GameEventType.SoulformReacted,
                    soulform.Name + " is " + response.CurrentActivity.ToLower() +
                    " (action: " + actionDesc + ")");
            }
            else if (response.TargetX.HasValue && response.TargetZ.HasValue)
            {
                int nx = Mathf.Clamp(response.TargetX.Value, 0, app?.Bubble?.Width ?? 60);
                int nz = Mathf.Clamp(response.TargetZ.Value, 0, app?.Bubble?.Height ?? 60);
                int ny = response.TargetY.HasValue
                    ? Mathf.Clamp(response.TargetY.Value, 0, app?.Bubble?.Depth ?? 8)
                    : soulform.GridY;

                var path = Pathfinding3D.FindPath(app.Bubble,
                    soulform.GridX, soulform.GridY, soulform.GridZ,
                    nx, ny, nz);
                watchState.CurrentPath = path;
                watchState.CurrentPathIndex = 0;
                watchState.MoveTimer = 0f;

                watchState.EventLog?.Record(GameEventType.SoulformReacted,
                    soulform.Name + " is " + response.CurrentActivity.ToLower());
            }

        }

        private void ExecuteAction(SoulformActionCommand cmd, SoulformModel soulform)
        {
            watchState.CurrentAction = cmd.Action;
            watchState.ActionTargetId = cmd.TargetId;

            switch (cmd.Action)
            {
                case SoulformAction.MoveToLocation:
                    StartMovement(cmd, soulform);
                    break;

                case SoulformAction.GoToShelter:
                    StartMovement(cmd, soulform);
                    RecordMemory("Sought shelter at (" + cmd.TargetX + "," + cmd.TargetZ + ")",
                        MemoryCategory.WorldEvent, "shelter");
                    break;

                case SoulformAction.SearchForFood:
                    StartMovement(cmd, soulform);
                    RecordMemory("Searching for food nearby",
                        MemoryCategory.Discovery, "food", "hunger");
                    break;

                case SoulformAction.CollectResource:
                    StartMovement(cmd, soulform);
                    RecordMemory("Collected resources", MemoryCategory.Skill, "collect", "resource");
                    break;

                case SoulformAction.ObserveObject:
                    StartMovement(cmd, soulform);
                    RecordMemory("Observed " + (cmd.TargetId ?? "something interesting"),
                        MemoryCategory.Discovery, "observe", cmd.TargetId?.ToLower());
                    break;

                case SoulformAction.FleeDanger:
                    StartMovement(cmd, soulform);
                    RecordMemory("Fled from danger", MemoryCategory.Emotion, "fear", "danger");
                    break;

                case SoulformAction.BuildObject:
                    StartMovement(cmd, soulform);
                    RecordMemory("Building " + (cmd.TargetId ?? "a structure"),
                        MemoryCategory.Skill, "build", cmd.TargetId?.ToLower());
                    break;

                case SoulformAction.Rest:
                    soulform.CurrentActivity = "Resting";
                    RecordMemory("Resting to regain energy", MemoryCategory.Emotion, "rest");
                    break;

                case SoulformAction.PlayAnimation:
                    soulform.CurrentAnimation = cmd.TargetId ?? "Idle";
                    app?.Grid?.SetAnimation(soulform.CurrentAnimation);
                    break;

                case SoulformAction.SpeakToPlayer:
                case SoulformAction.SpeakToSoulform:
                    break;

                case SoulformAction.ContinueCurrentTask:
                    break;

                default:
                    StartMovement(cmd, soulform);
                    break;
            }
        }

        private void StartMovement(SoulformActionCommand cmd, SoulformModel soulform)
        {
            if (!cmd.TargetX.HasValue) return;
            int nx = cmd.TargetX.Value;
            int ny = cmd.TargetY ?? soulform.GridY;
            int nz = cmd.TargetZ ?? soulform.GridZ;

            var path = Pathfinding3D.FindPath(app?.Bubble,
                soulform.GridX, soulform.GridY, soulform.GridZ,
                nx, ny, nz);
            watchState.CurrentPath = path;
            watchState.CurrentPathIndex = 0;
            watchState.MoveTimer = 0f;
        }

        private void RecordMemory(string description, MemoryCategory category, params string[] keywords)
        {
            if (watchState?.MemoryBank == null) return;
            var kwList = keywords != null && keywords.Length > 0
                ? new List<string>(keywords)
                : null;
            watchState.MemoryBank.AddEvent(description, category, kwList);
        }

        private void OnGameEvent(GameEvent evt)
        {
            if (app?.LiveScreen == null) return;
            string timestamp = DateTime.Now.ToString("HH:mm");
            app.LiveScreen.AddEvent("[" + timestamp + "] " + evt.Description);

            if (watchState?.MemoryBank == null) return;
            switch (evt.Type)
            {
                case GameEventType.DayBegan:
                    watchState.MemoryBank.AddEvent(evt.Description, MemoryCategory.WorldEvent,
                        new List<string> { "day", "time" });
                    break;
                case GameEventType.RainStarted:
                    watchState.MemoryBank.AddEvent(evt.Description, MemoryCategory.WorldEvent,
                        new List<string> { "weather", "rain" });
                    break;
                case GameEventType.InterventionUsed:
                    watchState.MemoryBank.AddEvent(evt.Description, MemoryCategory.WorldEvent,
                        new List<string> { "intervention" });
                    break;
            }
        }

        private void OnUrgentGameEvent(GameEvent evt)
        {
            if (watchState?.Soulform == null) return;
            if (watchState.BrainService == null || !watchState.BrainService.IsConfigured) return;

            switch (evt.Type)
            {
                case GameEventType.SoulformFoundFood:
                case GameEventType.RainStarted:
                case GameEventType.DayBegan:
                case GameEventType.InterventionUsed:
                    if (eventTickCooldown <= 0f)
                    {
                        pendingBrainTick = true;
                        lastUrgentEvent = evt.Description;
                    }
                    break;
            }
        }

        private void OnSpeedChanged(WatchSpeed speed)
        {
            if (watchState == null) return;
            watchState.Speed = speed;
            app?.LiveScreen?.UpdateSpeed(watchState);
        }

        private void OnPauseToggled()
        {
            if (watchState == null) return;
            watchState.Speed = watchState.IsPaused ? WatchSpeed.Normal : WatchSpeed.Pause;
            app?.LiveScreen?.UpdateSpeed(watchState);
        }

        private void OnCameraModeChanged(Camera3DView view)
        {
            if (watchState == null) return;
            watchState.CurrentCameraView = view;
            app?.Grid?.Set3DView(view);
            app?.LiveScreen?.UpdateCameraHighlights(view);
        }

        private void OnInterventionClicked(int index)
        {
            if (watchState == null || app?.Bubble == null) return;
            var types = (InterventionType[])Enum.GetValues(typeof(InterventionType));
            if (index < 0 || index >= types.Length) return;
            InterventionType type = types[index];

            if (lockedInterventions.Contains(type))
            {
                ShowPlayerNotice(type + " is locked - learn your first skill to unlock it");
                return;
            }

            if (watchState.Cooldowns.ContainsKey(type))
            {
                ShowPlayerNotice(type + " is on cooldown");
                return;
            }

            if (watchState.ArchitectPoints != null && !watchState.ArchitectPoints.CanAffordIntervention())
            {
                ShowPlayerNotice(economyConfig != null
                    ? economyConfig.InsufficientAPMessage
                    : "The Soulform seems distant.");
                return;
            }

            if (watchState.ArchitectPoints != null && !watchState.ArchitectPoints.SpendIntervention())
                return;

            ExecuteIntervention(type);
            tutorial?.NotifyIntervention(type);
            UpdateAPDisplay();
        }

        private void ExecuteIntervention(InterventionType type)
        {
            var def = Intervention.FromType(type);
            if (def == null) return;

            var soulform = watchState.Soulform;
            int sx = soulform?.GridX ?? 12;
            int sy = soulform?.GridY ?? 0;
            int sz = soulform?.GridZ ?? 12;

            switch (type)
            {
                case InterventionType.AddFood:
                    PlaceNearby(sx, sy, sz, "bush");
                    break;
                case InterventionType.GrowTree:
                    PlaceNearby(sx, sy, sz, "tree");
                    break;
                case InterventionType.ChangeWeather:
                    string oldW = watchState.WeatherCondition;
                    string newW;
                    do { newW = WeatherCycle[Random.Range(0, WeatherCycle.Length)]; } while (newW == oldW);
                    watchState.WeatherCondition = newW;
                    watchState.EventLog?.Record(GameEventType.InterventionUsed,
                        "Changed weather to " + newW);
                    break;
                case InterventionType.DayNight:
                    float currentHour = watchState.ElapsedTime / 3600f;
                    watchState.ElapsedTime = currentHour < 12f
                        ? watchState.ElapsedTime + 12f * 3600f
                        : watchState.ElapsedTime - 12f * 3600f;
                    watchState.EventLog?.Record(GameEventType.InterventionUsed,
                        "Toggled day/night");
                    break;
                case InterventionType.SpawnStructure:
                    PlaceNearby(sx, sy, sz, "simple_shelter");
                    break;
                case InterventionType.SpawnAnimal:
                    {
                        var animalId = "animal_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
                        var species = (AnimalSpecies)UnityEngine.Random.Range(0, 4);
                        var diet = species == AnimalSpecies.Wolf ? AnimalDiet.Carnivore
                            : species == AnimalSpecies.Sheep ? AnimalDiet.Herbivore
                            : species == AnimalSpecies.Bear ? AnimalDiet.Omnivore
                            : AnimalDiet.Herbivore;
                        int asx = Mathf.Clamp(sx + UnityEngine.Random.Range(-5, 6), 1, 59);
                        int asz = Mathf.Clamp(sz + UnityEngine.Random.Range(-5, 6), 1, 59);
                        var animalData = new AnimalData(animalId, species, diet, asx, 0, asz);
                        watchState.EntityManager?.Register(animalData);
                        watchState.EventLog?.Record(GameEventType.AnimalSpawned,
                            species + " appeared at (" + asx + "," + asz + ")");
                    }
                    break;
                case InterventionType.SpawnNpc:
                    {
                        var npcId = "npc_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
                        int nsx = Mathf.Clamp(sx + UnityEngine.Random.Range(-5, 6), 1, 59);
                        int nsz = Mathf.Clamp(sz + UnityEngine.Random.Range(-5, 6), 1, 59);
                        var npcData = new NPCData(npcId, nsx, 0, nsz);
                        watchState.EntityManager?.Register(npcData);
                        watchState.EventLog?.Record(GameEventType.NpcSpawned,
                            "A visitor arrived at (" + nsx + "," + nsz + ")");
                    }
                    break;
                case InterventionType.RemindSoulform:
                    if (soulform != null)
                    {
                        soulform.CurrentThought = "I feel a presence guiding me...";
                        soulform.Curiosity = Mathf.Min(100, soulform.Curiosity + 10);
                        watchState.EventLog?.Record(GameEventType.SoulformReacted,
                            soulform.Name + " felt a nudge");
                    }
                    break;
            }

            float cooldown = def.CooldownSeconds;
            if (cooldown > 0f)
                watchState.Cooldowns[type] = cooldown;

            if (type == InterventionType.AddFood || type == InterventionType.GrowTree || type == InterventionType.SpawnStructure)
            {
                app?.Grid?.RefreshAll();
                app?.RefreshLivability();
            }
        }

        private void PlaceNearby(int cx, int cy, int cz, string itemId)
        {
            if (app?.Bubble == null) return;
            var def = BubblePlacementCatalog.Find(itemId);
            if (def == null) return;

            int nx = Mathf.Clamp(cx + Random.Range(-2, 3), 0, app.Bubble.Width - 1);
            int nz = Mathf.Clamp(cz + Random.Range(-2, 3), 0, app.Bubble.Height - 1);

            app.Bubble.Place(nx, cy, nz, def);
            var cell = app.Bubble.Get(nx, cy, nz);
            if (cell != null) cell.FromIntervention = true;

            watchState.EventLog?.Record(GameEventType.InterventionUsed,
                def.DisplayName + " placed near (" + cx + "," + cz + ")");
        }

        // ── Economy / skill / tutorial wiring ──

        private void InitializeInterventionLocks()
        {
            lockedInterventions.Clear();
            var types = (InterventionType[])Enum.GetValues(typeof(InterventionType));
            for (int i = 0; i < types.Length; i++)
            {
                bool locked = i >= StartingInterventionCount;
                if (locked) lockedInterventions.Add(types[i]);
                app?.LiveScreen?.SetInterventionEnabled(i, !locked);
            }
        }

        private void UnlockAllInterventions()
        {
            if (lockedInterventions.Count == 0) return;
            lockedInterventions.Clear();

            var types = (InterventionType[])Enum.GetValues(typeof(InterventionType));
            for (int i = 0; i < types.Length; i++)
            {
                bool onCooldown = watchState.Cooldowns.ContainsKey(types[i]);
                app?.LiveScreen?.SetInterventionCooldown(i, onCooldown);
            }
            ShowPlayerNotice("Your first skill unlocked new interventions!");
        }

        /// <summary>Surfaces a player-facing notice in the live event log.</summary>
        private void ShowPlayerNotice(string message)
        {
            if (app?.LiveScreen == null) return;
            app.LiveScreen.AddEvent("[" + DateTime.Now.ToString("HH:mm") + "] " + message);
        }

        private void UpdateAPDisplay()
        {
            int balance = watchState?.ArchitectPoints?.Balance ?? 0;
            app?.LiveScreen?.UpdateAP(balance);
            app?.SoulformChatScreen?.UpdateAP(balance, APReferenceMax());
        }

        private int APReferenceMax()
        {
            int starting = economyConfig != null ? economyConfig.StartingAP : 100;
            return Mathf.Max(starting, watchState?.ArchitectPoints?.Balance ?? 0);
        }

        private void HandleAPBalanceChanged(int balance)
        {
            UpdateAPDisplay();
        }

        private void HandleAPInsufficient()
        {
            ShowPlayerNotice(economyConfig != null
                ? economyConfig.InsufficientAPMessage
                : "The Soulform seems distant.");
        }

        private void HandleSkillAcquired(string skillName)
        {
            watchState?.ArchitectPoints?.OnSkillUnlocked();
            watchState?.EventLog?.Record(GameEventType.SkillLearned,
                "Skill learned: " + skillName + " (+" + (economyConfig?.SkillUnlockReward ?? 10) + " AP)");
            RecordMemory("Learned skill: " + skillName, MemoryCategory.Skill,
                "skill", skillName.ToLowerInvariant());
            UnlockAllInterventions();
            tutorial?.NotifySkill(skillName);
            UpdateAPDisplay();
        }

        private void HandleSkillExecuted(string skillName, int executions)
        {
            tutorial?.NotifySkill(skillName);
        }

        private void HandleSkillSetLeveled(string setName, int starLevel)
        {
            watchState?.ArchitectPoints?.OnSkillLeveledUp();
            watchState?.EventLog?.Record(GameEventType.SkillLearned,
                setName + " skill set reached " + starLevel + " star(s)");
            UpdateAPDisplay();
        }

        private void HandleActionCompleted(SoulformAction action, bool success)
        {
            tutorial?.NotifyActionCompleted(action, success);
        }

        private void HandleTutorialGuidance(string message)
        {
            watchState?.EventLog?.Record(GameEventType.TutorialPrompt, message);
        }

        private void OnOpenProfile()
        {
            if (watchState?.Soulform == null) return;
            if (app?.SoulformProfileScreen == null) return;
            app.ScreenController?.ShowScreen(UIScreenId.SoulformProfile);
            app.SoulformProfileScreen.Build(watchState);
        }

        private void OnOpenMasterNavigation()
        {
            var nav = app?.MasterNavigation;
            if (nav == null) return;
            nav.Toggle();
        }

        private void OnTalkObserve()
        {
            if (watchState == null) { Debug.LogWarning("Chat: no watch state"); return; }
            var soulform = watchState.Soulform;
            if (soulform == null)
            {
                soulform = SoulformModel.CreateDefault(SoulformTemperament.Inquisitive);
                soulform.Name = "Mysterious Stranger";
                watchState.Soulform = soulform;
                app.CurrentSoulform = soulform;
                Debug.Log("Chat: created placeholder soulform");
            }

            if (app?.SoulformChatScreen == null) { Debug.LogWarning("Chat: no chat screen"); return; }
            if (app?.ScreenController == null) { Debug.LogWarning("Chat: no screen controller"); return; }

            Debug.Log("Chat: opening for " + soulform.Name);
            app.ScreenController.ShowScreen(UIScreenId.SoulformChat);
            app.SoulformChatScreen.Build(soulform.Name);
            app.SoulformChatScreen.SetStatus(BuildChatStatus(soulform));
            app.SoulformChatScreen.SetupModel(soulform, soulform.CurrentExpression, soulform.CurrentAnimation);
            lastConversationArchive = BuildConversationArchive();
            app.SoulformChatScreen.SetConversations(lastConversationArchive);
            app.SoulformChatScreen.OnTopicSelected += OnArchiveTopicSelected;

            chatHistory.Clear();
            chatHistory.Add(new ChatMessage(SpeakerType.Soulform,
                soulform.CurrentThought,
                emotion: EmotionTag(soulform.Mood),
                expression: soulform.CurrentExpression));

            app.SoulformChatScreen.AddMessages(chatHistory);
        }

        private SoulformStatus BuildChatStatus(SoulformModel soulform)
        {
            var status = new SoulformStatus
            {
                Name = soulform.Name,
                Type = soulform.Temperament.ToString(),
                Mood = soulform.Mood.ToString(),
                Location = BuildLocationName(soulform),
                RelationshipLevel = Mathf.RoundToInt(Mathf.Max(0f, soulform.StartingAffinity) * 10f)
            };

            if (watchState?.Weather != null)
                status.Season = watchState.Weather.CurrentSeason.ToString();
            else if (watchState != null)
                status.Season = SeasonNames[Mathf.Clamp(watchState.Season, 0, SeasonNames.Length - 1)];

            int hour = watchState?.GameTime != null
                ? watchState.GameTime.Hour
                : Mathf.FloorToInt(watchState?.ElapsedTime ?? 0f) % 24;
            status.TimeOfDay = HourToTimeOfDay(hour);

            // Real Architect Points balance (the old values were placeholder math).
            status.CurrentAP = watchState?.ArchitectPoints?.Balance ?? 0;
            status.MaxAP = Mathf.Max(status.CurrentAP, APReferenceMax());

            return status;
        }

        private string BuildLocationName(SoulformModel soulform)
        {
            string gridPos = "(" + soulform.GridX + "," + soulform.GridZ + ")";
            var nearby = app?.Bubble?.Get(soulform.GridX, soulform.GridY, soulform.GridZ);
            if (nearby != null && !string.IsNullOrEmpty(nearby.ItemId))
                return "Near " + nearby.ItemId + " " + gridPos;
            return "Exploring " + gridPos;
        }

        private static string HourToTimeOfDay(int hour)
        {
            if (hour < 5) return "Late Night";
            if (hour < 8) return "Early Morning";
            if (hour < 12) return "Late Morning";
            if (hour < 14) return "Noon";
            if (hour < 17) return "Afternoon";
            if (hour < 20) return "Evening";
            return "Night";
        }

        private List<Conversation> BuildConversationArchive()
        {
            var conversations = new List<Conversation>();
            var bank = watchState?.MemoryBank;
            if (bank == null) return conversations;

            int limit = Mathf.Min(20, bank.Chapters.Count);
            for (int i = Mathf.Max(0, bank.Chapters.Count - limit); i < bank.Chapters.Count; i++)
            {
                var chapter = bank.GetChapter(i);
                if (chapter == null) continue;
                string title = chapter.Summary ?? "Chapter " + (i + 1);
                var conv = new Conversation("chapter-" + i, title);
                conv.LastUpdated = chapter.StartTime.UtcDateTime;
                foreach (var entry in chapter.Entries)
                {
                    var speaker = entry.Description.StartsWith("Player said", StringComparison.Ordinal)
                        ? SpeakerType.Player
                        : SpeakerType.Soulform;
                    conv.AddMessage(new ChatMessage(speaker, entry.Description,
                        entry.Timestamp.LocalDateTime.ToString("h:mm tt")));
                }
                if (conv.Messages.Count == 0)
                    conv.AddMessage(new ChatMessage(SpeakerType.Soulform, chapter.Summary));
                conversations.Add(conv);
            }
            return conversations;
        }

        private void OnChatMessage(string text)
        {
            if (watchState?.Soulform == null || app?.SoulformChatScreen == null) return;
            if (chatCoroutine != null) return;

            // Conversation is metered: opening is free, every message costs AP.
            if (watchState.ArchitectPoints != null)
            {
                int tokens = Mathf.Max(1, Mathf.CeilToInt(text.Length / 4f));
                if (!watchState.ArchitectPoints.CanAffordCommunication(tokens, true))
                {
                    ShowChatNotice(economyConfig != null
                        ? economyConfig.InsufficientAPMessage
                        : "The Soulform seems distant.");
                    return;
                }
                int cost = Mathf.CeilToInt(watchState.ArchitectPoints.CalculateCommunicationCost(tokens, true));
                watchState.ArchitectPoints.SpendAP(cost);
            }

            chatHistory.Add(new ChatMessage(SpeakerType.Player, text));
            app.SoulformChatScreen.AddMessage(chatHistory[chatHistory.Count - 1]);
            app.SoulformChatScreen.SetTyping(true);

            RecordMemory("Player said: " + text, MemoryCategory.Conversation, "chat", "player");

            string worldContext = BuildWorldContext();
            chatCoroutine = app.StartCoroutine(watchState.BrainService.QueryChat(text, worldContext,
                OnChatResult,
                error =>
                {
                    Debug.LogWarning("Chat: " + error);
                    chatHistory.Add(new ChatMessage(SpeakerType.Soulform, "..."));
                    app.SoulformChatScreen.AddMessage(chatHistory[chatHistory.Count - 1]);
                    app.SoulformChatScreen.SetTyping(false);
                    chatCoroutine = null;
                }));
        }

        private void OnChatResult(SoulformBrainResponse response)
        {
            if (app?.SoulformChatScreen == null) return;
            chatCoroutine = null;
            app.SoulformChatScreen.SetTyping(false);

            // Soulform replies are metered at the half rate; never block delivery.
            if (watchState?.ArchitectPoints != null && !string.IsNullOrEmpty(response.CurrentThought))
            {
                int tokens = Mathf.Max(1, Mathf.CeilToInt(response.CurrentThought.Length / 4f));
                int cost = Mathf.CeilToInt(watchState.ArchitectPoints.CalculateCommunicationCost(tokens, false));
                if (cost > 0 && watchState.ArchitectPoints.Balance >= cost)
                    watchState.ArchitectPoints.SpendAP(cost);
            }

            var reply = new ChatMessage(SpeakerType.Soulform,
                response.CurrentThought,
                emotion: response.EmotionTag,
                expression: response.Expression,
                animation: response.Animation);

            bool voiced = VoiceService.Speak(response.CurrentThought);
            reply.HasVoice = voiced;
            reply.VoiceClipId = voiced ? "tts-" + DateTime.Now.ToString("HHmmssfff") : null;

            chatHistory.Add(reply);
            app.SoulformChatScreen.AddMessage(reply);

            if (watchState?.Soulform != null)
            {
                watchState.Soulform.CurrentThought = response.CurrentThought;
                watchState.Soulform.CurrentExpression = response.Expression ?? "Neutral";
                watchState.Soulform.CurrentAnimation = response.Animation ?? "Idle";
                app.SoulformChatScreen.UpdateEmotion(response.Expression, response.Animation);
                app.SoulformChatScreen.Speak(response.CurrentThought);
            }
        }

        /// <summary>Shows a narrator-style notice inside the chat conversation.</summary>
        private void ShowChatNotice(string message)
        {
            if (app?.SoulformChatScreen == null) return;
            var notice = new ChatMessage(SpeakerType.Soulform, message);
            chatHistory.Add(notice);
            app.SoulformChatScreen.AddMessage(notice);
        }

        private void OnChatClose()
        {
            if (app?.SoulformChatScreen != null)
                app.SoulformChatScreen.OnTopicSelected -= OnArchiveTopicSelected;
            if (chatCoroutine != null)
            {
                app?.StopCoroutine(chatCoroutine);
                chatCoroutine = null;
                app?.SoulformChatScreen?.SetTyping(false);
            }
            app?.SoulformChatScreen?.CleanupModel();
            app?.ScreenController?.ShowScreen(UIScreenId.LiveWatch);
        }

        private void OnArchiveTopicSelected(Topic topic)
        {
            if (topic == null) return;
            var conversation = lastConversationArchive.Find(c => c.Id == topic.Id);
            if (conversation != null)
                app?.SoulformChatScreen?.LoadConversation(conversation);
        }

        private void OnProfileClose()
        {
            app?.ScreenController?.ShowScreen(UIScreenId.LiveWatch);
        }

        private static string EmotionTag(SoulformMood mood)
        {
            switch (mood)
            {
                case SoulformMood.Content:    return "[Content]";
                case SoulformMood.Curious:    return "[Curious]";
                case SoulformMood.Hungry:     return "[Hungry]";
                case SoulformMood.Tired:      return "[Tired]";
                case SoulformMood.Inspired:   return "[Inspired]";
                case SoulformMood.Distressed: return "[Distressed]";
                default:                      return "[Neutral]";
            }
        }

        private static string ExtractEmotionTag(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(text, @"^\[(\w+)\]");
            return match.Success ? match.Value : null;
        }

        private void OnOpenLivabilityDetails()
        {
            if (app?.Bubble == null) return;
            var report = BubbleLivability.Calculate(app.Bubble);
            app.LiveScreen?.UpdateLivabilityDetails(FormatLivabilityDetails(report));
            app.LiveScreen?.ShowLivabilityDetails();
        }

        private void OnCloseLivabilityDetails()
        {
            app.LiveScreen?.HideLivabilityDetails();
        }

        private void RefreshUI()
        {
            if (app?.LiveScreen == null || watchState == null) return;

            app.LiveScreen.UpdateTime(watchState.ElapsedTime,
                watchState.DayCount, "Day " + watchState.DayCount,
                watchState.Season, 1);

            app.LiveScreen.UpdateWeather(watchState.WeatherCondition,
                watchState.WeatherCondition == "Rain" ? "Cool" : "Mild");

            if (app.Bubble != null)
            {
                var report = BubbleLivability.Calculate(app.Bubble);
                app.LiveScreen.UpdateLivability(report.Percentage, report.Rating,
                    report.Percentage >= 80 ? "Thriving" : report.Percentage >= 60 ? "Stable" : "Fragile");
                app.LiveScreen.UpdateBubbleName(app.State?.WorldName ?? "World");
            }

            if (watchState.Soulform != null)
                app.LiveScreen.UpdateSoulform(watchState.Soulform);
        }

        private static string FormatLivabilityDetails(LivabilityReport report)
        {
            var lines = new System.Collections.Generic.List<string>();
            lines.Add("<b>WORLD CONDITIONS</b>");
            foreach (var c in report.WorldConditions)
            {
                string rating = c.Rating switch
                {
                    LivabilityRating.Good => "<color=#4DFF8A>Good</color>",
                    LivabilityRating.Fair => "<color=#A0D468>Fair</color>",
                    LivabilityRating.Mild => "<color=#FFB347>Mild</color>",
                    _ => "<color=#FF6B6B>Poor</color>"
                };
                int filled = c.Value / 5;
                int empty = 20 - filled;
                string bar = "[" + new string('#', filled) + new string('-', empty) + "]";
                lines.Add("  " + c.Name + ": " + rating + "  " + c.Value + "%");
                lines.Add("    " + bar);
            }
            lines.Add("");
            int met = 0;
            foreach (var c in report.LifeConditions)
                if (c.Met) met++;
            int total = report.LifeConditions.Count;
            string lifeRating = met == total ? "Good"
                : met >= total * 0.75f ? "Fair"
                : met >= total * 0.5f ? "Mild"
                : "Poor";
            lines.Add("  " + met + "/" + total + " met — " + lifeRating);
            lines.Add("");
            lines.Add(report.AnimaReady
                ? "<color=#4DFF8A><b>Ready to create your Primary Anima</b></color>"
                : "<color=#FFB347><b>Not yet ready for Primary Anima</b></color>");
            return string.Join("\n", lines);
        }

        private void TickEntityVisuals()
        {
            if (watchState.EntityVisualManager == null) return;
            watchState.EntityVisualManager.SyncAllVisuals();
        }

        private void ApplyStormDamageIfNeeded()
        {
            if (watchState.Weather == null || watchState.StormDamage == null) return;
            var weather = watchState.Weather.CurrentWeather;
            if (weather == WeatherType.Storm1 || weather == WeatherType.Storm2 || weather == WeatherType.Storm3)
            {
                if (watchState.GameTime == null || watchState.GameTime.TutorialDaysRemaining > 0) return;
                watchState.StormDamage.ApplyStormDamage(weather);
            }
        }
    }
}
