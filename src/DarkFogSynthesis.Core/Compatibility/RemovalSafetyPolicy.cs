using System.Collections.Generic;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>
    /// Conservative pure blockers for a removal preflight, not proof of inventory safety or vanilla compatibility.
    /// These predicates never clear resources, cancel tasks, refund items or change research progress.
    /// The runtime must supply every relevant native buffer and progress field from a paused, consistent snapshot.
    /// </summary>
    public static class RemovalSafetyPolicy
    {
        /// <summary>
        /// Every nonzero value blocks removal, including invalid negative values. A replicating flag also blocks.
        /// Null/unallocated buffer arrays are treated as empty, as are empty and all-zero arrays.
        /// </summary>
        public static bool HasProductionState(int time, int extraTime, int cycles, int extraCycles,
            bool replicating, params IReadOnlyList<int>?[]? buffers)
            => time != 0 || extraTime != 0 || cycles != 0 || extraCycles != 0 || replicating || HasBufferState(buffers);

        /// <summary>Any pending research bytes, extra bytes, reserved matrix inputs or proliferation points block removal.</summary>
        public static bool HasResearchState(int hashBytes, int extraHashBytes, params IReadOnlyList<int>?[]? buffers)
            => hashBytes != 0 || extraHashBytes != 0 || HasBufferState(buffers);

        /// <summary>
        /// A queue snapshot must be explicitly known; null current-tech/queues or negative IDs fail closed.
        /// Native zero IDs are empty queue slots. Only this mod's fixed custom IDs are otherwise blocked.
        /// This does not establish that unused technology states or machine/blueprint references are removable.
        /// </summary>
        public static bool HasCustomQueueReferences(int? currentTech, IReadOnlyList<int>? researchQueue,
            IReadOnlyList<int>? handcraftRecipeIds)
        {
            if (!currentTech.HasValue || researchQueue == null || handcraftRecipeIds == null) return true;
            if (currentTech.Value < 0 || IsCustomTechnology(currentTech.Value)) return true;
            for (int i = 0; i < researchQueue.Count; i++)
                if (researchQueue[i] < 0 || IsCustomTechnology(researchQueue[i])) return true;
            for (int i = 0; i < handcraftRecipeIds.Count; i++)
                if (handcraftRecipeIds[i] < 0 || IsCustomRecipe(handcraftRecipeIds[i])) return true;
            return false;
        }

        private static bool HasBufferState(IReadOnlyList<int>?[]? buffers)
        {
            if (buffers == null) return false;
            foreach (var buffer in buffers)
            {
                if (buffer == null) continue;
                for (int index = 0; index < buffer.Count; index++)
                    if (buffer[index] != 0) return true;
            }
            return false;
        }

        private static bool IsCustomTechnology(int id)
        {
            foreach (var technology in FrozenContent.Technologies)
                if (technology.Id.Value == id) return true;
            return false;
        }

        private static bool IsCustomRecipe(int id)
        {
            foreach (var recipe in FrozenContent.Recipes)
                if (recipe.Id.Value == id) return true;
            return false;
        }
    }
}
