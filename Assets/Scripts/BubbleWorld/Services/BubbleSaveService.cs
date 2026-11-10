using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class LoadResult
    {
        public BubbleModel Bubble;
        public SoulformModel Soulform;
        public string WorldName;
        public string WeatherCondition;
        public int LivabilityPercent;
    }

    public sealed class ExtendedLoadResult
    {
        public string SaveVersion;
        public BubbleModel Bubble;
        public SoulformModel Soulform;
        public string WorldName;
        public string WeatherCondition;
        public int LivabilityPercent;

        public DateTime? UtcLastSave;
        public DateTime? UtcLastLoad;
        public float ElapsedGameTime;
        public int DayCount = 1;
        public SeasonType Season = SeasonType.Spring;
        public int TutorialDaysRemaining = 7;

        public int ArchitectPoints = 100;
        public DateTime? LastPassiveAPTime;

        public string SkillData;
        public string MemoryChapters;
        public List<TileCompositionSaveData> TileCompositions;
        public List<TileStateSaveData> TileStates;
        public List<CooldownSaveData> Cooldowns;
    }

    public sealed class SaveExtendedRequest
    {
        public string WorldName;
        public StarterEnvironment Environment;
        public int GridWidth;
        public int GridHeight;
        public int GridDepth;
        public int LivabilityPercent;
        public string WeatherCondition;

        public float ElapsedGameTime;
        public int DayCount = 1;
        public SeasonType Season = SeasonType.Spring;
        public int TutorialDaysRemaining = 7;

        public int ArchitectPoints = 100;
        public DateTime? LastPassiveAPTime;

        public string SkillData;
        public string MemoryChapters;

        public IReadOnlyDictionary<(int x, int y, int z), BubbleCell> Cells;
        public SoulformModel Soulform;
        public List<CooldownSaveData> Cooldowns;
    }

    [Serializable]
    internal sealed class BubbleSaveData
    {
        public string environment;
        public int width;
        public int height;
        public int livability;
        public List<BubbleCellSaveData> cells;
    }

    public static class BubbleSaveService
    {
        public const string LegacyFileName = "bubbleworld_milestone1_save.json";

        public static string WorldsDirectory =>
            Path.Combine(Application.persistentDataPath, "worlds");

        public static string GetPath(string worldName)
        {
            return Path.Combine(WorldsDirectory, Sanitize(worldName) + ".json");
        }

        // Returns the exact JSON written (uploaded to the backend after saves —
        // the backend is the world-state source of truth).
        public static string SaveFull(BubbleModel bubble, LivabilityReport report,
            string worldName, SoulformModel soulform, string weather)
        {
            var data = new BubbleWorldSaveData
            {
                worldName = worldName,
                environmentType = bubble.Environment.ToString(),
                gridWidth = bubble.Width,
                gridHeight = bubble.Height,
                gridDepth = bubble.Depth,
                lastSaveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                livabilityPercent = report.Percentage,
                weatherCondition = weather,
                cells = new List<BubbleCellSaveData>(),
                soulform = soulform != null ? ToSaveData(soulform) : null
            };

            foreach (BubbleCell cell in bubble.Cells.Values)
            {
                data.cells.Add(new BubbleCellSaveData
                {
                    x = cell.X,
                    y = cell.Y,
                    z = cell.Z,
                    itemId = cell.ItemId,
                    layer = (int)cell.Layer,
                    isObject = cell.IsObject,
                    rotation = cell.Rotation,
                    autoTileVariant = (int)cell.AutoTileVariant,
                    fromIntervention = cell.FromIntervention,
                    groundItemId = cell.GroundItemId
                });
            }

            string path = GetPath(worldName);
            Directory.CreateDirectory(WorldsDirectory);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            return json;
        }

        public static LoadResult LoadFull(string worldName)
        {
            string path = GetPath(worldName);
            if (!File.Exists(path))
                throw new FileNotFoundException("Save file not found: " + path);

            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<BubbleWorldSaveData>(json);

            var environment = (StarterEnvironment)Enum.Parse(typeof(StarterEnvironment), data.environmentType);
            var bubble = new BubbleModel(environment, data.gridWidth, data.gridHeight,
                data.gridDepth > 0 ? data.gridDepth : 8);

            foreach (var cellData in data.cells)
            {
                var def = BubblePlacementCatalog.Find(cellData.itemId);
                if (def != null)
                {
                    int px = cellData.x;
                    int py = cellData.y;
                    int pz = cellData.z;
                    bubble.Place(px, py, pz, def);
                    var cell = bubble.Get(px, py, pz);
                    if (cell != null)
                    {
                        cell.Rotation = cellData.rotation;
                        cell.AutoTileVariant = (TileVariant)cellData.autoTileVariant;
                        cell.FromIntervention = cellData.fromIntervention;
                        cell.GroundItemId = cellData.groundItemId;
                    }
                }
            }

            SoulformModel soulform = null;
            if (data.soulform != null)
                soulform = FromSaveData(data.soulform);

            return new LoadResult
            {
                Bubble = bubble,
                Soulform = soulform,
                WorldName = data.worldName,
                WeatherCondition = data.weatherCondition ?? "Clear",
                LivabilityPercent = data.livabilityPercent
            };
        }

        public static bool IsVersionAtLeast(BubbleWorldSaveData data, string minimum)
        {
            var parts = data.saveVersion.Split('.');
            var minParts = minimum.Split('.');
            for (int i = 0; i < Math.Min(parts.Length, minParts.Length); i++)
            {
                if (int.TryParse(parts[i], out int a) && int.TryParse(minParts[i], out int b))
                {
                    if (a > b) return true;
                    if (a < b) return false;
                }
            }
            return parts.Length >= minParts.Length;
        }

        public static DateTime? ParseUtc(string utcString)
        {
            if (string.IsNullOrEmpty(utcString)) return null;
            if (DateTime.TryParse(utcString, out var dt)) return dt;
            return null;
        }

        [Obsolete("Use SaveFull instead. Kept for Milestone 1 test compatibility.")]
        public static void Save(BubbleModel bubble, LivabilityReport report, string path)
        {
            var data = new BubbleWorldSaveData
            {
                worldName = System.IO.Path.GetFileNameWithoutExtension(path),
                environmentType = bubble.Environment.ToString(),
                gridWidth = bubble.Width,
                gridHeight = bubble.Height,
                gridDepth = bubble.Depth,
                lastSaveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                livabilityPercent = report.Percentage,
                cells = new List<BubbleCellSaveData>(),
                soulform = null
            };
            foreach (BubbleCell cell in bubble.Cells.Values)
            {
                data.cells.Add(new BubbleCellSaveData
                {
                    x = cell.X, y = cell.Y, z = cell.Z, itemId = cell.ItemId,
                    layer = (int)cell.Layer, isObject = cell.IsObject, rotation = cell.Rotation,
                    autoTileVariant = (int)cell.AutoTileVariant, fromIntervention = cell.FromIntervention,
                    groundItemId = cell.GroundItemId
                });
            }
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        // Returns the exact JSON written (uploaded to the backend after saves —
        // the backend is the world-state source of truth).
        public static string SaveFullExtended(SaveExtendedRequest request)
        {
            var data = new BubbleWorldSaveData
            {
                saveVersion = "2.1",
                worldName = request.WorldName,
                environmentType = request.Environment.ToString(),
                gridWidth = request.GridWidth,
                gridHeight = request.GridHeight,
                gridDepth = request.GridDepth,
                lastSaveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                utcLastSave = DateTime.UtcNow.ToString("O"),
                livabilityPercent = request.LivabilityPercent,
                weatherCondition = request.WeatherCondition ?? "Clear",

                elapsedGameTime = request.ElapsedGameTime,
                dayCount = request.DayCount,
                currentSeason = (int)request.Season,
                tutorialDaysRemaining = request.TutorialDaysRemaining,

                architectPoints = request.ArchitectPoints,
                lastPassiveAPTime = request.LastPassiveAPTime?.ToString("O"),

                skillData = request.SkillData,
                memoryChapters = request.MemoryChapters,

                cells = new List<BubbleCellSaveData>(),
                soulform = request.Soulform != null ? ToSaveData(request.Soulform) : null,
                cooldowns = request.Cooldowns
            };

            foreach (BubbleCell cell in request.Cells.Values)
            {
                data.cells.Add(new BubbleCellSaveData
                {
                    x = cell.X, y = cell.Y, z = cell.Z,
                    itemId = cell.ItemId, layer = (int)cell.Layer,
                    isObject = cell.IsObject, rotation = cell.Rotation,
                    autoTileVariant = (int)cell.AutoTileVariant,
                    fromIntervention = cell.FromIntervention,
                    groundItemId = cell.GroundItemId
                });
            }

            string path = GetPath(request.WorldName);
            Directory.CreateDirectory(WorldsDirectory);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            return json;
        }

        public static ExtendedLoadResult LoadFullExtended(string worldName)
        {
            string path = GetPath(worldName);
            if (!File.Exists(path))
                throw new FileNotFoundException("Save file not found: " + path);

            return LoadExtendedFromJson(File.ReadAllText(path));
        }

        // Builds the load result from a snapshot JSON string — used for local save
        // files and for the authoritative world state pulled from the backend.
        public static ExtendedLoadResult LoadExtendedFromJson(string json)
        {
            var data = JsonUtility.FromJson<BubbleWorldSaveData>(json);

            bool isV21 = IsVersionAtLeast(data, "2.1");

            var environment = (StarterEnvironment)Enum.Parse(typeof(StarterEnvironment), data.environmentType);
            var bubble = new BubbleModel(environment, data.gridWidth, data.gridHeight,
                data.gridDepth > 0 ? data.gridDepth : 8);

            foreach (var cellData in data.cells)
            {
                var def = BubblePlacementCatalog.Find(cellData.itemId);
                if (def != null)
                {
                    int px = cellData.x;
                    int py = cellData.y;
                    int pz = cellData.z;
                    bubble.Place(px, py, pz, def);
                    var cell = bubble.Get(px, py, pz);
                    if (cell != null)
                    {
                        cell.Rotation = cellData.rotation;
                        cell.AutoTileVariant = (TileVariant)cellData.autoTileVariant;
                        cell.FromIntervention = cellData.fromIntervention;
                        cell.GroundItemId = cellData.groundItemId;
                    }
                }
            }

            SoulformModel soulform = null;
            if (data.soulform != null)
                soulform = FromSaveData(data.soulform);

            return new ExtendedLoadResult
            {
                SaveVersion = data.saveVersion,
                Bubble = bubble,
                Soulform = soulform,
                WorldName = data.worldName,
                WeatherCondition = data.weatherCondition ?? "Clear",
                LivabilityPercent = data.livabilityPercent,

                UtcLastSave = ParseUtc(data.utcLastSave),
                UtcLastLoad = DateTime.UtcNow,
                ElapsedGameTime = isV21 ? data.elapsedGameTime : 0f,
                DayCount = isV21 ? data.dayCount : 1,
                Season = isV21 ? (SeasonType)data.currentSeason : SeasonType.Spring,
                TutorialDaysRemaining = isV21 ? data.tutorialDaysRemaining : 7,

                ArchitectPoints = isV21 ? data.architectPoints : 100,
                LastPassiveAPTime = ParseUtc(data.lastPassiveAPTime),

                SkillData = isV21 ? data.skillData : null,
                MemoryChapters = isV21 ? data.memoryChapters : null,
                TileCompositions = isV21 ? data.tileCompositions : null,
                TileStates = isV21 ? data.tileStates : null,
                Cooldowns = isV21 ? data.cooldowns : null
            };
        }

        [Obsolete("Use LoadFull instead. Kept for Milestone 1 test compatibility.")]
        public static BubbleModel Load(string path)
        {
            string json = System.IO.File.ReadAllText(path);
            var data = JsonUtility.FromJson<BubbleWorldSaveData>(json);
            var environment = (StarterEnvironment)System.Enum.Parse(typeof(StarterEnvironment), data.environmentType);
            var bubble = new BubbleModel(environment, data.gridWidth, data.gridHeight,
                data.gridDepth > 0 ? data.gridDepth : 8);
            foreach (var cellData in data.cells)
            {
                var def = BubblePlacementCatalog.Find(cellData.itemId);
                if (def != null)
                {
                    bubble.Place(cellData.x, cellData.y, cellData.z, def);
                    var cell = bubble.Get(cellData.x, cellData.y, cellData.z);
                    if (cell != null)
                    {
                        cell.Rotation = cellData.rotation;
                        cell.AutoTileVariant = (TileVariant)cellData.autoTileVariant;
                        cell.FromIntervention = cellData.fromIntervention;
                        cell.GroundItemId = cellData.groundItemId;
                    }
                }
            }
            return bubble;
        }

        public static string[] ListSavedWorlds()
        {
            if (!Directory.Exists(WorldsDirectory))
                return Array.Empty<string>();

            var results = new List<string>();
            foreach (string file in Directory.GetFiles(WorldsDirectory, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var data = JsonUtility.FromJson<BubbleWorldSaveData>(json);
                    if (!string.IsNullOrEmpty(data.worldName))
                        results.Add(data.worldName);
                }
                catch
                {
                    results.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            return results.ToArray();
        }

        public static void Delete(string worldName)
        {
            string path = GetPath(worldName);
            if (File.Exists(path))
                File.Delete(path);
        }

        public static bool TryLoadLegacy(out LoadResult result)
        {
            result = null;
            string path = Path.Combine(Application.persistentDataPath, LegacyFileName);
            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                var oldData = JsonUtility.FromJson<BubbleSaveData>(json);
                var environment = (StarterEnvironment)Enum.Parse(typeof(StarterEnvironment), oldData.environment);
                var bubble = new BubbleModel(environment, oldData.width, oldData.height);

                foreach (var cellData in oldData.cells)
                {
                    var def = BubblePlacementCatalog.Find(cellData.itemId);
                    if (def != null)
                        bubble.Place(cellData.x, 0, cellData.y, def);
                }

                result = new LoadResult
                {
                    Bubble = bubble,
                    Soulform = null,
                    WorldName = environment + " World",
                    WeatherCondition = "Clear",
                    LivabilityPercent = oldData.livability
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static SoulformSaveData ToSaveData(SoulformModel model)
        {
            return new SoulformSaveData
            {
                name = model.Name,
                bodyTypeId = model.AppearanceId,
                temperament = model.Temperament.ToString(),
                gridX = model.GridX,
                gridY = model.GridY,
                gridZ = model.GridZ,
                mood = model.Mood.ToString(),
                comfort = model.Comfort,
                hunger = model.Hunger,
                curiosity = model.Curiosity,
                currentActivity = model.CurrentActivity,
                currentThought = model.CurrentThought,
                skinToneR = model.SkinTone.r,
                skinToneG = model.SkinTone.g,
                skinToneB = model.SkinTone.b,
                markingsId = model.MarkingsId,
                skinDetailsId = model.SkinDetailsId,
                hairStyleId = model.HairStyleId,
                hairColorR = model.HairColor.r,
                hairColorG = model.HairColor.g,
                hairColorB = model.HairColor.b,
                hairMaterialId = model.HairMaterialId,
                facialHairId = model.FacialHairId,
                voicePresetId = model.VoicePresetId,
                voicePitch = model.VoicePitch,
                voiceTone = model.VoiceTone,
                voiceTimbre = model.VoiceTimbre,
                outfitId = model.OutfitId,
                topId = model.TopId,
                bottomsId = model.BottomsId,
                footwearId = model.FootwearId,
                accessoryId = model.AccessoryId,
                startingAffinity = model.StartingAffinity,
                initialCalling = model.InitialCalling,
                preferredBiome = model.PreferredBiome.ToString(),
                bondTraits = model.BondTraits,
                meaning = model.Meaning,
                heightId = model.HeightId,
                faceShapeId = model.FaceShapeId,
                eyesId = model.EyesId,
                earsId = model.EarsId,
                noseId = model.NoseId
            };
        }

        private static SoulformModel FromSaveData(SoulformSaveData data)
        {
            var model = new SoulformModel
            {
                Name = data.name ?? "Soulform",
                Meaning = data.meaning ?? "",
                Temperament = Enum.TryParse(data.temperament, out SoulformTemperament t) ? t : SoulformTemperament.Inquisitive,
                Mood = Enum.TryParse(data.mood, out SoulformMood m) ? m : SoulformMood.Content,
                GridX = data.gridX,
                GridY = data.gridY,
                GridZ = data.gridZ,
                Comfort = data.comfort,
                Hunger = data.hunger,
                Curiosity = data.curiosity,
                CurrentActivity = data.currentActivity ?? "Observing",
                CurrentThought = data.currentThought ?? "I am here.",
                AppearanceId = data.bodyTypeId ?? "default",
                SkinTone = new Color(data.skinToneR, data.skinToneG, data.skinToneB),
                MarkingsId = data.markingsId ?? "none",
                SkinDetailsId = data.skinDetailsId ?? "none",
                HairStyleId = data.hairStyleId ?? "default",
                HairColor = new Color(data.hairColorR, data.hairColorG, data.hairColorB),
                HairMaterialId = data.hairMaterialId ?? "default",
                FacialHairId = data.facialHairId ?? "none",
                VoicePresetId = data.voicePresetId ?? "default",
                VoicePitch = data.voicePitch,
                VoiceTone = data.voiceTone,
                VoiceTimbre = data.voiceTimbre,
                OutfitId = data.outfitId ?? "",
                TopId = data.topId ?? "",
                BottomsId = data.bottomsId ?? "",
                FootwearId = data.footwearId ?? "",
                AccessoryId = data.accessoryId ?? "",
                StartingAffinity = data.startingAffinity,
                InitialCalling = data.initialCalling ?? "",
                PreferredBiome = Enum.TryParse(data.preferredBiome, out StarterEnvironment b) ? b : StarterEnvironment.MeadowGrassland,
                BondTraits = data.bondTraits ?? Array.Empty<string>(),
                HeightId = data.heightId ?? "average",
                FaceShapeId = data.faceShapeId ?? "default",
                EyesId = data.eyesId ?? "default",
                EarsId = data.earsId ?? "default",
                NoseId = data.noseId ?? "default"
            };
            return model;
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed_World";
            char[] invalid = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        }
    }
}
