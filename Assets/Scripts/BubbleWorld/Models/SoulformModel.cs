using UnityEngine;

namespace BubbleWorld
{
    public sealed class SoulformModel
    {
        public string Name = "Soulform";
        public string Meaning = "";
        public string Nature = "Explorer";
        public SoulformTemperament Temperament = SoulformTemperament.Inquisitive;
        public SoulformMood Mood = SoulformMood.Content;
        public int GridX;
        public int GridY;
        public int GridZ;

        public int Comfort = 50;
        public int Hunger = 50;
        public int Curiosity = 50;
        public float Health = 100f;

        public string CurrentActivity = "Observing";
        public string CurrentThought = "Taking in the surroundings.";
        public string CurrentExpression = "Neutral";
        public string CurrentAnimation = "Idle";

        public string AppearanceId = "default";

        public string BodyTypeId = "average";
        public string HeightId = "average";
        public string FaceShapeId = "default";
        public string EyesId = "default";
        public string EarsId = "default";
        public string NoseId = "default";

        public Color SkinTone = Color.white;
        public string MarkingsId = "none";
        public string SkinDetailsId = "none";

        public string HairStyleId = "default";
        public Color HairColor = new Color(0.3f, 0.2f, 0.15f);
        public string HairMaterialId = "default";
        public string FacialHairId = "none";

        public string VoicePresetId = "default";
        public float VoicePitch = 1f;
        public float VoiceTone = 1f;
        public float VoiceTimbre = 1f;

        public string OutfitId = "";
        public string TopId = "";
        public string BottomsId = "";
        public string FootwearId = "";
        public string AccessoryId = "";

        public float StartingAffinity = 0.5f;
        public string InitialCalling = "";
        public StarterEnvironment PreferredBiome = StarterEnvironment.MeadowGrassland;
        public string[] BondTraits = System.Array.Empty<string>();

        public static SoulformModel CreateDefault(SoulformTemperament temperament)
        {
            return new SoulformModel
            {
                Name = "Soulform",
                Temperament = temperament,
                Mood = SoulformMood.Content,
                GridX = 12,
                GridY = 0,
                GridZ = 12,
                CurrentThought = InitialThought(temperament)
            };
        }

        public static SoulformModel FromCreationData(SoulformCreationData data)
        {
            var model = CreateDefault(data.Temperament);
            model.Name = data.Name;
            model.Meaning = data.Meaning;
            model.AppearanceId = data.BodyTypeId;

            model.BodyTypeId = data.BodyTypeId;
            model.HeightId = data.HeightId;
            model.FaceShapeId = data.FaceShapeId;
            model.EyesId = data.EyesId;
            model.EarsId = data.EarsId;
            model.NoseId = data.NoseId;

            model.SkinTone = data.SkinTone;
            model.MarkingsId = data.MarkingsId;
            model.SkinDetailsId = data.SkinDetailsId;

            model.HairStyleId = data.HairStyleId;
            model.HairColor = data.HairColor;
            model.HairMaterialId = data.HairMaterialId;
            model.FacialHairId = data.FacialHairId;

            model.VoicePresetId = data.VoicePresetId;
            model.VoicePitch = data.VoicePitch;
            model.VoiceTone = data.VoiceTone;
            model.VoiceTimbre = data.VoiceTimbre;

            model.OutfitId = data.OutfitId;
            model.TopId = data.TopId;
            model.BottomsId = data.BottomsId;
            model.FootwearId = data.FootwearId;
            model.AccessoryId = data.AccessoryId;

            model.StartingAffinity = data.StartingAffinity;
            model.InitialCalling = data.InitialCalling;
            model.PreferredBiome = data.PreferredBiome;
            model.BondTraits = data.BondTraits;

            return model;
        }

        private static string InitialThought(SoulformTemperament t)
        {
            switch (t)
            {
                case SoulformTemperament.Inquisitive: return "What is this place? I want to explore every inch.";
                case SoulformTemperament.Creative: return "This world has potential. I can shape it.";
                case SoulformTemperament.Harmonious: return "Everything here feels connected. I belong.";
                case SoulformTemperament.Resilient: return "No matter what comes, I will adapt.";
                case SoulformTemperament.Assertive: return "I will make this world my own.";
                default: return "I am here.";
            }
        }
    }
}
