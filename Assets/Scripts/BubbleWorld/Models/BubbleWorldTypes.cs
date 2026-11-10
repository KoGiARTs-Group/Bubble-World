namespace BubbleWorld
{
    public enum StarterEnvironment
    {
        MeadowGrassland,
        Forest,
        BeachCoastal
    }

    public enum BuildTab
    {
        Terrain,
        Water,
        Plants,
        Resources,
        Climate,
        Interventions
    }

    public enum BrushSize
    {
        One = 1,
        Three = 3,
        Five = 5
    }

    public enum LayerBand
    {
        Underground = -1,
        GroundWaterBase = 0,
        LivingEnvironment = 1,
        ElevatedTerrain = 2,
        SkyAtmosphere = 3
    }

    public enum Mesh3DPrimitive
    {
        Cube,
        Sphere,
        Cylinder,
        Capsule,
        Plane,
        Quad
    }

    public enum Camera3DView
    {
        Top,
        Angle45,
        FollowSoulform,
        Free
    }

    public enum EditorTool
    {
        Select,
        MoveView,
        AddPlace,
        Delete,
        Rotate,
        Layers,
        Settings,
        Help,
        Watch
    }

    public enum UIScreenId
    {
        Title,
        StarterSelection,
        Editor,
        SoulformCreation,
        LiveWatch,
        SoulformChat,
        SoulformProfile
    }

    public enum SoulformTemperament
    {
        Inquisitive,
        Creative,
        Harmonious,
        Resilient,
        Assertive
    }

    public enum SoulformMood
    {
        Content,
        Curious,
        Hungry,
        Tired,
        Inspired,
        Distressed
    }

    public enum WatchSpeed
    {
        Pause = 0,
        Normal = 1,
        Double = 2,
        Quad = 4,
        Ten = 10
    }

    public enum GameEventType
    {
        SoulformFoundFood,
        RainStarted,
        InterventionUsed,
        NpcSpawned,
        AnimalSpawned,
        DayBegan,
        SoulformReacted,
        SkillLearned,
        TutorialPrompt
    }

    public enum InterventionType
    {
        AddFood,
        GrowTree,
        ChangeWeather,
        DayNight,
        SpawnStructure,
        SpawnAnimal,
        SpawnNpc,
        RemindSoulform
    }

    public enum HeightMode
    {
        Full,
        Half
    }

    public enum TileVariant
    {
        Center,
        EdgeNorth,
        EdgeSouth,
        EdgeEast,
        EdgeWest,
        OuterCornerNE,
        OuterCornerNW,
        OuterCornerSE,
        OuterCornerSW,
        InnerCornerNE,
        InnerCornerNW,
        InnerCornerSE,
        InnerCornerSW,
        Isolated,
        WaterStraight,
        WaterCorner,
        WaterTee,
        WaterCross,

        HalfCenter,
        HalfIsolated,
        HalfEdgeNorth,
        HalfEdgeSouth,
        HalfEdgeEast,
        HalfEdgeWest,
        HalfOuterCornerNE,
        HalfOuterCornerNW,
        HalfOuterCornerSE,
        HalfOuterCornerSW,
        HalfInnerCornerNE,
        HalfInnerCornerNW,
        HalfInnerCornerSE,
        HalfInnerCornerSW,

        FullToHalfEdgeNorth,
        FullToHalfEdgeSouth,
        FullToHalfEdgeEast,
        FullToHalfEdgeWest,
        FullToHalfOuterCornerNE,
        FullToHalfOuterCornerNW,
        FullToHalfOuterCornerSE,
        FullToHalfOuterCornerSW,
        FullToHalfInnerCornerNE,
        FullToHalfInnerCornerNW,
        FullToHalfInnerCornerSE,
        FullToHalfInnerCornerSW,
        HalfVerticalFront,
        HalfVerticalBack,
        HalfVerticalLeft,
        HalfVerticalRight,
        RoundedEdgeNorth,
        RoundedEdgeSouth,
        RoundedEdgeEast,
        RoundedEdgeWest,
    }

    public enum SeasonType
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    public enum WeatherType
    {
        Clear,
        Cloudy,
        Sunny,
        Rain,
        Snow,
        Windy,
        Random,
        Storm1,
        Storm2,
        Storm3
    }

    public enum AnimalDiet
    {
        Carnivore,
        Herbivore,
        Omnivore
    }

    public enum ExcessType
    {
        Water,
        Sun,
        Earth,
        Perfect
    }

    public enum ScarcityResponse
    {
        Cooperate,
        Fight,
        Migrate,
        Theft,
        Starvation,
        Leadership
    }

    public enum BaseMaterial
    {
        None,
        Earth,
        Sand,
        Clay,
        Rock,
        PackedSnow,
        Ice,
        Stone
    }

    public enum SurfaceCover
    {
        None,
        Grass,
        BareDirt,
        Mud,
        DryGrass,
        Snow,
        Puddle,
        HalfSnow,
        HalfIce,
        Stream
    }

    public enum FluidType
    {
        None,
        StandingWater,
        FlowingWater,
        ShallowWater,
        DeepWater
    }

    public enum FireState
    {
        None,
        Smoldering,
        Burning,
        Extinguished
    }
}
