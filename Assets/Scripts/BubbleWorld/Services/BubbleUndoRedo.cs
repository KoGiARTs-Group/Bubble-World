using System.Collections.Generic;

namespace BubbleWorld
{
    public sealed class CellChange
    {
        public (int x, int y, int z) Key;
        public BubbleCell OldCell;
        public BubbleCell NewCell;
    }

    public sealed class BubbleAction
    {
        public readonly List<CellChange> Changes = new List<CellChange>();
    }

    public sealed class BubbleUndoRedo
    {
        private readonly List<BubbleAction> undoStack = new List<BubbleAction>();
        private readonly List<BubbleAction> redoStack = new List<BubbleAction>();
        private const int MaxUndo = 100;

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        public void Record(BubbleAction action)
        {
            undoStack.Add(action);
            if (undoStack.Count > MaxUndo)
                undoStack.RemoveAt(0);
            redoStack.Clear();
        }

        public bool Undo(BubbleModel bubble)
        {
            if (undoStack.Count == 0) return false;
            BubbleAction action = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            ApplyAction(bubble, action, undo: true);
            redoStack.Add(action);
            return true;
        }

        public bool Redo(BubbleModel bubble)
        {
            if (redoStack.Count == 0) return false;
            BubbleAction action = redoStack[redoStack.Count - 1];
            redoStack.RemoveAt(redoStack.Count - 1);
            ApplyAction(bubble, action, undo: false);
            undoStack.Add(action);
            return true;
        }

        public static Dictionary<(int x, int y, int z), BubbleCell> SnapshotRegion(BubbleModel bubble,
            int centerX, int centerY, int centerZ, BrushSize brushSize, bool includeNeighbors = false)
        {
            int size = (int)brushSize;
            int radius = size / 2;
            int captureRadius = includeNeighbors ? radius + 1 : radius;
            var snapshot = new Dictionary<(int x, int y, int z), BubbleCell>();

            for (int z = centerZ - captureRadius; z <= centerZ + captureRadius; z++)
            {
                for (int y = centerY - captureRadius; y <= centerY + captureRadius; y++)
                {
                    for (int x = centerX - captureRadius; x <= centerX + captureRadius; x++)
                    {
                        if (!bubble.IsInside(x, y, z)) continue;
                        var key = (x, y, z);
                        if (bubble.Cells.TryGetValue(key, out BubbleCell cell))
                            snapshot[key] = cell.Clone();
                    }
                }
            }
            return snapshot;
        }

        public static Dictionary<(int x, int y, int z), BubbleCell> SnapshotRegion(BubbleModel bubble,
            int centerX, int centerZ, BrushSize brushSize, int maxHeight, bool includeNeighbors = false)
        {
            int size = (int)brushSize;
            int radius = size / 2;
            int captureRadius = includeNeighbors ? radius + 1 : radius;
            var snapshot = new Dictionary<(int x, int y, int z), BubbleCell>();

            for (int z = centerZ - captureRadius; z <= centerZ + captureRadius; z++)
                for (int x = centerX - captureRadius; x <= centerX + captureRadius; x++)
                    for (int y = 0; y <= maxHeight; y++)
                    {
                        if (!bubble.IsInside(x, y, z)) continue;
                        var key = (x, y, z);
                        if (bubble.Cells.TryGetValue(key, out BubbleCell cell))
                            snapshot[key] = cell.Clone();
                    }
            return snapshot;
        }

        public static BubbleAction BuildAction(Dictionary<(int x, int y, int z), BubbleCell> before,
            BubbleModel after, int centerX, int centerY, int centerZ, BrushSize brushSize)
        {
            var action = new BubbleAction();
            int size = (int)brushSize;
            int radius = size / 2;
            int captureRadius = radius + 1;

            foreach (var kv in before)
            {
                after.Cells.TryGetValue(kv.Key, out BubbleCell newCell);
                if (!CellsEqual(kv.Value, newCell))
                {
                    action.Changes.Add(new CellChange
                    {
                        Key = kv.Key,
                        OldCell = kv.Value,
                        NewCell = newCell != null ? newCell.Clone() : null
                    });
                }
            }

            for (int z = centerZ - captureRadius; z <= centerZ + captureRadius; z++)
            {
                for (int y = centerY - captureRadius; y <= centerY + captureRadius; y++)
                {
                    for (int x = centerX - captureRadius; x <= centerX + captureRadius; x++)
                    {
                        if (!after.IsInside(x, y, z)) continue;
                        var key = (x, y, z);
                        if (!before.ContainsKey(key) && after.Cells.TryGetValue(key, out BubbleCell newCell))
                        {
                            action.Changes.Add(new CellChange
                            {
                                Key = key,
                                OldCell = null,
                                NewCell = newCell.Clone()
                            });
                        }
                    }
                }
            }

            return action;
        }

        public static BubbleAction BuildAction(Dictionary<(int x, int y, int z), BubbleCell> before,
            BubbleModel after, int centerX, int centerZ, BrushSize brushSize, int maxHeight)
        {
            var action = new BubbleAction();
            int size = (int)brushSize;
            int radius = size / 2;
            int captureRadius = radius + 1;

            foreach (var kv in before)
            {
                after.Cells.TryGetValue(kv.Key, out BubbleCell newCell);
                if (!CellsEqual(kv.Value, newCell))
                {
                    action.Changes.Add(new CellChange
                    {
                        Key = kv.Key,
                        OldCell = kv.Value,
                        NewCell = newCell != null ? newCell.Clone() : null
                    });
                }
            }

            for (int z = centerZ - captureRadius; z <= centerZ + captureRadius; z++)
                for (int x = centerX - captureRadius; x <= centerX + captureRadius; x++)
                    for (int y = 0; y <= maxHeight; y++)
                    {
                        if (!after.IsInside(x, y, z)) continue;
                        var key = (x, y, z);
                        if (!before.ContainsKey(key) && after.Cells.TryGetValue(key, out BubbleCell newCell))
                        {
                            action.Changes.Add(new CellChange
                            {
                                Key = key,
                                OldCell = null,
                                NewCell = newCell.Clone()
                            });
                        }
                    }

            return action;
        }

        private static void ApplyAction(BubbleModel bubble, BubbleAction action, bool undo)
        {
            foreach (CellChange change in action.Changes)
            {
                BubbleCell target = undo ? change.OldCell : change.NewCell;
                if (target != null)
                    bubble.SetCellRaw(change.Key, target.Clone());
                else
                    bubble.RemoveCellRaw(change.Key);
            }
        }

        private static bool CellsEqual(BubbleCell a, BubbleCell b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z
                && a.ItemId == b.ItemId
                && a.Layer == b.Layer
                && a.IsObject == b.IsObject
                && a.Rotation == b.Rotation
                && a.AutoTileVariant == b.AutoTileVariant;
        }
    }
}
