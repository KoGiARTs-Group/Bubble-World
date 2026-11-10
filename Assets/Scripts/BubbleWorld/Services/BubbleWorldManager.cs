using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public enum BubbleConnectionDirection
    {
        North,
        East,
        South,
        West
    }

    public sealed class BubbleConnection
    {
        public string FromBubbleId;
        public string ToBubbleId;
        public BubbleConnectionDirection Direction;
        public bool IsOpen;
    }

    public sealed class BubbleInstance
    {
        public string BubbleId;
        public BubbleModel Model;
        public WeatherManager Weather;
        public SourceLogicSystem SourceLogic;
        public EntityManager EntityManager;
        public EntityVisualManager VisualManager;
        public EntityMovementSystem MovementSystem;
        public StormDamageSystem StormDamage;
        public GameTimeManager GameTime;
        public SpawnManager SpawnManager;
        public int SeasonOffset;
        public bool IsUnlocked = true;
        public StarterEnvironment Environment;

        public SeasonType CurrentSeason
        {
            get
            {
                if (GameTime != null) return GameTime.Season;
                return (SeasonType)((UnityEngine.Time.frameCount / 30 + SeasonOffset) % 4);
            }
        }
    }

    public sealed class BubbleWorldManager
    {
        private readonly Dictionary<string, BubbleInstance> bubbles = new Dictionary<string, BubbleInstance>();
        private readonly List<BubbleConnection> connections = new List<BubbleConnection>();
        private string activeBubbleId;

        public IReadOnlyDictionary<string, BubbleInstance> Bubbles => bubbles;
        public IReadOnlyList<BubbleConnection> Connections => connections.AsReadOnly();
        public BubbleInstance ActiveBubble => activeBubbleId != null && bubbles.TryGetValue(activeBubbleId, out var b) ? b : null;
        public string ActiveBubbleId => activeBubbleId;

        public event Action<string> OnActiveBubbleChanged;
        public event Action<string> OnBubbleAdded;
        public event Action<string, string> OnEntityMigrated;

        public BubbleInstance AddBubble(string id, BubbleModel model, StarterEnvironment env,
            int seasonOffset = 0)
        {
            var instance = new BubbleInstance
            {
                BubbleId = id,
                Model = model,
                Environment = env,
                SeasonOffset = seasonOffset,
                EntityManager = new EntityManager()
            };
            instance.MovementSystem = new EntityMovementSystem(instance.EntityManager, model);
            instance.EntityManager.EntityMovementSystem = instance.MovementSystem;
            bubbles[id] = instance;
            OnBubbleAdded?.Invoke(id);
            return instance;
        }

        public void RemoveBubble(string id)
        {
            if (!bubbles.TryGetValue(id, out var instance)) return;
            instance.VisualManager?.DestroyAll();
            bubbles.Remove(id);
            if (activeBubbleId == id)
                activeBubbleId = null;
        }

        public BubbleInstance GetBubble(string id)
        {
            bubbles.TryGetValue(id, out var b);
            return b;
        }

        public void SetActiveBubble(string id)
        {
            if (!bubbles.ContainsKey(id)) return;
            if (activeBubbleId == id) return;
            activeBubbleId = id;
            OnActiveBubbleChanged?.Invoke(id);
        }

        public void AddConnection(string from, string to, BubbleConnectionDirection direction, bool open = true)
        {
            var connection = new BubbleConnection
            {
                FromBubbleId = from,
                ToBubbleId = to,
                Direction = direction,
                IsOpen = open
            };
            connections.Add(connection);

            var reverse = new BubbleConnection
            {
                FromBubbleId = to,
                ToBubbleId = from,
                Direction = ReverseDirection(direction),
                IsOpen = open
            };
            connections.Add(reverse);
        }

        public List<BubbleConnection> GetConnectionsFrom(string bubbleId)
        {
            var result = new List<BubbleConnection>();
            foreach (var c in connections)
            {
                if (c.FromBubbleId == bubbleId && c.IsOpen)
                    result.Add(c);
            }
            return result;
        }

        public List<string> GetConnectedBubbleIds(string bubbleId)
        {
            var result = new List<string>();
            foreach (var c in connections)
            {
                if (c.FromBubbleId == bubbleId && c.IsOpen)
                    result.Add(c.ToBubbleId);
            }
            return result;
        }

        public bool MigrateEntity(string entityId, string fromBubbleId, string toBubbleId)
        {
            if (!bubbles.TryGetValue(fromBubbleId, out var from) ||
                !bubbles.TryGetValue(toBubbleId, out var to)) return false;

            var entity = from.EntityManager.GetEntity(entityId);
            if (entity == null) return false;

            from.EntityManager.Deregister(entityId);
            to.EntityManager.Register(entity);

            OnEntityMigrated?.Invoke(entityId, toBubbleId);
            return true;
        }

        public void UpdateAll(float deltaSeconds, WorldTimeContext context)
        {
            foreach (var kv in bubbles)
            {
                var instance = kv.Value;
                if (instance.GameTime != null)
                {
                    instance.MovementSystem?.OnGameTick(deltaSeconds, context);
                }
            }
        }

        private static BubbleConnectionDirection ReverseDirection(BubbleConnectionDirection dir)
        {
            switch (dir)
            {
                case BubbleConnectionDirection.North: return BubbleConnectionDirection.South;
                case BubbleConnectionDirection.South: return BubbleConnectionDirection.North;
                case BubbleConnectionDirection.East: return BubbleConnectionDirection.West;
                case BubbleConnectionDirection.West: return BubbleConnectionDirection.East;
                default: return dir;
            }
        }
    }
}
