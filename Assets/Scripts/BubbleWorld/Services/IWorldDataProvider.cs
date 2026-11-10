using System.Collections.Generic;

namespace BubbleWorld
{
    public interface IWorldDataProvider
    {
        TileComposition GetTileComposition(int x, int y, int z);
        void SetTileComposition(int x, int y, int z, TileComposition composition);

        TileState GetTileState(int x, int y, int z);
        void SetTileState(int x, int y, int z, TileState state);

        WeatherType GetWeather();
        void SetWeather(WeatherType weather);

        SeasonType GetSeason();

        EntityData GetEntity(string id);
        IReadOnlyList<EntityData> GetAllEntities();

        SoulformModel GetSoulform();
        ArchitectPoints GetAP();

        ActionValidationResult PushAction(SoulformActionCommand action);

        BubbleModel GetBubbleModel();
        BubbleCell GetCell(int x, int y, int z);

        bool IsInside(int x, int y, int z);
    }

    public sealed class ActionValidationResult
    {
        public bool Success;
        public string Error;
        public SoulformActionCommand Command;

        public static ActionValidationResult Ok(SoulformActionCommand cmd)
            => new ActionValidationResult { Success = true, Command = cmd };

        public static ActionValidationResult Fail(string error)
            => new ActionValidationResult { Success = false, Error = error };
    }
}
