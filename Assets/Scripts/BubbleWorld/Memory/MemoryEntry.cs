using System;

namespace BubbleWorld
{
    public enum MemoryCategory
    {
        Conversation,
        Discovery,
        Skill,
        Relationship,
        WorldEvent,
        Emotion
    }

    [Serializable]
    public sealed class MemoryEntry
    {
        public DateTimeOffset Timestamp;
        public string Description;
        public MemoryCategory Category;
        public string[] Keywords;
    }
}
