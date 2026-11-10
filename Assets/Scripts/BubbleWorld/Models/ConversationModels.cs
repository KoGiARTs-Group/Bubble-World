using System;
using System.Collections.Generic;

namespace BubbleWorld
{
    /// <summary>
    /// A full conversation archive entry. Holds the title shown in the archive,
    /// its message history, and archive state (pin/unread).
    /// </summary>
    [Serializable]
    public sealed class Conversation
    {
        public string Id;
        public string Title;
        public DateTime LastUpdated;
        public List<ChatMessage> Messages;
        public bool IsPinned;
        public bool HasUnread;

        public Conversation(string id, string title)
        {
            Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
            Title = string.IsNullOrEmpty(title) ? "Untitled" : title;
            LastUpdated = DateTime.Now;
            Messages = new List<ChatMessage>();
        }

        public void AddMessage(ChatMessage message)
        {
            if (message == null) return;
            Messages.Add(message);
            LastUpdated = DateTime.Now;
        }
    }

    /// <summary>
    /// An entry in the conversation archive sidebar. Represents a conversation
    /// the player can select to load its message history.
    /// </summary>
    [Serializable]
    public sealed class Topic
    {
        public string Id;
        public string Name;
        public string Preview;
        public DateTime Timestamp;
        public bool Unread;
        public bool Important;

        public Topic(string id, string name, string preview)
        {
            Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
            Name = string.IsNullOrEmpty(name) ? "Untitled" : name;
            Preview = preview ?? string.Empty;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Immutable snapshot of the soulform's current status, consumed by the
    /// top status bar widgets. Populated from world state rather than being
    /// hardcoded in the UI.
    /// </summary>
    [Serializable]
    public sealed class SoulformStatus
    {
        public string Name;
        public string Type;
        public string Mood;
        public string Location;
        public string Season;
        public string TimeOfDay;
        public int CurrentAP;
        public int MaxAP;
        public int RelationshipLevel;

        public SoulformStatus()
        {
            Name = string.Empty;
            Type = string.Empty;
            Mood = string.Empty;
            Location = string.Empty;
            Season = string.Empty;
            TimeOfDay = string.Empty;
            CurrentAP = 0;
            MaxAP = 1;
            RelationshipLevel = 0;
        }

        public SoulformStatus Clone()
        {
            return new SoulformStatus
            {
                Name = Name,
                Type = Type,
                Mood = Mood,
                Location = Location,
                Season = Season,
                TimeOfDay = TimeOfDay,
                CurrentAP = CurrentAP,
                MaxAP = MaxAP,
                RelationshipLevel = RelationshipLevel
            };
        }
    }
}
