using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class SkillProgression : IWorldSaveData
    {
        private readonly SkillConfig config;
        private readonly DeterministicRandom random;
        private readonly List<SkillSet> skillSets = new List<SkillSet>();

        public IReadOnlyList<SkillSet> SkillSets => skillSets;
        public string SaveKey => "SkillProgression";

        public event Action<string, int> OnSkillExecuted;
        public event Action<string> OnSkillAcquired;
        public event Action<string, int> OnSkillSetLeveled;

        public SkillProgression(SkillConfig config, int seed)
        {
            this.config = config;
            random = new DeterministicRandom(seed);
            InitializeSkillSets();
        }

        private void InitializeSkillSets()
        {
            foreach (var setEntry in config.SkillSets)
            {
                var set = new SkillSet
                {
                    Name = setEntry.Name,
                    StarLevel = 0
                };

                foreach (var skillName in setEntry.Skills)
                {
                    set.Skills.Add(new Skill
                    {
                        Name = skillName,
                        IsAcquired = false,
                        SuccessfulExecutions = 0,
                        SuccessPercentage = config.GetSuccessPercentage(0)
                    });
                }

                skillSets.Add(set);
            }
        }

        public bool AttemptSkill(string skillName)
        {
            var (skill, skillSet) = FindSkill(skillName);
            if (skill == null) return false;

            int successPct = config.GetSuccessPercentage(skillSet.StarLevel);
            skill.SuccessPercentage = successPct;

            int roll = random.RangeInclusive(1, 100);
            bool success = roll <= successPct;

            if (success)
            {
                if (!skill.IsAcquired)
                {
                    skill.IsAcquired = true;
                    string setName = FindSkillSetName(skillName);
                    OnSkillAcquired?.Invoke(skillName);
                }

                skill.SuccessfulExecutions++;
                OnSkillExecuted?.Invoke(skillName, skill.SuccessfulExecutions);

                CheckStarFill(skillSet);

                return true;
            }

            return false;
        }

        private void CheckStarFill(SkillSet skillSet)
        {
            bool allFilled = skillSet.Skills
                .Where(s => s.IsAcquired)
                .All(s => s.SuccessfulExecutions >= config.ExecutionsPerStar);

            if (allFilled && skillSet.StarLevel < 5)
            {
                skillSet.StarLevel++;
                OnSkillSetLeveled?.Invoke(skillSet.Name, skillSet.StarLevel);

                foreach (var skill in skillSet.Skills)
                {
                    skill.SuccessPercentage = config.GetSuccessPercentage(skillSet.StarLevel);
                }
            }
        }

        public int GetStarLevel(string skillName)
        {
            var (_, set) = FindSkill(skillName);
            return set?.StarLevel ?? 0;
        }

        public float GetSustainabilityRating(string skillName = null)
        {
            if (!string.IsNullOrEmpty(skillName))
            {
                var (skill, _) = FindSkill(skillName);
                return skill?.SuccessPercentage ?? 0;
            }

            if (skillSets.Count == 0) return 0;
            float total = 0;
            int count = 0;
            foreach (var set in skillSets)
            {
                foreach (var skill in set.Skills)
                {
                    total += skill.SuccessPercentage;
                    count++;
                }
            }
            return count > 0 ? total / count : 0;
        }

        private (Skill, SkillSet) FindSkill(string skillName)
        {
            foreach (var set in skillSets)
            {
                foreach (var skill in set.Skills)
                {
                    if (skill.Name == skillName)
                        return (skill, set);
                }
            }
            return (null, null);
        }

        private string FindSkillSetName(string skillName)
        {
            foreach (var set in skillSets)
            {
                foreach (var skill in set.Skills)
                {
                    if (skill.Name == skillName)
                        return set.Name;
                }
            }
            return null;
        }

        public string Serialize()
        {
            var data = new SkillProgressionData();
            foreach (var set in skillSets)
            {
                var setData = new SkillSetData
                {
                    name = set.Name,
                    starLevel = set.StarLevel,
                    skills = set.Skills.Select(s => new SkillData
                    {
                        name = s.Name,
                        isAcquired = s.IsAcquired,
                        executions = s.SuccessfulExecutions,
                        successPct = s.SuccessPercentage
                    }).ToList()
                };
                data.skillSets.Add(setData);
            }
            return JsonUtility.ToJson(data);
        }

        public void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            var d = JsonUtility.FromJson<SkillProgressionData>(data);
            if (d?.skillSets == null) return;

            skillSets.Clear();
            foreach (var setData in d.skillSets)
            {
                var set = new SkillSet
                {
                    Name = setData.name,
                    StarLevel = setData.starLevel
                };
                if (setData.skills != null)
                {
                    foreach (var s in setData.skills)
                    {
                        set.Skills.Add(new Skill
                        {
                            Name = s.name,
                            IsAcquired = s.isAcquired,
                            SuccessfulExecutions = s.executions,
                            SuccessPercentage = s.successPct
                        });
                    }
                }
                skillSets.Add(set);
            }
        }

        [Serializable]
        private sealed class SkillProgressionData
        {
            public List<SkillSetData> skillSets = new List<SkillSetData>();
        }

        [Serializable]
        private sealed class SkillSetData
        {
            public string name;
            public int starLevel;
            public List<SkillData> skills;
        }

        [Serializable]
        private sealed class SkillData
        {
            public string name;
            public bool isAcquired;
            public int executions;
            public float successPct;
        }
    }
}
