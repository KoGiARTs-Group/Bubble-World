using System;
using System.Collections.Generic;

namespace BubbleWorld
{
    [Serializable]
    public sealed class SkillSet
    {
        public string Name;
        public int StarLevel;
        public List<Skill> Skills = new List<Skill>();
    }
}
