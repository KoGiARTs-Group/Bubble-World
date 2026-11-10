using UnityEngine;

namespace BubbleWorld
{
    public sealed class SoulformCreationData
    {
        public string Name = "";
        public string Meaning = "";

        public string BodyTypeId = "average";
        public string HeightId = "average";
        public string FaceShapeId = "default";
        public string EyesId = "default";
        public string EarsId = "default";
        public string NoseId = "default";

        public Color SkinTone = SkinTonePresets[2];
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

        public SoulformTemperament Temperament = SoulformTemperament.Inquisitive;
        public string Nature = "Explorer";

        public string OutfitId = "";
        public string TopId = "";
        public string BottomsId = "";
        public string FootwearId = "";
        public string AccessoryId = "";

        public float StartingAffinity = 0.5f;
        public string InitialCalling = "";
        public StarterEnvironment PreferredBiome = StarterEnvironment.MeadowGrassland;
        public string[] BondTraits = System.Array.Empty<string>();

        public static readonly Color[] SkinTonePresets = new Color[]
        {
            new Color(1f,    0.87f, 0.73f),
            new Color(1f,    0.80f, 0.64f),
            new Color(0.96f, 0.73f, 0.53f),
            new Color(0.82f, 0.57f, 0.38f),
            new Color(0.60f, 0.40f, 0.26f),
            new Color(0.40f, 0.26f, 0.16f),
            new Color(0.26f, 0.16f, 0.10f),
            new Color(0.15f, 0.10f, 0.06f),
        };

        public ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return new ValidationResult(false, "Name is required", 0);

            return new ValidationResult(true, null, null);
        }

        public SoulformModel ToRuntimeModel()
        {
            var model = SoulformModel.CreateDefault(Temperament);
            model.Name = Name;
            model.Nature = Nature;
            model.AppearanceId = BodyTypeId;
            return model;
        }
    }

    public sealed class ValidationResult
    {
        public bool IsValid;
        public string ErrorMessage;
        public int? FailedCategoryIndex;

        public ValidationResult(bool valid, string error, int? category)
        {
            IsValid = valid;
            ErrorMessage = error;
            FailedCategoryIndex = category;
        }
    }
}
