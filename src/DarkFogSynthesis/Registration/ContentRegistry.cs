using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Registration;
using HarmonyLib;
using UnityEngine;
using xiaoye97;

namespace DarkFogSynthesis.Registration
{
    /// <summary>Queues content once, then binds references only after both prototype sets exist.</summary>
    internal sealed class ContentRegistry
    {
        private readonly Dictionary<int, RecipeProto> recipes = new Dictionary<int, RecipeProto>();
        private readonly Dictionary<int, TechProto> technologies = new Dictionary<int, TechProto>();
        private bool queued;
        internal bool Ready { get; private set; }

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
            foreach (var node in layout.Candidates)
                // CommonAPI's main-tree ID contract is <= 2000. Upgrade-page coordinates are a separate canvas.
                Require(!LDB.techs.dataArray.Any(t => t != null && t.ID <= 2000 && t.Position.x == node.Position.X && t.Position.y == node.Position.Y),
                    "Candidate technology position is occupied; do not move vanilla nodes. ID " + node.Tech);

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
                Require(LDB.recipes.dataArray.Count(r => r.ID == recipe.ID) == 1, "Duplicate recipe ID: " + recipe.ID);
                Require(LDB.recipes.dataArray.Count(r => r.GridIndex == recipe.GridIndex) == 1, "Recipe selector collision: " + recipe.GridIndex);
            }
            foreach (var definition in FrozenContent.Technologies)
            {
                var tech = LDB.techs.Select(definition.Id.Value);
                Require(ReferenceEquals(tech, technologies[definition.Id.Value]) && LDB.techs.dataArray.Count(t => t.ID == tech.ID) == 1,
                    "Technology ID collision: " + definition.Id);
                Require(tech.PreTechs.SequenceEqual(definition.ExplicitPrerequisites.Select(t => t.Value)) &&
                    tech.PreTechsImplicit.SequenceEqual(definition.ImplicitPrerequisites.Select(t => t.Value)), "New technology prerequisites changed.");
                Require(tech.HashNeeded == definition.HashNeeded && tech.ItemPoints.SequenceEqual(definition.CandidateItemPoints) &&
                    tech.Items.SequenceEqual(definition.ResearchCost.Select(i => i.Item.Value)), "New technology research costs changed.");
                Require(tech.PreItem.Length == 0 && !tech.IsHiddenTech && tech.AddItems.Length == 0 && tech.UnlockFunctions.Length == 0,
                    "Unexpected new technology effects.");
            }
        }

        internal void ValidateExecutionCache()
        {
            var field = AccessTools.Field(typeof(RecipeProto), "recipeExecuteData");
            if (!(field?.GetValue(null) is IDictionary<int, RecipeExecuteData> cache))
                throw new InvalidOperationException("Unsupported or uninitialized native recipe execution cache.");
            foreach (var definition in FrozenContent.Recipes)
            {
                Require(cache.TryGetValue(definition.Id.Value, out var data), "Missing native execution cache: " + definition.Id);
                Require(data!.productive && data.timeSpend == definition.TimeSpendTicks * 10000 && data.extraTimeSpend == definition.TimeSpendTicks * 100000
                    && data.requires.SequenceEqual(definition.Inputs.Select(i => i.Item.Value)) && data.requireCounts.SequenceEqual(definition.Inputs.Select(i => i.Count))
                    && data.products.SequenceEqual(new[] { definition.Output.Item.Value }) && data.productCounts.SequenceEqual(new[] { definition.Output.Count }),
                    "Native execution cache does not match the frozen recipe: " + definition.Id);
            }
        }

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
