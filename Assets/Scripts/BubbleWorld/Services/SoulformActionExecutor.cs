using UnityEngine;

namespace BubbleWorld
{
    public sealed class SoulformActionExecutor
    {
        private readonly BubbleModel bubble;
        private readonly EntityManager entityManager;
        private readonly SkillProgression skillProgression;
        private readonly EventLog eventLog;
        private readonly MemoryBank memoryBank;

        public event System.Action<SoulformAction, bool> OnActionCompleted;
        public event System.Action OnSoulformDied;

        public SoulformActionExecutor(BubbleModel bubble, EntityManager entityManager,
            SkillProgression skillProgression, EventLog eventLog, MemoryBank memoryBank)
        {
            this.bubble = bubble;
            this.entityManager = entityManager;
            this.skillProgression = skillProgression;
            this.eventLog = eventLog;
            this.memoryBank = memoryBank;
        }

        public void ExecuteAction(SoulformActionCommand cmd, SoulformModel soulform)
        {
            if (cmd == null || !cmd.IsValid) return;
            if (soulform == null) return;

            var entity = entityManager.SoulformEntity;

            switch (cmd.Action)
            {
                case SoulformAction.SearchForFood:
                    ExecuteSearchForFood(cmd, soulform);
                    break;
                case SoulformAction.GoToShelter:
                    ExecuteGoToShelter(cmd, soulform);
                    break;
                case SoulformAction.CollectResource:
                    ExecuteCollectResource(cmd, soulform);
                    break;
                case SoulformAction.BuildObject:
                    ExecuteBuildObject(cmd, soulform);
                    break;
                case SoulformAction.ObserveObject:
                    ExecuteObserve(cmd, soulform);
                    break;
                case SoulformAction.FleeDanger:
                    ExecuteFlee(cmd, soulform);
                    break;
                case SoulformAction.Rest:
                    ExecuteRest(soulform);
                    break;
                default:
                    OnActionCompleted?.Invoke(cmd.Action, false);
                    break;
            }
        }

        public bool AttemptSkill(string skillName, SoulformModel soulform)
        {
            if (skillProgression == null) return false;

            bool success = skillProgression.AttemptSkill(skillName);
            string desc = success
                ? "Successfully attempted " + skillName
                : "Failed to perform " + skillName;

            eventLog?.Record(success ? GameEventType.SoulformReacted : GameEventType.SoulformReacted,
                soulform.Name + ": " + desc);

            memoryBank?.AddEvent(desc, MemoryCategory.Skill,
                new System.Collections.Generic.List<string> { "skill", skillName.ToLower() });

            OnActionCompleted?.Invoke(SoulformAction.ContinueCurrentTask, success);
            return success;
        }

        public void ApplyFailureConsequence(SoulformModel soulform, string consequenceType)
        {
            switch (consequenceType)
            {
                case "unsafe_food":
                    soulform.Health = Mathf.Max(0, soulform.Health - 20);
                    soulform.Hunger = Mathf.Max(0, soulform.Hunger - 10);
                    soulform.Mood = SoulformMood.Distressed;
                    eventLog?.Record(GameEventType.SoulformReacted,
                        soulform.Name + " ate something unsafe and feels sick");
                    break;
                case "wrong_material":
                    soulform.Comfort = Mathf.Max(0, soulform.Comfort - 10);
                    eventLog?.Record(GameEventType.SoulformReacted,
                        soulform.Name + " used the wrong materials and wasted time");
                    break;
                case "dangerous_decision":
                    soulform.Health = Mathf.Max(0, soulform.Health - 30);
                    eventLog?.Record(GameEventType.SoulformReacted,
                        soulform.Name + " made a dangerous choice and got hurt");
                    break;
                case "exhaustion":
                    soulform.Comfort = Mathf.Max(0, soulform.Comfort - 20);
                    soulform.Hunger = Mathf.Max(0, soulform.Hunger - 15);
                    break;
            }

            if (soulform.Health <= 0)
            {
                OnSoulformDied?.Invoke();
            }
        }

        private void ExecuteSearchForFood(SoulformActionCommand cmd, SoulformModel soulform)
        {
            soulform.CurrentActivity = "Searching for food";
            soulform.CurrentThought = "I need to find something to eat...";
            OnActionCompleted?.Invoke(SoulformAction.SearchForFood, true);
        }

        private void ExecuteGoToShelter(SoulformActionCommand cmd, SoulformModel soulform)
        {
            soulform.CurrentActivity = "Seeking shelter";
            soulform.CurrentThought = "I need to get out of the weather.";
            memoryBank?.AddEvent("Sought shelter at (" + cmd.TargetX + "," + cmd.TargetZ + ")",
                MemoryCategory.WorldEvent, new System.Collections.Generic.List<string> { "shelter" });
            OnActionCompleted?.Invoke(SoulformAction.GoToShelter, true);
        }

        private void ExecuteCollectResource(SoulformActionCommand cmd, SoulformModel soulform)
        {
            soulform.CurrentActivity = "Collecting resources";
            memoryBank?.AddEvent("Collected resources",
                MemoryCategory.Skill, new System.Collections.Generic.List<string> { "collect", "resource" });
            AttemptSkill("Foraging", soulform);
            OnActionCompleted?.Invoke(SoulformAction.CollectResource, true);
        }

        private void ExecuteBuildObject(SoulformActionCommand cmd, SoulformModel soulform)
        {
            soulform.CurrentActivity = "Building " + (cmd.TargetId ?? "a structure");
            memoryBank?.AddEvent("Building " + (cmd.TargetId ?? "a structure"),
                MemoryCategory.Skill, new System.Collections.Generic.List<string> { "build", cmd.TargetId?.ToLower() });
            AttemptSkill("Building", soulform);
            OnActionCompleted?.Invoke(SoulformAction.BuildObject, true);
        }

        private void ExecuteObserve(SoulformActionCommand cmd, SoulformModel soulform)
        {
            soulform.CurrentActivity = "Observing";
            soulform.Curiosity = Mathf.Min(100, soulform.Curiosity + 5);
            memoryBank?.AddEvent("Observed " + (cmd.TargetId ?? "something interesting"),
                MemoryCategory.Discovery, new System.Collections.Generic.List<string> { "observe", cmd.TargetId?.ToLower() });
            OnActionCompleted?.Invoke(SoulformAction.ObserveObject, true);
        }

        private void ExecuteFlee(SoulformActionCommand cmd, SoulformModel soulform)
        {
            soulform.CurrentActivity = "Fleeing from danger";
            soulform.Mood = SoulformMood.Distressed;
            memoryBank?.AddEvent("Fled from danger",
                MemoryCategory.Emotion, new System.Collections.Generic.List<string> { "fear", "danger" });
            OnActionCompleted?.Invoke(SoulformAction.FleeDanger, true);
        }

        private void ExecuteRest(SoulformModel soulform)
        {
            soulform.CurrentActivity = "Resting";
            soulform.Comfort = Mathf.Min(100, soulform.Comfort + 10);
            memoryBank?.AddEvent("Resting to regain energy",
                MemoryCategory.Emotion, new System.Collections.Generic.List<string> { "rest" });
            OnActionCompleted?.Invoke(SoulformAction.Rest, true);
        }
    }
}
