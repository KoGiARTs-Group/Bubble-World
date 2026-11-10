using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class EntityMovementSystem : IWorldTimeReceiver
    {
        private readonly EntityManager entityManager;
        private readonly BubbleModel bubble;
        private readonly Dictionary<string, List<(int x, int y, int z)>> entityPaths = new Dictionary<string, List<(int x, int y, int z)>>();
        private readonly Dictionary<string, int> pathIndices = new Dictionary<string, int>();
        private readonly Dictionary<string, float> moveTimers = new Dictionary<string, float>();
        private const float MoveInterval = 0.5f;

        public event System.Action<EntityData, int, int, int> OnEntityArrived;

        public EntityMovementSystem(EntityManager entityManager, BubbleModel bubble)
        {
            this.entityManager = entityManager;
            this.bubble = bubble;
        }

        public void OnGameTick(float deltaSeconds, WorldTimeContext context)
        {
            ProcessMovement(deltaSeconds);
            ProcessPendingMovements();
        }

        public void OnFastForward(float totalMissedSeconds, WorldTimeContext context) { }

        public void SetPath(string entityId, List<(int x, int y, int z)> path)
        {
            if (path == null || path.Count == 0) return;
            entityPaths[entityId] = path;
            pathIndices[entityId] = 0;
            moveTimers[entityId] = 0f;
        }

        public void ClearPath(string entityId)
        {
            entityPaths.Remove(entityId);
            pathIndices.Remove(entityId);
            moveTimers.Remove(entityId);
        }

        public bool HasPath(string entityId) => entityPaths.ContainsKey(entityId);

        private void ProcessMovement(float delta)
        {
            var toRemove = new List<string>();

            foreach (var kv in entityPaths)
            {
                string id = kv.Key;
                var path = kv.Value;
                int idx = pathIndices.GetValueOrDefault(id, 0);

                if (idx >= path.Count)
                {
                    toRemove.Add(id);
                    continue;
                }

                if (!moveTimers.ContainsKey(id))
                    moveTimers[id] = 0f;

                moveTimers[id] += delta;
                if (moveTimers[id] >= MoveInterval)
                {
                    moveTimers[id] -= MoveInterval;

                    var (nx, ny, nz) = path[idx];
                    if (IsWalkable(nx, ny, nz))
                    {
                        entityManager.MoveEntity(id, nx, ny, nz);
                        pathIndices[id] = idx + 1;
                    }
                    else
                    {
                        toRemove.Add(id);
                    }
                }
            }

            foreach (var id in toRemove)
            {
                var entity = entityManager.GetEntity(id);
                if (entity != null && pathIndices.TryGetValue(id, out int finalIdx))
                {
                    int pi = Mathf.Min(finalIdx, entityPaths[id].Count - 1);
                    var dest = entityPaths[id][pi];
                    OnEntityArrived?.Invoke(entity, dest.x, dest.y, dest.z);
                }
                entityPaths.Remove(id);
                pathIndices.Remove(id);
                moveTimers.Remove(id);
            }
        }

        private void ProcessPendingMovements()
        {
            foreach (var entity in entityManager.GetAllEntities())
            {
                if (entity.PendingMovement == EntityMovementType.None) continue;
                if (HasPath(entity.EntityId)) continue;

                switch (entity.PendingMovement)
                {
                    case EntityMovementType.Wander:
                        StartWander(entity);
                        break;
                    case EntityMovementType.MoveTo:
                        StartMoveTo(entity, entity.TargetX, entity.TargetY, entity.TargetZ);
                        break;
                    case EntityMovementType.Flee:
                        StartFlee(entity, entity.TargetX, entity.TargetZ);
                        break;
                }
                entity.PendingMovement = EntityMovementType.None;
            }
        }

        private void StartWander(EntityData entity)
        {
            var dirs = new (int dx, int dz)[]
            {
                (1, 0), (-1, 0), (0, 1), (0, -1)
            };
            var rng = new System.Random(entity.EntityId.GetHashCode());
            var shuffled = new List<(int dx, int dz)>(dirs);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            foreach (var (dx, dz) in shuffled)
            {
                int nx = entity.GridX + dx;
                int nz = entity.GridZ + dz;
                if (IsWalkable(nx, entity.GridY, nz))
                {
                    SetPath(entity.EntityId, new List<(int x, int y, int z)> { (nx, entity.GridY, nz) });
                    return;
                }
            }
        }

        private void StartMoveTo(EntityData entity, int tx, int ty, int tz)
        {
            if (bubble == null) return;
            var path = Pathfinding3D.FindPath(bubble,
                entity.GridX, entity.GridY, entity.GridZ,
                tx, ty, tz);
            if (path != null && path.Count > 0)
                SetPath(entity.EntityId, path);
        }

        private void StartFlee(EntityData entity, int threatX, int threatZ)
        {
            int dx = entity.GridX - threatX;
            int dz = entity.GridZ - threatZ;
            if (dx == 0 && dz == 0)
            {
                dx = UnityEngine.Random.Range(-1, 2);
                dz = dx == 0 ? 1 : 0;
            }

            int fleeX = entity.GridX + (dx > 0 ? 3 : dx < 0 ? -3 : UnityEngine.Random.Range(-3, 4));
            int fleeZ = entity.GridZ + (dz > 0 ? 3 : dz < 0 ? -3 : UnityEngine.Random.Range(-3, 4));
            fleeX = Mathf.Clamp(fleeX, 0, bubble?.Width ?? 60);
            fleeZ = Mathf.Clamp(fleeZ, 0, bubble?.Height ?? 60);

            StartMoveTo(entity, fleeX, entity.GridY, fleeZ);
        }

        private bool IsWalkable(int x, int y, int z)
        {
            if (bubble == null) return false;
            if (!bubble.IsInside(x, y, z)) return false;
            var cell = bubble.Get(x, y, z);
            if (cell == null) return true;
            var def = BubblePlacementCatalog.Find(cell.ItemId);
            return def == null || !def.IsObject;
        }
    }
}
