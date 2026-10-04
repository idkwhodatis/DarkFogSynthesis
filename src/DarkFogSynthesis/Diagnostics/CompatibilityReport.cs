using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using BepInEx;
using CommonAPI;
using DarkFogSynthesis.Core.Definitions;
using HarmonyLib;

namespace DarkFogSynthesis.Diagnostics
{
    internal static class CompatibilityReport
    {
        internal static string GameVersion
        {
            get
            {
                var field = AccessTools.Field(typeof(GameConfig), "gameVersion");
                return field?.GetValue(null)?.ToString() ?? typeof(GameData).Assembly.GetName().Version?.ToString() ?? "unknown";
            }
        }

        internal static string Export(bool registered, string? error)
        {
            string directory = Path.Combine(Paths.ConfigPath, "DarkFogSynthesis", "diagnostics");
            Directory.CreateDirectory(directory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var report = new
            {
                schemaVersion = 1,
                capturedUtc = DateTime.UtcNow.ToString("O"),
                status = "EXPERIMENTAL_NOT_GAME_VALIDATED",
                gameVersion = GameVersion,
                unityVersion = UnityEngine.Application.unityVersion,
                clrVersion = Environment.Version.ToString(),
                pluginVersion = Plugin.Version,
                prototypesRegistered = registered,
                error,
                releaseBlockers = new[] { "Target-version P0 production/discovery checks", "Inventory-preserving cleanup and vanilla reload", "A/B/C/D integrity, achievements, metadata and Milky Way checks", "Measured bilingual technology layout" },
                assemblies = new[] { typeof(GameData).Assembly, typeof(BaseUnityPlugin).Assembly, typeof(CommonAPIPlugin).Assembly, typeof(xiaoye97.LDBTool).Assembly, typeof(Plugin).Assembly }
                    .Select(a => new { name = a.GetName().Name, version = a.GetName().Version?.ToString(), sha256 = Hash(a.Location) }).ToArray(),
                // This is an observed runtime snapshot, never an assertion that these fields passed gameplay tests.
                recipes = LDB.recipes.dataArray.Where(r => FrozenContent.Recipes.Any(d => d.Id.Value == r.ID))
                    .Select(r => new { id = r.ID, type = r.Type.ToString(), r.Handcraft, r.NonProductive, r.productive, r.TimeSpend, r.Items, r.ItemCounts, r.Results, r.ResultCounts, r.GridIndex, unlockTech = r.preTech?.ID }).ToArray(),
                items = LDB.items.dataArray.Where(i => FrozenContent.Recipes.Any(r => r.Output.Item.Value == i.ID || r.Inputs.Any(v => v.Item.Value == i.ID)))
                    .Select(i => new { id = i.ID, nameKey = i.Name, i.UnlockKey, type = i.Type.ToString(), i.GridIndex }).ToArray(),
                technologies = LDB.techs.dataArray.Select(t => new { id = t.ID, page = t.page, nameKey = t.Name, t.IsHiddenTech, t.Published,
                    t.PreItem, t.PreTechs, t.PreTechsImplicit, t.UnlockRecipes, t.Items, t.ItemPoints, t.HashNeeded,
                    position = new { x = t.Position.x, y = t.Position.y }, preCache = t.preTechArray?.Select(p => p.ID).ToArray(), postCache = t.postTechArray?.Select(p => p.ID).ToArray() }).ToArray(),
                discoveryObservation = GameMain.data?.history == null ? null : new
                {
                    peaceMode = GameMain.data.gameDesc.isPeaceMode,
                    items = FrozenContent.Recipes.Select(r => new { itemId = r.Output.Item.Value,
                        nativeItemUnlocked = GameMain.data.history.ItemUnlocked(r.Output.Item.Value),
                        recordedSpecialDiscovery = GameMain.data.history.enemyDropItemUnlocked.Contains(r.Output.Item.Value) }).ToArray()
                }
            };
            string path = Path.Combine(directory, "runtime-" + stamp + ".json");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream)) writer.Write(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(report));
            return path;
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
