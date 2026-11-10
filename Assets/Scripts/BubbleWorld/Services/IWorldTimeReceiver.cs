namespace BubbleWorld
{
    public interface IWorldTimeReceiver
    {
        void OnGameTick(float deltaSeconds, WorldTimeContext context);
        void OnFastForward(float totalMissedSeconds, WorldTimeContext context);
    }

    public sealed class WorldTimeContext
    {
        public float ElapsedGameTime;
        public int DayCount;
        public int Hour;
        public int Minute;
        public SeasonType Season;
        public bool IsOffline;
        public bool IsPaused;
    }
}
