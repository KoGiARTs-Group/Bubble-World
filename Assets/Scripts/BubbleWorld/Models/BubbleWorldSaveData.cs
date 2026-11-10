using System;
using System.Collections.Generic;

namespace BubbleWorld
{
    [Serializable]
    public sealed class BubbleWorldSaveData
    {
        public string saveVersion = "2.0";
        public string worldName;
        public string environmentType;
        public int gridWidth;
        public int gridHeight;
        public int gridDepth;
        public string lastSaveDate;

        public List<BubbleCellSaveData> cells;

        public int livabilityPercent;
        public string weatherCondition;

        public SoulformSaveData soulform;

        public string utcLastSave;
        public string utcLastLoad;
        public float elapsedGameTime;
        public int dayCount = 1;
        public int currentSeason;
        public int tutorialDaysRemaining = 7;

        public int architectPoints = 100;
        public string lastPassiveAPTime;

        public string skillData;
        public string memoryChapters;

        public List<TileCompositionSaveData> tileCompositions;
        public List<TileStateSaveData> tileStates;
        public List<CooldownSaveData> cooldowns;
    }

    [Serializable]
    public sealed class BubbleCellSaveData
    {
        public int x;
        public int y;
        public int z;
        public string itemId;
        public int layer;
        public bool isObject;
        public int rotation;
        public int autoTileVariant;
        public bool fromIntervention;
        public string groundItemId;
    }

    [Serializable]
    public sealed class TileCompositionSaveData
    {
        public int cellIndex;
        public int baseMaterial;
        public int surfaceCover;
        public int fluidLayer;
        public float fluidDepth;
        public float flowRate;
        public float sourceStrength;
        public bool northConnection;
        public bool eastConnection;
        public bool southConnection;
        public bool westConnection;
    }

    [Serializable]
    public sealed class TileStateSaveData
    {
        public int cellIndex;
        public float currentMoisture;
        public float surfaceWaterAmount;
        public float currentTemperature;
        public float frozenWaterAmount;
        public float currentFertility;
        public float erosionDamage;
        public float contaminationLevel;
        public int fireState;
        public int resourceRemaining;
        public string occupantId;
        public int waterAccumulation;
        public int sunAccumulation;
        public int earthAccumulation;
        public int consecutiveExcessDays;
        public int daysSinceLastCheck;
    }

    [Serializable]
    public sealed class CooldownSaveData
    {
        public string key;
        public float remaining;
    }

    [Serializable]
    public sealed class SoulformSaveData
    {
        public string name;
        public string meaning;

        public string bodyTypeId;
        public string heightId;
        public string faceShapeId;
        public string eyesId;
        public string earsId;
        public string noseId;

        public float skinToneR;
        public float skinToneG;
        public float skinToneB;
        public string markingsId;
        public string skinDetailsId;

        public string hairStyleId;
        public float hairColorR;
        public float hairColorG;
        public float hairColorB;
        public string hairMaterialId;
        public string facialHairId;

        public string voicePresetId;
        public float voicePitch;
        public float voiceTone;
        public float voiceTimbre;

        public string temperament;

        public string outfitId;
        public string topId;
        public string bottomsId;
        public string footwearId;
        public string accessoryId;

        public float startingAffinity;
        public string initialCalling;
        public string preferredBiome;
        public string[] bondTraits;

        public int gridX;
        public int gridY;
        public int gridZ;
        public string mood;
        public int comfort;
        public int hunger;
        public int curiosity;
        public string currentActivity;
        public string currentThought;
    }
}
