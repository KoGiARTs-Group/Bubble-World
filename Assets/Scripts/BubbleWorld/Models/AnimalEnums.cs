namespace BubbleWorld
{
    public enum AnimalState
    {
        Docile,
        Hungry,
        Hunting,
        Foraging,
        Socializing,
        Fleeing,
        Attacking
    }

    public enum NPCBaselineState
    {
        Busy,
        Socializing,
        Working
    }

    public enum NPCCurrentState
    {
        SafeHungry,
        SafeStarving,
        SafeLonely,
        SafeBored,
        HasFoodShelter,
        Threatened,
        NeedsShelter
    }

    public enum NPCResponseAction
    {
        Forage,
        Wait,
        Steal,
        Fight,
        Ask,
        Hunt,
        None,
        Socialize,
        LookForRelationship,
        PlayGames,
        Read,
        Sing,
        PlayInstrument,
        Share,
        Hide,
        Flee,
        FleeForHelp,
        Take,
        Build
    }
}
