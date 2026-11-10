using System;
using System.Collections.Generic;

namespace BubbleWorld
{
    public sealed class WatchState
    {
        public WatchSpeed Speed = WatchSpeed.Normal;
        public float ElapsedTime;
        public int DayCount = 1;
        public bool IsPaused => Speed == WatchSpeed.Pause;

        public string WeatherCondition = "Clear";
        public int Season;

        public SoulformModel Soulform;
        public Camera3DView CurrentCameraView = Camera3DView.FollowSoulform;

        public EventLog EventLog;
        public SoulformBrainService BrainService;
        public Dictionary<InterventionType, float> Cooldowns = new Dictionary<InterventionType, float>();
        public float BrainTickTimer;

        public List<(int x, int y, int z)> CurrentPath;
        public int CurrentPathIndex;
        public float MoveTimer;

        public const float DayLength = 86400f;
        public const float WeatherChangeInterval = 30f;

        public GameTimeManager GameTime;
        public WeatherManager Weather;
        public SourceLogicSystem SourceLogic;
        public ArchitectPoints ArchitectPoints;
        public SkillProgression SkillProgression;
        public MemoryBank MemoryBank;
        public SpawnManager SpawnManager;
        public AnimalAI AnimalAI;
        public NPCAI NPCAI;
        public SoulformAction? CurrentAction;
        public string ActionTargetId;

        public DateTime? UtcLastSave;
        public DateTime? UtcLastLoad;
        public float TimeMultiplier = 1f;
        public int TutorialDaysRemaining = 7;

        public EntityManager EntityManager;
        public EntityVisualManager EntityVisualManager;
        public EntityMovementSystem EntityMovementSystem;
        public SoulformActionExecutor ActionExecutor;
        public SoulformDeathHandler DeathHandler;
        public WeatherVisualSystem WeatherVisuals;
        public DayNightCycle DayNight;
        public StormDamageSystem StormDamage;
        public BubbleWorldManager BubbleWorldManager;
        public IWorldDataProvider DataProvider;
        public int LastProcessedDay = -1;
    }
}
