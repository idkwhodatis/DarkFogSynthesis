using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class RemovalHistoryGuardTests
    {
        internal static void Run(Action<bool, string> check)
        {
            foreach (string change in new[] { "none", "recipe-remove", "recipe-add", "tech-remove", "tech-add",
                "tech-value", "queue-remove", "queue-add", "queue-order", "queue-duplicate", "current", "save-throws", "before-save" })
            {
                var expectedRecipes = new[] { 1, 2 };
                var expectedTechs = new Dictionary<int, long> { [100] = 10, [101] = 20 };
                var expectedQueue = new[] { 100, 101, 0 };
                var recipes = new HashSet<int>(expectedRecipes);
                var techs = new Dictionary<int, long>(expectedTechs);
                var queue = expectedQueue.ToList();
                int currentTech = 100, saves = 0, verifications = 0, reports = 0;
                var sentinel = new InvalidOperationException("Harmless save failure");
                Exception? observed = null;
                if (change == "before-save") recipes.Remove(1);
                try
                {
                    RemovalHistoryGuard.VerifyAcrossSave(() => {
                        verifications++;
                        RemovalHistoryGuard.EnsurePreserved(expectedRecipes, expectedTechs, expectedQueue, 100,
                            recipes, techs, queue, currentTech);
                    }, () => {
                        saves++;
                        switch (change)
                        {
                            case "recipe-remove": recipes.Remove(1); break;
                            case "recipe-add": recipes.Add(3); break;
                            case "tech-remove": techs.Remove(100); break;
                            case "tech-add": techs.Add(102, 30); break;
                            case "tech-value": techs[101]++; break;
                            case "queue-remove": queue.Remove(101); break;
                            case "queue-add": queue.Add(102); break;
                            case "queue-order": queue.Reverse(); break;
                            case "queue-duplicate": queue.Add(101); break;
                            case "current": currentTech = 101; break;
                            case "save-throws": throw sentinel;
                        }
                    });
                    reports++;
                }
                catch (InvalidOperationException error) { observed = error; }
                check(reports == (change == "none" ? 1 : 0), "Incorrect preservation report: " + change);
                check((observed == null) == (change == "none"), "Missing preservation failure: " + change);
                check(saves == (change == "before-save" ? 0 : 1), "Precheck did not precede save: " + change);
                check(verifications == (change == "before-save" || change == "save-throws" ? 1 : 2),
                    "The callback boundary was not checked on both sides: " + change);
                if (change == "save-throws") check(ReferenceEquals(observed, sentinel), "Save error identity changed");
                // Guard must neither repair peer writes nor mutate its independent baseline.
                if (change == "tech-remove") check(!techs.ContainsKey(100), "Peer deletion was overwritten");
                if (change == "recipe-add") check(recipes.Contains(3), "Peer unlock was removed");
                if (change == "queue-add") check(queue.Contains(102), "Peer queue write was undone");
                check(expectedRecipes.SequenceEqual(new[] { 1, 2 }) && expectedTechs[100] == 10 && expectedTechs.Count == 2 &&
                    expectedQueue.SequenceEqual(new[] { 100, 101, 0 }), "Expected history was mutated");
            }
            // Allow only native set ordering and unused queue slots; retain duplicates and order of real entries.
            RemovalHistoryGuard.EnsurePreserved(new[] { 1, 2 }, new Dictionary<int, long> { [100] = 10 },
                new[] { 100, 0, 100, 101 }, 100, new[] { 2, 1 }, new Dictionary<int, long> { [100] = 10 },
                new[] { 0, 100, 100, 101, 0, 0 }, 100);
            check(true, "Equivalent native history representation refused");
            int callbacks = 0;
            foreach (bool missingVerify in new[] { false, true })
            {
                bool refused = false;
                try { RemovalHistoryGuard.VerifyAcrossSave(missingVerify ? null! : () => callbacks++,
                    missingVerify ? () => callbacks++ : null!); }
                catch (ArgumentNullException) { refused = true; }
                check(refused && callbacks == 0, "Invalid callback invoked another callback");
            }
        }
    }
}
