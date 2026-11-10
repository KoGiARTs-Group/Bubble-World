using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class SpawnManager : IWorldTimeReceiver
    {
        private readonly SpawnConfig config;
        private readonly DeterministicRandom random;
        private readonly Dictionary<string, int> npcResourceDays = new Dictionary<string, int>();
        private readonly List<string> disgruntledNPCs = new List<string>();
        private int daysSinceLastAnimalSpawn;
        private int daysSinceLastNPCSpawn;
        private int lastProcessedDay = -1;

        public EntityManager EntityManager { get; set; }
        public BubbleModel Bubble { get; set; }
        public AIConfig AIConfig { get; set; }
        public EventLog EventLog { get; set; }

        public IReadOnlyList<string> DisgruntledNPCs => disgruntledNPCs.AsReadOnly();

        public event System.Action<string> OnNPCBorn;
        public event System.Action<string, ScarcityResponse> OnNPCBecomesDisgruntled;

        public SpawnManager(SpawnConfig config, int seed)
        {
            this.config = config;
            random = new DeterministicRandom(seed);
        }

        public void OnGameTick(float deltaSeconds, WorldTimeContext context)
        {
            if (context.DayCount == lastProcessedDay) return;
            lastProcessedDay = context.DayCount;

            daysSinceLastAnimalSpawn++;
            daysSinceLastNPCSpawn++;

            TrySpawnAnimal();
            TrySpawnNPC();
            UpdateDisgruntledStatus();
        }

        public void OnFastForward(float totalMissedSeconds, WorldTimeContext context)
        {
            int daysMissed = Mathf.FloorToInt(totalMissedSeconds / 86400f);
            daysSinceLastAnimalSpawn += daysMissed;
            daysSinceLastNPCSpawn += daysMissed;
        }

        public bool CanSpawnAnimal(bool foodAvailable, bool waterAvailable)
        {
            if (daysSinceLastAnimalSpawn < config.AnimalSpawnCooldownDays)
                return false;
            if (config.AnimalSpawnRequiresFood && !foodAvailable)
                return false;
            if (config.AnimalSpawnRequiresWater && !waterAvailable)
                return false;
            return true;
        }

        public bool CanSpawnNPC(int consecutiveFood, int consecutiveWater, int consecutiveShelter)
        {
            if (daysSinceLastNPCSpawn < config.NPCSpawnCooldownDays)
                return false;
            if (consecutiveFood < config.NPCSpawnFoodDays)
                return false;
            if (consecutiveWater < config.NPCSpawnWaterDays)
                return false;
            if (consecutiveShelter < config.NPCSpawnShelterDays)
                return false;
            return true;
        }

        public void OnAnimalSpawned()
        {
            daysSinceLastAnimalSpawn = 0;
        }

        public void OnNPCSpawned()
        {
            daysSinceLastNPCSpawn = 0;
        }

        public void TrackResourceDay(string npcId, bool hasAdequateResources)
        {
            if (!npcResourceDays.ContainsKey(npcId))
                npcResourceDays[npcId] = 0;

            if (hasAdequateResources)
            {
                npcResourceDays[npcId] = 0;
                if (disgruntledNPCs.Contains(npcId))
                    disgruntledNPCs.Remove(npcId);
            }
            else
            {
                npcResourceDays[npcId]++;
                if (npcResourceDays[npcId] >= config.DisgruntledTriggerDays &&
                    !disgruntledNPCs.Contains(npcId))
                {
                    disgruntledNPCs.Add(npcId);
                    var response = RollScarcityResponse();
                    OnNPCBecomesDisgruntled?.Invoke(npcId, response);
                }
            }
        }

        public ScarcityResponse RollScarcityResponse()
        {
            int roll = random.RangeInclusive(1, 100);
            foreach (var entry in config.ScarcityResponseTable)
            {
                if (roll >= entry.RangeStart && roll <= entry.RangeEnd)
                    return entry.Response;
            }
            return ScarcityResponse.Cooperate;
        }

        private void TrySpawnAnimal()
        {
            if (EntityManager == null || Bubble == null) return;
            if (daysSinceLastAnimalSpawn < config.AnimalSpawnCooldownDays) return;

            var report = BubbleLivability.Calculate(Bubble);
            bool food = report.Metrics.GetValueOrDefault("Food Potential", 0) >= 40;
            bool water = report.Metrics.GetValueOrDefault("Water Access", 0) >= 40;

            if (!CanSpawnAnimal(food, water)) return;

            if (random.Value01() > config.AnimalSpawnChance) return;

            AnimalSpecies species = (AnimalSpecies)random.RangeInclusive(0, 3);
            AnimalDiet diet = species == AnimalSpecies.Wolf ? AnimalDiet.Carnivore
                : species == AnimalSpecies.Sheep ? AnimalDiet.Herbivore
                : species == AnimalSpecies.Bear ? AnimalDiet.Omnivore
                : AnimalDiet.Herbivore;

            int sx = random.RangeInclusive(5, Bubble.Width - 5);
            int sz = random.RangeInclusive(5, Bubble.Height - 5);

            string id = "animal_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var animalData = new AnimalData(id, species, diet, sx, 0, sz);
            EntityManager.Register(animalData);

            if (AIConfig != null)
            {
                var ai = new AnimalAI(AIConfig, diet, random.RangeInclusive(1, 999999), species)
                {
                    BoundAnimalData = animalData,
                    EntityManager = EntityManager,
                    Bubble = Bubble
                };
                EntityManager.RegisterAnimalAI(id, ai);
            }

            OnAnimalSpawned();
            EventLog?.Record(GameEventType.AnimalSpawned,
                species + " appeared at (" + sx + "," + sz + ")");
        }

        private void TrySpawnNPC()
        {
            if (EntityManager == null || Bubble == null) return;
            if (daysSinceLastNPCSpawn < config.NPCSpawnCooldownDays) return;

            int totalNPCs = EntityManager.GetAllNPCs().Count;
            if (totalNPCs >= config.MaxNPCCount) return;

            if (random.Value01() > config.NPCSpawnChance) return;

            int sx = random.RangeInclusive(5, Bubble.Width - 5);
            int sz = random.RangeInclusive(5, Bubble.Height - 5);

            string id = "npc_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var npcData = new NPCData(id, sx, 0, sz);
            EntityManager.Register(npcData);

            if (AIConfig != null)
            {
                var ai = new NPCAI(AIConfig, random.RangeInclusive(1, 999999))
                {
                    BoundNPCData = npcData,
                    EntityManager = EntityManager,
                    Bubble = Bubble
                };
                EntityManager.RegisterNPCAI(id, ai);
            }

            OnNPCSpawned();
            OnNPCBorn?.Invoke(id);
            EventLog?.Record(GameEventType.NpcSpawned,
                "A visitor arrived at (" + sx + "," + sz + ")");
        }

        private void UpdateDisgruntledStatus()
        {
            if (EntityManager == null) return;
            var npcs = EntityManager.GetAllNPCs();
            foreach (var npc in npcs)
            {
                var report = Bubble != null ? BubbleLivability.Calculate(Bubble) : null;
                bool hasAdequate = report != null
                    && report.Metrics.GetValueOrDefault("Food Potential", 0) >= 30
                    && report.Metrics.GetValueOrDefault("Water Access", 0) >= 30
                    && report.Metrics.GetValueOrDefault("Shelter", 0) >= 20;

                TrackResourceDay(npc.EntityId, hasAdequate);
                npc.HasAdequateResources = hasAdequate;
            }
        }
    }
}
