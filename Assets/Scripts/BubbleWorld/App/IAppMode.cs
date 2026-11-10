namespace BubbleWorld
{
    public interface IAppMode
    {
        void OnEnter(BubbleWorldApp app);
        void OnUpdate(BubbleWorldApp app);
        void OnExit(BubbleWorldApp app);
    }
}
