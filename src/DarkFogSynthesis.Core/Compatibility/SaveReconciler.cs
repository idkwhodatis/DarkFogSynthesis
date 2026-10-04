using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Compatibility
{
    public static class SaveReconciler
    {
        /// <summary>
        /// Return only this mod's missing recipe unlocks whose designated technology is already completed.
        /// Use the native recipe-only unlock path, never a technology completion/reward path.
        /// Already researched custom technology is respected; merely late-game vanilla progress grants nothing.
        /// </summary>
        public static IReadOnlyList<RecipeId> PlanMissingRecipes(
            IEnumerable<TechId> completedTechs, IEnumerable<RecipeId> unlockedRecipes)
        {
            if (completedTechs == null) throw new ArgumentNullException(nameof(completedTechs));
            if (unlockedRecipes == null) throw new ArgumentNullException(nameof(unlockedRecipes));
            var completed = new HashSet<TechId>(completedTechs);
            var unlocked = new HashSet<RecipeId>(unlockedRecipes);
            return FrozenList.Copy(FrozenContent.Recipes
                .Where(recipe => completed.Contains(recipe.UnlockTech) && !unlocked.Contains(recipe.Id))
                .Select(recipe => recipe.Id));
        }
    }
}
