using System;
using System.Collections.Generic;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Compatibility
{
    public static class RecipeBufferContract
    {
        /// <summary>Read-only shape check; it neither resets buffers nor proves their inventory semantics.</summary>
        public static bool Matches(RecipeDefinition definition, IReadOnlyList<int>? served,
            IReadOnlyList<int>? inc, IReadOnlyList<int>? produced)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return served != null && served.Count == definition.Inputs.Count && inc != null && inc.Count == served.Count
                && produced != null && produced.Count == 1;
        }
    }
}
