using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class EntityManager
    {
        private readonly Dictionary<string, EntityData> entities = new Dictionary<string, EntityData>();
        private readonly Dictionary<(int, int, int), List<EntityData>> spatialIndex = new Dictionary<(int, int, int), List<EntityData>>();
        private readonly Dictionary<string, NPCAI> npcAIs = new Dictionary<string, NPCAI>();
        private readonly Dictionary<string, AnimalAI> animalAIs = new Dictionary<string, AnimalAI>();
        private SoulformEntityData soulformEntity;

        public event Action<EntityData> OnEntitySpawned;
        public event Action<string> OnEntityRemoved;
        public event Action<EntityData> OnEntityMoved;
        public event Action<EntityData> OnEntityStateChanged;

        public IReadOnlyDictionary<string, EntityData> Entities => entities;
        public SoulformEntityData SoulformEntity => soulformEntity;

        public void Register(EntityData entity)
        {
            if (entity == null || string.IsNullOrEmpty(entity.EntityId)) return;
            entities[entity.EntityId] = entity;
            AddToSpatial(entity.GridX, entity.GridY, entity.GridZ, entity);

            if (entity is SoulformEntityData sf)
                soulformEntity = sf;

            OnEntitySpawned?.Invoke(entity);
        }

        public void Deregister(string id)
        {
            if (!entities.TryGetValue(id, out var entity)) return;
            RemoveFromSpatial(entity.GridX, entity.GridY, entity.GridZ, entity);
            entities.Remove(id);
            npcAIs.Remove(id);
            animalAIs.Remove(id);
            OnEntityRemoved?.Invoke(id);
        }

        public EntityData GetEntity(string id)
        {
            entities.TryGetValue(id, out var e);
            return e;
        }

        public NPCData GetNPC(string id)
        {
            return GetEntity(id) as NPCData;
        }

        public AnimalData GetAnimal(string id)
        {
            return GetEntity(id) as AnimalData;
        }

        public List<EntityData> GetEntitiesAt(int x, int y, int z)
        {
            var key = (x, y, z);
            return spatialIndex.TryGetValue(key, out var list)
                ? new List<EntityData>(list)
                : new List<EntityData>();
        }

        public List<EntityData> GetEntitiesInRange(int cx, int cy, int cz, int radius)
        {
            var result = new List<EntityData>();
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dz) > radius) continue;
                    var atPos = GetEntitiesAt(cx + dx, cy, cz + dz);
                    result.AddRange(atPos);
                }
            }
            return result;
        }

        public List<NPCData> GetAllNPCs()
        {
            var result = new List<NPCData>();
            foreach (var kv in entities)
            {
                if (kv.Value is NPCData npc)
                    result.Add(npc);
            }
            return result;
        }

        public List<AnimalData> GetAllAnimals()
        {
            var result = new List<AnimalData>();
            foreach (var kv in entities)
            {
                if (kv.Value is AnimalData animal)
                    result.Add(animal);
            }
            return result;
        }

        public IReadOnlyList<EntityData> GetAllEntities()
        {
            var result = new List<EntityData>(entities.Values);
            return result.AsReadOnly();
        }

        public bool MoveEntity(string id, int newX, int newY, int newZ)
        {
            if (!entities.TryGetValue(id, out var entity)) return false;

            RemoveFromSpatial(entity.GridX, entity.GridY, entity.GridZ, entity);
            entity.GridX = newX;
            entity.GridY = newY;
            entity.GridZ = newZ;
            AddToSpatial(newX, newY, newZ, entity);

            OnEntityMoved?.Invoke(entity);
            return true;
        }

        public void RegisterNPCAI(string npcId, NPCAI ai)
        {
            npcAIs[npcId] = ai;
        }

        public NPCAI GetNPCAI(string npcId)
        {
            npcAIs.TryGetValue(npcId, out var ai);
            return ai;
        }

        public void RegisterAnimalAI(string animalId, AnimalAI ai)
        {
            animalAIs[animalId] = ai;
        }

        public AnimalAI GetAnimalAI(string animalId)
        {
            animalAIs.TryGetValue(animalId, out var ai);
            return ai;
        }

        public IReadOnlyDictionary<string, NPCAI> AllNPCAIs => npcAIs;
        public IReadOnlyDictionary<string, AnimalAI> AllAnimalAIs => animalAIs;

        public EntityMovementSystem EntityMovementSystem { get; set; }

        public void NotifyStateChanged(EntityData entity)
        {
            OnEntityStateChanged?.Invoke(entity);
        }

        public AnimalData FindNearestHerbivore(int x, int y, int z, int maxRange = 20)
        {
            AnimalData nearest = null;
            int nearestDist = int.MaxValue;
            foreach (var kv in entities)
            {
                if (kv.Value is AnimalData animal && animal.Diet == AnimalDiet.Herbivore && !animal.IsDead)
                {
                    int dist = Math.Abs(animal.GridX - x) + Math.Abs(animal.GridZ - z);
                    if (dist < nearestDist && dist <= maxRange)
                    {
                        nearestDist = dist;
                        nearest = animal;
                    }
                }
            }
            return nearest;
        }

        public AnimalData FindNearestFoodAnimal(int x, int y, int z, int maxRange = 20)
        {
            AnimalData nearest = null;
            int nearestDist = int.MaxValue;
            foreach (var kv in entities)
            {
                if (kv.Value is AnimalData animal && !animal.IsDead)
                {
                    if (animal.Diet == AnimalDiet.Herbivore || animal.Diet == AnimalDiet.Omnivore)
                    {
                        int dist = Math.Abs(animal.GridX - x) + Math.Abs(animal.GridZ - z);
                        if (dist < nearestDist && dist <= maxRange)
                        {
                            nearestDist = dist;
                            nearest = animal;
                        }
                    }
                }
            }
            return nearest;
        }

        private void AddToSpatial(int x, int y, int z, EntityData entity)
        {
            var key = (x, y, z);
            if (!spatialIndex.TryGetValue(key, out var list))
            {
                list = new List<EntityData>();
                spatialIndex[key] = list;
            }
            if (!list.Contains(entity))
                list.Add(entity);
        }

        private void RemoveFromSpatial(int x, int y, int z, EntityData entity)
        {
            var key = (x, y, z);
            if (spatialIndex.TryGetValue(key, out var list))
            {
                list.Remove(entity);
                if (list.Count == 0)
                    spatialIndex.Remove(key);
            }
        }
    }
}
