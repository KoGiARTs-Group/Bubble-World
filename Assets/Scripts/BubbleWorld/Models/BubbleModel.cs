using System.Collections.Generic;
using System.Linq;

namespace BubbleWorld
{
    public sealed class BubbleModel
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int Depth;
        public readonly StarterEnvironment Environment;
        private readonly Dictionary<(int x, int y, int z), BubbleCell> cells = new Dictionary<(int, int, int), BubbleCell>();
        private readonly Dictionary<(int x, int y, int z), (int px, int py, int pz)> ownedCells = new Dictionary<(int, int, int), (int, int, int)>();

        public BubbleModel(StarterEnvironment environment, int width = 60, int height = 60, int depth = 8)
        {
            Environment = environment;
            Width = width;
            Height = height;
            Depth = depth;
        }

        public IReadOnlyDictionary<(int x, int y, int z), BubbleCell> Cells => cells;

        public bool IsInside(int x, int y, int z)
        {
            return x >= 0 && y >= 0 && z >= 0 && x < Width && y < Depth && z < Height;
        }

        public const int MaxStackHeight = 6;

        public bool IsOwned(int x, int y, int z)
        {
            return ownedCells.ContainsKey((x, y, z));
        }

        public int FindStackY(int x, int z, int maxY)
        {
            for (int y = 0; y <= maxY; y++)
                if (Get(x, y, z) == null)
                    return y;
            return -1;
        }

        public int FindTopY(int x, int z, int maxY)
        {
            for (int y = maxY; y >= 0; y--)
                if (Get(x, y, z) != null)
                    return y;
            return -1;
        }

        public void Place(int x, int y, int z, BubblePlacementDefinition item)
        {
            if (!IsInside(x, y, z) || item == null) return;

            var key = (x, y, z);
            CleanupOwnedBy(key);
            var newCell = new BubbleCell(x, y, z, item);

            if (item.IsObject && cells.TryGetValue(key, out var oldCell))
            {
                if (!string.IsNullOrEmpty(oldCell.GroundItemId))
                    newCell.GroundItemId = oldCell.GroundItemId;
                else
                {
                    var oldDef = BubblePlacementCatalog.Find(oldCell.ItemId);
                    if (oldDef != null && !oldDef.IsObject)
                        newCell.GroundItemId = oldCell.ItemId;
                }
            }

            cells[key] = newCell;

            if (item.OccupyRadius > 0)
                RegisterOwned(key, x, y, z, item.OccupyRadius);
        }

        public void Remove(int x, int y, int z)
        {
            var objKey = (x, y, z);
            if (ownedCells.TryGetValue(objKey, out var parentKey))
            {
                ownedCells.Remove(objKey);
                CleanupOwnedBy(parentKey);
                cells.Remove(parentKey);
                return;
            }
            CleanupOwnedBy(objKey);

            if (cells.TryGetValue(objKey, out var cell) && !string.IsNullOrEmpty(cell.GroundItemId))
            {
                var groundDef = BubblePlacementCatalog.Find(cell.GroundItemId);
                if (groundDef != null)
                {
                    cells[objKey] = new BubbleCell(x, y, z, groundDef);
                    return;
                }
            }

            cells.Remove(objKey);
        }

        public void Remove(int x, int y, int z, BubblePlacementDefinition item)
        {
            if (item == null)
            {
                Remove(x, y, z);
                return;
            }

            var key = (x, y, z);
            CleanupOwnedBy(key);

            if (cells.TryGetValue(key, out var cell) && !string.IsNullOrEmpty(cell.GroundItemId))
            {
                var groundDef = BubblePlacementCatalog.Find(cell.GroundItemId);
                if (groundDef != null)
                {
                    cells[key] = new BubbleCell(x, y, z, groundDef);
                    return;
                }
            }

            cells.Remove(key);
        }

        public BubbleCell Get(int x, int y, int z)
        {
            cells.TryGetValue((x, y, z), out BubbleCell cell);
            return cell;
        }

        public void SetCellRaw((int x, int y, int z) key, BubbleCell cell)
        {
            cells[key] = cell;
        }

        public void RemoveCellRaw((int x, int y, int z) key)
        {
            cells.Remove(key);
        }

        public void RebuildOwnedCells()
        {
            ownedCells.Clear();
            foreach (var kv in cells)
            {
                if (kv.Value == null) continue;
                BubblePlacementDefinition def = BubblePlacementCatalog.Find(kv.Value.ItemId);
                if (def != null && def.OccupyRadius > 0)
                    RegisterOwned(kv.Key, kv.Value.X, kv.Value.Y, kv.Value.Z, def.OccupyRadius);
            }
        }

        public int ApplyBrush(int centerX, int centerY, int centerZ, BrushSize brushSize, BubblePlacementDefinition item, bool remove)
        {
            int size = (int)brushSize;
            int radius = size / 2;
            int changed = 0;

            for (int z = centerZ - radius; z <= centerZ + radius; z++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int y;
                    if (remove)
                    {
                        y = FindTopY(x, z, MaxStackHeight);
                        if (y < 0) continue;
                        Remove(x, y, z, item);
                    }
                    else
                    {
                        y = FindStackY(x, z, MaxStackHeight);
                        if (y < 0) continue;
                        if (IsOwned(x, y, z) && Get(x, y, z) == null && item != null && item.IsObject) continue;
                        Place(x, y, z, item);
                    }

                    changed++;
                }
            }

            return changed;
        }

        public HashSet<(int x, int y, int z)> CollectDirtyRegion(int centerX, int centerY, int centerZ, BrushSize brushSize)
        {
            int size = (int)brushSize;
            int radius = size / 2;
            var dirty = new HashSet<(int x, int y, int z)>();

            for (int z = centerZ - radius; z <= centerZ + radius; z++)
                for (int x = centerX - radius; x <= centerX + radius; x++)
                    for (int y = 0; y <= MaxStackHeight; y++)
                        if (IsInside(x, y, z))
                            dirty.Add((x, y, z));

            int borderRadius = radius + 1;
            for (int z = centerZ - borderRadius; z <= centerZ + borderRadius; z++)
                for (int x = centerX - borderRadius; x <= centerX + borderRadius; x++)
                    for (int y = 0; y <= MaxStackHeight; y++)
                        if (IsInside(x, y, z))
                            dirty.Add((x, y, z));

            return dirty;
        }

        private void RegisterOwned((int x, int y, int z) parentKey, int cx, int cy, int cz, int radius)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;
                        int ox = cx + dx, oy = cy + dy, oz = cz + dz;
                        if (IsInside(ox, oy, oz))
                            ownedCells[(ox, oy, oz)] = parentKey;
                    }
                }
            }
        }

        private void CleanupOwnedBy((int x, int y, int z) parentKey)
        {
            List<(int, int, int)> toRemove = ownedCells
                .Where(kv => kv.Value.Equals(parentKey))
                .Select(kv => kv.Key)
                .ToList();
            foreach (var k in toRemove)
            {
                cells.Remove(k);
                ownedCells.Remove(k);
            }
        }
    }
}
