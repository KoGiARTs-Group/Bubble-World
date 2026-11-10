using System;

namespace BubbleWorld
{
    [Serializable]
    public struct TileState
    {
        public float CurrentMoisture;
        public float SurfaceWaterAmount;
        public float CurrentTemperature;
        public float FrozenWaterAmount;
        public float CurrentFertility;
        public float ErosionDamage;
        public float ContaminationLevel;
        public FireState FireState;
        public int ResourceRemaining;
        public string OccupantID;

        public int WaterAccumulation;
        public int SunAccumulation;
        public int EarthAccumulation;
        public int ConsecutiveExcessDays;
        public int DaysSinceLastCheck;
        public float GrowthPotential;

        public bool IsExcessWater => WaterAccumulation >= 12;
        public bool IsExcessSun => SunAccumulation >= 12;
        public bool IsExcessEarth => EarthAccumulation >= 12;
        public bool HasVisibleFluid => SurfaceWaterAmount > 0.5f;

        public static TileState Default(float temperature)
        {
            return new TileState
            {
                CurrentMoisture = 0.1f,
                SurfaceWaterAmount = 0f,
                CurrentTemperature = temperature,
                FrozenWaterAmount = 0f,
                CurrentFertility = 0.3f,
                ErosionDamage = 0f,
                ContaminationLevel = 0f,
                FireState = FireState.None,
                ResourceRemaining = 0,
                OccupantID = null,
                WaterAccumulation = 0,
                SunAccumulation = 0,
                EarthAccumulation = 0,
                ConsecutiveExcessDays = 0,
                DaysSinceLastCheck = 0,
                GrowthPotential = 0f
            };
        }
    }
}
