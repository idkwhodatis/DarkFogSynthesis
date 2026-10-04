using System;
using System.Collections.Generic;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Registration
{
    public static class ProtoIdCollisionGuard
    {
        /// <summary>Run before registering any content. Existing content is never overwritten or assigned another ID.</summary>
        public static void AssertVacant(Func<TechId, bool> techExists, Func<RecipeId, bool> recipeExists)
        {
            if (techExists == null) throw new ArgumentNullException(nameof(techExists));
            if (recipeExists == null) throw new ArgumentNullException(nameof(recipeExists));
            var collisions = new List<string>();
            foreach (var tech in FrozenContent.Technologies)
                if (techExists(tech.Id)) collisions.Add("Tech " + tech.Id + " (" + tech.Key + ")");
            foreach (var recipe in FrozenContent.Recipes)
                if (recipeExists(recipe.Id)) collisions.Add("Recipe " + recipe.Id + " (" + recipe.Key + ")");
            if (collisions.Count > 0)
                throw new InvalidOperationException("Dark Fog Synthesis fixed Proto ID collision: "
                    + string.Join(", ", collisions) + ". Content registration must stop; IDs must not be remapped at runtime.");
        }
    }
}
