using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class GridChunk
    {
        public readonly int CX;
        public readonly int CY;
        public readonly int CZ;
        public readonly GameObject Root;
        public const int ChunkSize = 8;

        private readonly Dictionary<(int x, int y, int z), GameObject> cellObjects = new Dictionary<(int, int, int), GameObject>();
        private readonly Dictionary<(int x, int y, int z), GameObject> groundObjects = new Dictionary<(int, int, int), GameObject>();
        private bool dirty;
        private int occupiedCount;

        public bool IsEmpty => occupiedCount == 0;

        public GridChunk(int cx, int cy, int cz, Transform parent)
        {
            CX = cx;
            CY = cy;
            CZ = cz;
            Root = new GameObject("Chunk_" + cx + "_" + cy + "_" + cz);
            Root.transform.SetParent(parent, false);
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        public void Rebuild(BubbleModel model, GridConfig config, EnvironmentVisualProfile profile)
        {
            foreach (var go in cellObjects.Values) { if (go != null) Object.Destroy(go); }
            cellObjects.Clear();
            foreach (var go in groundObjects.Values) { if (go != null) Object.Destroy(go); }
            groundObjects.Clear();

            int originX = CX * ChunkSize;
            int originY = CY * ChunkSize;
            int originZ = CZ * ChunkSize;

            for (int dz = 0; dz < ChunkSize; dz++)
            {
                for (int dy = 0; dy < ChunkSize; dy++)
                {
                    for (int dx = 0; dx < ChunkSize; dx++)
                    {
                        int wx = originX + dx;
                        int wy = originY + dy;
                        int wz = originZ + dz;

                        if (!model.IsInside(wx, wy, wz)) continue;

                        BubbleCell cell = model.Get(wx, wy, wz);
                        if (cell == null) continue;

                        GameObject go = BuildCellGO(cell, config, profile);
                        if (go != null)
                        {
                            go.transform.SetParent(Root.transform, false);
                            cellObjects[(wx, wy, wz)] = go;
                        }

                        if (!string.IsNullOrEmpty(cell.GroundItemId))
                        {
                            BubblePlacementDefinition groundDef = BubblePlacementCatalog.Find(cell.GroundItemId);
                            if (groundDef != null)
                            {
                                var groundCell = new BubbleCell(wx, wy, wz, groundDef);
                                int mask = AutoTileSystem.ComputeGroundMask(model, wx, wy, wz, cell.GroundItemId);
                                groundCell.AutoTileVariant = AutoTileSystem.GetVariant(mask);
                                GameObject groundGO = BuildCellGO(groundCell, config, profile);
                                if (groundGO != null)
                                {
                                    groundGO.transform.SetParent(Root.transform, false);
                                    groundGO.name = "Ground_" + (groundDef.DisplayName ?? "tile");
                                    groundGO.transform.position += new Vector3(0f, -0.005f, 0f);
                                    groundObjects[(wx, wy, wz)] = groundGO;
                                }
                            }
                        }
                    }
                }
            }

            occupiedCount = cellObjects.Count;
            dirty = false;

            if (occupiedCount == 0)
                Root.SetActive(false);
            else
                Root.SetActive(true);
        }

        public void Destroy()
        {
            foreach (var go in cellObjects.Values) { if (go != null) Object.Destroy(go); }
            cellObjects.Clear();
            foreach (var go in groundObjects.Values) { if (go != null) Object.Destroy(go); }
            groundObjects.Clear();
            if (Root != null) Object.Destroy(Root);
            occupiedCount = 0;
        }

        private static GameObject BuildCellGO(BubbleCell cell, GridConfig config, EnvironmentVisualProfile profile)
        {
            BubblePlacementDefinition item = BubblePlacementCatalog.Find(cell.ItemId);
            if (item == null) return null;

            GameObject tile = null;
            int variantRotation = 0;
            GameObject prefab = ResolvePrefab(cell, profile, out variantRotation);

            if (prefab != null)
            {
                tile = Object.Instantiate(prefab);
                tile.name = item.DisplayName;
                PlacementItem itemAsset = BubblePlacementCatalog.FindItemAsset(cell.ItemId);
                Vector3 soScale = itemAsset != null ? itemAsset.ModelScale3D : Vector3.one;
                tile.transform.localScale = soScale;
            }
            else
            {
                variantRotation = VariantFallbackRotation(cell.AutoTileVariant);
                tile = new GameObject(item.DisplayName);
                BuildFallback(tile, cell, item, profile);
            }

            Vector3 worldPos = ToWorld(cell.X, cell.Y, cell.Z, config);
            tile.transform.position = worldPos;

            if (item.IsObject)
            {
                PlacementItem itemAsset = BubblePlacementCatalog.FindItemAsset(cell.ItemId);
                if (itemAsset != null && itemAsset.YOffset3D != 0f)
                    tile.transform.position += new Vector3(0f, itemAsset.YOffset3D, 0f);
            }

            float totalYaw = 0f;

            if (variantRotation != 0)
                totalYaw += variantRotation;

            if (cell.Rotation > 0)
                totalYaw += cell.Rotation * 90f;

            if (totalYaw != 0f)
            {
                Vector3 r = tile.transform.localEulerAngles;
                tile.transform.localEulerAngles = new Vector3(r.x, r.y + totalYaw, r.z);
            }

            return tile;
        }

        private static void BuildFallback(GameObject tile, BubbleCell cell, BubblePlacementDefinition item, EnvironmentVisualProfile profile)
        {
            Mesh fallbackMesh = PrimitiveMeshUtility.GetVariantMesh(cell.AutoTileVariant);
            if (fallbackMesh == null)
                fallbackMesh = AssetFallback.GetFallbackMesh(item.Tab);
            Vector3 scale = AssetFallback.GetFallbackScale(item.Tab, item.IsObject);

            MeshFilter filter = tile.AddComponent<MeshFilter>();
            MeshRenderer renderer = tile.AddComponent<MeshRenderer>();

            Material mat = null;
            if (profile != null)
                mat = profile.GetMaterial(cell.ItemId, cell.AutoTileVariant);

            if (mat == null && profile != null)
                mat = profile.defaultTerrainMaterial;

            if (mat == null)
                mat = GetPrimitiveMaterial(item.Color);

            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            filter.sharedMesh = fallbackMesh;
            tile.transform.localScale = scale;
        }

        private static GameObject ResolvePrefab(BubbleCell cell, EnvironmentVisualProfile profile)
        {
            return ResolvePrefab(cell, profile, out _);
        }

        private static GameObject ResolvePrefab(BubbleCell cell, EnvironmentVisualProfile profile, out int variantRotation)
        {
            variantRotation = 0;

            if (profile != null)
            {
                if (profile.TryGetVariant(cell.ItemId, cell.AutoTileVariant, out GameObject profilePrefab, out variantRotation))
                    return profilePrefab;

                GameObject fallback = profile.GetPrefab(cell.ItemId, cell.AutoTileVariant);
                if (fallback != null) return fallback;

                if (ItemIsNotTerrain(cell.ItemId))
                    AssetFallback.LogWarningOnce(cell.ItemId, cell.AutoTileVariant);
            }

            PlacementItem itemAsset = BubblePlacementCatalog.FindItemAsset(cell.ItemId);
            if (itemAsset != null && itemAsset.Prefab3D != null)
                return itemAsset.Prefab3D;
            return null;
        }

        private static bool ItemIsNotTerrain(string itemId)
        {
            PlacementItem item = BubblePlacementCatalog.FindItemAsset(itemId);
            return item == null || (item.Tab != BuildTab.Terrain && item.Tab != BuildTab.Water);
        }

        private static int VariantFallbackRotation(TileVariant variant)
        {
            switch (variant)
            {
                case TileVariant.Center:         return 0;
                case TileVariant.Isolated:       return 0;
                case TileVariant.EdgeNorth:      return 0;
                case TileVariant.EdgeEast:       return 90;
                case TileVariant.EdgeSouth:      return 180;
                case TileVariant.EdgeWest:       return 270;
                case TileVariant.OuterCornerNE:  return 180;
                case TileVariant.OuterCornerSE:  return 270;
                case TileVariant.OuterCornerSW:  return 0;
                case TileVariant.OuterCornerNW:  return 90;
                case TileVariant.InnerCornerNE:  return 0;
                case TileVariant.InnerCornerSE:  return 90;
                case TileVariant.InnerCornerSW:  return 180;
                case TileVariant.InnerCornerNW:  return 270;
                case TileVariant.HalfCenter:     return 0;
                case TileVariant.HalfIsolated:   return 0;
                case TileVariant.HalfEdgeNorth:  return 0;
                case TileVariant.HalfEdgeEast:   return 90;
                case TileVariant.HalfEdgeSouth:  return 180;
                case TileVariant.HalfEdgeWest:   return 270;
                case TileVariant.HalfOuterCornerNE: return 180;
                case TileVariant.HalfOuterCornerSE: return 270;
                case TileVariant.HalfOuterCornerSW: return 0;
                case TileVariant.HalfOuterCornerNW: return 90;
                case TileVariant.HalfInnerCornerNE: return 0;
                case TileVariant.HalfInnerCornerSE: return 90;
                case TileVariant.HalfInnerCornerSW: return 180;
                case TileVariant.HalfInnerCornerNW: return 270;
                case TileVariant.FullToHalfEdgeNorth: return 0;
                case TileVariant.FullToHalfEdgeEast:  return 90;
                case TileVariant.FullToHalfEdgeSouth: return 180;
                case TileVariant.FullToHalfEdgeWest:  return 270;
                case TileVariant.FullToHalfOuterCornerNE: return 180;
                case TileVariant.FullToHalfOuterCornerSE: return 270;
                case TileVariant.FullToHalfOuterCornerSW: return 0;
                case TileVariant.FullToHalfOuterCornerNW: return 90;
                case TileVariant.FullToHalfInnerCornerNE: return 0;
                case TileVariant.FullToHalfInnerCornerSE: return 90;
                case TileVariant.FullToHalfInnerCornerSW: return 180;
                case TileVariant.FullToHalfInnerCornerNW: return 270;
                case TileVariant.HalfVerticalFront:
                case TileVariant.HalfVerticalBack:
                case TileVariant.HalfVerticalLeft:
                case TileVariant.HalfVerticalRight:  return 0;
                case TileVariant.RoundedEdgeNorth:   return 180;
                case TileVariant.RoundedEdgeSouth:   return 0;
                case TileVariant.RoundedEdgeEast:    return 270;
                case TileVariant.RoundedEdgeWest:    return 90;
                default:                         return 0;
            }
        }

        private static Material GetPrimitiveMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }

        private static Vector3 ToWorld(int x, int y, int z, GridConfig config)
        {
            float offsetX = config.Width * 0.5f * config.TileSize - config.TileSize * 0.5f;
            float offsetZ = config.Height * 0.5f * config.TileSize - config.TileSize * 0.5f;
            float worldX = x * config.TileSize - offsetX;
            float worldZ = z * config.TileSize - offsetZ;
            float worldY = y * config.TileHeight;
            return new Vector3(worldX, worldY, worldZ);
        }
    }
}
