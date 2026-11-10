using System;

namespace BubbleWorld
{
    [Serializable]
    public sealed class BubbleCell
    {
        public int X;
        public int Z;
        public int Y;
        public string ItemId;
        public LayerBand Layer;
        public bool IsObject;
        public int Rotation;
        public TileVariant AutoTileVariant = TileVariant.Center;
        public bool FromIntervention;
        public string GroundItemId;
        public string TileCategory;
        public HeightMode TileHeight = HeightMode.Full;

        public TileComposition Composition;
        public TileState State;

        public bool HasTileData => Composition.BaseMaterial != BaseMaterial.None;

        public BubbleCell(int x, int y, int z, BubblePlacementDefinition item)
        {
            X = x;
            Y = y;
            Z = z;
            ItemId = item != null ? item.Id : null;
            Layer = item != null ? item.Layer : LayerBand.GroundWaterBase;
            IsObject = item != null && item.IsObject;
            TileCategory = item != null ? item.TileCategory : null;
            TileHeight = HeightMode.Full;
            Rotation = 0;
        }

        public BubbleCell Clone()
        {
            BubbleCell c = new BubbleCell(X, Y, Z, null);
            c.ItemId = ItemId;
            c.Layer = Layer;
            c.IsObject = IsObject;
            c.Rotation = Rotation;
            c.AutoTileVariant = AutoTileVariant;
            c.FromIntervention = FromIntervention;
            c.GroundItemId = GroundItemId;
            c.TileCategory = TileCategory;
            c.TileHeight = TileHeight;
            c.Composition = Composition;
            c.State = State;
            return c;
        }
    }
}
