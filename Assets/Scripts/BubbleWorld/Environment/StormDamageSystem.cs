using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class StormDamageSystem : IWorldTimeReceiver
    {
        private readonly BubbleModel bubble;
        private readonly EntityManager entityManager;
        private readonly EventLog eventLog;
        private readonly DeterministicRandom random;

        public StormDamageSystem(BubbleModel bubble, EntityManager entityManager,
            EventLog eventLog, int seed)
        {
            this.bubble = bubble;
            this.entityManager = entityManager;
            this.eventLog = eventLog;
            random = new DeterministicRandom(seed);
        }

        public void OnGameTick(float deltaSeconds, WorldTimeContext context)
        {
        }

        public void OnFastForward(float totalMissedSeconds, WorldTimeContext context) { }

        public void ApplyStormDamage(WeatherType stormLevel)
        {
            if (stormLevel != WeatherType.Storm1 && stormLevel != WeatherType.Storm2
                && stormLevel != WeatherType.Storm3) return;

            int intensity = stormLevel == WeatherType.Storm1 ? 1
                : stormLevel == WeatherType.Storm2 ? 2 : 3;

            ApplyTerrainDamage(intensity);
            ApplyEntityDamage(intensity);
        }

        private void ApplyTerrainDamage(int intensity)
        {
            if (bubble == null) return;

            var damagedCells = new List<(int x, int y, int z)>();

            foreach (var kv in bubble.Cells)
            {
                var cell = kv.Value;
                if (cell == null) continue;

                float damageChance = intensity * 0.02f;
                if (random.Value01() > damageChance) continue;

                if (cell.IsObject && (cell.ItemId == "plant" || cell.ItemId == "bush"
                    || cell.ItemId == "flowers"))
                {
                    if (intensity >= 2)
                        damagedCells.Add(kv.Key);
                }
                else if (cell.ItemId == "tree" && intensity >= 3)
                {
                    damagedCells.Add(kv.Key);
                }

                cell.State.ErosionDamage += intensity * 0.5f;
            }

            foreach (var key in damagedCells)
            {
                var cell = bubble.Get(key.x, key.y, key.z);
                if (cell != null)
                {
                    string revertId = "barren_dry";
                    var groundId = cell.GroundItemId;
                    if (!string.IsNullOrEmpty(groundId))
                        revertId = groundId;

                    var revertDef = BubblePlacementCatalog.Find(revertId);
                    if (revertDef != null)
                    {
                        bubble.Remove(key.x, key.y, key.z);
                        bubble.Place(key.x, key.y, key.z, revertDef);
                        eventLog?.Record(GameEventType.RainStarted,
                            "Storm destroyed " + cell.ItemId + " at (" + key.x + "," + key.z + ")");
                    }
                }
            }
        }

        private void ApplyEntityDamage(int intensity)
        {
            if (entityManager == null) return;

            foreach (var entity in entityManager.GetAllEntities())
            {
                if (entity.IsDead) continue;

                float damageChance = intensity * 0.1f;
                if (random.Value01() > damageChance) continue;

                entity.Health -= intensity * 10f;

                if (entity is AnimalData animal)
                {
                    animal.IsAttacked = true;
                    animal.CurrentState = AnimalState.Fleeing;
                    eventLog?.Record(GameEventType.AnimalSpawned,
                        animal.Species + " was damaged by the storm");
                }
                else if (entity is NPCData npc)
                {
                    npc.CurrentState = NPCCurrentState.NeedsShelter;
                    eventLog?.Record(GameEventType.NpcSpawned,
                        "NPC " + npc.EntityId + " was caught in the storm");
                }

                if (entity.Health <= 0)
                {
                    entity.IsDead = true;
                    entityManager.Deregister(entity.EntityId);
                }
            }
        }
    }
}
