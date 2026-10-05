using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Registration
{
    /// <summary>One registration attempt's detached native arguments; not another readiness state.</summary>
    public sealed class PreparedRecipe
    {
        public RecipeDefinition Definition { get; }
        public int GridIndex { get; }
        public int[] Items { get; }
        public int[] ItemCounts { get; }
        public int[] Results { get; }
        public int[] ResultCounts { get; }
        internal PreparedRecipe(RecipeDefinition definition, int grid)
        {
            Definition = definition;
            GridIndex = grid;
            Items = definition.Inputs.Select(v => v.Item.Value).ToArray();
            ItemCounts = definition.Inputs.Select(v => v.Count).ToArray();
            Results = new[] { definition.Output.Item.Value };
            ResultCounts = new[] { definition.Output.Count };
        }
    }

    public sealed class PreparedTechnology<TIcon> where TIcon : class
    {
        public TechDefinition Definition { get; }
        public TIcon Icon { get; }
        public int[] Prerequisites { get; }
        public int[] ImplicitPrerequisites { get; }
        public int[] ResearchItems { get; }
        public int[] ItemPoints { get; }
        public int[] UnlockRecipes { get; }
        internal PreparedTechnology(TechDefinition definition, TIcon icon)
        {
            Definition = definition;
            Icon = icon;
            Prerequisites = definition.ExplicitPrerequisites.Select(v => v.Value).ToArray();
            ImplicitPrerequisites = definition.ImplicitPrerequisites.Select(v => v.Value).ToArray();
            ResearchItems = definition.ResearchCost.Select(v => v.Item.Value).ToArray();
            ItemPoints = definition.CandidateItemPoints.ToArray();
            UnlockRecipes = definition.UnlockRecipes.Select(v => v.Value).ToArray();
        }
    }

    public sealed class PreparedContent<TIcon> where TIcon : class
    {
        public IReadOnlyList<PreparedTechnology<TIcon>> Technologies { get; }
        public IReadOnlyList<PreparedRecipe> Recipes { get; }
        internal PreparedContent(PreparedTechnology<TIcon>[] technologies, PreparedRecipe[] recipes)
        {
            Technologies = Array.AsReadOnly(technologies);
            Recipes = Array.AsReadOnly(recipes);
        }
    }

    public static class ContentPreparation
    {
        /// <summary>Resolve all assets, then allocate the tab and check every slot before any caller queues prototypes.
        /// Tab allocation may have framework side effects. Failure does not authorize retry or pretend rollback.</summary>
        public static PreparedContent<TIcon> Prepare<TIcon>(Func<TechDefinition, TIcon> resolveIcon,
            Func<int> allocateTab, Action<int> validateSlot) where TIcon : class
        {
            if (resolveIcon == null) throw new ArgumentNullException(nameof(resolveIcon));
            if (allocateTab == null) throw new ArgumentNullException(nameof(allocateTab));
            if (validateSlot == null) throw new ArgumentNullException(nameof(validateSlot));
            var technologies = FrozenContent.Technologies.Select(d =>
                new PreparedTechnology<TIcon>(d, resolveIcon(d) ?? throw new InvalidOperationException("Missing technology icon: " + d.Key))).ToArray();
            int tab = allocateTab();
            if (tab <= 0) throw new InvalidOperationException("Invalid assigned recipe tab.");
            var recipes = FrozenContent.Recipes.Select((d, i) =>
                new PreparedRecipe(d, checked(tab * 1000 + 101 + i))).ToArray();
            foreach (var recipe in recipes) validateSlot(recipe.GridIndex);
            return new PreparedContent<TIcon>(technologies, recipes);
        }
    }
}
