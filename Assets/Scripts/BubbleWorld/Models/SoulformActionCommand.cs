using System;

namespace BubbleWorld
{
    public sealed class SoulformActionCommand
    {
        public SoulformAction Action;
        public string TargetId;
        public int? TargetX;
        public int? TargetY;
        public int? TargetZ;
        public string ValidationError;
        public bool IsValid => string.IsNullOrEmpty(ValidationError);

        public SoulformActionCommand(SoulformAction action)
        {
            Action = action;
        }

        public static readonly string[] AllActionNames =
        {
            "MoveToLocation", "GoToShelter", "SearchForFood", "CollectResource",
            "BuildObject", "Rest", "ObserveObject", "SpeakToPlayer",
            "SpeakToSoulform", "FleeDanger", "PlayAnimation", "ContinueCurrentTask"
        };

        public static bool IsAllowed(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Array.Exists(AllActionNames, n =>
                n.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
