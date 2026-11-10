using System;

namespace BubbleWorld
{
    public enum EntityType
    {
        NPC,
        Animal,
        Soulform
    }

    public enum EntityFacing
    {
        North,
        South,
        East,
        West
    }

    public enum EntityMovementType
    {
        None,
        Wander,
        MoveTo,
        Flee
    }

    [Serializable]
    public class EntityData
    {
        public string EntityId;
        public EntityType EntityType;
        public int GridX;
        public int GridY;
        public int GridZ;
        public EntityFacing Facing = EntityFacing.North;
        public float Health = 100f;
        public float Hunger = 50f;
        public EntityMovementType PendingMovement;
        public int TargetX;
        public int TargetY;
        public int TargetZ;
        public string TargetEntityId;
        public bool IsDead;

        public EntityData(string id, EntityType type, int x, int y, int z)
        {
            EntityId = id;
            EntityType = type;
            GridX = x;
            GridY = y;
            GridZ = z;
        }
    }

    [Serializable]
    public sealed class NPCData : EntityData
    {
        public NPCBaselineState BaselineState;
        public NPCCurrentState CurrentState = NPCCurrentState.HasFoodShelter;
        public NPCResponseAction CurrentAction = NPCResponseAction.None;
        public bool IsDisgruntled;
        public int DisgruntledDays;
        public int ConsecutiveResourceShortageDays;
        public bool HasAdequateResources = true;
        public bool HasShelter;
        public int ConsecutiveFoodDays;
        public int ConsecutiveWaterDays;
        public int ConsecutiveShelterDays;

        public NPCData(string id, int x, int y, int z)
            : base(id, EntityType.NPC, x, y, z) { }
    }

    [Serializable]
    public sealed class AnimalData : EntityData
    {
        public AnimalSpecies Species;
        public AnimalDiet Diet;
        public AnimalState CurrentState = AnimalState.Docile;
        public float HungerLevel = 80f;
        public float MaxHunger = 100f;
        public float HungerRate = 2f;
        public bool IsAttacked;
        public float SocialCooldown;
        public string CurrentFoodTarget;

        public AnimalData(string id, AnimalSpecies species, AnimalDiet diet, int x, int y, int z)
            : base(id, EntityType.Animal, x, y, z)
        {
            Species = species;
            Diet = diet;
            switch (species)
            {
                case AnimalSpecies.Wolf:
                    MaxHunger = 100f; HungerRate = 3f; break;
                case AnimalSpecies.Sheep:
                    MaxHunger = 80f; HungerRate = 2.5f; break;
                case AnimalSpecies.Bear:
                    MaxHunger = 150f; HungerRate = 1.5f; break;
                default:
                    MaxHunger = 100f; HungerRate = 2f; break;
            }
            HungerLevel = MaxHunger * 0.7f;
        }

        public bool IsHungry => (HungerLevel / MaxHunger) <= 0.30f;
    }

    [Serializable]
    public sealed class SoulformEntityData : EntityData
    {
        public string CurrentSkillAttempt;
        public string CurrentActivity;

        public SoulformEntityData(string id, int x, int y, int z)
            : base(id, EntityType.Soulform, x, y, z) { }
    }
}
