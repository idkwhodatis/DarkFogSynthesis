using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Registration;
using DarkFogSynthesis.Core.Compatibility;
using DarkFogSynthesis.Progression;
using HarmonyLib;
using UnityEngine;
using xiaoye97;

namespace DarkFogSynthesis.Registration
{
    /// <summary>Queues content once, then binds references only after both prototype sets exist.</summary>
    internal sealed class ContentRegistry
    {
        // Only immutable definitions are cached. Live prototype identity and execution arrays are
        // still read and validated at every existing import/session checkpoint.
        private static readonly IReadOnlyDictionary<int, RecipeDefinition> recipeDefinitions =
            FrozenContent.Recipes.ToDictionary(r => r.Id.Value);
        private readonly Dictionary<int, RecipeProto> recipes = new Dictionary<int, RecipeProto>();
        private readonly Dictionary<int, TechProto> technologies = new Dictionary<int, TechProto>();
        private bool queued;
        private IReadOnlyCollection<int> combinedPreCacheTechs = Array.Empty<int>();
        internal bool Ready { get; private set; }
        internal bool OwnsRecipe(int id) => recipes.TryGetValue(id, out var recipe) && ReferenceEquals(LDB.recipes.Select(id), recipe);

        internal void Register()
        {
            if (queued) return;
            // Validate the entire batch before handing any prototype to LDBTool.
            var pending = PendingPrototypes().ToArray();
            foreach (var definition in FrozenContent.Technologies)
                Require(LDB.techs.Select(definition.Id.Value) == null && !pending.OfType<TechProto>().Any(p => p.ID == definition.Id.Value),
                    "Technology ID collision: " + definition.Id);
            foreach (var definition in FrozenContent.Recipes)
                Require(LDB.recipes.Select(definition.Id.Value) == null && !pending.OfType<RecipeProto>().Any(p => p.ID == definition.Id.Value),
                    "Recipe ID collision: " + definition.Id);
            foreach (var item in FrozenContent.Recipes.SelectMany(r => r.Inputs.Concat(new[] { r.Output }))
                .Select(i => i.Item.Value).Concat(FrozenContent.Technologies.SelectMany(t => t.ResearchCost.Select(i => i.Item.Value))).Distinct())
                Require(LDB.items.Select(item) != null, "Required vanilla ItemProto is missing: " + item);
            foreach (var tech in RequiredVanillaTechs())
                Require(LDB.techs.Select(tech) != null, "Required vanilla TechProto is missing: " + tech);
            foreach (var output in FrozenContent.Recipes.Select(r => r.Output.Item.Value))
                Require(LDB.items.Select(output).UnlockKey == -2, "Unexpected vanilla special-item discovery key: " + output);
            foreach (var edge in FrozenContent.CombatPrerequisites)
            {
                var hidden = LDB.techs.Select(edge.HiddenTech.Value);
                Require(hidden.IsHiddenTech && hidden.PreItem != null && hidden.PreItem.Length > 0,
                    "Unexpected vanilla discovery structure on technology " + edge.HiddenTech);
            }

            var layout = TechLayoutResolver.Resolve(Diagnostics.CompatibilityReport.GameVersion);
            var knownTechAnchors = LDB.techs.dataArray.Concat(pending.OfType<TechProto>()).Where(t => t != null)
                .Select(t => new KeyValuePair<TechId, TechPosition>(new TechId(t.ID), new TechPosition(t.Position.x, t.Position.y))).ToArray();
            foreach (var node in layout.Candidates)
                Require(TechLayoutResolver.FindMainTreeAnchorCollisions(node.Tech, node.Position, knownTechAnchors).Count == 0,
                    "Candidate technology position is occupied by a current or pending main-page node; do not move vanilla nodes. ID " + node.Tech);

            using (ProtoRegistry.StartModLoad(Plugin.Guid))
            {
                int tab = TabSystem.RegisterTab(Plugin.Guid + ".recipes", new TabData(
                    ProtoIds.StringKey("recipes.tab"), LDB.items.Select(VanillaIds.Items.DarkFogMatrix.Value).IconPath));
                int column = 1;
                foreach (var definition in FrozenContent.Technologies)
                {
                    var position = layout.Candidates.Single(p => p.Tech == definition.Id).Position;
                    TechProto tech = ProtoRegistry.RegisterTech(definition.Id.Value, definition.NameKey,
                        definition.DescriptionKey, definition.ConclusionKey, "",
                        definition.ExplicitPrerequisites.Select(p => p.Value).ToArray(),
                        definition.ResearchCost.Select(p => p.Item.Value).ToArray(), definition.CandidateItemPoints.ToArray(),
                        definition.HashNeeded, definition.UnlockRecipes.Select(p => p.Value).ToArray(), new Vector2(position.X, position.Y));
                    Require(tech.ID == definition.Id.Value, "LDBTool changed a stable technology ID. Restore its default CustomID entry; migration is not supported.");
                    tech.PreTechsImplicit = definition.ImplicitPrerequisites.Select(p => p.Value).ToArray();
                    tech.Level = 0;
                    tech.MaxLevel = 0;
                    tech.IsHiddenTech = false;
                    AccessTools.Field(typeof(TechProto), "_iconSprite").SetValue(tech,
                        Localization.Strings.LoadIcon(definition.Key == "energy_analysis" ? "energy-analysis" : "information-topology"));
                    technologies.Add(definition.Id.Value, tech);
                }
                foreach (var definition in FrozenContent.Recipes)
                {
                    int grid = tab * 1000 + 100 + column++;
                    Require(!pending.OfType<RecipeProto>().Concat(LDB.recipes.dataArray).Any(p => p.GridIndex == grid),
                        "Recipe selector grid collision: " + grid);
                    RecipeProto recipe = ProtoRegistry.RegisterRecipe(definition.Id.Value, RecipeType(definition.Machine),
                        definition.TimeSpendTicks, definition.Inputs.Select(p => p.Item.Value).ToArray(),
                        definition.Inputs.Select(p => p.Count).ToArray(), new[] { definition.Output.Item.Value },
                        new[] { definition.Output.Count }, ProtoIds.StringKey("recipe." + definition.Key + ".description"),
                        definition.UnlockTech.Value, grid, definition.NameKey, LDB.items.Select(definition.Output.Item.Value).IconPath);
                    Require(recipe.ID == definition.Id.Value && recipe.GridIndex == grid,
                        "LDBTool changed a stable recipe ID or occupied selector slot. Restore its default CustomID/CustomGridIndex entry.");
                    recipe.Handcraft = true;
                    recipe.NonProductive = false;
                    SetProductive(recipe);
                    recipes.Add(definition.Id.Value, recipe);
                }
            }
            queued = true;
        }

        internal void BindAndValidate()
        {
            Require(queued, "Content registration did not run.");
            if (Ready) return;
            foreach (var definition in FrozenContent.Recipes)
            {
                RecipeProto recipe = LDB.recipes.Select(definition.Id.Value);
                TechProto tech = LDB.techs.Select(definition.UnlockTech.Value);
                Require(ReferenceEquals(recipe, recipes[definition.Id.Value]), "Recipe ID was replaced by another mod: " + definition.Id);
                if (tech == null) throw new InvalidOperationException("Missing recipe unlock technology: " + definition.UnlockTech);
                var unlocks = tech.UnlockRecipes ?? Array.Empty<int>();
                if (!unlocks.Contains(recipe.ID)) tech.UnlockRecipes = unlocks.Concat(new[] { recipe.ID }).ToArray();
                tech.unlockRecipeArray = tech.UnlockRecipes.Select(id => LDB.recipes.Select(id)).ToArray();
                Require(tech.unlockRecipeArray.All(p => p != null), "Unresolved unlock recipe on technology " + tech.ID);
                recipe.preTech = tech;
                recipe.NonProductive = false;
                SetProductive(recipe); // Set before LDBTool creates RecipeExecuteData from this value.
                ItemProto output = LDB.items.Select(definition.Output.Item.Value);
                output.recipes = output.recipes ?? new List<RecipeProto>();
                output.handcrafts = output.handcrafts ?? new List<RecipeProto>();
                if (!output.recipes.Contains(recipe)) output.recipes.Add(recipe);
                if (!output.handcrafts.Contains(recipe)) output.handcrafts.Add(recipe);
                if (output.maincraft == null) { output.maincraft = recipe; output.maincraftProductCount = definition.Output.Count; }
                if (output.handcraft == null) { output.handcraft = recipe; output.handcraftProductCount = definition.Output.Count; }
            }
            foreach (var definition in FrozenContent.Technologies)
            {
                TechProto tech = LDB.techs.Select(definition.Id.Value);
                Require(ReferenceEquals(tech, technologies[definition.Id.Value]), "Technology ID was replaced by another mod: " + definition.Id);
                tech.Preload();
                tech.Preload2();
                var iconField = AccessTools.Field(typeof(TechProto), "_iconSprite");
                if (iconField == null) throw new InvalidOperationException("Unsupported technology icon cache.");
                iconField.SetValue(tech, Localization.Strings.LoadIcon(definition.Key == "energy_analysis" ? "energy-analysis" : "information-topology"));
                Require(tech.iconSprite != null, "Technology icon could not be loaded: " + definition.Id);
                foreach (int prerequisite in tech.PreTechs)
                {
                    var parent = LDB.techs.Select(prerequisite);
                    var children = parent.postTechArray ?? Array.Empty<TechProto>();
                    if (!children.Contains(tech)) parent.postTechArray = children.Concat(new[] { tech }).ToArray();
                }
            }
            combinedPreCacheTechs = LiveProgressionValidator.CaptureSynthesisPreCacheModes();
            Validate();
            Ready = true;
        }

        internal void Validate()
        {
            foreach (var definition in FrozenContent.Recipes)
            {
                RecipeProto recipe = LDB.recipes.Select(definition.Id.Value);
                Require(ReferenceEquals(recipe, recipes[definition.Id.Value]), "Prototype identity changed: " + definition.Id);
                Require(recipe.preTech != null && recipe.preTech.ID == definition.UnlockTech.Value, "Incorrect preTech: " + definition.Id);
                Require(recipe.Handcraft && recipe.productive && !recipe.NonProductive && recipe.Type == RecipeType(definition.Machine), "Recipe capabilities changed: " + definition.Id);
                Require(recipe.TimeSpend == definition.TimeSpendTicks && recipe.Items.SequenceEqual(definition.Inputs.Select(i => i.Item.Value))
                    && recipe.ItemCounts.SequenceEqual(definition.Inputs.Select(i => i.Count))
                    && recipe.Results.SequenceEqual(new[] { definition.Output.Item.Value })
                    && recipe.ResultCounts.SequenceEqual(new[] { definition.Output.Count }), "Frozen recipe data changed: " + definition.Id);
                var owners = LDB.techs.dataArray.Where(t => (t.UnlockRecipes ?? Array.Empty<int>()).Contains(recipe.ID)).Select(t => t.ID).ToArray();
                Require(owners.SequenceEqual(new[] { definition.UnlockTech.Value }), "Recipe has an unexpected or duplicate unlock owner: " + definition.Id);
                var owner = LDB.techs.Select(definition.UnlockTech.Value);
                var expectedUnlocks = owner.UnlockRecipes.Select(id => LDB.recipes.Select(id)).ToArray();
                Require(owner.unlockRecipeArray != null && owner.unlockRecipeArray.Length == expectedUnlocks.Length &&
                    owner.unlockRecipeArray.Zip(expectedUnlocks, ReferenceEquals).All(same => same),
                    "Unlock recipe cache changed after binding: technology " + owner.ID);
                var output = LDB.items.Select(definition.Output.Item.Value);
                Require(output.recipes != null && output.recipes.Count(r => ReferenceEquals(r, recipe)) == 1 &&
                    output.handcrafts != null && output.handcrafts.Count(r => ReferenceEquals(r, recipe)) == 1,
                    "The output item's recipe/handcraft caches do not contain the registered recipe exactly once: " + definition.Id);
                ValidateItemFallback(output.maincraft, output.maincraftProductCount, output.ID, "maincraft");
                ValidateItemFallback(output.handcraft, output.handcraftProductCount, output.ID, "handcraft");
                Require(LDB.recipes.dataArray.Count(r => r.ID == recipe.ID) == 1, "Duplicate recipe ID: " + recipe.ID);
                Require(LDB.recipes.dataArray.Count(r => r.GridIndex == recipe.GridIndex) == 1, "Recipe selector collision: " + recipe.GridIndex);
            }
            foreach (var definition in FrozenContent.Technologies)
            {
                var tech = LDB.techs.Select(definition.Id.Value);
                Require(ReferenceEquals(tech, technologies[definition.Id.Value]) && LDB.techs.dataArray.Count(t => t.ID == tech.ID) == 1,
                    "Technology ID collision: " + definition.Id);
                Require(tech.Published && !tech.IsObsolete,
                    "Synthesis technology is unpublished or obsolete: " + definition.Id);
                Require(tech.HashNeeded == definition.HashNeeded && tech.ItemPoints.SequenceEqual(definition.CandidateItemPoints) &&
                    tech.Items.SequenceEqual(definition.ResearchCost.Select(i => i.Item.Value)), "New technology research costs changed.");
                Require(tech.PreItem.Length == 0 && !tech.IsHiddenTech && tech.AddItems.Length == 0 && tech.UnlockFunctions.Length == 0,
                    "Unexpected new technology effects.");
                var finalAnchors = LDB.techs.dataArray.Where(t => t != null).Select(t =>
                    new KeyValuePair<TechId, TechPosition>(new TechId(t.ID), new TechPosition(t.Position.x, t.Position.y)));
                Require(TechLayoutResolver.FindMainTreeAnchorCollisions(definition.Id, new TechPosition(tech.Position.x, tech.Position.y), finalAnchors).Count == 0,
                    "A later registration occupied this mod's main-page technology anchor: " + definition.Id + ". Exact-anchor checks do not certify expanded layout bounds.");
            }
            LiveProgressionValidator.ValidateSynthesis(combinedPreCacheTechs);
        }

        internal void ValidateExecutionCache()
        {
            var field = AccessTools.Field(typeof(RecipeProto), "recipeExecuteData");
            if (!(field?.GetValue(null) is IDictionary<int, RecipeExecuteData> cache))
                throw new InvalidOperationException("Unsupported or uninitialized native recipe execution cache.");
            foreach (var definition in FrozenContent.Recipes)
            {
                Require(cache.TryGetValue(definition.Id.Value, out var data), "Missing native execution cache: " + definition.Id);
                Require(ExecutionMatches(data, definition),
                    "Native execution cache does not match the frozen recipe: " + definition.Id);
            }
        }

        internal void ValidateSavedRecipeCaches(GameData game)
        {
            // A valid global cache does not establish that existing machines refer to matching execute data.
            // Validate only our recipe IDs; do not globally replace other mods' buffers or execution references.
            for (int f = 0; f < game.factoryCount; ++f)
            {
                var factory = game.factories[f];
                if (factory?.factorySystem == null) continue;
                var system = factory.factorySystem;
                for (int i = 1; i < system.assemblerCursor; ++i)
                {
                    var machine = system.assemblerPool[i];
                    if (machine.id != i || !recipeDefinitions.TryGetValue(machine.recipeId, out var definition)) continue;
                    if (!ExecutionMatches(machine.recipeExecuteData, definition) ||
                        !BufferShapeMatches(machine.served, machine.incServed, machine.produced, definition))
                        throw new InvalidOperationException("Saved assembler cache/buffer shape mismatch for recipe " + definition.Id + " on planet " + factory.planetId);
                }
                for (int i = 1; i < system.labCursor; ++i)
                {
                    var machine = system.labPool[i];
                    if (machine.id != i || machine.researchMode || !recipeDefinitions.TryGetValue(machine.recipeId, out var definition)) continue;
                    if (!ExecutionMatches(machine.recipeExecuteData, definition) ||
                        !BufferShapeMatches(machine.served, machine.incServed, machine.produced, definition))
                        throw new InvalidOperationException("Saved lab cache/buffer shape mismatch for recipe " + definition.Id + " on planet " + factory.planetId);
                }
            }
        }

        internal void ValidateImportedAssembler(AssemblerComponent machine)
        {
            if (machine.id <= 0 || !OwnsRecipe(machine.recipeId)) return;
            var definition = recipeDefinitions[machine.recipeId];
            Require(ExecutionMatches(machine.recipeExecuteData, definition) &&
                BufferShapeMatches(machine.served, machine.incServed, machine.produced, definition),
                "Owned assembler import has incompatible execute data or buffer shape. Load stopped before LDBTool's known buffer sanitizer; no replacement arrays were created.");
        }

        internal void ValidateImportedLab(LabComponent machine)
        {
            if (machine.id <= 0 || machine.researchMode || !OwnsRecipe(machine.recipeId)) return;
            var definition = recipeDefinitions[machine.recipeId];
            Require(ExecutionMatches(machine.recipeExecuteData, definition) &&
                BufferShapeMatches(machine.served, machine.incServed, machine.produced, definition),
                "Owned lab import has incompatible execute data or buffer shape. Load stopped without clearing its buffers.");
        }

        private static bool ExecutionMatches(RecipeExecuteData? data, RecipeDefinition definition) => data != null &&
            RecipeExecutionContract.Matches(definition, data.productive, data.timeSpend, data.extraTimeSpend,
                data.requires, data.requireCounts, data.products, data.productCounts);

        private static bool BufferShapeMatches(int[]? served, int[]? inc, int[]? produced, RecipeDefinition definition) =>
            RecipeBufferContract.Matches(definition, served, inc, produced);

        private static IEnumerable<int> RequiredVanillaTechs() => FrozenContent.Technologies
            .SelectMany(t => t.ExplicitPrerequisites.Concat(t.ImplicitPrerequisites)).Select(t => t.Value)
            .Concat(FrozenContent.Recipes.Select(r => r.UnlockTech.Value).Where(id => id != ProtoIds.EnergyAnalysis.Value && id != ProtoIds.InformationTopology.Value))
            .Concat(FrozenContent.CombatPrerequisites.SelectMany(p => new[] { p.HiddenTech.Value, p.RequiredCombatTech.Value })).Distinct();

        private static IEnumerable<Proto> PendingPrototypes()
        {
            // LDBTool exposes no public collision query; inspect its documented staging queues, never mutate them.
            var field = AccessTools.Field(typeof(LDBTool), "TotalDict");
            if (field == null) throw new InvalidOperationException("Unsupported LDBTool: no staging collision inventory.");
            if (!(field.GetValue(null) is IEnumerable sets)) throw new InvalidOperationException("Cannot inspect LDBTool staging prototypes.");
            foreach (IEnumerable set in sets) foreach (object value in set) if (value is Proto proto) yield return proto;
        }

        private static ERecipeType RecipeType(ProductionMachine machine) => machine == ProductionMachine.Smelter ? ERecipeType.Smelt
            : machine == ProductionMachine.MatrixLab ? ERecipeType.Research : ERecipeType.Assemble;
        private static void ValidateItemFallback(RecipeProto? recipe, int cachedCount, int itemId, string cacheName)
        {
            Require(recipe != null && ReferenceEquals(LDB.recipes.Select(recipe.ID), recipe),
                "Missing or stale " + cacheName + " recipe cache for item " + itemId);
            int result = Array.IndexOf(recipe!.Results, itemId);
            Require(result >= 0 && result < recipe.ResultCounts.Length && recipe.ResultCounts[result] > 0 && cachedCount == recipe.ResultCounts[result],
                "Incorrect " + cacheName + " product-count cache for item " + itemId);
            // This may legitimately be another mod's existing alternate recipe. Never replace it during validation.
        }
        private static void SetProductive(RecipeProto recipe)
        {
            // The current game's setter is nonpublic; publicized reference assemblies must not hide that boundary.
            var setter = AccessTools.PropertySetter(typeof(RecipeProto), nameof(RecipeProto.productive));
            if (setter == null) throw new InvalidOperationException("Unsupported RecipeProto productivity cache API.");
            setter.Invoke(recipe, new object[] { true });
        }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
