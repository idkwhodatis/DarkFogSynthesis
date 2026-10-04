using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class NativeMatrixContractTests
    {
        internal static void Run(Action<bool, string> assert)
        {
            int[] expected = { 6001, 6002, 6003, 6004, 6005, 6006 };
            Check(null);
            for (int length = 0; length <= 12; ++length)
                Check(Enumerable.Range(6001, length).ToArray());

            // Each index must remain observable after a successful call on the same array.
            // Caching by array identity would silently accept these third-party mutations.
            var live = expected.ToArray();
            foreach (int replacement in new[] { int.MinValue, -1, 0, 5201, 6000, 6007, int.MaxValue })
            {
                for (int index = 0; index < live.Length; ++index)
                {
                    assert(NativeMatrixContract.IsSupported(live), "Restored live registry is supported.");
                    int previous = live[index];
                    live[index] = replacement;
                    Check(live);
                    assert(!NativeMatrixContract.IsSupported(live), "An in-place matrix registry edit is rechecked.");
                    live[index] = previous;
                    assert(NativeMatrixContract.IsSupported(live), "Restoring the same array is immediately recognized.");
                }
            }

            var random = new Random(195148102);
            for (int trial = 0; trial < 1024; ++trial)
            {
                var ids = new int[random.Next(0, 10)];
                for (int index = 0; index < ids.Length; ++index)
                    ids[index] = random.Next(4) == 0 ? random.Next(5998, 6010) : 6001 + index;
                Check(ids);
            }

            void Check(int[]? ids)
            {
                int[]? snapshot = ids?.ToArray();
                bool supported = ids != null && ids.SequenceEqual(expected);
                assert(NativeMatrixContract.IsSupported(ids) == supported, "Concrete native-array check matches the independent oracle.");
                assert(NativeMatrixContract.IsSupported((IReadOnlyList<int>?)ids) == supported,
                    "Existing interface-array API retains the same result.");
                if (ids == null) return;
                assert(NativeMatrixContract.IsSupported(Array.AsReadOnly(ids)) == supported,
                    "Read-only collection callers retain the same result.");
                assert(NativeMatrixContract.IsSupported(new List<int>(ids)) == supported,
                    "List callers retain the same result.");
                assert(ids.SequenceEqual(snapshot!), "No overload rewrites the caller's matrix registry.");
            }
        }
    }
}
