namespace BubbleWorld
{
    public static class SoulformUnlockRule
    {
        public static bool IsUnlocked(BubbleModel bubble)
        {
            if (bubble == null) return false;
            LivabilityReport report = BubbleLivability.Calculate(bubble);
            return report.Percentage >= 60;
        }

        public static string LockReason(BubbleModel bubble)
        {
            if (bubble == null) return "No world created";
            LivabilityReport report = BubbleLivability.Calculate(bubble);
            if (report.Percentage < 60)
                return "Reach 60% livability to create a Soulform (currently " + report.Percentage + "%)";
            return "";
        }
    }
}
