using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "PlacementItem", menuName = "BubbleWorld/Placement Item")]
    public sealed class PlacementItem : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public Sprite Icon;

        [Header("Classification")]
        public BuildTab Tab;
        public LayerBand Layer;
        public bool IsObject;

        [Header("Appearance")]
        public Color TintColor = Color.white;

        [Header("3D Model")]
        [Tooltip("Optional 3D prefab spawned in 3D mode. If null, a primitive mesh is used.")]
        public GameObject Prefab3D;
        [Tooltip("Primitive mesh used when Prefab3D is null.")]
        public Mesh3DPrimitive FallbackPrimitive = Mesh3DPrimitive.Cube;
        [Tooltip("Per-axis scale applied to the 3D model.")]
        public Vector3 ModelScale3D = Vector3.one;
        [Tooltip("Extra Y offset for 3D models (lifts item above ground).")]
        public float YOffset3D = 0f;

        [Header("Livability Stats")]
        [Range(0, 10)] public int Food;
        [Range(0, 10)] public int Water;
        [Range(0, 10)] public int Shelter;
        [Range(0, 10)] public int Light;
        [Range(0, 10)] public int Safety;
        [Range(0, 10)] public int Weather;
        [Range(0, 10)] public int Stability;
        [Range(-5, 5)] public int Space;

        [Header("Auto-Tiling")]
        [Tooltip("Optional group ID for auto-tiling. Items with the same TileCategory blend edges together (e.g. \"dirt\" for Dirt, Barren Dry, Patchy Dirt). Leave empty to tile by ItemId only.")]
        public string TileCategory;

        [Header("Placement")]
        [Tooltip("Radius of cells this item occupies (0 = single cell, 1 = 3x3).")]
        [Range(0, 3)] public int OccupyRadius = 0;

        public BubblePlacementDefinition ToDefinition()
        {
            return new BubblePlacementDefinition(
                Id, DisplayName, Tab, Layer, TintColor, IsObject,
                Food, Water, Shelter, Light, Safety, Weather, Stability, Space, OccupyRadius,
                TileCategory);
        }
    }
}
