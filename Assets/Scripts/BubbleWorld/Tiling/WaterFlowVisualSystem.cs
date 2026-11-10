using UnityEngine;

namespace BubbleWorld
{
    public static class WaterFlowVisualSystem
    {
        public static TileVariant GetWaterVariant(FluidLayerData fluid)
        {
            int mask = fluid.ConnectionMask;
            int count = CountBits(mask);

            switch (count)
            {
                case 0:
                    return TileVariant.Center;
                case 1:
                case 2 when mask == 5 || mask == 10:
                    return TileVariant.WaterStraight;
                case 2:
                    return TileVariant.WaterCorner;
                case 3:
                    return TileVariant.WaterTee;
                case 4:
                    return TileVariant.WaterCross;
                default:
                    return TileVariant.Center;
            }
        }

        public static void ApplyToCell(BubbleCell cell)
        {
            if (cell.Composition.FluidData.FluidType == FluidType.None)
                return;
            cell.AutoTileVariant = GetWaterVariant(cell.Composition.FluidData);
        }

        private static int CountBits(int v)
        {
            int c = 0;
            while (v != 0) { c += v & 1; v >>= 1; }
            return c;
        }
    }
}
