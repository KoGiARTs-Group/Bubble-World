using System.Collections.Generic;

namespace BubbleWorld
{
    public sealed class LocalWorldDataProvider : IWorldDataProvider
    {
        private readonly BubbleModel bubble;
        private readonly WeatherManager weather;
        private readonly EntityManager entityManager;
        private readonly SoulformModel soulform;
        private readonly ArchitectPoints ap;
        private readonly ActionValidatorWrapper validator;

        public LocalWorldDataProvider(BubbleModel bubble, WeatherManager weather,
            EntityManager entityManager, SoulformModel soulform, ArchitectPoints ap)
        {
            this.bubble = bubble;
            this.weather = weather;
            this.entityManager = entityManager;
            this.soulform = soulform;
            this.ap = ap;
            this.validator = new ActionValidatorWrapper();
        }

        public TileComposition GetTileComposition(int x, int y, int z)
        {
            var cell = bubble.Get(x, y, z);
            return cell != null ? cell.Composition : default;
        }

        public void SetTileComposition(int x, int y, int z, TileComposition composition)
        {
            var cell = bubble.Get(x, y, z);
            if (cell != null) cell.Composition = composition;
        }

        public TileState GetTileState(int x, int y, int z)
        {
            var cell = bubble.Get(x, y, z);
            return cell != null ? cell.State : default;
        }

        public void SetTileState(int x, int y, int z, TileState state)
        {
            var cell = bubble.Get(x, y, z);
            if (cell != null) cell.State = state;
        }

        public WeatherType GetWeather() => weather?.CurrentWeather ?? WeatherType.Clear;
        public void SetWeather(WeatherType weatherType)
        {
            weather?.SetWeatherDirectly(weatherType, weather.CurrentSeason);
        }

        public SeasonType GetSeason() => weather?.CurrentSeason ?? SeasonType.Spring;

        public EntityData GetEntity(string id) => entityManager?.GetEntity(id);
        public IReadOnlyList<EntityData> GetAllEntities() => entityManager?.GetAllEntities() ?? new List<EntityData>().AsReadOnly();

        public SoulformModel GetSoulform() => soulform;
        public ArchitectPoints GetAP() => ap;

        public ActionValidationResult PushAction(SoulformActionCommand action)
        {
            if (soulform == null || bubble == null)
                return ActionValidationResult.Fail("No soulform or bubble");

            var validated = ActionValidator.Validate(action, bubble,
                soulform.GridX, soulform.GridY, soulform.GridZ);

            if (!validated.IsValid)
                return ActionValidationResult.Fail(validated.ValidationError);

            return ActionValidationResult.Ok(validated);
        }

        public BubbleModel GetBubbleModel() => bubble;
        public BubbleCell GetCell(int x, int y, int z) => bubble?.Get(x, y, z);

        public bool IsInside(int x, int y, int z) => bubble?.IsInside(x, y, z) ?? false;
    }

    internal sealed class ActionValidatorWrapper { }
}
