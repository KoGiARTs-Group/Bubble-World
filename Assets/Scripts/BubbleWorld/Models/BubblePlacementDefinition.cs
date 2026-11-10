using UnityEngine;

namespace BubbleWorld
{
    public sealed class BubblePlacementDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly BuildTab Tab;
        public readonly LayerBand Layer;
        public readonly Color Color;
        public readonly bool IsObject;
        public readonly int Food;
        public readonly int Water;
        public readonly int Shelter;
        public readonly int Light;
        public readonly int Safety;
        public readonly int Weather;
        public readonly int Stability;
        public readonly int Space;
        public readonly int OccupyRadius;
        public readonly string TileCategory;
        public readonly TileVariant? FixedVariant;

        public BubblePlacementDefinition(
            string id,
            string displayName,
            BuildTab tab,
            LayerBand layer,
            Color color,
            bool isObject,
            int food,
            int water,
            int shelter,
            int light,
            int safety,
            int weather,
            int stability,
            int space,
            int occupyRadius = 0,
            string tileCategory = null,
            TileVariant? fixedVariant = null)
        {
            Id = id;
            DisplayName = displayName;
            Tab = tab;
            Layer = layer;
            Color = color;
            IsObject = isObject;
            Food = food;
            Water = water;
            Shelter = shelter;
            Light = light;
            Safety = safety;
            Weather = weather;
            Stability = stability;
            Space = space;
            OccupyRadius = occupyRadius;
            TileCategory = tileCategory ?? id;
            FixedVariant = fixedVariant;
        }
    }
}
