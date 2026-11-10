using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class GridChunkManager
    {
        private readonly Dictionary<(int cx, int cy, int cz), GridChunk> chunks = new Dictionary<(int, int, int), GridChunk>();
        private Transform root;

        public void Initialize(BubbleModel model, GridConfig config, EnvironmentVisualProfile profile, Transform root)
        {
            Destroy();
            this.root = root;

            int chunksX = Mathf.CeilToInt((float)model.Width / GridChunk.ChunkSize);
            int chunksY = Mathf.CeilToInt((float)model.Depth / GridChunk.ChunkSize);
            int chunksZ = Mathf.CeilToInt((float)model.Height / GridChunk.ChunkSize);

            for (int cz = 0; cz < chunksZ; cz++)
            {
                for (int cy = 0; cy < chunksY; cy++)
                {
                    for (int cx = 0; cx < chunksX; cx++)
                    {
                        var chunk = new GridChunk(cx, cy, cz, root);
                        chunks[(cx, cy, cz)] = chunk;
                    }
                }
            }

            RebuildAll(model, config, profile);
        }

        public void RebuildCell(int x, int y, int z, BubbleModel model, GridConfig config, EnvironmentVisualProfile profile)
        {
            var chunkCoord = ChunkCoord(x, y, z);
            if (chunks.TryGetValue(chunkCoord, out GridChunk chunk))
            {
                chunk.Rebuild(model, config, profile);
            }
        }

        public void RebuildRegion(HashSet<(int x, int y, int z)> cells, BubbleModel model, GridConfig config, EnvironmentVisualProfile profile)
        {
            var rebuilt = new HashSet<(int, int, int)>();

            foreach (var (x, y, z) in cells)
            {
                var cc = ChunkCoord(x, y, z);
                if (rebuilt.Add(cc) && chunks.TryGetValue(cc, out GridChunk chunk))
                {
                    chunk.Rebuild(model, config, profile);
                }
            }
        }

        public void RebuildAll(BubbleModel model, GridConfig config, EnvironmentVisualProfile profile)
        {
            foreach (var chunk in chunks.Values)
            {
                chunk.Rebuild(model, config, profile);
            }
        }

        public void Cull(Camera camera, float maxDist)
        {
            if (camera == null) return;

            foreach (var chunk in chunks.Values)
            {
                if (chunk.IsEmpty) continue;
                Vector3 chunkCenter = chunk.Root.transform.position;
                float dist = Vector3.Distance(camera.transform.position, chunkCenter);
                chunk.Root.SetActive(dist <= maxDist);
            }
        }

        public void Destroy()
        {
            foreach (var chunk in chunks.Values)
            {
                chunk.Destroy();
            }
            chunks.Clear();
        }

        private static (int cx, int cy, int cz) ChunkCoord(int x, int y, int z)
        {
            return (x >> 3, y >> 3, z >> 3);
        }
    }
}
