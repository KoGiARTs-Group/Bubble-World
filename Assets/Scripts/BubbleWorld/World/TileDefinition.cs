using System;
using UnityEngine;

namespace BubbleWorld
{
    public enum TileGroupCategory
    {
        Ground,
        Slopes,
        Rounded,
        Water
    }

    public enum CollisionType
    {
        None,
        Box,
        Mesh
    }

    public enum SurfaceType
    {
        Dirt,
        Grass,
        Rock,
        Water,
        Snow,
        Sand
    }

    [Serializable]
    public sealed class TileDefinition
    {
        public int Id;
        public string Name;
        public TileGroupCategory Category;
        public bool Walkable = true;
        public float Height = 1f;
        public int[] WaterConnections;
        public float MoistureModifier;
        public CollisionType Collision;
        public SurfaceType Surface;
        public GameObject[] PrefabVariants;
    }
}
