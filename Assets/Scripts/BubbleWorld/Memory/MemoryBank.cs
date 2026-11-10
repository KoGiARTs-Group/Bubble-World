using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class MemoryBank : IWorldSaveData
    {
        private readonly MemoryConfig config;
        private readonly List<MemoryChapter> chapters = new List<MemoryChapter>();
        private MemoryChapter currentChapter;

        public IReadOnlyList<MemoryChapter> Chapters => chapters;
        public MemoryChapter CurrentChapter => currentChapter;
        public string SaveKey => "MemoryBank";

        public event Action<int> OnChapterClosed;

        public MemoryBank(MemoryConfig config)
        {
            this.config = config;
            OpenNewChapter();
        }

        private void OpenNewChapter()
        {
            currentChapter = new MemoryChapter
            {
                Index = chapters.Count,
                Summary = $"Chapter {chapters.Count + 1}",
                StartTime = DateTimeOffset.UtcNow
            };
            chapters.Add(currentChapter);
        }

        public void AddEvent(string description, MemoryCategory category, List<string> keywords = null)
        {
            var entry = new MemoryEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Description = description,
                Category = category,
                Keywords = keywords?.ToArray() ?? Array.Empty<string>()
            };

            currentChapter.Entries.Add(entry);

            if (currentChapter.Entries.Count >= config.AutoCloseChapterEntryCount)
                CloseCurrentChapter();
        }

        public void AddSkillLearned(string skillName)
        {
            if (!currentChapter.SkillsLearned.Contains(skillName))
                currentChapter.SkillsLearned.Add(skillName);
            AddEvent($"Learned skill: {skillName}", MemoryCategory.Skill, new List<string> { "skill", skillName.ToLower() });
        }

        public void AddRelationship(string personName)
        {
            if (!currentChapter.ImportantPeople.Contains(personName))
                currentChapter.ImportantPeople.Add(personName);
        }

        public void AddPlace(string placeName)
        {
            if (!currentChapter.ImportantPlaces.Contains(placeName))
                currentChapter.ImportantPlaces.Add(placeName);
        }

        public void AddMajorEvent(string description)
        {
            currentChapter.MajorEvents.Add(description);
            AddEvent(description, MemoryCategory.WorldEvent);
        }

        public void CloseCurrentChapter()
        {
            currentChapter.EndTime = DateTimeOffset.UtcNow;
            currentChapter.Summary = BuildChapterSummary(currentChapter);
            int closedIndex = currentChapter.Index;
            OpenNewChapter();
            OnChapterClosed?.Invoke(closedIndex);
        }

        public MemoryChapter GetChapter(int index)
        {
            if (index >= 0 && index < chapters.Count)
                return chapters[index];
            return null;
        }

        public string GetChapterSummary(int index)
        {
            var chapter = GetChapter(index);
            return chapter?.Summary ?? "Chapter not found.";
        }

        public string BuildContextString(int maxTokens = 128000)
        {
            var sb = new StringBuilder();
            int estimatedTokens = 0;

            for (int i = Math.Max(0, chapters.Count - 5); i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                sb.AppendLine($"--- {chapter.Summary} ---");

                int start = Math.Max(0, chapter.Entries.Count - 20);
                for (int j = start; j < chapter.Entries.Count; j++)
                {
                    var entry = chapter.Entries[j];
                    string line = $"[{entry.Timestamp:HH:mm}] ({entry.Category}) {entry.Description}";
                    sb.AppendLine(line);

                    estimatedTokens += line.Length / 4;
                    if (estimatedTokens >= maxTokens)
                        return sb.ToString();
                }
            }

            return sb.ToString();
        }

        public string[] RetrieveMemoriesForContext(int chapterIndex, string query)
        {
            var chapter = GetChapter(chapterIndex);
            if (chapter == null) return Array.Empty<string>();

            string queryLower = query?.ToLowerInvariant() ?? "";
            return chapter.Entries
                .Where(e => string.IsNullOrEmpty(queryLower) ||
                            e.Description.ToLowerInvariant().Contains(queryLower) ||
                            (e.Keywords != null && e.Keywords.Any(k => k.Contains(queryLower))))
                .Select(e => e.Description)
                .Take(10)
                .ToArray();
        }

        private static string BuildChapterSummary(MemoryChapter chapter)
        {
            if (chapter.MajorEvents.Count > 0)
            {
                return $"Chapter {chapter.Index + 1}: {string.Join(", ", chapter.MajorEvents.Take(3))}";
            }
            if (chapter.SkillsLearned.Count > 0)
            {
                return $"Chapter {chapter.Index + 1}: Learned {string.Join(", ", chapter.SkillsLearned)}";
            }
            return $"Chapter {chapter.Index + 1}: {chapter.Entries.Count} memories";
        }

        public string Serialize()
        {
            var data = new MemoryBankData();
            foreach (var ch in chapters)
            {
                data.chapters.Add(new ChapterData
                {
                    index = ch.Index,
                    summary = ch.Summary,
                    startTimeTicks = ch.StartTime.Ticks,
                    endTimeTicks = ch.EndTime.Ticks,
                    entries = ch.Entries.Select(e => new EntryData
                    {
                        timestampTicks = e.Timestamp.Ticks,
                        description = e.Description,
                        category = (int)e.Category,
                        keywords = e.Keywords
                    }).ToList(),
                    importantPeople = ch.ImportantPeople,
                    importantPlaces = ch.ImportantPlaces,
                    skillsLearned = ch.SkillsLearned,
                    majorEvents = ch.MajorEvents
                });
            }
            return JsonUtility.ToJson(data);
        }

        public void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            var d = JsonUtility.FromJson<MemoryBankData>(data);
            if (d?.chapters == null) return;

            chapters.Clear();
            foreach (var cd in d.chapters)
            {
                var chapter = new MemoryChapter
                {
                    Index = cd.index,
                    Summary = cd.summary,
                    StartTime = new DateTimeOffset(cd.startTimeTicks, TimeSpan.Zero),
                    EndTime = new DateTimeOffset(cd.endTimeTicks, TimeSpan.Zero),
                    Entries = cd.entries?.Select(e => new MemoryEntry
                    {
                        Timestamp = new DateTimeOffset(e.timestampTicks, TimeSpan.Zero),
                        Description = e.description,
                        Category = (MemoryCategory)e.category,
                        Keywords = e.keywords
                    }).ToList() ?? new List<MemoryEntry>(),
                    ImportantPeople = cd.importantPeople ?? new List<string>(),
                    ImportantPlaces = cd.importantPlaces ?? new List<string>(),
                    SkillsLearned = cd.skillsLearned ?? new List<string>(),
                    MajorEvents = cd.majorEvents ?? new List<string>()
                };
                chapters.Add(chapter);
            }

            currentChapter = chapters.Count > 0 ? chapters[chapters.Count - 1] : null;
            if (currentChapter == null) OpenNewChapter();
        }

        [Serializable]
        private sealed class MemoryBankData
        {
            public List<ChapterData> chapters = new List<ChapterData>();
        }

        [Serializable]
        private sealed class ChapterData
        {
            public int index;
            public string summary;
            public long startTimeTicks;
            public long endTimeTicks;
            public List<EntryData> entries;
            public List<string> importantPeople;
            public List<string> importantPlaces;
            public List<string> skillsLearned;
            public List<string> majorEvents;
        }

        [Serializable]
        private sealed class EntryData
        {
            public long timestampTicks;
            public string description;
            public int category;
            public string[] keywords;
        }
    }
}
