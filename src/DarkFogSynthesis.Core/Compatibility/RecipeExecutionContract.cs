using System;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Compatibility
{
    public static class RecipeExecutionContract
    {
        /// <summary>
        /// Read-only exact execution-data check. Reads current arrays on every call; no game references,
        /// memoized validation results, replacement arrays or per-call enumeration allocations are used.
        /// </summary>
        public static bool Matches(RecipeDefinition definition, bool productive, int timeSpend, int extraTimeSpend,
            int[]? requires, int[]? requireCounts, int[]? products, int[]? productCounts)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!productive || timeSpend != definition.TimeSpendTicks * 10000 ||
                extraTimeSpend != definition.TimeSpendTicks * 100000 ||
                requires == null || requires.Length != definition.Inputs.Count ||
                requireCounts == null || requireCounts.Length != definition.Inputs.Count ||
                products == null || products.Length != 1 || products[0] != definition.Output.Item.Value ||
                productCounts == null || productCounts.Length != 1 || productCounts[0] != definition.Output.Count)
                return false;

            for (int i = 0; i < definition.Inputs.Count; i++)
            {
                var input = definition.Inputs[i];
                if (requires[i] != input.Item.Value || requireCounts[i] != input.Count) return false;
            }
            return true;
        }
    }
}
