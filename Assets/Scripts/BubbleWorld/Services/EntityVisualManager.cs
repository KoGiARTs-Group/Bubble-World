using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class EntityVisualManager
    {
        private readonly Dictionary<string, GameObject> visuals = new Dictionary<string, GameObject>();
        private readonly EntityManager entityManager;
        private readonly Transform parent;
        private readonly Dictionary<EntityType, GameObject> prefabs = new Dictionary<EntityType, GameObject>();

        public EntityVisualManager(EntityManager entityManager, Transform parent = null)
        {
            this.entityManager = entityManager;
            this.parent = parent;

            entityManager.OnEntitySpawned += OnEntitySpawned;
            entityManager.OnEntityRemoved += OnEntityRemoved;
            entityManager.OnEntityMoved += OnEntityMoved;
        }

        public void SetPrefab(EntityType type, GameObject prefab)
        {
            prefabs[type] = prefab;
        }

        public void SyncAllVisuals()
        {
            foreach (var entity in entityManager.GetAllEntities())
            {
                EnsureVisual(entity);
                UpdatePosition(entity);
            }
        }

        private void OnEntitySpawned(EntityData entity)
        {
            EnsureVisual(entity);
            UpdatePosition(entity);
        }

        private void OnEntityRemoved(string id)
        {
            if (visuals.TryGetValue(id, out var go))
            {
                Object.Destroy(go);
                visuals.Remove(id);
            }
        }

        private void OnEntityMoved(EntityData entity)
        {
            UpdatePosition(entity);
        }

        private void EnsureVisual(EntityData entity)
        {
            if (visuals.ContainsKey(entity.EntityId)) return;
            if (entity.EntityType == EntityType.Soulform) return;

            GameObject go;

            if (prefabs.TryGetValue(entity.EntityType, out var prefab) && prefab != null)
            {
                go = Object.Instantiate(prefab, parent);
            }
            else
            {
                go = CreatePlaceholderVisual(entity);
            }

            go.name = entity.EntityType + "_" + entity.EntityId;
            visuals[entity.EntityId] = go;
        }

        private GameObject CreatePlaceholderVisual(EntityData entity)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(parent);

            float height = entity.EntityType == EntityType.Animal ? 0.5f : 1f;
            go.transform.localScale = new Vector3(0.5f, height, 0.5f);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = GetColorForType(entity);
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            return go;
        }

        private void UpdatePosition(EntityData entity)
        {
            if (!visuals.TryGetValue(entity.EntityId, out var go)) return;
            go.transform.position = new Vector3(entity.GridX, entity.GridY + 0.5f, entity.GridZ);
        }

        private static Color GetColorForType(EntityData entity)
        {
            switch (entity.EntityType)
            {
                case EntityType.NPC: return new Color(0.2f, 0.6f, 1f);
                case EntityType.Animal:
                    var animal = entity as AnimalData;
                    if (animal != null)
                    {
                        switch (animal.Species)
                        {
                            case AnimalSpecies.Wolf: return new Color(0.5f, 0.5f, 0.5f);
                            case AnimalSpecies.Sheep: return new Color(0.9f, 0.9f, 0.85f);
                            case AnimalSpecies.Bear: return new Color(0.4f, 0.25f, 0.1f);
                        }
                    }
                    return new Color(0.6f, 0.4f, 0.2f);
                default: return Color.white;
            }
        }

        public void DestroyAll()
        {
            foreach (var kv in visuals)
            {
                if (kv.Value != null)
                    Object.Destroy(kv.Value);
            }
            visuals.Clear();
        }

        public void UpdateEntityVisual(EntityData entity)
        {
            if (entity == null) return;
            if (visuals.TryGetValue(entity.EntityId, out var go))
            {
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.material.color = GetColorForType(entity);
            }
        }

        public bool HasVisual(string entityId) => visuals.ContainsKey(entityId);
        public GameObject GetVisual(string entityId)
        {
            visuals.TryGetValue(entityId, out var go);
            return go;
        }
    }
}
