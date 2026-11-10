using UnityEngine;

namespace BubbleWorld
{
    public sealed class NPCAI : IWorldTimeReceiver
    {
        private readonly AIConfig config;
        private readonly DeterministicRandom random;
        private int lastEvaluatedDay = -1;

        public NPCBaselineState BaselineState { get; private set; }
        public NPCCurrentState CurrentState { get; private set; }
        public NPCResponseAction CurrentAction { get; private set; }
        public float LonelinessTrigger { get; private set; }
        public float BoredomTrigger { get; private set; }
        public bool HasInadequateResources { get; set; }
        public int ConsecutiveDaysResourceShortage { get; set; }
        public NPCData BoundNPCData { get; set; }
        public EntityManager EntityManager { get; set; }
        public BubbleModel Bubble { get; set; }

        public NPCAI(AIConfig config, int seed)
        {
            this.config = config;
            random = new DeterministicRandom(seed);
            LonelinessTrigger = random.RangeInclusive(1, 12);
            BoredomTrigger = random.RangeInclusive(1, 10);
        }

        public void OnGameTick(float deltaSeconds, WorldTimeContext context)
        {
            if (context.DayCount != lastEvaluatedDay)
            {
                lastEvaluatedDay = context.DayCount;
                EvaluateDaily();
                UpdateBoundNPCData();
            }

            UpdateHourly(deltaSeconds, context);
        }

        public void OnFastForward(float totalMissedSeconds, WorldTimeContext context)
        {
            int daysMissed = Mathf.FloorToInt(totalMissedSeconds / 86400f);
            for (int i = 0; i < daysMissed; i++)
            {
                EvaluateDaily();
            }
            UpdateBoundNPCData();
        }

        public void EvaluateDaily()
        {
            EvaluateBaseline();
            EvaluateCurrentState();
            ExecuteTriggerResponse();
        }

        private void EvaluateBaseline()
        {
            int roll = random.RangeInclusive(1, 100);
            var baseline = config.NPCBaseline;

            if (roll <= baseline.BusyEnd)
                BaselineState = NPCBaselineState.Busy;
            else if (roll <= baseline.SocializingEnd)
                BaselineState = NPCBaselineState.Socializing;
            else
                BaselineState = NPCBaselineState.Working;
        }

        private void EvaluateCurrentState()
        {
            int roll = random.RangeInclusive(1, 100);

            foreach (var entry in config.NPCStates)
            {
                if (roll < entry.RangeStart || roll > entry.RangeEnd) continue;

                if (!string.IsNullOrEmpty(entry.TriggerCondition))
                {
                    if (entry.TriggerCondition.StartsWith("loneliness<="))
                    {
                        int max = int.Parse(entry.TriggerCondition.Replace("loneliness<=", ""));
                        if (LonelinessTrigger > max) continue;
                    }
                    else if (entry.TriggerCondition.StartsWith("boredom<="))
                    {
                        int max = int.Parse(entry.TriggerCondition.Replace("boredom<=", ""));
                        if (BoredomTrigger > max) continue;
                    }
                }

                CurrentState = ParseCurrentState(entry.StateName);
                return;
            }

            CurrentState = NPCCurrentState.HasFoodShelter;
        }

        private void ExecuteTriggerResponse()
        {
            string stateName = CurrentState.ToString();
            int roll = random.RangeInclusive(1, 100);

            foreach (var entry in config.NPCTriggerResponses)
            {
                if (entry.StateName != stateName) continue;

                foreach (var response in entry.Responses)
                {
                    if (roll >= response.RangeStart && roll <= response.RangeEnd)
                    {
                        CurrentAction = ParseResponseAction(response.Action);
                        return;
                    }
                }
            }

            CurrentAction = NPCResponseAction.None;
        }

        public bool ShouldTalkAboutMajorEvent()
        {
            return random.RangeInclusive(1, 100) <= 50;
        }

        private static NPCCurrentState ParseCurrentState(string name)
        {
            return name switch
            {
                "SafeHungry" => NPCCurrentState.SafeHungry,
                "SafeStarving" => NPCCurrentState.SafeStarving,
                "SafeLonely" => NPCCurrentState.SafeLonely,
                "SafeBored" => NPCCurrentState.SafeBored,
                "HasFoodShelter" => NPCCurrentState.HasFoodShelter,
                "Threatened" => NPCCurrentState.Threatened,
                "NeedsShelter" => NPCCurrentState.NeedsShelter,
                _ => NPCCurrentState.HasFoodShelter
            };
        }

        private static NPCResponseAction ParseResponseAction(string action)
        {
            return action switch
            {
                "Forage" => NPCResponseAction.Forage,
                "Wait" => NPCResponseAction.Wait,
                "Steal" => NPCResponseAction.Steal,
                "Fight" => NPCResponseAction.Fight,
                "Ask" => NPCResponseAction.Ask,
                "Hunt" => NPCResponseAction.Hunt,
                "Socialize" => NPCResponseAction.Socialize,
                "LookForRelationship" => NPCResponseAction.LookForRelationship,
                "PlayGames" => NPCResponseAction.PlayGames,
                "Read" => NPCResponseAction.Read,
                "Sing" => NPCResponseAction.Sing,
                "PlayInstrument" => NPCResponseAction.PlayInstrument,
                "Share" => NPCResponseAction.Share,
                "Hide" => NPCResponseAction.Hide,
                "Flee" => NPCResponseAction.Flee,
                "FleeForHelp" => NPCResponseAction.FleeForHelp,
                "Take" => NPCResponseAction.Take,
                "Build" => NPCResponseAction.Build,
                _ => NPCResponseAction.None
            };
        }

        private void UpdateBoundNPCData()
        {
            if (BoundNPCData == null) return;
            BoundNPCData.BaselineState = BaselineState;
            BoundNPCData.CurrentState = CurrentState;
            BoundNPCData.CurrentAction = CurrentAction;
            BoundNPCData.HasAdequateResources = !HasInadequateResources;
            BoundNPCData.DisgruntledDays = ConsecutiveDaysResourceShortage;
            BoundNPCData.IsDisgruntled = ConsecutiveDaysResourceShortage >= 3;
        }

        private void UpdateHourly(float deltaSeconds, WorldTimeContext context)
        {
            if (BoundNPCData == null || Bubble == null) return;

            float hours = deltaSeconds / 3600f;
            BoundNPCData.Hunger = Mathf.Max(0, BoundNPCData.Hunger - hours * 2f);

            ExecuteCurrentAction();

            bool isNight = context.Hour < 6 || context.Hour >= 20;
            if (isNight && CurrentAction != NPCResponseAction.None)
            {
                CurrentAction = NPCResponseAction.Wait;
                BoundNPCData.CurrentAction = NPCResponseAction.Wait;
            }
        }

        private void ExecuteCurrentAction()
        {
            if (BoundNPCData == null || EntityManager == null || Bubble == null) return;
            if (EntityManager.EntityMovementSystem != null && EntityManager.EntityMovementSystem.HasPath(BoundNPCData.EntityId)) return;

            switch (CurrentAction)
            {
                case NPCResponseAction.Forage:
                    FindAndMoveToFood();
                    break;
                case NPCResponseAction.Hunt:
                    FindAndMoveToPrey();
                    break;
                case NPCResponseAction.Socialize:
                    FindAndMoveToOtherNPC();
                    break;
                case NPCResponseAction.Flee:
                case NPCResponseAction.FleeForHelp:
                    BoundNPCData.PendingMovement = EntityMovementType.Wander;
                    break;
                case NPCResponseAction.Build:
                    BoundNPCData.PendingMovement = EntityMovementType.Wander;
                    break;
                default:
                    if (random.Value01() < 0.1f)
                        BoundNPCData.PendingMovement = EntityMovementType.Wander;
                    break;
            }
        }

        private void FindAndMoveToFood()
        {
            if (Bubble == null) return;
            int bestDist = int.MaxValue;
            int bx = BoundNPCData.GridX, bz = BoundNPCData.GridZ;

            for (int dz = -8; dz <= 8; dz++)
            {
                for (int dx = -8; dx <= 8; dx++)
                {
                    int nx = BoundNPCData.GridX + dx;
                    int nz = BoundNPCData.GridZ + dz;
                    var cell = Bubble.Get(nx, 0, nz);
                    if (cell == null) continue;
                    if (cell.ItemId == "bush" || cell.ItemId == "plant" || cell.ItemId == "tree")
                    {
                        int dist = Mathf.Abs(dx) + Mathf.Abs(dz);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bx = nx;
                            bz = nz;
                        }
                    }
                }
            }

            if (bestDist < int.MaxValue)
            {
                var path = Pathfinding3D.FindPath(Bubble,
                    BoundNPCData.GridX, BoundNPCData.GridY, BoundNPCData.GridZ,
                    bx, BoundNPCData.GridY, bz);
                if (path != null)
                    EntityManager.EntityMovementSystem?.SetPath(BoundNPCData.EntityId, path);
            }
        }

        private void FindAndMoveToPrey()
        {
            var prey = EntityManager.FindNearestFoodAnimal(
                BoundNPCData.GridX, BoundNPCData.GridY, BoundNPCData.GridZ);
            if (prey != null)
            {
                var path = Pathfinding3D.FindPath(Bubble,
                    BoundNPCData.GridX, BoundNPCData.GridY, BoundNPCData.GridZ,
                    prey.GridX, prey.GridY, prey.GridZ);
                if (path != null)
                    EntityManager.EntityMovementSystem?.SetPath(BoundNPCData.EntityId, path);
            }
        }

        private void FindAndMoveToOtherNPC()
        {
            var npcs = EntityManager.GetAllNPCs();
            if (npcs.Count <= 1) return;

            NPCData nearest = null;
            int bestDist = int.MaxValue;
            foreach (var npc in npcs)
            {
                if (npc.EntityId == BoundNPCData.EntityId) continue;
                int dist = Mathf.Abs(npc.GridX - BoundNPCData.GridX)
                         + Mathf.Abs(npc.GridZ - BoundNPCData.GridZ);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = npc;
                }
            }

            if (nearest != null && bestDist > 2)
            {
                var path = Pathfinding3D.FindPath(Bubble,
                    BoundNPCData.GridX, BoundNPCData.GridY, BoundNPCData.GridZ,
                    nearest.GridX, nearest.GridY, nearest.GridZ);
                if (path != null && path.Count > 0)
                {
                    var trimmed = path.GetRange(0, Mathf.Min(path.Count, 5));
                    EntityManager.EntityMovementSystem?.SetPath(BoundNPCData.EntityId, trimmed);
                }
            }
        }
    }
}
