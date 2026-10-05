using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using BepInEx;
using CommonAPI;
using DarkFogSynthesis.Core.Definitions;
using HarmonyLib;

namespace DarkFogSynthesis.Diagnostics
{
    /// <summary>Observed state, never a repair, release approval, or gameplay certificate.</summary>
    internal static class CompatibilityReport
    {
        private static readonly string ProcessId = System.Guid.NewGuid().ToString("N");
        private sealed class SessionIdentity { internal readonly string Value = System.Guid.NewGuid().ToString("N"); }
        private static readonly ConditionalWeakTable<GameData, SessionIdentity> SessionIds = new ConditionalWeakTable<GameData, SessionIdentity>();
        private static readonly HashSet<int> OwnedRecipes = new HashSet<int>(FrozenContent.Recipes.Select(r => r.Id.Value));

        internal static string GameVersion
        {
            get
            {
                var field = AccessTools.Field(typeof(GameConfig), "gameVersion");
                return field?.GetValue(null)?.ToString() ?? typeof(GameData).Assembly.GetName().Version?.ToString() ?? "unknown";
            }
        }

        internal static string Export(bool registered, bool sessionReady, string? error,
            string stage = "manual", GameData? data = null, bool extendToCombat = false, bool? peaceOverride = null)
        {
            if (stage == "manual") data = data ?? GameMain.data;
            bool? peace = peaceOverride ?? data?.gameDesc?.isPeaceMode;
            // Discovery/machine arrays belong to the simulation. Only inspect them in a paused,
            // validated current session from the manual UI action, never an import worker callback.
            bool inspectLive = stage == "manual" && data != null && sessionReady && GameMain.isPaused &&
                !GameMain.isLoading && ReferenceEquals(GameMain.data, data);
            var report = new
            {
                schemaVersion = 3,
                capturedUtc = DateTime.UtcNow.ToString("O"),
                processId = ProcessId,
                sessionId = data == null ? null : SessionIds.GetValue(data, _ => new SessionIdentity()).Value,
                stage,
                policy = new { peaceMode = peace, extendToCombat, effectiveApply = peace.HasValue ? (bool?)(peace.Value || extendToCombat) : null },
                status = "EXPERIMENTAL_NOT_GAME_VALIDATED",
                gameVersion = GameVersion,
                unityVersion = UnityEngine.Application.unityVersion,
                clrVersion = Environment.Version.ToString(),
                pluginVersion = Plugin.Version,
                prototypesRegistered = registered,
                sessionReady,
                error,
                releaseBlockers = new[] { "Target-version production/discovery checks", "Inventory-preserving cleanup and vanilla reload", "Integrity, achievements, metadata and online checks", "Measured bilingual technology layout" },
                assemblies = new[] { typeof(GameData).Assembly, typeof(BaseUnityPlugin).Assembly, typeof(CommonAPIPlugin).Assembly,
                    typeof(xiaoye97.LDBTool).Assembly, typeof(Plugin).Assembly, typeof(FrozenContent).Assembly, typeof(Harmony).Assembly }
                    .Distinct().OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
                    .Select(a => new { name = a.GetName().Name, version = a.GetName().Version?.ToString(), sha256 = Hash(a.Location) }).ToArray(),
                // Whole static tables permit reporting unrelated drift. Arrays are detached now;
                // no serializer follows mutable native collections later or on another thread.
                recipes = LDB.recipes.dataArray.Where(r => r != null).OrderBy(r => r.ID)
                    .Select(r => new { id = r.ID, nameKey = r.Name, type = r.Type.ToString(), r.Handcraft, r.NonProductive,
                        r.productive, r.TimeSpend, Items = Copy(r.Items), ItemCounts = Copy(r.ItemCounts), Results = Copy(r.Results),
                        ResultCounts = Copy(r.ResultCounts), r.GridIndex, unlockTech = r.preTech?.ID }).ToArray(),
                items = LDB.items.dataArray.Where(i => i != null).OrderBy(i => i.ID)
                    .Select(i => new { id = i.ID, nameKey = i.Name, i.UnlockKey, type = i.Type.ToString(), i.GridIndex,
                        recipes = i.recipes?.Select(r => r?.ID).ToArray(), handcrafts = i.handcrafts?.Select(r => r?.ID).ToArray(),
                        maincraft = i.maincraft?.ID, i.maincraftProductCount, handcraft = i.handcraft?.ID, i.handcraftProductCount }).ToArray(),
                technologies = LDB.techs.dataArray.Where(t => t != null).OrderBy(t => t.ID)
                    .Select(t => new { id = t.ID, page = t.page, nameKey = t.Name, t.IsHiddenTech, t.Published, t.IsObsolete,
                        PreItem = Copy(t.PreItem), PreTechs = Copy(t.PreTechs), PreTechsImplicit = Copy(t.PreTechsImplicit),
                        UnlockRecipes = Copy(t.UnlockRecipes), Items = Copy(t.Items), ItemPoints = Copy(t.ItemPoints), t.HashNeeded,
                        position = new { x = t.Position.x, y = t.Position.y },
                        preCache = t.preTechArray?.Select(p => p?.ID).ToArray(), postCache = t.postTechArray?.Select(p => p?.ID).ToArray(),
                        unlockCache = t.unlockRecipeArray?.Select(p => p?.ID).ToArray(), isLabTech = ObserveLabClassification(t) }).ToArray(),
                liveObservationStatus = inspectLive ? "paused-current-session" : "not_captured_pause_a_validated_session_and_export_manually",
                discoveryObservation = !inspectLive || data?.history == null ? null : new
                {
                    peaceMode = data.gameDesc.isPeaceMode,
                    items = FrozenContent.Recipes.Select(r => new { itemId = r.Output.Item.Value,
                        nativeItemUnlocked = data.history.ItemUnlocked(r.Output.Item.Value),
                        recordedSpecialDiscovery = data.history.enemyDropItemUnlocked.Contains(r.Output.Item.Value) }).ToArray()
                },
                machines = inspectLive ? ObserveMachines(data!) : Array.Empty<object>()
            };
            // Serialize the detached snapshot before creating any output file.
            string json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(report);
            string directory = Path.Combine(Paths.ConfigPath, "DarkFogSynthesis", "diagnostics");
            Directory.CreateDirectory(directory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            string path = Path.Combine(directory, "runtime-" + stage + "-" + stamp + ".json");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream)) writer.Write(json);
            return path;
        }

        private static int[]? Copy(int[]? values) => values?.ToArray();

        private static bool? ObserveLabClassification(TechProto tech)
        {
            // Optional diagnostic only; original API visibility is not assumed from publicized references.
            var property = AccessTools.Property(typeof(TechProto), "IsLabTech");
            if (property?.GetValue(tech, null) is bool value) return value;
            return AccessTools.Field(typeof(TechProto), "IsLabTech")?.GetValue(tech) is bool field ? (bool?)field : null;
        }

        private static object? Execution(RecipeExecuteData? cache) => cache == null ? null : new
        {
            cache.productive, cache.timeSpend, cache.extraTimeSpend,
            requires = Copy(cache.requires), requireCounts = Copy(cache.requireCounts),
            products = Copy(cache.products), productCounts = Copy(cache.productCounts)
        };

        private static object[] ObserveMachines(GameData data)
        {
            var records = new List<object>();
            for (int f = 0; f < data.factoryCount; f++)
            {
                var factory = data.factories[f];
                var system = factory?.factorySystem;
                if (system == null) continue;
                for (int id = 1; id < system.assemblerCursor; id++)
                {
                    var machine = system.assemblerPool[id];
                    if (machine.id != id || !OwnedRecipes.Contains(machine.recipeId)) continue;
                    records.Add(new { kind = "assembler", planetId = factory!.planetId, id, machine.recipeId,
                        cache = Execution(machine.recipeExecuteData), served = Copy(machine.served), incServed = Copy(machine.incServed),
                        produced = Copy(machine.produced), machine.time, machine.extraTime, machine.replicating });
                }
                for (int id = 1; id < system.labCursor; id++)
                {
                    var machine = system.labPool[id];
                    if (machine.id != id || !OwnedRecipes.Contains(machine.recipeId)) continue;
                    records.Add(new { kind = "lab", planetId = factory!.planetId, id, machine.recipeId,
                        cache = Execution(machine.recipeExecuteData), served = Copy(machine.served), incServed = Copy(machine.incServed),
                        produced = Copy(machine.produced), machine.time, machine.extraTime, machine.replicating, machine.researchMode });
                }
            }
            return records.ToArray();
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
