namespace BubbleWorld
{
    public sealed class SoulformBrainResponse
    {
        public string CurrentThought;
        public SoulformMood Mood;
        public string CurrentActivity;
        public int? TargetX;
        public int? TargetY;
        public int? TargetZ;
        public string Expression = "Neutral";
        public string Animation = "Idle";
        public string EmotionTag = "[Neutral]";
        public string ValidationResult;
        public string Action;
        public string ActionTarget;
        public SoulformActionCommand ParsedCommand;
    }
}
