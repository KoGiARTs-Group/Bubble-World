using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "GameTimeConfig", menuName = "BubbleWorld/Config/Game Time Config")]
    public sealed class GameTimeConfig : ScriptableObject
    {
        [Header("Day & Time")]
        [Tooltip("Game seconds per full day")]
        public float DayLength = 86400f;

        [Tooltip("Game hours between weather rolls (fraction of day)")]
        public float WeatherRollIntervalHours = 0.5f;

        [Tooltip("Available time multiplier options")]
        public float[] TimeMultiplierOptions = new[] { 1f, 2f, 4f };

        [Header("Offline")]
        [Tooltip("Max real-world hours of offline progression")]
        public float OfflineCapHours = 24f;

        [Tooltip("Game hours per chunk during offline catch-up")]
        public int ChunkIntervalHours = 1;

        [Header("Tutorial")]
        [Tooltip("In-game days before offline catch-up is allowed")]
        public int TutorialProtectionDays = 7;
    }
}
