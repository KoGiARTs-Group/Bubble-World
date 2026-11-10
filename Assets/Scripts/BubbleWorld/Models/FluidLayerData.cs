using System;

namespace BubbleWorld
{
    [Serializable]
    public struct FluidLayerData
    {
        public FluidType FluidType;
        public float FluidDepth;
        public float FlowRate;
        public float SourceStrength;

        public bool NorthConnection;
        public bool EastConnection;
        public bool SouthConnection;
        public bool WestConnection;

        public int ConnectionMask =>
            (NorthConnection ? 1 : 0) |
            (EastConnection ? 2 : 0) |
            (SouthConnection ? 4 : 0) |
            (WestConnection ? 8 : 0);

        public static FluidLayerData None => new FluidLayerData
        {
            FluidType = FluidType.None,
            FluidDepth = 0f,
            FlowRate = 0f,
            SourceStrength = 0f
        };

        public static FluidLayerData Flowing(float depth, float rate)
        {
            return new FluidLayerData
            {
                FluidType = FluidType.FlowingWater,
                FluidDepth = depth,
                FlowRate = rate,
                SourceStrength = 1f
            };
        }
    }
}
