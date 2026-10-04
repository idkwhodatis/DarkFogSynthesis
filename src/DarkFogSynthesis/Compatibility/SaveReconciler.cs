using System.Linq;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Compatibility
{
    internal static class SaveReconciler
    {
        internal static int Reconcile(GameHistoryData history)
        {
            var missing = Core.Compatibility.SaveReconciler.PlanMissingRecipes(
                FrozenContent.Recipes.Select(r => r.UnlockTech).Distinct().Where(id => history.TechUnlocked(id.Value)),
                FrozenContent.Recipes.Where(r => history.RecipeUnlocked(r.Id.Value)).Select(r => r.Id));
            foreach (var recipe in missing) history.UnlockRecipe(recipe.Value);
            // Never run NotifyTechUnlock/UnlockTech: only the missing recipe flags are changed.
            return missing.Count;
        }
    }
}
