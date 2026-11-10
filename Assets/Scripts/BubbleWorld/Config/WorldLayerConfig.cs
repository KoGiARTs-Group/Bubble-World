using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "WorldLayerConfig", menuName = "BubbleWorld/World Layer Config")]
    public sealed class WorldLayerConfig : ScriptableObject
    {
        [SerializeField] private LayerEntry[] layerEntries;

        [System.Serializable]
        public struct LayerEntry
        {
            public LayerBand layer;
            public bool enableAutoTiling;
            public bool visible;
        }

        public LayerEntry Get(LayerBand layer)
        {
            int index = (int)layer + 1;
            if (layerEntries != null && index >= 0 && index < layerEntries.Length)
                return layerEntries[index];
            return new LayerEntry { layer = layer, visible = true };
        }

        public void SetVisible(LayerBand layer, bool visible)
        {
            int index = (int)layer + 1;
            if (layerEntries != null && index >= 0 && index < layerEntries.Length)
            {
                var e = layerEntries[index];
                e.visible = visible;
                layerEntries[index] = e;
            }
        }

        public bool IsAutoTilingLayer(LayerBand layer)
        {
            return Get(layer).enableAutoTiling;
        }

        public static WorldLayerConfig CreateDefault()
        {
            var cfg = CreateInstance<WorldLayerConfig>();
            cfg.layerEntries = new LayerEntry[]
            {
                new LayerEntry { layer = LayerBand.Underground, enableAutoTiling = false, visible = true },
                new LayerEntry { layer = LayerBand.GroundWaterBase, enableAutoTiling = true, visible = true },
                new LayerEntry { layer = LayerBand.LivingEnvironment, enableAutoTiling = false, visible = true },
                new LayerEntry { layer = LayerBand.ElevatedTerrain, enableAutoTiling = false, visible = true },
                new LayerEntry { layer = LayerBand.SkyAtmosphere, enableAutoTiling = false, visible = true },
            };
            return cfg;
        }
    }
}
