using System;
using UnityEngine;

namespace BubbleWorld
{
    public enum TutorialStep
    {
        HungerEmerges = 0,
        DiscoverDeer,
        GatherFuel,
        StartFire,
        PrepareFood,
        Eat,
        BuildShelter,
        Complete
    }

    /// <summary>
    /// Scripted opening sequence for the investor vertical slice:
    /// hunger -> discover deer -> gather fuel -> start fire -> prepare food -> eat -> build shelter.
    /// Session-only (not persisted). Every step advances on a gameplay condition
    /// or a real-time timeout so the demo can never stall mid-pitch.
    /// </summary>
    public sealed class TutorialSequence
    {
        /// <summary>Fired whenever a new step's guidance should be shown to the player.</summary>
        public event Action<string> OnGuidance;

        public TutorialStep Step { get; private set; } = TutorialStep.HungerEmerges;
        public bool IsComplete => Step == TutorialStep.Complete;

        private const float StepTimeoutSeconds = 180f;
        private const float HungerThreshold = 40f;
        private const int DeerDiscoveryRadius = 10;

        private float stepEnteredAt;
        private float hungerAtStepStart;
        private SoulformModel lastSoulform;

        private BubbleModel scannedBubble;
        private bool deerFound;
        private int deerX = -1;
        private int deerZ = -1;

        private static readonly string[] Guidance =
        {
            "Tutorial 1/7: Your Soulform has just been born in the meadow. Watch its hunger rise.",
            "Tutorial 2/7: It is hungry! A dead deer lies near the stream - watch your Soulform discover the carcass.",
            "Tutorial 3/7: Gather fuel. Your Soulform will collect resources when it is ready.",
            "Tutorial 4/7: Start a fire. Your Soulform will attempt the Fire Making skill when it dares.",
            "Tutorial 5/7: Prepare food. Your Soulform will search for food by the fire.",
            "Tutorial 6/7: Watch your Soulform eat - its hunger will fall.",
            "Tutorial 7/7: Build shelter before nightfall - place a Simple Shelter, or let your Soulform build it."
        };

        /// <summary>Starts (or restarts) the sequence at the first step.</summary>
        public void Begin()
        {
            Step = TutorialStep.HungerEmerges;
            stepEnteredAt = Time.realtimeSinceStartup;
            hungerAtStepStart = 0f;
            EmitGuidance();
        }

        /// <summary>Called every frame from WatchMode. Advances the step when its condition is met.</summary>
        public void Evaluate(float delta, SoulformModel soulform, BubbleModel bubble)
        {
            if (IsComplete) return;
            lastSoulform = soulform ?? lastSoulform;
            if (soulform == null) return;

            bool advance = false;
            switch (Step)
            {
                case TutorialStep.HungerEmerges:
                    advance = soulform.Hunger >= HungerThreshold;
                    break;
                case TutorialStep.DiscoverDeer:
                    advance = IsNearDeer(soulform, bubble);
                    break;
                case TutorialStep.Eat:
                    advance = hungerAtStepStart - soulform.Hunger >= 15f;
                    break;
            }

            if (!advance && Time.realtimeSinceStartup - stepEnteredAt > StepTimeoutSeconds)
                advance = true;

            if (advance) Advance();
        }

        /// <summary>Advances the PrepareFood step when the player hands over food.</summary>
        public void NotifyIntervention(InterventionType type)
        {
            if (IsComplete) return;
            if (Step == TutorialStep.GatherFuel && type == InterventionType.GrowTree) Advance();
            else if (Step == TutorialStep.PrepareFood && type == InterventionType.AddFood) Advance();
            else if (Step == TutorialStep.BuildShelter && type == InterventionType.SpawnStructure) Advance();
        }

        /// <summary>Advances steps driven by the Soulform's own actions.</summary>
        public void NotifyActionCompleted(SoulformAction action, bool success)
        {
            if (IsComplete || !success) return;
            if (Step == TutorialStep.GatherFuel && action == SoulformAction.CollectResource) Advance();
            else if (Step == TutorialStep.PrepareFood && action == SoulformAction.SearchForFood) Advance();
            else if (Step == TutorialStep.BuildShelter && action == SoulformAction.BuildObject) Advance();
        }

        /// <summary>Advances skill-driven steps (Fire Making, Shelter Building).</summary>
        public void NotifySkill(string skillName)
        {
            if (IsComplete || string.IsNullOrEmpty(skillName)) return;
            if (Step == TutorialStep.StartFire &&
                string.Equals(skillName, "Fire Making", StringComparison.OrdinalIgnoreCase)) Advance();
            else if (Step == TutorialStep.BuildShelter &&
                string.Equals(skillName, "Shelter Building", StringComparison.OrdinalIgnoreCase)) Advance();
        }

        private bool IsNearDeer(SoulformModel soulform, BubbleModel bubble)
        {
            if (bubble == null) return false;

            if (scannedBubble != bubble)
            {
                scannedBubble = bubble;
                deerFound = false;
                deerX = -1;
                deerZ = -1;
                for (int z = 0; z < bubble.Height && !deerFound; z++)
                {
                    for (int x = 0; x < bubble.Width; x++)
                    {
                        var cell = bubble.Get(x, 0, z);
                        if (cell != null && cell.ItemId == "dead_deer")
                        {
                            deerX = x;
                            deerZ = z;
                            deerFound = true;
                            break;
                        }
                    }
                }
            }

            if (!deerFound) return false;
            return Mathf.Abs(soulform.GridX - deerX) + Mathf.Abs(soulform.GridZ - deerZ) <= DeerDiscoveryRadius;
        }

        private void Advance()
        {
            if (IsComplete) return;
            Step++;
            stepEnteredAt = Time.realtimeSinceStartup;
            hungerAtStepStart = lastSoulform != null ? lastSoulform.Hunger : hungerAtStepStart;

            if (Step >= TutorialStep.Complete)
            {
                Step = TutorialStep.Complete;
                OnGuidance?.Invoke(
                    "Tutorial complete! Your Soulform survived its first days. The rest of its story is yours to shape.");
            }
            else
            {
                EmitGuidance();
            }
        }

        private void EmitGuidance()
        {
            int index = (int)Step;
            if (index >= 0 && index < Guidance.Length)
                OnGuidance?.Invoke(Guidance[index]);
        }
    }
}
