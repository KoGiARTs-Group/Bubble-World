using System;
using UnityEngine;

namespace BubbleWorld
{
    public static class ActionValidator
    {
        public static SoulformActionCommand Validate(SoulformActionCommand cmd, BubbleModel bubble, int soulformX, int soulformY, int soulformZ)
        {
            if (cmd == null)
                return new SoulformActionCommand(SoulformAction.ContinueCurrentTask)
                    { ValidationError = "Null command" };

            if (!cmd.IsValid)
                return cmd;

            if (!SoulformActionCommand.IsAllowed(cmd.Action.ToString()))
            {
                cmd.ValidationError = "Action not in allowed list: " + cmd.Action;
                return cmd;
            }

            if (NeedsTargetResolution(cmd.Action) && !cmd.TargetX.HasValue)
            {
                if (!string.IsNullOrEmpty(cmd.TargetId))
                    ResolveTargetId(cmd, bubble, soulformX, soulformY, soulformZ);
                else
                    ResolveByAction(cmd, bubble, soulformX, soulformY, soulformZ);
            }

            if (cmd.TargetX.HasValue)
            {
                cmd.TargetX = Mathf.Clamp(cmd.TargetX.Value, 0, (bubble?.Width ?? 60) - 1);
                cmd.TargetY = Mathf.Clamp(cmd.TargetY.Value, 0, (bubble?.Depth ?? 8) - 1);
                cmd.TargetZ = Mathf.Clamp(cmd.TargetZ.Value, 0, (bubble?.Height ?? 60) - 1);
            }

            return cmd;
        }

        private static bool NeedsTargetResolution(SoulformAction action)
        {
            switch (action)
            {
                case SoulformAction.MoveToLocation:
                case SoulformAction.GoToShelter:
                case SoulformAction.SearchForFood:
                case SoulformAction.CollectResource:
                case SoulformAction.BuildObject:
                case SoulformAction.ObserveObject:
                case SoulformAction.FleeDanger:
                    return true;
                default:
                    return false;
            }
        }

        private static void ResolveTargetId(SoulformActionCommand cmd, BubbleModel bubble, int sx, int sy, int sz)
        {
            if (bubble == null) return;
            string id = cmd.TargetId;

            if (TryFindItem(bubble, id, out var pos))
            {
                cmd.TargetX = pos.x; cmd.TargetY = pos.y; cmd.TargetZ = pos.z;
            }
            else
            {
                ResolveByAction(cmd, bubble, sx, sy, sz);
            }
        }

        private static void ResolveByAction(SoulformActionCommand cmd, BubbleModel bubble, int sx, int sy, int sz)
        {
            if (bubble == null) return;

            switch (cmd.Action)
            {
                case SoulformAction.GoToShelter:
                {
                    var pos = FindNearest(bubble, sx, sy, sz, def => def.Shelter > 0);
                    if (pos.HasValue) { cmd.TargetX = pos.Value.x; cmd.TargetY = pos.Value.y; cmd.TargetZ = pos.Value.z; }
                    else { cmd.TargetX = sx; cmd.TargetY = sy; cmd.TargetZ = sz; }
                    break;
                }
                case SoulformAction.SearchForFood:
                case SoulformAction.CollectResource:
                {
                    var pos = FindNearest(bubble, sx, sy, sz, def => def.Food > 0);
                    if (pos.HasValue) { cmd.TargetX = pos.Value.x; cmd.TargetY = pos.Value.y; cmd.TargetZ = pos.Value.z; }
                    else { cmd.TargetX = sx; cmd.TargetY = sy; cmd.TargetZ = sz; }
                    break;
                }
                case SoulformAction.FleeDanger:
                {
                    int w = bubble?.Width ?? 60;
                    int h = bubble?.Height ?? 60;
                    cmd.TargetX = Mathf.Clamp(sx + UnityEngine.Random.Range(-5, 6), 0, w - 1);
                    cmd.TargetY = sy;
                    cmd.TargetZ = Mathf.Clamp(sz + UnityEngine.Random.Range(-5, 6), 0, h - 1);
                    break;
                }
                default:
                    cmd.TargetX = sx; cmd.TargetY = sy; cmd.TargetZ = sz;
                    break;
            }
        }

        private static Vector3Int? FindNearest(BubbleModel bubble, int sx, int sy, int sz, Func<BubblePlacementDefinition, bool> match)
        {
            int best = int.MaxValue;
            Vector3Int? result = null;

            foreach (var kvp in bubble.Cells)
            {
                var cell = kvp.Value;
                if (string.IsNullOrEmpty(cell.ItemId)) continue;
                var def = BubblePlacementCatalog.Find(cell.ItemId);
                if (def != null && match(def))
                {
                    int d = Mathf.Abs(cell.X - sx) + Mathf.Abs(cell.Y - sy) + Mathf.Abs(cell.Z - sz);
                    if (d < best) { best = d; result = new Vector3Int(cell.X, cell.Y, cell.Z); }
                }
            }
            return result;
        }

        private static bool TryFindItem(BubbleModel bubble, string partialId, out Vector3Int pos)
        {
            pos = default;
            foreach (var kvp in bubble.Cells)
            {
                var cell = kvp.Value;
                if (cell.ItemId != null && cell.ItemId.IndexOf(partialId, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pos = new Vector3Int(cell.X, cell.Y, cell.Z);
                    return true;
                }
            }
            return false;
        }
    }
}
