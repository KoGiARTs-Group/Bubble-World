namespace BubbleWorld
{
    public sealed class Intervention
    {
        public readonly InterventionType Type;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly float CooldownSeconds;

        public Intervention(InterventionType type, string displayName, string description, float cooldownSeconds = 0f)
        {
            Type = type;
            DisplayName = displayName;
            Description = description;
            CooldownSeconds = cooldownSeconds;
        }

        public static Intervention FromType(InterventionType type)
        {
            switch (type)
            {
                case InterventionType.AddFood:
                    return new Intervention(type, "Add Food", "Drop food near the Soulform", 5f);
                case InterventionType.GrowTree:
                    return new Intervention(type, "Grow Tree", "Instant tree growth", 10f);
                case InterventionType.ChangeWeather:
                    return new Intervention(type, "Change Weather", "Shift the current weather", 15f);
                case InterventionType.DayNight:
                    return new Intervention(type, "Day / Night", "Toggle between day and night", 30f);
                case InterventionType.SpawnStructure:
                    return new Intervention(type, "Spawn Structure", "Place a basic structure", 10f);
                case InterventionType.SpawnAnimal:
                    return new Intervention(type, "Spawn Animal", "Introduce an animal", 20f);
                case InterventionType.SpawnNpc:
                    return new Intervention(type, "Spawn NPC", "Spawn a non-player character", 30f);
                case InterventionType.RemindSoulform:
                    return new Intervention(type, "Remind Soulform", "Nudge the Soulform to act", 3f);
                default:
                    return new Intervention(type, type.ToString(), "", 0f);
            }
        }
    }
}
