using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Progression;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class PrerequisiteCachePolicyTests
    {
        internal static void Run(Action<bool, string> check)
        {
            foreach (var originalMode in new[] { PrerequisiteCacheMode.Combined, PrerequisiteCacheMode.ExplicitOnly })
            {
                foreach (bool foreignEdits in new[] { false, true })
                {
                    var child = new TechId(1901);
                    var raw = FrozenContent.CombatPrerequisites.ToDictionary(e => e.HiddenTech,
                        _ => (IReadOnlyList<TechId>)new[] { new TechId(1312) });
                    int[] implicitIds = { 1820 };
                    int[] cache = PrerequisiteCachePolicy.Rebuild(new[] { 1312 }, implicitIds, originalMode);
                    var owned = OwnedPrerequisiteChanges.Empty;
                    var retained = PrerequisiteCacheModes.Empty;
                    for (int cycle = 0; cycle < 10; cycle++)
                    {
                        var plan = ProgressionPolicy.PlanSession(raw, owned, true, false);
                        check(plan.CanApply, "Forward application refused a valid cache fixture");
                        var observed = plan.Edits.ToDictionary(e => e.Tech.Value, e => e.Tech == child
                            ? retained.Resolve(child.Value, raw[child].Select(id => id.Value), implicitIds, cache)
                            : PrerequisiteCacheMode.ExplicitOnly);
                        var next = retained.Retain(plan.OwnedChanges, observed);
                        foreach (var edit in plan.Edits) raw[edit.Tech] = edit.After.ToArray();
                        cache = PrerequisiteCachePolicy.Rebuild(raw[child].Select(id => id.Value), implicitIds, next.Entries[child.Value]);
                        retained = next;
                        owned = plan.OwnedChanges;
                        check(retained.Entries[child.Value] == originalMode, "Application changed the original representation");
                        // After the explicit append, a combined cache can equal the explicit list.
                        // Repeated application must retain the first observation despite no graph edit.
                        var repeated = ProgressionPolicy.PlanSession(raw, owned, true, false);
                        check(repeated.CanApply && repeated.Edits.Count == 0, "Repeated application is not idempotent");
                        check(retained.Resolve(child.Value, raw[child].Select(id => id.Value), implicitIds, cache) == originalMode,
                            "Repeated resolution forgot the ambiguous original representation");
                        retained = retained.Retain(repeated.OwnedChanges, new Dictionary<int, PrerequisiteCacheMode>());
                        owned = repeated.OwnedChanges;
                        if (foreignEdits && cycle == 0)
                        {
                            raw[child] = raw[child].Concat(new[] { new TechId(1777) }).ToArray();
                            implicitIds = new[] { 1820, 1778 };
                            cache = PrerequisiteCachePolicy.Rebuild(raw[child].Select(id => id.Value), implicitIds, originalMode);
                        }
                        var restore = ProgressionPolicy.PlanRestore(raw, owned);
                        check(restore.CanApply, "Restoration lost identifiable forward ownership");
                        var restoreMode = retained.Resolve(child.Value, raw[child].Select(id => id.Value), implicitIds, cache);
                        foreach (var edit in restore.Edits) raw[edit.Tech] = edit.After.ToArray();
                        cache = PrerequisiteCachePolicy.Rebuild(raw[child].Select(id => id.Value), implicitIds, restoreMode);
                        owned = restore.OwnedChanges;
                        var previous = retained;
                        retained = retained.Retain(owned, new Dictionary<int, PrerequisiteCacheMode>());
                        check(retained.Entries.Count == 0 && previous.Entries[child.Value] == originalMode,
                            "Ownership completion mutated the prior snapshot or retained obsolete modes");
                        int[] expectedExplicit = foreignEdits ? new[] { 1312, 1777 } : new[] { 1312 };
                        int[] expectedCache = originalMode == PrerequisiteCacheMode.ExplicitOnly ? expectedExplicit :
                            expectedExplicit.Concat(implicitIds).Distinct().ToArray();
                        check(raw[child].Select(id => id.Value).SequenceEqual(expectedExplicit), "A foreign forward edge was lost");
                        check(cache.SequenceEqual(expectedCache), "Restore dropped an implicit cache entry or promoted explicit-only semantics");
                        check(owned.Entries.Count == 0, "Restoration retained forward ownership");
                    }
                }
            }
            var initial = FrozenContent.CombatPrerequisites.ToDictionary(e => e.HiddenTech,
                _ => (IReadOnlyList<TechId>)new[] { new TechId(1312) });
            var pending = ProgressionPolicy.PlanSession(initial, OwnedPrerequisiteChanges.Empty, true, false);
            Refuses(() => PrerequisiteCacheModes.Empty.Retain(pending.OwnedChanges, new Dictionary<int, PrerequisiteCacheMode>()), check);
            int[] explicitIds = { 1312 }, implicitList = { 1820 }, originalCache = { 1312, 1820 };
            var mode = PrerequisiteCachePolicy.Resolve(explicitIds, implicitList, originalCache);
            int[] rebuilt = PrerequisiteCachePolicy.Rebuild(explicitIds, implicitList, mode);
            rebuilt[0] = -1;
            check(explicitIds[0] == 1312 && originalCache[0] == 1312 && implicitList[0] == 1820,
                "Cache policy mutated caller arrays");
            check(PrerequisiteCachePolicy.Resolve(new[] { 1312, 1820 }, implicitList, originalCache, mode) == mode,
                "Ambiguous overlap changed retained combined mode");
            Refuses(() => PrerequisiteCachePolicy.Resolve(explicitIds, implicitList, explicitIds, mode), check);
            Refuses(() => PrerequisiteCachePolicy.Resolve(explicitIds, implicitList, originalCache, PrerequisiteCacheMode.ExplicitOnly), check);
            Refuses(() => PrerequisiteCachePolicy.Resolve(explicitIds, implicitList, new[] { 999 }), check);
            Refuses(() => PrerequisiteCachePolicy.Resolve(explicitIds, implicitList, originalCache, (PrerequisiteCacheMode)99), check);
            Refuses(() => PrerequisiteCachePolicy.Rebuild(explicitIds, implicitList, (PrerequisiteCacheMode)99), check);
            Refuses(() => PrerequisiteCachePolicy.Resolve(null!, implicitList, originalCache), check);
            Refuses(() => PrerequisiteCachePolicy.Rebuild(explicitIds, new[] { 0 }, mode), check);
        }

        private static void Refuses(Action action, Action<bool, string> check)
        {
            bool refused = false;
            try { action(); }
            catch (InvalidOperationException) { refused = true; }
            catch (ArgumentException) { refused = true; }
            check(refused, "Invalid or incompatible prerequisite cache was accepted");
        }
    }
}
