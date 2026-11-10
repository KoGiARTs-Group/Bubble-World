using System;
using System.Collections.Generic;

namespace BubbleWorld
{
    public static class Pathfinding3D
    {
        private static readonly (int dx, int dy, int dz)[] Neighbors =
        {
            (1, 0, 0), (-1, 0, 0),
            (0, 1, 0), (0, -1, 0),
            (0, 0, 1), (0, 0, -1)
        };

        public static List<(int x, int y, int z)> FindPath(
            BubbleModel model,
            int startX, int startY, int startZ,
            int endX, int endY, int endZ,
            int maxNodes = 512)
        {
            if (model == null) return null;
            if (startX == endX && startY == endY && startZ == endZ)
                return new List<(int, int, int)> { (startX, startY, startZ) };

            var open = new MinHeap(maxNodes);
            var cameFrom = new Dictionary<(int, int, int), (int, int, int)>();
            var gScore = new Dictionary<(int, int, int), int>();
            var fScore = new Dictionary<(int, int, int), int>();

            var startKey = (startX, startY, startZ);
            var goalKey = (endX, endY, endZ);

            gScore[startKey] = 0;
            fScore[startKey] = Heuristic(startX, startY, startZ, endX, endY, endZ);
            open.Push(startKey, fScore[startKey]);

            int nodesProcessed = 0;

            while (open.Count > 0 && nodesProcessed < maxNodes)
            {
                var current = open.Pop();
                nodesProcessed++;

                if (current.Equals(goalKey))
                {
                    return ReconstructPath(cameFrom, current);
                }

                foreach (var (dx, dy, dz) in Neighbors)
                {
                    int nx = current.x + dx;
                    int ny = current.y + dy;
                    int nz = current.z + dz;

                    if (!model.IsInside(nx, ny, nz)) continue;
                    if (IsBlocked(model, nx, ny, nz)) continue;

                    var neighbor = (nx, ny, nz);
                    int tentativeG = gScore.GetValueOrDefault(current, int.MaxValue) + 1;

                    if (tentativeG < gScore.GetValueOrDefault(neighbor, int.MaxValue))
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        int f = tentativeG + Heuristic(nx, ny, nz, endX, endY, endZ);
                        fScore[neighbor] = f;
                        open.Push(neighbor, f);
                    }
                }
            }

            return null;
        }

        private static bool IsBlocked(BubbleModel model, int x, int y, int z)
        {
            BubbleCell cell = model.Get(x, y, z);
            if (cell == null) return false;

            BubblePlacementDefinition def = BubblePlacementCatalog.Find(cell.ItemId);
            return def != null && def.IsObject;
        }

        private static int Heuristic(int ax, int ay, int az, int bx, int by, int bz)
        {
            return Math.Abs(ax - bx) + Math.Abs(ay - by) + Math.Abs(az - bz);
        }

        private static List<(int x, int y, int z)> ReconstructPath(
            Dictionary<(int, int, int), (int, int, int)> cameFrom,
            (int x, int y, int z) current)
        {
            var path = new List<(int, int, int)>();
            var node = current;

            while (cameFrom.ContainsKey(node))
            {
                path.Add(node);
                node = cameFrom[node];
            }

            path.Add(node);
            path.Reverse();
            return path;
        }

        private sealed class MinHeap
        {
            private readonly List<(int x, int y, int z)> items = new List<(int, int, int)>();
            private readonly Dictionary<(int, int, int), int> priorities = new Dictionary<(int, int, int), int>();
            private readonly int maxSize;

            public int Count => items.Count;

            public MinHeap(int maxSize)
            {
                this.maxSize = maxSize;
            }

            public void Push((int x, int y, int z) item, int priority)
            {
                if (items.Count >= maxSize) return;

                if (priorities.TryGetValue(item, out int existing) && existing <= priority)
                    return;

                priorities[item] = priority;
                items.Add(item);
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (priorities[items[p]] <= priority) break;
                    (items[p], items[i]) = (items[i], items[p]);
                    i = p;
                }
            }

            public (int x, int y, int z) Pop()
            {
                if (items.Count == 0) return (0, 0, 0);

                var result = items[0];
                int last = items.Count - 1;
                items[0] = items[last];
                items.RemoveAt(last);

                int i = 0;
                while (true)
                {
                    int smallest = i;
                    int left = 2 * i + 1;
                    int right = 2 * i + 2;

                    if (left < items.Count && priorities[items[left]] < priorities[items[smallest]])
                        smallest = left;
                    if (right < items.Count && priorities[items[right]] < priorities[items[smallest]])
                        smallest = right;

                    if (smallest == i) break;
                    (items[i], items[smallest]) = (items[smallest], items[i]);
                    i = smallest;
                }

                return result;
            }
        }
    }
}
