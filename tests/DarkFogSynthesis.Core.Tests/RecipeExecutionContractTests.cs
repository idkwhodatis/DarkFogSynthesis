using System;
using System.Linq;
using DarkFogSynthesis.Core.Compatibility;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class RecipeExecutionContractTests
    {
        internal static void Run(Action<bool, string> assert)
        {
            foreach (var recipe in FrozenContent.Recipes)
            {
                int time = recipe.TimeSpendTicks * 10000, extra = recipe.TimeSpendTicks * 100000;
                int[][] arrays =
                {
                    recipe.Inputs.Select(i => i.Item.Value).ToArray(),
                    recipe.Inputs.Select(i => i.Count).ToArray(),
                    new[] { recipe.Output.Item.Value }, new[] { recipe.Output.Count }
                };
                var before = arrays.Select(a => a.ToArray()).ToArray();
                assert(Matches(recipe, true, time, extra, arrays), "Exact frozen execution data must match.");
                assert(!Matches(recipe, false, time, extra, arrays), "Nonproductive execution data must fail.");
                foreach (int invalid in new[] { int.MinValue, -1, 0, time - 1, time + 1, int.MaxValue })
                    assert(!Matches(recipe, true, invalid, extra, arrays), "Every incorrect base execution time must fail.");
                foreach (int invalid in new[] { int.MinValue, -1, 0, extra - 1, extra + 1, int.MaxValue })
                    assert(!Matches(recipe, true, time, invalid, arrays), "Every incorrect extra execution time must fail.");

                for (int a = 0; a < arrays.Length; a++)
                {
                    foreach (int[]? malformed in new int[]?[]
                        { null, Array.Empty<int>(), arrays[a].Take(arrays[a].Length - 1).ToArray(), arrays[a].Concat(new[] { 0 }).ToArray() })
                    {
                        var changed = (int[]?[])arrays.Clone(); changed[a] = malformed;
                        assert(!Matches(recipe, true, time, extra, changed), "Null, empty, truncated and oversized arrays must fail.");
                    }
                    for (int i = 0; i < arrays[a].Length; i++)
                    {
                        arrays[a][i]++;
                        assert(!Matches(recipe, true, time, extra, arrays), "In-place changes must be read on every call.");
                        arrays[a][i]--;
                        assert(Matches(recipe, true, time, extra, arrays), "Restored data must pass without replacing the array.");
                    }
                }
                var reversed = (int[][])arrays.Clone(); reversed[0] = arrays[0].Reverse().ToArray();
                assert(!Matches(recipe, true, time, extra, reversed), "Required item order is part of the contract.");
                for (int i = 0; i < arrays.Length; i++)
                    assert(arrays[i].SequenceEqual(before[i]), "Execution validation must not mutate caller arrays.");
            }

            // Deterministic differential coverage preserves all original LINQ acceptance semantics,
            // including several simultaneous corruptions and successful calls between failures.
            var random = new Random(725104);
            for (int iteration = 0; iteration < 2048; iteration++)
            {
                var recipe = FrozenContent.Recipes[random.Next(FrozenContent.Recipes.Count)];
                bool productive = random.Next(8) != 0;
                int time = recipe.TimeSpendTicks * 10000 + (random.Next(8) == 0 ? 1 : 0);
                int extra = recipe.TimeSpendTicks * 100000 + (random.Next(8) == 0 ? 1 : 0);
                int[]?[] arrays =
                {
                    recipe.Inputs.Select(i => i.Item.Value).ToArray(),
                    recipe.Inputs.Select(i => i.Count).ToArray(),
                    new[] { recipe.Output.Item.Value }, new[] { recipe.Output.Count }
                };
                for (int a = 0; a < arrays.Length; a++)
                {
                    switch (random.Next(8))
                    {
                        case 0: arrays[a] = null; break;
                        case 1: arrays[a] = arrays[a]!.Concat(new[] { 0 }).ToArray(); break;
                        case 2: arrays[a] = arrays[a]!.Take(arrays[a]!.Length - 1).ToArray(); break;
                        case 3: arrays[a]![random.Next(arrays[a]!.Length)]++; break;
                    }
                }
                bool oldResult = OriginalMatches(recipe, productive, time, extra, arrays);
                assert(oldResult == Matches(recipe, productive, time, extra, arrays), "Indexed validation must agree with the original exact LINQ checks.");
            }

            bool nullRejected = false;
            try { RecipeExecutionContract.Matches(null!, true, 0, 0, null, null, null, null); }
            catch (ArgumentNullException) { nullRejected = true; }
            assert(nullRejected, "Null recipe definition must be rejected explicitly.");
        }

        private static bool Matches(RecipeDefinition recipe, bool productive, int time, int extra, int[]?[] arrays)
            => RecipeExecutionContract.Matches(recipe, productive, time, extra, arrays[0], arrays[1], arrays[2], arrays[3]);

        private static bool OriginalMatches(RecipeDefinition recipe, bool productive, int time, int extra, int[]?[] arrays)
            => productive && time == recipe.TimeSpendTicks * 10000 && extra == recipe.TimeSpendTicks * 100000 &&
                arrays[0] != null && arrays[0]!.SequenceEqual(recipe.Inputs.Select(i => i.Item.Value)) &&
                arrays[1] != null && arrays[1]!.SequenceEqual(recipe.Inputs.Select(i => i.Count)) &&
                arrays[2] != null && arrays[2]!.SequenceEqual(new[] { recipe.Output.Item.Value }) &&
                arrays[3] != null && arrays[3]!.SequenceEqual(new[] { recipe.Output.Count });
    }
}
