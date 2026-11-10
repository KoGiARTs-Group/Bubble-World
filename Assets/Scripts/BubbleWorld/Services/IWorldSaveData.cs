namespace BubbleWorld
{
    public interface IWorldSaveData
    {
        string SaveKey { get; }
        string Serialize();
        void Deserialize(string data);
    }
}
