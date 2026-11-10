using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "MemoryConfig", menuName = "BubbleWorld/Config/Memory Config")]
    public sealed class MemoryConfig : ScriptableObject
    {
        [Header("Context Window")]
        [Tooltip("Maximum tokens in the AI context window")]
        public int ContextWindowTokens = 128000;

        [Header("Chapters")]
        [Tooltip("Maximum number of chapters in long-term memory")]
        public int MaxChapters = 50;

        [Tooltip("Maximum memory entries per chapter")]
        public int MaxEntriesPerChapter = 500;

        [Tooltip("Entry count that triggers auto-closing a chapter")]
        public int AutoCloseChapterEntryCount = 400;
    }
}
