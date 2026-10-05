using System;
using System.Collections.Generic;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>Read-only selector preflight; never clears, refunds or completes pending research.</summary>
    public static class LabStackPreflight
    {
        public static bool IsEmpty(int recipeId, int techId, bool researchMode, bool replicating,
            int time, int extraTime, int cycleCount, int extraCycleCount, int hashBytes, int extraHashBytes,
            IReadOnlyList<int>? served, IReadOnlyList<int>? incServed, IReadOnlyList<int>? produced,
            IReadOnlyList<int>? matrixServed, IReadOnlyList<int>? matrixIncServed)
            => recipeId == 0 && techId == 0 && !researchMode &&
                !RemovalSafetyPolicy.HasProductionState(time, extraTime, cycleCount, extraCycleCount,
                    replicating, served, incServed, produced) &&
                !RemovalSafetyPolicy.HasResearchState(hashBytes, extraHashBytes, matrixServed, matrixIncServed);

        // The native adapter supplies live slot access. Buffer checks are restricted to the selected
        // connected stack; scanning predecessors does not inspect every lab's buffers on the planet.
        public static bool TryCollect(int selected, int cursor, int capacity, Func<int, int> idAt,
            Func<int, int> nextAt, Func<int, bool> isEmptyAt, out List<int> stack)
        {
            if (idAt == null) throw new ArgumentNullException(nameof(idAt));
            if (nextAt == null) throw new ArgumentNullException(nameof(nextAt));
            if (isEmptyAt == null) throw new ArgumentNullException(nameof(isEmptyAt));
            stack = new List<int>();
            if (selected <= 0 || selected >= cursor || selected >= capacity || idAt(selected) != selected) return false;
            var preceding = new Dictionary<int, int>();
            var ambiguous = new HashSet<int>();
            for (int i = 1; i < cursor && i < capacity; i++)
            {
                if (idAt(i) != i || nextAt(i) <= 0) continue;
                int next = nextAt(i);
                if (preceding.ContainsKey(next)) ambiguous.Add(next);
                else preceding.Add(next, i);
            }
            var seen = new HashSet<int>();
            int root = selected;
            while (preceding.TryGetValue(root, out int previous))
            {
                if (ambiguous.Contains(root) || !seen.Add(root)) return false;
                root = previous;
            }
            seen.Clear();
            for (int id = root; id != 0; id = nextAt(id))
            {
                if (id <= 0 || id >= cursor || id >= capacity || !seen.Add(id) || ambiguous.Contains(id)) return false;
                if (idAt(id) != id || !isEmptyAt(id)) return false;
                stack.Add(id);
            }
            return stack.Contains(selected);
        }
    }
}
