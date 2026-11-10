using System;

namespace BubbleWorld
{
    public sealed class SoulformCreationController
    {
        public static readonly string[] CategoryNames =
        {
            "Identity", "Form", "Skin", "Hair", "Voice",
            "Temperament", "Clothing", "World Bond"
        };

        public SoulformCreationData Data { get; private set; }
        public int CurrentCategoryIndex { get; private set; }
        public string CurrentCategoryName => CategoryNames[CurrentCategoryIndex];
        public int CategoryCount => CategoryNames.Length;

        public event Action<int> OnCategoryChanged;
        public event Action<string, object> OnAppearanceChanged;
        public event Action<SoulformTemperament> OnTemperamentChanged;
        public event Action<string, string> OnClothingChanged;
        public event Action<string, float> OnVoiceChanged;
        public event Action<string, object> OnWorldBondChanged;
        public event Action OnPreviewUpdated;
        public event Action<bool, string> OnCreationValidated;
        public event Action<SoulformModel> OnCreationCompleted;
        public event Action OnBackPressed;

        private static SoulformCreationData savedProgress;

        public SoulformCreationController()
        {
            Data = new SoulformCreationData();
        }

        public void Initialize(SoulformCreationData existing = null)
        {
            Data = existing ?? new SoulformCreationData();
            CurrentCategoryIndex = 0;
        }

        public void SetCategory(int index)
        {
            if (index < 0 || index >= CategoryNames.Length) return;
            CurrentCategoryIndex = index;
            OnCategoryChanged?.Invoke(index);
        }

        public void SetName(string name)
        {
            Data.Name = name ?? "";
            OnAppearanceChanged?.Invoke("Name", Data.Name);
        }

        public void SetMeaning(string meaning)
        {
            Data.Meaning = meaning ?? "";
            OnAppearanceChanged?.Invoke("Meaning", Data.Meaning);
        }

        public void SetAppearance(string field, object value)
        {
            switch (field)
            {
                case "BodyTypeId": Data.BodyTypeId = (string)value; break;
                case "HeightId": Data.HeightId = (string)value; break;
                case "FaceShapeId": Data.FaceShapeId = (string)value; break;
                case "EyesId": Data.EyesId = (string)value; break;
                case "EarsId": Data.EarsId = (string)value; break;
                case "NoseId": Data.NoseId = (string)value; break;
                case "SkinTone": Data.SkinTone = (UnityEngine.Color)value; break;
                case "MarkingsId": Data.MarkingsId = (string)value; break;
                case "SkinDetailsId": Data.SkinDetailsId = (string)value; break;
                case "HairStyleId": Data.HairStyleId = (string)value; break;
                case "HairColor": Data.HairColor = (UnityEngine.Color)value; break;
                case "HairMaterialId": Data.HairMaterialId = (string)value; break;
                case "FacialHairId": Data.FacialHairId = (string)value; break;
                default: return;
            }
            OnAppearanceChanged?.Invoke(field, value);
            OnPreviewUpdated?.Invoke();
        }

        public void SetVoicePreset(string presetId)
        {
            Data.VoicePresetId = presetId ?? "default";
            OnVoiceChanged?.Invoke("Preset", 0);
        }

        public void SetVoiceFloat(string property, float value)
        {
            switch (property)
            {
                case "Pitch": Data.VoicePitch = UnityEngine.Mathf.Clamp(value, 0.5f, 2f); break;
                case "Tone": Data.VoiceTone = UnityEngine.Mathf.Clamp(value, 0.5f, 2f); break;
                case "Timbre": Data.VoiceTimbre = UnityEngine.Mathf.Clamp(value, 0.5f, 2f); break;
                default: return;
            }
            OnVoiceChanged?.Invoke(property, Data.VoicePitch);
        }

        public void SetTemperament(SoulformTemperament temperament)
        {
            Data.Temperament = temperament;
            OnTemperamentChanged?.Invoke(temperament);
            OnPreviewUpdated?.Invoke();
        }

        public void SetClothing(string slot, string itemId)
        {
            switch (slot)
            {
                case "Outfit": Data.OutfitId = itemId ?? ""; break;
                case "Tops": Data.TopId = itemId ?? ""; break;
                case "Bottoms": Data.BottomsId = itemId ?? ""; break;
                case "Footwear": Data.FootwearId = itemId ?? ""; break;
                case "Accessories": Data.AccessoryId = itemId ?? ""; break;
                default: return;
            }
            OnClothingChanged?.Invoke(slot, itemId ?? "");
            OnPreviewUpdated?.Invoke();
        }

        public void SetStartingAffinity(float value)
        {
            Data.StartingAffinity = UnityEngine.Mathf.Clamp01(value);
            OnWorldBondChanged?.Invoke("StartingAffinity", Data.StartingAffinity);
        }

        public void SetInitialCalling(string calling)
        {
            Data.InitialCalling = calling ?? "";
            OnWorldBondChanged?.Invoke("InitialCalling", Data.InitialCalling);
        }

        public void SetPreferredBiome(StarterEnvironment biome)
        {
            Data.PreferredBiome = biome;
            OnWorldBondChanged?.Invoke("PreferredBiome", biome);
        }

        public void SetBondTraits(string[] traits)
        {
            Data.BondTraits = traits ?? Array.Empty<string>();
            OnWorldBondChanged?.Invoke("BondTraits", Data.BondTraits);
        }

        public bool Validate()
        {
            var result = Data.Validate();
            OnCreationValidated?.Invoke(result.IsValid, result.ErrorMessage);
            return result.IsValid;
        }

        public void SaveAndContinue()
        {
            if (!Validate()) return;
            var model = Data.ToRuntimeModel();
            SaveProgress();
            OnCreationCompleted?.Invoke(model);
        }

        public void NotifyPreviewUpdated()
        {
            OnPreviewUpdated?.Invoke();
        }

        public void GoBack()
        {
            OnBackPressed?.Invoke();
        }

        public void SaveProgress()
        {
            savedProgress = Data;
        }

        public bool RestoreProgress()
        {
            if (savedProgress != null)
            {
                Data = savedProgress;
                return true;
            }
            return false;
        }

        public static void ClearSavedProgress()
        {
            savedProgress = null;
        }
    }
}
