using System;
using System.Collections.Generic;

namespace BubbleWorld
{
    [Serializable]
    public sealed class MemoryChapter
    {
        public int Index;
        public string Summary;
        public DateTimeOffset StartTime;
        public DateTimeOffset EndTime;
        public List<MemoryEntry> Entries = new List<MemoryEntry>();
        public List<string> ImportantPeople = new List<string>();
        public List<string> ImportantPlaces = new List<string>();
        public List<string> SkillsLearned = new List<string>();
        public List<string> MajorEvents = new List<string>();
    }
}
