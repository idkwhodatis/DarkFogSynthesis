using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class LabStackPreflightTests
    {
        // Plain test values, not native game classes. Tests call the production predicate AND traversal.
        internal static void Run(Action<bool, string> check)
        {
            string[] fields = { "none", "recipe", "tech", "research", "replicating", "time", "extraTime", "cycles", "extraCycles",
                "hashBytes", "extraHashBytes", "served", "incServed", "produced", "matrixServed", "matrixIncServed" };
            foreach (string field in fields)
            foreach (int value in new[] { -1, 1 })
            foreach (int selected in new[] { 1, 2, 3 })
            foreach (int changed in new[] { 1, 2, 3 })
            {
                int[] ids = { 0, 1, 2, 3, 4 }, next = { 0, 2, 3, 0, 0 };
                int[] buffer = { 0, value, 0 };
                int emptyChecks = 0;
                int V(string key, int id) => field == key && changed == id ? value : 0;
                IReadOnlyList<int>? B(string key, int id) => field == key && changed == id ? buffer : null;
                bool Empty(int id)
                {
                    check(id != 4, "Unrelated lab buffers were scanned"); emptyChecks++;
                    return LabStackPreflight.IsEmpty(V("recipe", id), V("tech", id), V("research", id) != 0,
                        V("replicating", id) != 0, V("time", id), V("extraTime", id), V("cycles", id), V("extraCycles", id),
                        V("hashBytes", id), V("extraHashBytes", id), B("served", id), B("incServed", id), B("produced", id),
                        B("matrixServed", id), B("matrixIncServed", id));
                }
                bool accepted = LabStackPreflight.TryCollect(selected, ids.Length, ids.Length, i => ids[i], i => next[i], Empty, out var stack);
                string label = field + "/" + value + "/selected=" + selected + "/changed=" + changed;
                check(accepted == (field == "none"), "Nonempty lab preflight admission: " + label);
                if (accepted) check(stack.SequenceEqual(new[] { 1, 2, 3 }) && emptyChecks == 3, "Incomplete connected stack: " + label);
                check(buffer.SequenceEqual(new[] { 0, value, 0 }) && ids.SequenceEqual(new[] { 0, 1, 2, 3, 4 }) &&
                    next.SequenceEqual(new[] { 0, 2, 3, 0, 0 }), "Preflight modified caller state");
            }
            foreach (string caseName in new[] { "single", "zero-arrays", "invalid-selected", "selected-outside", "capacity", "removed", "cycle", "ambiguous", "dangling" })
            {
                int[] ids = { 0, 1, 2, 3 }, next = { 0, 2, 3, 0 }; int selected = 1, capacity = 4;
                switch (caseName)
                {
                    case "single": next[1] = 0; break;
                    case "invalid-selected": selected = 0; break;
                    case "selected-outside": selected = 4; break;
                    case "capacity": capacity = 1; break;
                    case "removed": ids[2] = 0; break;
                    case "cycle": next[3] = 1; break;
                    case "ambiguous": next[3] = 2; break;
                    case "dangling": next[3] = 99; break;
                }
                bool accepted = LabStackPreflight.TryCollect(selected, ids.Length, capacity, i => ids[i], i => next[i],
                    _ => LabStackPreflight.IsEmpty(0, 0, false, false, 0, 0, 0, 0, 0, 0,
                        Array.Empty<int>(), new int[2], null, new int[6], new int[6]), out var stack);
                check(accepted == (caseName == "single" || caseName == "zero-arrays"), "Stack graph regression: " + caseName);
                if (caseName == "single") check(stack.SequenceEqual(new[] { 1 }), "Single lab changed");
            }
        }
    }
}
