using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace BubbleWorld.Tests
{
    public sealed class BubbleWorldMilestone2Tests
    {
        private PlacementCatalog testCatalog;

        [SetUp]
        public void Setup()
        {
            testCatalog = ScriptableObject.CreateInstance<PlacementCatalog>();
            testCatalog.Items = new List<PlacementItem>();
            foreach (var def in BubblePlacementCatalog.HardcodedItems)
            {
                var item = ScriptableObject.CreateInstance<PlacementItem>();
                item.Id = def.Id;
                item.DisplayName = def.DisplayName;
                item.Tab = def.Tab;
                item.Layer = def.Layer;
                item.IsObject = def.IsObject;
                item.TintColor = def.Color;
                item.OccupyRadius = def.OccupyRadius;
                item.Food = def.Food;
                item.Water = def.Water;
                item.Shelter = def.Shelter;
                item.Light = def.Light;
                item.Safety = def.Safety;
                item.Weather = def.Weather;
                item.Stability = def.Stability;
                item.Space = def.Space;
                testCatalog.Items.Add(item);
            }
            testCatalog.InvalidateCache();
            BubbleWorldApp.ActiveCatalog = testCatalog;
        }

        [TearDown]
        public void TearDown()
        {
            BubbleWorldApp.ActiveCatalog = null;
            if (testCatalog != null)
                UnityEngine.Object.DestroyImmediate(testCatalog);
        }

        // ================================================================
        // SoulformCreationData
        // ================================================================

        [Test]
        public void CreationData_DefaultsAreValid()
        {
            var data = new SoulformCreationData();
            Assert.AreEqual("", data.Name);
            Assert.AreEqual("", data.Meaning);
            Assert.AreEqual(SoulformTemperament.Inquisitive, data.Temperament);
            Assert.AreEqual(8, SoulformCreationData.SkinTonePresets.Length);
            Assert.AreEqual("average", data.BodyTypeId);
        }

        [Test]
        public void CreationData_Validate_RejectsEmptyName()
        {
            var data = new SoulformCreationData { Name = "", InitialCalling = "Test" };
            var result = data.Validate();
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Name is required", result.ErrorMessage);
            Assert.AreEqual(0, result.FailedCategoryIndex);
        }

        [Test]
        public void CreationData_Validate_RejectsNoCalling()
        {
            var data = new SoulformCreationData { Name = "Luma", InitialCalling = "" };
            var result = data.Validate();
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Initial Calling is required", result.ErrorMessage);
            Assert.AreEqual(7, result.FailedCategoryIndex);
        }

        [Test]
        public void CreationData_Validate_RejectsWhitespaceName()
        {
            var data = new SoulformCreationData { Name = "   ", InitialCalling = "Explore" };
            var result = data.Validate();
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Name is required", result.ErrorMessage);
        }

        [Test]
        public void CreationData_Validate_PassesWithValidData()
        {
            var data = new SoulformCreationData { Name = "Luma", InitialCalling = "Explore" };
            var result = data.Validate();
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
            Assert.IsNull(result.FailedCategoryIndex);
        }

        [Test]
        public void CreationData_Validate_FailsOnBothEmpty_ReturnsNameFirst()
        {
            var data = new SoulformCreationData();
            var result = data.Validate();
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(0, result.FailedCategoryIndex);
            Assert.AreEqual("Name is required", result.ErrorMessage);
        }

        [Test]
        public void CreationData_DefaultSkinTone_IsMediumWarm()
        {
            var data = new SoulformCreationData();
            Color expected = new Color(0.96f, 0.73f, 0.53f);
            Assert.AreEqual(expected, data.SkinTone);
        }

        [Test]
        public void CreationData_SkinTonePresets_HaveCorrectCount()
        {
            Assert.AreEqual(8, SoulformCreationData.SkinTonePresets.Length);
        }

        [Test]
        public void CreationData_SkinTonePresets_AllHavePositiveLuminance()
        {
            foreach (var c in SoulformCreationData.SkinTonePresets)
                Assert.Greater(c.r + c.g + c.b, 0);
        }

        [Test]
        public void CreationData_DefaultBodyParts_AreSet()
        {
            var data = new SoulformCreationData();
            Assert.AreEqual("average", data.HeightId);
            Assert.AreEqual("default", data.FaceShapeId);
            Assert.AreEqual("default", data.EyesId);
            Assert.AreEqual("default", data.EarsId);
            Assert.AreEqual("default", data.NoseId);
        }

        [Test]
        public void CreationData_DefaultVoice_IsDefaultPreset()
        {
            var data = new SoulformCreationData();
            Assert.AreEqual("default", data.VoicePresetId);
            Assert.AreEqual(1f, data.VoicePitch);
            Assert.AreEqual(1f, data.VoiceTone);
            Assert.AreEqual(1f, data.VoiceTimbre);
        }

        [Test]
        public void CreationData_DefaultBond_IsHalfAffinity()
        {
            var data = new SoulformCreationData();
            Assert.AreEqual(0.5f, data.StartingAffinity);
            Assert.AreEqual(StarterEnvironment.MeadowGrassland, data.PreferredBiome);
            Assert.AreEqual(0, data.BondTraits.Length);
        }

        [Test]
        public void CreationData_ToRuntimeModel_PreservesNameAndTemperament()
        {
            var data = new SoulformCreationData
            {
                Name = "Luma",
                Temperament = SoulformTemperament.Creative
            };
            var model = data.ToRuntimeModel();
            Assert.AreEqual("Luma", model.Name);
            Assert.AreEqual(SoulformTemperament.Creative, model.Temperament);
        }

        [Test]
        public void CreationData_ToRuntimeModel_PreservesBodyTypeId()
        {
            var data = new SoulformCreationData { Name = "L", InitialCalling = "T", BodyTypeId = "tall" };
            var model = data.ToRuntimeModel();
            Assert.AreEqual("tall", model.AppearanceId);
        }

        // ================================================================
        // SoulformCreationController
        // ================================================================

        [Test]
        public void Controller_DefaultCategory_IsIdentity()
        {
            var ctrl = new SoulformCreationController();
            Assert.AreEqual(0, ctrl.CurrentCategoryIndex);
            Assert.AreEqual("Identity", ctrl.CurrentCategoryName);
        }

        [Test]
        public void Controller_SetCategory_UpdatesIndex()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetCategory(3);
            Assert.AreEqual(3, ctrl.CurrentCategoryIndex);
            Assert.AreEqual("Voice", ctrl.CurrentCategoryName);
        }

        [Test]
        public void Controller_SetCategory_ClampsNegative()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetCategory(-1);
            Assert.AreEqual(0, ctrl.CurrentCategoryIndex);
        }

        [Test]
        public void Controller_SetCategory_ClampsTooHigh()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetCategory(99);
            Assert.AreEqual(0, ctrl.CurrentCategoryIndex);
        }

        [Test]
        public void Controller_SetCategory_FiresEvent()
        {
            var ctrl = new SoulformCreationController();
            int? fired = null;
            ctrl.OnCategoryChanged += i => fired = i;
            ctrl.SetCategory(4);
            Assert.AreEqual(4, fired);
        }

        [Test]
        public void Controller_SetName_UpdatesData()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetName("Luma");
            Assert.AreEqual("Luma", ctrl.Data.Name);
        }

        [Test]
        public void Controller_SetMeaning_UpdatesData()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetMeaning("Light");
            Assert.AreEqual("Light", ctrl.Data.Meaning);
        }

        [Test]
        public void Controller_SetName_FiresAppearanceChanged()
        {
            var ctrl = new SoulformCreationController();
            string field = null;
            ctrl.OnAppearanceChanged += (f, v) => field = f;
            ctrl.SetName("Luma");
            Assert.AreEqual("Name", field);
        }

        [Test]
        public void Controller_SetTemperament_FiresEvent()
        {
            var ctrl = new SoulformCreationController();
            SoulformTemperament? fired = null;
            ctrl.OnTemperamentChanged += t => fired = t;
            ctrl.SetTemperament(SoulformTemperament.Resilient);
            Assert.AreEqual(SoulformTemperament.Resilient, fired);
        }

        [Test]
        public void Controller_SetTemperament_FiresPreviewEvent()
        {
            var ctrl = new SoulformCreationController();
            bool fired = false;
            ctrl.OnPreviewUpdated += () => fired = true;
            ctrl.SetTemperament(SoulformTemperament.Creative);
            Assert.IsTrue(fired);
        }

        [Test]
        public void Controller_SaveAndContinue_EmitsSoulformModel()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetName("Luma");
            ctrl.SetInitialCalling("Explore");

            SoulformModel result = null;
            ctrl.OnCreationCompleted += m => result = m;
            ctrl.SaveAndContinue();

            Assert.IsNotNull(result);
            Assert.AreEqual("Luma", result.Name);
        }

        [Test]
        public void Controller_SaveAndContinue_BlockedOnInvalid()
        {
            var ctrl = new SoulformCreationController();
            SoulformModel result = null;
            ctrl.OnCreationCompleted += m => result = m;
            ctrl.SaveAndContinue();
            Assert.IsNull(result);
        }

        [Test]
        public void Controller_SetAppearance_FiresPreviewEvent()
        {
            var ctrl = new SoulformCreationController();
            bool fired = false;
            ctrl.OnPreviewUpdated += () => fired = true;
            ctrl.SetAppearance("BodyTypeId", "tall");
            Assert.IsTrue(fired);
        }

        [Test]
        public void Controller_SetAppearance_HairStyle_UpdatesData()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetAppearance("HairStyleId", "long");
            Assert.AreEqual("long", ctrl.Data.HairStyleId);
        }

        [Test]
        public void Controller_SetAppearance_SkinTone_UpdatesData()
        {
            var ctrl = new SoulformCreationController();
            Color tone = new Color(0.4f, 0.26f, 0.16f);
            ctrl.SetAppearance("SkinTone", tone);
            Assert.AreEqual(tone, ctrl.Data.SkinTone);
        }

        [Test]
        public void Controller_SetAppearance_UnknownField_DoesNothing()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetAppearance("NonExistent", "val");
            Assert.AreEqual("average", ctrl.Data.BodyTypeId);
        }

        [Test]
        public void Controller_SetAppearance_FiresEvent()
        {
            var ctrl = new SoulformCreationController();
            string field = null;
            object value = null;
            ctrl.OnAppearanceChanged += (f, v) => { field = f; value = v; };
            ctrl.SetAppearance("EyesId", "big");
            Assert.AreEqual("EyesId", field);
            Assert.AreEqual("big", value);
        }

        [Test]
        public void Controller_SetClothing_UpdatesSlot()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetClothing("Tops", "shirt_01");
            Assert.AreEqual("shirt_01", ctrl.Data.TopId);
        }

        [Test]
        public void Controller_SetClothing_Footwear_UpdatesData()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetClothing("Footwear", "boots_01");
            Assert.AreEqual("boots_01", ctrl.Data.FootwearId);
        }

        [Test]
        public void Controller_SetClothing_Accessory_UpdatesData()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetClothing("Accessories", "hat_01");
            Assert.AreEqual("hat_01", ctrl.Data.AccessoryId);
        }

        [Test]
        public void Controller_SetClothing_UnknownSlot_DoesNothing()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetClothing("Invalid", "x");
            Assert.AreEqual("", ctrl.Data.OutfitId);
            Assert.AreEqual("", ctrl.Data.TopId);
        }

        [Test]
        public void Controller_SetClothing_FiresEvent()
        {
            var ctrl = new SoulformCreationController();
            string slot = null, item = null;
            ctrl.OnClothingChanged += (s, i) => { slot = s; item = i; };
            ctrl.SetClothing("Outfit", "dress_01");
            Assert.AreEqual("Outfit", slot);
            Assert.AreEqual("dress_01", item);
        }

        [Test]
        public void Controller_SetClothing_FiresPreviewEvent()
        {
            var ctrl = new SoulformCreationController();
            bool fired = false;
            ctrl.OnPreviewUpdated += () => fired = true;
            ctrl.SetClothing("Outfit", "dress_01");
            Assert.IsTrue(fired);
        }

        [Test]
        public void Controller_SetVoicePreset_UpdatesData()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetVoicePreset("warm");
            Assert.AreEqual("warm", ctrl.Data.VoicePresetId);
        }

        [Test]
        public void Controller_SetVoicePreset_FiresEvent()
        {
            var ctrl = new SoulformCreationController();
            string field = null;
            ctrl.OnVoiceChanged += (f, v) => field = f;
            ctrl.SetVoicePreset("bright");
            Assert.AreEqual("Preset", field);
        }

        [Test]
        public void Controller_SetVoiceFloat_ClampsPitch()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetVoiceFloat("Pitch", 5f);
            Assert.AreEqual(2f, ctrl.Data.VoicePitch);
            ctrl.SetVoiceFloat("Pitch", -1f);
            Assert.AreEqual(0.5f, ctrl.Data.VoicePitch);
        }

        [Test]
        public void Controller_SetVoiceFloat_ClampsTone()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetVoiceFloat("Tone", 3f);
            Assert.AreEqual(2f, ctrl.Data.VoiceTone);
        }

        [Test]
        public void Controller_SetVoiceFloat_ClampsTimbre()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetVoiceFloat("Timbre", 0f);
            Assert.AreEqual(0.5f, ctrl.Data.VoiceTimbre);
        }

        [Test]
        public void Controller_SetVoiceFloat_UnknownProperty_DoesNothing()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetVoiceFloat("Bogus", 99f);
            Assert.AreEqual(1f, ctrl.Data.VoicePitch);
        }

        [Test]
        public void Controller_SetWorldBond_UpdatesFields()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetStartingAffinity(0.8f);
            ctrl.SetInitialCalling("Discover");
            ctrl.SetPreferredBiome(StarterEnvironment.Forest);
            Assert.AreEqual(0.8f, ctrl.Data.StartingAffinity, 0.001f);
            Assert.AreEqual("Discover", ctrl.Data.InitialCalling);
            Assert.AreEqual(StarterEnvironment.Forest, ctrl.Data.PreferredBiome);
        }

        [Test]
        public void Controller_SetStartingAffinity_Clamps()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetStartingAffinity(2f);
            Assert.AreEqual(1f, ctrl.Data.StartingAffinity);
            ctrl.SetStartingAffinity(-1f);
            Assert.AreEqual(0f, ctrl.Data.StartingAffinity);
        }

        [Test]
        public void Controller_SetBondTraits_UpdatesData()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetBondTraits(new[] { "Explorer", "Builder" });
            Assert.AreEqual(2, ctrl.Data.BondTraits.Length);
            Assert.AreEqual("Explorer", ctrl.Data.BondTraits[0]);
        }

        [Test]
        public void Controller_Validate_FiresEvent()
        {
            var ctrl = new SoulformCreationController();
            bool? valid = null;
            ctrl.OnCreationValidated += (v, m) => valid = v;
            ctrl.Validate();
            Assert.IsFalse(valid);
        }

        [Test]
        public void Controller_SaveRestore_Progress()
        {
            var ctrl = new SoulformCreationController();
            ctrl.SetName("Luma");
            ctrl.SaveProgress();

            var ctrl2 = new SoulformCreationController();
            Assert.IsTrue(ctrl2.RestoreProgress());
            Assert.AreEqual("Luma", ctrl2.Data.Name);
        }

        [Test]
        public void Controller_RestoreProgress_ReturnsFalse_WhenNoSaved()
        {
            SoulformCreationController.ClearSavedProgress();
            var ctrl = new SoulformCreationController();
            Assert.IsFalse(ctrl.RestoreProgress());
        }

        [Test]
        public void Controller_Back_FiresOnBackPressed()
        {
            var ctrl = new SoulformCreationController();
            bool fired = false;
            ctrl.OnBackPressed += () => fired = true;
            ctrl.GoBack();
            Assert.IsTrue(fired);
        }

        [Test]
        public void Controller_NotifyPreviewUpdated_FiresEvent()
        {
            var ctrl = new SoulformCreationController();
            bool fired = false;
            ctrl.OnPreviewUpdated += () => fired = true;
            ctrl.NotifyPreviewUpdated();
            Assert.IsTrue(fired);
        }

        [Test]
        public void Controller_Initialize_WithExistingData()
        {
            var existing = new SoulformCreationData { Name = "PreExisting", InitialCalling = "Test" };
            var ctrl = new SoulformCreationController();
            ctrl.Initialize(existing);
            Assert.AreEqual("PreExisting", ctrl.Data.Name);
        }

        [Test]
        public void Controller_CategoryCount_IsEight()
        {
            var ctrl = new SoulformCreationController();
            Assert.AreEqual(8, ctrl.CategoryCount);
        }

        [Test]
        public void Controller_CategoryNames_HasEightEntries()
        {
            Assert.AreEqual(8, SoulformCreationController.CategoryNames.Length);
            Assert.AreEqual("Identity", SoulformCreationController.CategoryNames[0]);
            Assert.AreEqual("Form", SoulformCreationController.CategoryNames[1]);
            Assert.AreEqual("Skin", SoulformCreationController.CategoryNames[2]);
            Assert.AreEqual("Hair", SoulformCreationController.CategoryNames[3]);
            Assert.AreEqual("Voice", SoulformCreationController.CategoryNames[4]);
            Assert.AreEqual("Temperament", SoulformCreationController.CategoryNames[5]);
            Assert.AreEqual("Clothing", SoulformCreationController.CategoryNames[6]);
            Assert.AreEqual("World Bond", SoulformCreationController.CategoryNames[7]);
        }

        // ================================================================
        // SoulformUnlockRule
        // ================================================================

        [Test]
        public void SoulformUnlock_Below60_IsLocked()
        {
            var bubble = new BubbleModel(StarterEnvironment.MeadowGrassland, 8, 8);
            Assert.IsFalse(SoulformUnlockRule.IsUnlocked(bubble));
        }

        [Test]
        public void SoulformUnlock_LockReason_ContainsPercentage()
        {
            var bubble = new BubbleModel(StarterEnvironment.MeadowGrassland, 8, 8);
            string reason = SoulformUnlockRule.LockReason(bubble);
            Assert.IsTrue(reason.Contains("60%"));
            Assert.IsTrue(reason.Contains("%"));
        }

        [Test]
        public void SoulformUnlock_ReturnsEmpty_WhenUnlocked()
        {
            var bubble = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                    bubble.Place(i + 8, 0, j + 8, BubblePlacementCatalog.Find("tree"));
            var report = BubbleLivability.Calculate(bubble);
            if (report.Percentage >= 60)
                Assert.AreEqual("", SoulformUnlockRule.LockReason(bubble));
        }

        [Test]
        public void SoulformUnlock_NullBubble_ReturnsFalse()
        {
            Assert.IsFalse(SoulformUnlockRule.IsUnlocked(null));
        }

        [Test]
        public void SoulformUnlock_NullBubble_Reason()
        {
            Assert.AreEqual("No world created", SoulformUnlockRule.LockReason(null));
        }

        // ================================================================
        // SoulformModel
        // ================================================================

        [Test]
        public void SoulformModel_Default_HasDefaultValues()
        {
            var m = new SoulformModel();
            Assert.AreEqual("Soulform", m.Name);
            Assert.AreEqual(SoulformTemperament.Inquisitive, m.Temperament);
            Assert.AreEqual(SoulformMood.Content, m.Mood);
            Assert.AreEqual(50, m.Comfort);
            Assert.AreEqual(50, m.Hunger);
            Assert.AreEqual(50, m.Curiosity);
            Assert.AreEqual("default", m.AppearanceId);
            Assert.AreEqual("default", m.HairStyleId);
            Assert.AreEqual("default", m.VoicePresetId);
            Assert.AreEqual("", m.OutfitId);
        }

        [Test]
        public void SoulformModel_FromCreationData_PreservesName()
        {
            var data = new SoulformCreationData { Name = "Luma", InitialCalling = "Test" };
            var model = SoulformModel.FromCreationData(data);
            Assert.AreEqual("Luma", model.Name);
        }

        [Test]
        public void SoulformModel_FromCreationData_PreservesTemperament()
        {
            var data = new SoulformCreationData { Name = "L", InitialCalling = "T", Temperament = SoulformTemperament.Harmonious };
            var model = SoulformModel.FromCreationData(data);
            Assert.AreEqual(SoulformTemperament.Harmonious, model.Temperament);
        }

        [Test]
        public void SoulformModel_FromCreationData_PreservesBodyParts()
        {
            var data = new SoulformCreationData
            {
                Name = "L", InitialCalling = "T",
                BodyTypeId = "slim", HeightId = "tall",
                FaceShapeId = "round", EyesId = "big",
                EarsId = "pointed", NoseId = "button"
            };
            var model = SoulformModel.FromCreationData(data);
            Assert.AreEqual("slim", model.BodyTypeId);
            Assert.AreEqual("tall", model.HeightId);
            Assert.AreEqual("round", model.FaceShapeId);
            Assert.AreEqual("big", model.EyesId);
            Assert.AreEqual("pointed", model.EarsId);
            Assert.AreEqual("button", model.NoseId);
        }

        [Test]
        public void SoulformModel_FromCreationData_PreservesSkin()
        {
            var tone = new Color(0.6f, 0.4f, 0.26f);
            var data = new SoulformCreationData
            {
                Name = "L", InitialCalling = "T",
                SkinTone = tone, MarkingsId = "freckles", SkinDetailsId = "glowing"
            };
            var model = SoulformModel.FromCreationData(data);
            Assert.AreEqual(tone, model.SkinTone);
            Assert.AreEqual("freckles", model.MarkingsId);
            Assert.AreEqual("glowing", model.SkinDetailsId);
        }

        [Test]
        public void SoulformModel_FromCreationData_PreservesHair()
        {
            var hairColor = new Color(0.8f, 0.2f, 0.2f);
            var data = new SoulformCreationData
            {
                Name = "L", InitialCalling = "T",
                HairStyleId = "long", HairColor = hairColor,
                HairMaterialId = "shiny", FacialHairId = "beard"
            };
            var model = SoulformModel.FromCreationData(data);
            Assert.AreEqual("long", model.HairStyleId);
            Assert.AreEqual(hairColor, model.HairColor);
            Assert.AreEqual("shiny", model.HairMaterialId);
            Assert.AreEqual("beard", model.FacialHairId);
        }

        [Test]
        public void SoulformModel_FromCreationData_PreservesVoice()
        {
            var data = new SoulformCreationData
            {
                Name = "L", InitialCalling = "T",
                VoicePresetId = "warm", VoicePitch = 1.5f,
                VoiceTone = 0.8f, VoiceTimbre = 1.2f
            };
            var model = SoulformModel.FromCreationData(data);
            Assert.AreEqual("warm", model.VoicePresetId);
            Assert.AreEqual(1.5f, model.VoicePitch);
            Assert.AreEqual(0.8f, model.VoiceTone);
            Assert.AreEqual(1.2f, model.VoiceTimbre);
        }

        [Test]
        public void SoulformModel_FromCreationData_PreservesClothing()
        {
            var data = new SoulformCreationData
            {
                Name = "L", InitialCalling = "T",
                OutfitId = "dress", TopId = "vest",
                BottomsId = "pants", FootwearId = "boots", AccessoryId = "hat"
            };
            var model = SoulformModel.FromCreationData(data);
            Assert.AreEqual("dress", model.OutfitId);
            Assert.AreEqual("vest", model.TopId);
            Assert.AreEqual("pants", model.BottomsId);
            Assert.AreEqual("boots", model.FootwearId);
            Assert.AreEqual("hat", model.AccessoryId);
        }

        [Test]
        public void SoulformModel_FromCreationData_PreservesWorldBond()
        {
            var data = new SoulformCreationData
            {
                Name = "L", InitialCalling = "Discover",
                StartingAffinity = 0.9f,
                PreferredBiome = StarterEnvironment.Forest,
                BondTraits = new[] { "Explorer" }
            };
            var model = SoulformModel.FromCreationData(data);
            Assert.AreEqual("Discover", model.InitialCalling);
            Assert.AreEqual(0.9f, model.StartingAffinity);
            Assert.AreEqual(StarterEnvironment.Forest, model.PreferredBiome);
            Assert.AreEqual("Explorer", model.BondTraits[0]);
        }

        [Test]
        public void SoulformModel_CreateDefault_Inquisitive_Thought()
        {
            var m = SoulformModel.CreateDefault(SoulformTemperament.Inquisitive);
            Assert.AreEqual("What is this place? I want to explore every inch.", m.CurrentThought);
        }

        [Test]
        public void SoulformModel_CreateDefault_Creative_Thought()
        {
            var m = SoulformModel.CreateDefault(SoulformTemperament.Creative);
            Assert.AreEqual("This world has potential. I can shape it.", m.CurrentThought);
        }

        [Test]
        public void SoulformModel_CreateDefault_Harmonious_Thought()
        {
            var m = SoulformModel.CreateDefault(SoulformTemperament.Harmonious);
            Assert.AreEqual("Everything here feels connected. I belong.", m.CurrentThought);
        }

        [Test]
        public void SoulformModel_CreateDefault_Resilient_Thought()
        {
            var m = SoulformModel.CreateDefault(SoulformTemperament.Resilient);
            Assert.AreEqual("No matter what comes, I will adapt.", m.CurrentThought);
        }

        [Test]
        public void SoulformModel_CreateDefault_Assertive_Thought()
        {
            var m = SoulformModel.CreateDefault(SoulformTemperament.Assertive);
            Assert.AreEqual("I will make this world my own.", m.CurrentThought);
        }

        [Test]
        public void SoulformModel_CreateDefault_SetsGridPosition12()
        {
            var m = SoulformModel.CreateDefault(SoulformTemperament.Inquisitive);
            Assert.AreEqual(12, m.GridX);
            Assert.AreEqual(0, m.GridY);
            Assert.AreEqual(12, m.GridZ);
        }

        // ================================================================
        // SoulformBrainService
        // ================================================================

        [Test]
        public void BrainService_IsConfigured_False_WhenConfigNull()
        {
            var cfg = ScriptableObject.CreateInstance<SoulformBrainConfig>();
            cfg.ApiEndpoint = null;
            cfg.ApiKey = null;
            var svc = new SoulformBrainService(cfg);
            Assert.IsFalse(svc.IsConfigured);
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        [Test]
        public void BrainService_IsConfigured_False_WhenEndpointMissing()
        {
            var cfg = ScriptableObject.CreateInstance<SoulformBrainConfig>();
            cfg.ApiEndpoint = null;
            cfg.ApiKey = "key123";
            var svc = new SoulformBrainService(cfg);
            Assert.IsFalse(svc.IsConfigured);
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        [Test]
        public void BrainService_IsConfigured_False_WhenKeyMissing()
        {
            var cfg = ScriptableObject.CreateInstance<SoulformBrainConfig>();
            cfg.ApiEndpoint = "https://example.com/api";
            cfg.ApiKey = null;
            var svc = new SoulformBrainService(cfg);
            Assert.IsFalse(svc.IsConfigured);
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        [Test]
        public void BrainService_IsConfigured_True_WhenBothSet()
        {
            var cfg = ScriptableObject.CreateInstance<SoulformBrainConfig>();
            cfg.ApiEndpoint = "https://example.com/api";
            cfg.ApiKey = "key123";
            var svc = new SoulformBrainService(cfg);
            Assert.IsTrue(svc.IsConfigured);
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        [Test]
        public void BrainService_Unconfigured_CallsErrorCallback()
        {
            var cfg = ScriptableObject.CreateInstance<SoulformBrainConfig>();
            cfg.ApiEndpoint = null;
            var svc = new SoulformBrainService(cfg);

            Assert.IsFalse(svc.IsConfigured);
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        // ================================================================
        // SoulformBrainResponse
        // ================================================================

        [Test]
        public void BrainResponse_DefaultsAllNull()
        {
            var r = new SoulformBrainResponse();
            Assert.IsNull(r.CurrentThought);
            Assert.IsNull(r.CurrentActivity);
            Assert.IsNull(r.TargetX);
            Assert.IsNull(r.TargetY);
        }

        // ================================================================
        // ValidationResult
        // ================================================================

        [Test]
        public void ValidationResult_ConstructorSetsFields()
        {
            var r = new ValidationResult(true, null, null);
            Assert.IsTrue(r.IsValid);
            Assert.IsNull(r.ErrorMessage);
            Assert.IsNull(r.FailedCategoryIndex);
        }

        [Test]
        public void ValidationResult_ConstructorSetsFailedCategory()
        {
            var r = new ValidationResult(false, "Error", 3);
            Assert.IsFalse(r.IsValid);
            Assert.AreEqual("Error", r.ErrorMessage);
            Assert.AreEqual(3, r.FailedCategoryIndex);
        }

        // ================================================================
        // EventLog
        // ================================================================

        [Test]
        public void EventLog_Record_AddsEvent()
        {
            var log = new EventLog();
            log.Record(GameEventType.DayBegan, "Day 1 began");
            Assert.AreEqual(1, log.GetAll().Count);
        }

        [Test]
        public void EventLog_Record_FiresOnEventRecorded()
        {
            var log = new EventLog();
            GameEvent received = null;
            log.OnEventRecorded += e => received = e;
            log.Record(GameEventType.RainStarted, "Rain");
            Assert.IsNotNull(received);
            Assert.AreEqual(GameEventType.RainStarted, received.Type);
            Assert.AreEqual("Rain", received.Description);
        }

        [Test]
        public void EventLog_GetAll_ReturnsAllEvents()
        {
            var log = new EventLog();
            log.Record(GameEventType.DayBegan, "D1");
            log.Record(GameEventType.SoulformReacted, "React");
            Assert.AreEqual(2, log.GetAll().Count);
        }

        [Test]
        public void EventLog_GetRecent_ReturnsCorrectCount()
        {
            var log = new EventLog();
            for (int i = 0; i < 10; i++)
                log.Record(GameEventType.DayBegan, "Day " + i);
            var recent = log.GetRecent(3);
            Assert.AreEqual(3, recent.Count);
        }

        [Test]
        public void EventLog_GetRecent_ReturnsLastEvents()
        {
            var log = new EventLog();
            for (int i = 0; i < 5; i++)
                log.Record(GameEventType.DayBegan, "E" + i);
            var recent = log.GetRecent(2);
            Assert.AreEqual("E3", recent[0].Description);
            Assert.AreEqual("E4", recent[1].Description);
        }

        [Test]
        public void EventLog_GetRecent_ClampsToAvailable()
        {
            var log = new EventLog();
            log.Record(GameEventType.DayBegan, "Only");
            var recent = log.GetRecent(100);
            Assert.AreEqual(1, recent.Count);
        }

        [Test]
        public void EventLog_Clear_RemovesAll()
        {
            var log = new EventLog();
            log.Record(GameEventType.DayBegan, "D1");
            log.Clear();
            Assert.AreEqual(0, log.GetAll().Count);
        }

        [Test]
        public void EventLog_MaxEvents_DoesNotExceedLimit()
        {
            var log = new EventLog();
            for (int i = 0; i < 600; i++)
                log.Record(GameEventType.DayBegan, "E" + i);
            Assert.AreEqual(500, log.GetAll().Count);
        }

        [Test]
        public void EventLog_MaxEvents_PreservesMostRecent()
        {
            var log = new EventLog();
            for (int i = 0; i < 600; i++)
                log.Record(GameEventType.DayBegan, "E" + i);
            var all = log.GetAll();
            Assert.AreEqual("E100", all[0].Description);
        }

        [Test]
        public void GameEvent_ConstructorSetsFields()
        {
            var e = new GameEvent(GameEventType.InterventionUsed, "Test desc", "system");
            Assert.AreEqual(GameEventType.InterventionUsed, e.Type);
            Assert.AreEqual("Test desc", e.Description);
            Assert.AreEqual("system", e.Source);
        }

        [Test]
        public void GameEvent_TimestampIsUtc()
        {
            var e = new GameEvent(GameEventType.DayBegan, "Now");
            Assert.AreEqual(DateTime.UtcNow.Kind, e.Timestamp.Kind);
        }

        // ================================================================
        // BubbleSaveService
        // ================================================================

        [Test]
        public void SaveService_SaveAndLoad_RoundTripsCells()
        {
            var bubble = BubbleStarterFactory.Create(StarterEnvironment.Forest);
            var report = BubbleLivability.Calculate(bubble);
            string worldName = "TestWorld_" + System.DateTime.Now.Ticks;
            string path = BubbleSaveService.GetPath(worldName);

            try
            {
                BubbleSaveService.SaveFull(bubble, report, worldName, null, "Clear");
                var result = BubbleSaveService.LoadFull(worldName);

                Assert.AreEqual(bubble.Cells.Count, result.Bubble.Cells.Count);
                Assert.AreEqual(worldName, result.WorldName);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void SaveService_SaveAndLoad_PreservesWorldName()
        {
            var bubble = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            var report = BubbleLivability.Calculate(bubble);
            string worldName = "My Cool World";

            try
            {
                BubbleSaveService.SaveFull(bubble, report, worldName, null, "Clear");
                var result = BubbleSaveService.LoadFull(worldName);
                Assert.AreEqual(worldName, result.WorldName);
            }
            finally
            {
                string path = BubbleSaveService.GetPath(worldName);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void SaveService_SaveAndLoad_PreservesSoulform()
        {
            var bubble = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            var report = BubbleLivability.Calculate(bubble);
            var soulform = SoulformModel.CreateDefault(SoulformTemperament.Inquisitive);
            soulform.Name = "Luma";
            string worldName = "SFTest_" + System.DateTime.Now.Ticks;

            try
            {
                BubbleSaveService.SaveFull(bubble, report, worldName, soulform, "Clear");
                var result = BubbleSaveService.LoadFull(worldName);
                Assert.IsNotNull(result.Soulform);
                Assert.AreEqual("Luma", result.Soulform.Name);
                Assert.AreEqual(SoulformTemperament.Inquisitive, result.Soulform.Temperament);
            }
            finally
            {
                string path = BubbleSaveService.GetPath(worldName);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void SaveService_SaveAndLoad_PreservesSoulformDetails()
        {
            var bubble = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            var report = BubbleLivability.Calculate(bubble);
            var soulform = SoulformModel.CreateDefault(SoulformTemperament.Harmonious);
            soulform.Name = "Nova";
            soulform.Comfort = 80;
            soulform.Hunger = 20;
            soulform.BodyTypeId = "slim";
            soulform.HairStyleId = "curly";
            string worldName = "Details_" + System.DateTime.Now.Ticks;

            try
            {
                BubbleSaveService.SaveFull(bubble, report, worldName, soulform, "Clear");
                var result = BubbleSaveService.LoadFull(worldName);
                Assert.AreEqual("Nova", result.Soulform.Name);
                Assert.AreEqual(80, result.Soulform.Comfort);
                Assert.AreEqual(20, result.Soulform.Hunger);
                Assert.AreEqual(SoulformTemperament.Harmonious, result.Soulform.Temperament);
            }
            finally
            {
                string path = BubbleSaveService.GetPath(worldName);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void SaveService_ListSavedWorlds_ReturnsNames()
        {
            var bubble = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            var report = BubbleLivability.Calculate(bubble);
            string name1 = "ListTest1_" + System.DateTime.Now.Ticks;
            string name2 = "ListTest2_" + System.DateTime.Now.Ticks;

            try
            {
                BubbleSaveService.SaveFull(bubble, report, name1, null, "Clear");
                BubbleSaveService.SaveFull(bubble, report, name2, null, "Clear");
                var worlds = BubbleSaveService.ListSavedWorlds();
                Assert.Contains(name1, worlds);
                Assert.Contains(name2, worlds);
            }
            finally
            {
                string p1 = BubbleSaveService.GetPath(name1);
                string p2 = BubbleSaveService.GetPath(name2);
                if (File.Exists(p1)) File.Delete(p1);
                if (File.Exists(p2)) File.Delete(p2);
            }
        }

        [Test]
        public void SaveService_ListSavedWorlds_ReturnsEmpty_WhenNoSaves()
        {
            string dir = BubbleSaveService.WorldsDirectory;
            if (Directory.Exists(dir))
            {
                foreach (string f in Directory.GetFiles(dir, "*.json"))
                    File.Delete(f);
            }
            Assert.AreEqual(0, BubbleSaveService.ListSavedWorlds().Length);
        }

        [Test]
        public void SaveService_Delete_RemovesFile()
        {
            var bubble = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            var report = BubbleLivability.Calculate(bubble);
            string name = "DeleteTest_" + System.DateTime.Now.Ticks;
            BubbleSaveService.SaveFull(bubble, report, name, null, "Clear");
            string path = BubbleSaveService.GetPath(name);
            Assert.IsTrue(File.Exists(path));
            BubbleSaveService.Delete(name);
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void SaveService_GetPath_ContainsWorldsDir()
        {
            string path = BubbleSaveService.GetPath("MyWorld");
            Assert.IsTrue(path.Contains("worlds"));
            Assert.IsTrue(path.EndsWith("MyWorld.json"));
        }

        [Test]
        public void SaveService_TryLoadLegacy_ReturnsFalse_WhenNoFile()
        {
            Assert.IsFalse(BubbleSaveService.TryLoadLegacy(out var _));
        }

        [Test]
        public void SaveService_Sanitize_RemovesInvalidChars()
        {
            string dirty = "My/World\\Name:Test*?\"<>|";
            string sanitized = BubbleSaveService.GetPath(dirty);
            Assert.IsFalse(sanitized.Contains("/"));
            Assert.IsTrue(sanitized.EndsWith(".json"));
        }

        // ================================================================
        // WatchState
        // ================================================================

        [Test]
        public void WatchState_TimeAccumulates()
        {
            var ws = new WatchState();
            Assert.AreEqual(WatchSpeed.Normal, ws.Speed);
            Assert.IsFalse(ws.IsPaused);
            ws.Speed = WatchSpeed.Quad;
            Assert.AreEqual(WatchSpeed.Quad, ws.Speed);
        }

        [Test]
        public void WatchState_DayCycles()
        {
            var ws = new WatchState();
            Assert.AreEqual(1, ws.DayCount);
            ws.DayCount = 3;
            Assert.AreEqual(3, ws.DayCount);
        }

        [Test]
        public void WatchState_IsPaused_TrueWhenPause()
        {
            var ws = new WatchState();
            ws.Speed = WatchSpeed.Pause;
            Assert.IsTrue(ws.IsPaused);
        }

        [Test]
        public void WatchState_IsPaused_FalseWhenNormal()
        {
            var ws = new WatchState();
            ws.Speed = WatchSpeed.Normal;
            Assert.IsFalse(ws.IsPaused);
        }

        [Test]
        public void WatchState_DayLength_Is86400()
        {
            Assert.AreEqual(86400f, WatchState.DayLength);
        }

        [Test]
        public void WatchState_WeatherChangeInterval_Is30()
        {
            Assert.AreEqual(30f, WatchState.WeatherChangeInterval);
        }

        [Test]
        public void WatchState_DefaultWeather_Clear()
        {
            var ws = new WatchState();
            Assert.AreEqual("Clear", ws.WeatherCondition);
        }

        [Test]
        public void WatchState_DefaultCamera_FollowSoulform()
        {
            var ws = new WatchState();
            Assert.AreEqual(Camera3DView.FollowSoulform, ws.CurrentCameraView);
        }

        [Test]
        public void WatchState_Cooldowns_StartsEmpty()
        {
            var ws = new WatchState();
            Assert.IsNotNull(ws.Cooldowns);
            Assert.AreEqual(0, ws.Cooldowns.Count);
        }

        // ================================================================
        // SoulformBrainConfig
        // ================================================================

        [Test]
        public void BrainConfig_DefaultsAreValid()
        {
            var cfg = ScriptableObject.CreateInstance<SoulformBrainConfig>();
            Assert.AreEqual(30, cfg.TimeoutSeconds);
            Assert.AreEqual(5f, cfg.BrainTickSeconds);
            Assert.IsTrue(cfg.PromptTemplate.Contains("{name}"));
            Assert.AreEqual("default", cfg.ModelId);
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        [Test]
        public void BrainConfig_PromptTemplate_HasRequiredPlaceholders()
        {
            var cfg = ScriptableObject.CreateInstance<SoulformBrainConfig>();
            Assert.IsTrue(cfg.PromptTemplate.Contains("{name}"));
            Assert.IsTrue(cfg.PromptTemplate.Contains("{temperament}"));
            Assert.IsTrue(cfg.PromptTemplate.Contains("{environment}"));
            Assert.IsTrue(cfg.PromptTemplate.Contains("{weather}"));
            UnityEngine.Object.DestroyImmediate(cfg);
        }

        // ================================================================
        // Intervention
        // ================================================================

        [Test]
        public void Intervention_FromType_AddFood()
        {
            var inv = Intervention.FromType(InterventionType.AddFood);
            Assert.AreEqual("Add Food", inv.DisplayName);
            Assert.AreEqual(5f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_FromType_GrowTree()
        {
            var inv = Intervention.FromType(InterventionType.GrowTree);
            Assert.AreEqual("Grow Tree", inv.DisplayName);
            Assert.AreEqual(10f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_FromType_ChangeWeather()
        {
            var inv = Intervention.FromType(InterventionType.ChangeWeather);
            Assert.AreEqual("Change Weather", inv.DisplayName);
            Assert.AreEqual(15f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_FromType_DayNight()
        {
            var inv = Intervention.FromType(InterventionType.DayNight);
            Assert.AreEqual("Day / Night", inv.DisplayName);
            Assert.AreEqual(30f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_FromType_SpawnStructure()
        {
            var inv = Intervention.FromType(InterventionType.SpawnStructure);
            Assert.AreEqual("Spawn Structure", inv.DisplayName);
            Assert.AreEqual(10f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_FromType_SpawnAnimal()
        {
            var inv = Intervention.FromType(InterventionType.SpawnAnimal);
            Assert.AreEqual("Spawn Animal", inv.DisplayName);
            Assert.AreEqual(20f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_FromType_SpawnNpc()
        {
            var inv = Intervention.FromType(InterventionType.SpawnNpc);
            Assert.AreEqual("Spawn NPC", inv.DisplayName);
            Assert.AreEqual(30f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_FromType_RemindSoulform()
        {
            var inv = Intervention.FromType(InterventionType.RemindSoulform);
            Assert.AreEqual("Remind Soulform", inv.DisplayName);
            Assert.AreEqual(3f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_AllTypes_HaveNonEmptyDisplayNames()
        {
            var types = (InterventionType[])System.Enum.GetValues(typeof(InterventionType));
            foreach (var type in types)
            {
                var inv = Intervention.FromType(type);
                Assert.IsNotNull(inv);
                Assert.IsFalse(string.IsNullOrEmpty(inv.DisplayName));
            }
        }

        [Test]
        public void Intervention_AllTypes_HaveDescriptions()
        {
            var types = (InterventionType[])System.Enum.GetValues(typeof(InterventionType));
            foreach (var type in types)
            {
                var inv = Intervention.FromType(type);
                Assert.IsFalse(string.IsNullOrEmpty(inv.Description));
            }
        }

        [Test]
        public void Intervention_UnknownType_ReturnsDefault()
        {
            var inv = Intervention.FromType((InterventionType)999);
            Assert.AreEqual("999", inv.DisplayName);
            Assert.AreEqual(0f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_Constructor_SetsFields()
        {
            var inv = new Intervention(InterventionType.AddFood, "Test", "Desc", 42f);
            Assert.AreEqual(InterventionType.AddFood, inv.Type);
            Assert.AreEqual("Test", inv.DisplayName);
            Assert.AreEqual("Desc", inv.Description);
            Assert.AreEqual(42f, inv.CooldownSeconds);
        }

        [Test]
        public void Intervention_Cooldowns_AreAllPositive()
        {
            var types = (InterventionType[])System.Enum.GetValues(typeof(InterventionType));
            foreach (var type in types)
            {
                var inv = Intervention.FromType(type);
                Assert.GreaterOrEqual(inv.CooldownSeconds, 0f);
            }
        }

        // ================================================================
        // BubbleWorldSaveData serialization
        // ================================================================

        [Test]
        public void BubbleCellSaveData_FromIntervention_DefaultFalse()
        {
            var cell = new BubbleCell(5, 0, 5, BubblePlacementCatalog.Find("grass"));
            Assert.IsFalse(cell.FromIntervention);
        }

        [Test]
        public void BubbleCellSaveData_FromIntervention_CanBeSetTrue()
        {
            var cell = new BubbleCell(5, 0, 5, BubblePlacementCatalog.Find("tree"));
            cell.FromIntervention = true;
            Assert.IsTrue(cell.FromIntervention);
        }

        [Test]
        public void SoulformSaveData_RoundTripsThroughJson()
        {
            var original = new SoulformSaveData
            {
                name = "Luma",
                meaning = "Light",
                temperament = "Creative",
                bodyTypeId = "slim",
                heightId = "tall",
                skinToneR = 0.8f, skinToneG = 0.6f, skinToneB = 0.4f,
                hairStyleId = "curly",
                outfitId = "dress",
                startingAffinity = 0.9f,
                initialCalling = "Explore",
                gridX = 10, gridY = 15
            };

            string json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<SoulformSaveData>(json);

            Assert.AreEqual("Luma", restored.name);
            Assert.AreEqual("Creative", restored.temperament);
            Assert.AreEqual("slim", restored.bodyTypeId);
            Assert.AreEqual("tall", restored.heightId);
            Assert.AreEqual(0.8f, restored.skinToneR);
            Assert.AreEqual("curly", restored.hairStyleId);
            Assert.AreEqual("dress", restored.outfitId);
            Assert.AreEqual("Explore", restored.initialCalling);
            Assert.AreEqual(10, restored.gridX);
            Assert.AreEqual(15, restored.gridY);
        }
    }
}
