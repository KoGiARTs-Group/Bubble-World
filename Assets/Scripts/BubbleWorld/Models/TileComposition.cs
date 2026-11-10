using System;

namespace BubbleWorld
{
    [Serializable]
    public struct TileComposition
    {
        public BaseMaterial BaseMaterial;
        public SurfaceCover SurfaceCover;
        public FluidType FluidLayer;
        public FluidLayerData FluidData;
        public bool IsHalfTile;
    }
}
