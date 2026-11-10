using UnityEngine;

namespace BubbleWorld
{
    public enum AnimalSpecies
    {
        Wolf,
        Sheep,
        Bear,
        Generic
    }

    public sealed class AnimalAI : IWorldTimeReceiver
    {
        private readonly AIConfig config;
        private readonly DeterministicRandom random;
        private readonly AnimalSpecies species;

        public AnimalDiet Diet { get; private set; }
        public AnimalSpecies Species => species;
        public float HungerLevel { get; private set; }
        public float MaxHunger { get; private set; }
        public float HungerRate { get; private set; }
        public bool IsAttacked { get; set; }
        public AnimalState CurrentState { get; private set; } = AnimalState.Docile;
        public float SocialCooldown { get; set; }
        public string CurrentFoodTarget { get; set; }
        public AnimalData BoundAnimalData { get; set; }
        public EntityManager EntityManager { get; set; }
        public BubbleModel Bubble { get; set; }

        private static readonly float HungerThreshold = 0.30f;

        public AnimalAI(AIConfig config, AnimalDiet diet, int seed, AnimalSpecies species = AnimalSpecies.Generic)
        {
            this.config = config;
            Diet = diet;
            this.species = species;
            random = new DeterministicRandom(seed);

            switch (species)
            {
                case AnimalSpecies.Wolf:
                    MaxHunger = 100f;
                    HungerRate = 3f;
                    break;
                case AnimalSpecies.Sheep:
                    MaxHunger = 80f;
                    HungerRate = 2.5f;
                    break;
                case AnimalSpecies.Bear:
                    MaxHunger = 150f;
                    HungerRate = 1.5f;
                    break;
                default:
                    MaxHunger = 100f;
                    HungerRate = 2f;
                    break;
            }

            HungerLevel = MaxHunger * random.RangeInclusive(50, 90) / 100f;
            SocialCooldown = random.RangeInclusive(10, 60);
        }

        public void OnGameTick(float deltaSeconds, WorldTimeContext context)
        {
            float hours = deltaSeconds / 3600f;
            HungerLevel = Mathf.Max(0, HungerLevel - hours * HungerRate);

            if (SocialCooldown > 0)
                SocialCooldown -= deltaSeconds;

            UpdateState();
            UpdateBoundAnimalData();
            ExecuteAnimalBehavior();
        }

        public void OnFastForward(float totalMissedSeconds, WorldTimeContext context)
        {
            float hours = totalMissedSeconds / 3600f;
            HungerLevel = Mathf.Max(0, HungerLevel - hours * HungerRate);
            UpdateState();
            UpdateBoundAnimalData();
        }

        private void UpdateState()
        {
            float hungerRatio = HungerLevel / MaxHunger;
            bool isHungry = hungerRatio <= HungerThreshold;

            switch (Diet)
            {
                case AnimalDiet.Carnivore:
                    UpdateCarnivore(isHungry);
                    break;
                case AnimalDiet.Herbivore:
                    UpdateHerbivore(isHungry);
                    break;
                case AnimalDiet.Omnivore:
                    UpdateOmnivore(isHungry);
                    break;
            }
        }

        private void UpdateCarnivore(bool isHungry)
        {
            if (IsAttacked)
            {
                string action = RollAction(config.Carnivore.SafetyRolls);
                CurrentState = action == "Flee" ? AnimalState.Fleeing : AnimalState.Attacking;
                return;
            }

            if (isHungry)
            {
                string action = RollAction(config.Carnivore.FoodRolls);
                CurrentState = action == "Hunt" ? AnimalState.Hunting : AnimalState.Foraging;
                return;
            }

            if (SocialCooldown <= 0)
            {
                CurrentState = AnimalState.Socializing;
                SocialCooldown = random.RangeInclusive(30, 120);
                return;
            }

            CurrentState = AnimalState.Docile;
        }

        private void UpdateHerbivore(bool isHungry)
        {
            if (IsAttacked)
            {
                string action = RollAction(config.Herbivore.SafetyRolls);
                CurrentState = action == "Flee" ? AnimalState.Fleeing : AnimalState.Attacking;
                return;
            }

            if (isHungry)
            {
                CurrentState = AnimalState.Foraging;
                return;
            }

            if (SocialCooldown <= 0)
            {
                CurrentState = AnimalState.Socializing;
                SocialCooldown = random.RangeInclusive(30, 120);
                return;
            }

            CurrentState = AnimalState.Docile;
        }

        private void UpdateOmnivore(bool isHungry)
        {
            if (IsAttacked)
            {
                string action = RollAction(config.Omnivore.SafetyRolls);
                CurrentState = action == "Flee" ? AnimalState.Fleeing : AnimalState.Attacking;
                return;
            }

            if (isHungry)
            {
                string action = RollAction(config.Omnivore.FoodRolls);
                CurrentState = action == "Hunt" ? AnimalState.Hunting : AnimalState.Foraging;
                return;
            }

            if (SocialCooldown <= 0)
            {
                CurrentState = AnimalState.Socializing;
                SocialCooldown = random.RangeInclusive(30, 120);
                return;
            }

            CurrentState = AnimalState.Docile;
        }

        public void Feed(float amount)
        {
            HungerLevel = Mathf.Min(MaxHunger, HungerLevel + amount);
        }

        public bool IsHungry => (HungerLevel / MaxHunger) <= HungerThreshold;
        public float HungerRatio => HungerLevel / MaxHunger;

        private string RollAction(RollRangeEntry[] rolls)
        {
            int roll = random.RangeInclusive(1, 100);
            foreach (var entry in rolls)
            {
                if (roll >= entry.RangeStart && roll <= entry.RangeEnd)
                    return entry.Action;
            }
            return CurrentState == AnimalState.Hungry ? "Forage" : "Idle";
        }

        private void UpdateBoundAnimalData()
        {
            if (BoundAnimalData == null) return;
            BoundAnimalData.HungerLevel = HungerLevel;
            BoundAnimalData.CurrentState = CurrentState;
            BoundAnimalData.IsAttacked = IsAttacked;
            BoundAnimalData.SocialCooldown = SocialCooldown;
        }

        private void ExecuteAnimalBehavior()
        {
            if (BoundAnimalData == null || EntityManager == null || Bubble == null) return;
            if (EntityManager.EntityMovementSystem != null && EntityManager.EntityMovementSystem.HasPath(BoundAnimalData.EntityId)) return;

            switch (CurrentState)
            {
                case AnimalState.Hunting:
                    HuntPrey();
                    break;
                case AnimalState.Foraging:
                    ForageForFood();
                    break;
                case AnimalState.Fleeing:
                    FleeFromThreat();
                    break;
                case AnimalState.Socializing:
                    MoveTowardSameSpecies();
                    break;
                case AnimalState.Docile:
                    if (random.Value01() < 0.05f)
                        BoundAnimalData.PendingMovement = EntityMovementType.Wander;
                    break;
            }
        }

        private void HuntPrey()
        {
            if (EntityManager == null || Bubble == null) return;

            var prey = EntityManager.FindNearestHerbivore(
                BoundAnimalData.GridX, BoundAnimalData.GridY, BoundAnimalData.GridZ);
            if (prey != null)
            {
                int dist = Mathf.Abs(prey.GridX - BoundAnimalData.GridX)
                         + Mathf.Abs(prey.GridZ - BoundAnimalData.GridZ);
                if (dist <= 1)
                {
                    prey.Health -= 30f;
                    HungerLevel = Mathf.Min(MaxHunger, HungerLevel + 40f);
                    if (prey.Health <= 0)
                    {
                        prey.IsDead = true;
                        EntityManager.Deregister(prey.EntityId);
                    }
                }
                else
                {
                    var path = Pathfinding3D.FindPath(Bubble,
                        BoundAnimalData.GridX, BoundAnimalData.GridY, BoundAnimalData.GridZ,
                        prey.GridX, prey.GridY, prey.GridZ);
                    if (path != null)
                        EntityManager.EntityMovementSystem?.SetPath(BoundAnimalData.EntityId, path);
                }
            }
        }

        private void ForageForFood()
        {
            if (Bubble == null) return;

            int bestDist = int.MaxValue;
            int bx = BoundAnimalData.GridX, bz = BoundAnimalData.GridZ;

            string[] foodItems = Diet == AnimalDiet.Carnivore
                ? new[] { "bush", "plant" }
                : new[] { "bush", "plant", "tree", "grass" };

            for (int dz = -6; dz <= 6; dz++)
            {
                for (int dx = -6; dx <= 6; dx++)
                {
                    int nx = BoundAnimalData.GridX + dx;
                    int nz = BoundAnimalData.GridZ + dz;
                    var cell = Bubble.Get(nx, 0, nz);
                    if (cell == null) continue;

                    foreach (var foodId in foodItems)
                    {
                        if (cell.ItemId == foodId)
                        {
                            int dist = Mathf.Abs(dx) + Mathf.Abs(dz);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bx = nx;
                                bz = nz;
                            }
                        }
                    }
                }
            }

            if (bestDist <= 1)
            {
                HungerLevel = Mathf.Min(MaxHunger, HungerLevel + 30f);
                CurrentState = AnimalState.Docile;
            }
            else if (bestDist < int.MaxValue)
            {
                var path = Pathfinding3D.FindPath(Bubble,
                    BoundAnimalData.GridX, BoundAnimalData.GridY, BoundAnimalData.GridZ,
                    bx, BoundAnimalData.GridY, bz);
                if (path != null)
                    EntityManager.EntityMovementSystem?.SetPath(BoundAnimalData.EntityId, path);
            }
            else
            {
                BoundAnimalData.PendingMovement = EntityMovementType.Wander;
            }
        }

        private void FleeFromThreat()
        {
            if (BoundAnimalData == null) return;
            BoundAnimalData.PendingMovement = EntityMovementType.Wander;
            IsAttacked = false;
        }

        private void MoveTowardSameSpecies()
        {
            if (EntityManager == null || Bubble == null) return;

            var animals = EntityManager.GetAllAnimals();
            AnimalData nearest = null;
            int bestDist = int.MaxValue;
            foreach (var a in animals)
            {
                if (a.EntityId == BoundAnimalData.EntityId) continue;
                if (a.Species != BoundAnimalData.Species) continue;
                int dist = Mathf.Abs(a.GridX - BoundAnimalData.GridX)
                         + Mathf.Abs(a.GridZ - BoundAnimalData.GridZ);
                if (dist < bestDist && dist > 2)
                {
                    bestDist = dist;
                    nearest = a;
                }
            }

            if (nearest != null)
            {
                var path = Pathfinding3D.FindPath(Bubble,
                    BoundAnimalData.GridX, BoundAnimalData.GridY, BoundAnimalData.GridZ,
                    nearest.GridX, nearest.GridY, nearest.GridZ);
                if (path != null && path.Count > 0)
                {
                    var trimmed = path.GetRange(0, Mathf.Min(path.Count, 4));
                    EntityManager.EntityMovementSystem?.SetPath(BoundAnimalData.EntityId, trimmed);
                }
            }
        }
    }
}
