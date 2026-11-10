using System;
using System.Collections.Generic;

namespace BubbleWorld
{
    public sealed class GameEvent
    {
        public GameEventType Type;
        public string Description;
        public DateTime Timestamp;
        public string Source;

        public GameEvent(GameEventType type, string description, string source = "")
        {
            Type = type;
            Description = description;
            Timestamp = DateTime.UtcNow;
            Source = source;
        }
    }

    public sealed class EventLog
    {
        private readonly List<GameEvent> events = new List<GameEvent>();
        private const int MaxEvents = 500;

        public event Action<GameEvent> OnEventRecorded;

        public void Record(GameEventType type, string description, string source = "")
        {
            var evt = new GameEvent(type, description, source);
            events.Add(evt);
            if (events.Count > MaxEvents)
                events.RemoveAt(0);
            OnEventRecorded?.Invoke(evt);
        }

        public IReadOnlyList<GameEvent> GetAll()
        {
            return events.AsReadOnly();
        }

        public List<GameEvent> GetRecent(int count)
        {
            int take = Math.Min(count, events.Count);
            return events.GetRange(events.Count - take, take);
        }

        public void Clear()
        {
            events.Clear();
        }
    }
}
