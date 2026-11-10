using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "EnvironmentVisualProfile", menuName = "BubbleWorld/Environment Visual Profile")]
    public sealed class EnvironmentVisualProfile : ScriptableObject
    {
        public StarterEnvironment environment;

        [Header("Terrain Type Palette (preferred — single source of variant meshes)")]
        public TerrainType[] terrainTypes;

        [Header("Manual Terrain/Water Overrides (checked first, overrides TerrainType)")]
        public VisualOverride[] terrainOverrides;

        [Header("Vegetation Overrides")]
        public VisualOverride[] vegetationOverrides;

        [Header("Shared Materials")]
        public Material defaultTerrainMaterial;

        public GameObject GetPrefab(string itemId, TileVariant variant)
        {
            if (TryGetFromOverrides(terrainOverrides, itemId, variant, out var ov))
                return ov.prefab;
            if (TryGetFromOverrides(vegetationOverrides, itemId, variant, out ov))
                return ov.prefab;
            if (TryGetFromTerrainType(itemId, variant, out _, out _, out _))
            {
                TerrainType tt = FindTerrainType(itemId);
                if (tt.TryGetEntry(variant, out GameObject p, out _, out _))
                    return p;
            }
            return null;
        }

        public Material GetMaterial(string itemId, TileVariant variant)
        {
            if (TryGetFromOverrides(terrainOverrides, itemId, variant, out var ov))
                return ov.material ?? defaultTerrainMaterial;
            if (TryGetFromOverrides(vegetationOverrides, itemId, variant, out ov))
                return ov.material ?? defaultTerrainMaterial;
            if (TryGetFromTerrainType(itemId, variant, out _, out Material m, out _))
                return m ?? defaultTerrainMaterial;
            return defaultTerrainMaterial;
        }

        public bool TryGetVariant(string itemId, TileVariant variant, out GameObject prefab, out int rotationY)
        {
            if (TryGetFromOverrides(terrainOverrides, itemId, variant, out var ov) && ov.prefab != null)
            {
                prefab = ov.prefab;
                rotationY = 0;
                return true;
            }
            if (TryGetFromOverrides(vegetationOverrides, itemId, variant, out ov) && ov.prefab != null)
            {
                prefab = ov.prefab;
                rotationY = 0;
                return true;
            }
            if (TryGetFromTerrainType(itemId, variant, out prefab, out _, out rotationY))
                return prefab != null;
            prefab = null;
            rotationY = 0;
            return false;
        }

        private bool TryGetFromTerrainType(string itemId, TileVariant variant, out GameObject prefab, out Material material, out int rotationY)
        {
            TerrainType tt = FindTerrainType(itemId);
            if (tt != null && tt.TryGetEntry(variant, out prefab, out material, out rotationY))
                return true;
            prefab = null;
            material = null;
            rotationY = 0;
            return false;
        }

        private TerrainType FindTerrainType(string itemId)
        {
            if (terrainTypes == null) return null;
            for (int i = 0; i < terrainTypes.Length; i++)
            {
                if (terrainTypes[i] != null && terrainTypes[i].Id == itemId)
                    return terrainTypes[i];
            }
            return null;
        }

        private static bool TryGetFromOverrides(VisualOverride[] list, string itemId, TileVariant variant, out VisualOverride result)
        {
            if (list != null)
            {
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i].itemId == itemId && list[i].variant == variant)
                    {
                        result = list[i];
                        return true;
                    }
                }
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i].itemId == itemId && list[i].variant == TileVariant.Center)
                    {
                        result = list[i];
                        return true;
                    }
                }
            }
            result = default;
            return false;
        }
    }

    [System.Serializable]
    public struct VisualOverride
    {
        public string itemId;
        public TileVariant variant;
        public GameObject prefab;
        public Material material;
    }
}
