using System;

namespace BubbleWorld
{
    [Serializable]
    public sealed class Skill
    {
        public string Name;
        public bool IsAcquired;
        public int SuccessfulExecutions;
        public bool IsFilledStar => IsAcquired && SuccessfulExecutions >= 5;
        public float SuccessPercentage;
    }
}
