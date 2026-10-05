using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Progression;
using DarkFogSynthesis.Registration;

namespace DarkFogSynthesis.Progression
{
    internal sealed class RuntimeProgression
    {
        private OwnedPrerequisiteChanges owned = OwnedPrerequisiteChanges.Empty;
        // Retain representation only while the corresponding forward edit is owned. A combined
        // cache may temporarily equal the explicit list after our append; that does not change its mode.
        private PrerequisiteCacheModes ownedPreCacheModes = PrerequisiteCacheModes.Empty;
        private readonly Dictionary<int, TechProto[]> ownedPostCache = new Dictionary<int, TechProto[]>();
        private readonly Dictionary<int, OwnedReversePrerequisiteChange> ownedReverseLinks = new Dictionary<int, OwnedReversePrerequisiteChange>();

        internal void Apply(bool isPeaceMode, bool extendToCombat)
        {
            if (ProgressionPolicy.ShouldApply(isPeaceMode, extendToCombat))
                foreach (var edge in FrozenContent.CombatPrerequisites)
                {
                    var prerequisite = LDB.techs.Select(edge.RequiredCombatTech.Value);
                    ContentRegistry.Require(prerequisite != null && prerequisite.Published && !prerequisite.IsObsolete,
                        "Required combat technology " + edge.RequiredCombatTech + " is missing, unpublished or obsolete. A mod such as UXAssist may disable it; review that mod's configuration manually. DarkFogSynthesis will not re-enable or unlock it.");
                }
            ApplyPlan(ProgressionPolicy.PlanSession(Snapshot(), owned, isPeaceMode, extendToCombat));
        }

        // Called after application/final startup, never as a substitute for applying the mode policy.
        // Synthesis availability/prerequisites are independently checked by ContentRegistry.Validate.
        internal void ValidateLive(bool isPeaceMode, bool extendToCombat) =>
            LiveProgressionValidator.ValidateHidden(isPeaceMode, extendToCombat);

        internal void Restore()
        {
            if (owned.Entries.Count == 0 && ownedPostCache.Count == 0) return;
            ApplyPlan(ProgressionPolicy.PlanRestore(Snapshot(), owned));
        }

        private IReadOnlyDictionary<TechId, IReadOnlyList<TechId>> Snapshot() => FrozenContent.CombatPrerequisites.ToDictionary(
            p => p.HiddenTech, p => (IReadOnlyList<TechId>)(LDB.techs.Select(p.HiddenTech.Value).PreTechs ?? Array.Empty<int>()).Select(i => new TechId(i)).ToArray());

        private void ApplyPlan(PrerequisitePlan plan)
        {
            ContentRegistry.Require(plan.CanApply, "Cannot isolate progression changes: " + string.Join("; ", plan.Conflicts));
            var cacheModes = new Dictionary<int, PrerequisiteCacheMode>();
            // Validate retained contracts even when repeated application produces no forward edit.
            foreach (var entry in ownedPreCacheModes.Entries)
            {
                var tech = LDB.techs.Select(entry.Key)
                    ?? throw new InvalidOperationException("Missing technology with an owned prerequisite cache: " + entry.Key);
                PrerequisiteCachePolicy.Resolve(tech.PreTechs ?? Array.Empty<int>(), tech.PreTechsImplicit ?? Array.Empty<int>(),
                    (tech.preTechArray ?? Array.Empty<TechProto>()).Select(t => t.ID), entry.Value);
            }
            var beforeCache = new Dictionary<int, TechProto[]>();
            // Validate all cache contracts before touching any prototype.
            foreach (var edit in plan.Edits)
            {
                var tech = LDB.techs.Select(edit.Tech.Value);
                ContentRegistry.Require((tech.PreTechs ?? Array.Empty<int>()).SequenceEqual(edit.Before.Select(i => i.Value)), "Prerequisite graph changed while planning.");
                var cache = tech.preTechArray ?? Array.Empty<TechProto>();
                int[] ids = cache.Select(t => t.ID).ToArray();
                cacheModes[tech.ID] = ownedPreCacheModes.Resolve(tech.ID, tech.PreTechs ?? Array.Empty<int>(),
                    tech.PreTechsImplicit ?? Array.Empty<int>(), ids);
                beforeCache[tech.ID] = cache;
                foreach (var id in edit.After) ContentRegistry.Require(LDB.techs.Select(id.Value) != null, "Missing prerequisite technology " + id);
            }
            // Prepare ownership metadata before mutation, and publish it only after successful edits.
            var nextPreCacheModes = ownedPreCacheModes.Retain(plan.OwnedChanges, cacheModes);
            // A third party replacing the cache is ambiguous. Do not delete its changes.
            foreach (var entry in ownedPostCache)
                ContentRegistry.Require(ReferenceEquals(LDB.techs.Select(entry.Key).postTechArray, entry.Value),
                    "Another mod replaced a prerequisite's reverse cache during this session: " + entry.Key);

            var reversePlans = new Dictionary<int, ReversePrerequisitePlan>();
            foreach (var edge in FrozenContent.CombatPrerequisites)
            {
                var parent = LDB.techs.Select(edge.RequiredCombatTech.Value);
                var child = LDB.techs.Select(edge.HiddenTech.Value);
                var edit = plan.Edits.SingleOrDefault(p => p.Tech == edge.HiddenTech);
                bool forwardExistsAfter = edit != null ? edit.After.Contains(edge.RequiredCombatTech)
                    : (child.PreTechs ?? Array.Empty<int>()).Contains(edge.RequiredCombatTech.Value);
                forwardExistsAfter = forwardExistsAfter || (child.PreTechsImplicit ?? Array.Empty<int>()).Contains(edge.RequiredCombatTech.Value);
                bool ownsForwardAfter = plan.OwnedChanges.Entries.Any(o => o.Tech == edge.HiddenTech && o.Prerequisite == edge.RequiredCombatTech);
                ownedReverseLinks.TryGetValue(parent.ID, out var previousReverse);
                var reversePlan = ReversePrerequisitePolicy.Plan(
                    (parent.postTechArray ?? Array.Empty<TechProto>()).Select(t => new TechId(t.ID)).ToArray(),
                    edge.HiddenTech, previousReverse, ownsForwardAfter, forwardExistsAfter);
                ContentRegistry.Require(reversePlan.CanApply, "Cannot isolate reverse cache for technology " + parent.ID + ": " + reversePlan.Conflict);
                reversePlans.Add(parent.ID, reversePlan);
            }

            var originalPost = FrozenContent.CombatPrerequisites.Select(e => e.RequiredCombatTech.Value).Distinct()
                .ToDictionary(id => id, id => LDB.techs.Select(id).postTechArray);
            var discovery = FrozenContent.CombatPrerequisites.ToDictionary(e => e.HiddenTech.Value,
                e => Tuple.Create(LDB.techs.Select(e.HiddenTech.Value).IsHiddenTech,
                    (LDB.techs.Select(e.HiddenTech.Value).PreItem ?? Array.Empty<int>()).ToArray(),
                    (LDB.techs.Select(e.HiddenTech.Value).PreTechsImplicit ?? Array.Empty<int>()).ToArray()));
            try
            {
                foreach (var edit in plan.Edits)
                {
                    var tech = LDB.techs.Select(edit.Tech.Value);
                    tech.PreTechs = edit.After.Select(t => t.Value).ToArray();
                    var cacheIds = PrerequisiteCachePolicy.Rebuild(tech.PreTechs,
                        tech.PreTechsImplicit ?? Array.Empty<int>(), cacheModes[tech.ID]);
                    tech.preTechArray = cacheIds.Select(id => LDB.techs.Select(id)).ToArray();
                }
                // Own only a reverse edge that we actually appended. A preexisting or still-needed foreign
                // link survives restoration, even when our forward edge was removed by the pure planner.
                foreach (var entry in reversePlans)
                {
                    if (entry.Value.HasEdit)
                        LDB.techs.Select(entry.Key).postTechArray = entry.Value.After.Select(id => LDB.techs.Select(id.Value)).ToArray();
                }
                foreach (var entry in discovery)
                {
                    var tech = LDB.techs.Select(entry.Key);
                    ContentRegistry.Require(tech.IsHiddenTech == entry.Value.Item1
                        && (tech.PreItem ?? Array.Empty<int>()).SequenceEqual(entry.Value.Item2)
                        && (tech.PreTechsImplicit ?? Array.Empty<int>()).SequenceEqual(entry.Value.Item3), "Discovery or implicit prerequisite data changed unexpectedly.");
                }
                owned = plan.OwnedChanges;
                ownedPostCache.Clear();
                ownedReverseLinks.Clear();
                foreach (var entry in reversePlans)
                    if (entry.Value.OwnedChange != null)
                    {
                        ownedReverseLinks[entry.Key] = entry.Value.OwnedChange;
                        ownedPostCache[entry.Key] = LDB.techs.Select(entry.Key).postTechArray;
                    }
                ownedPreCacheModes = nextPreCacheModes;
            }
            catch
            {
                foreach (var edit in plan.Edits)
                {
                    var tech = LDB.techs.Select(edit.Tech.Value);
                    tech.PreTechs = edit.Before.Select(i => i.Value).ToArray();
                    tech.preTechArray = beforeCache[tech.ID];
                }
                foreach (var pair in originalPost) LDB.techs.Select(pair.Key).postTechArray = pair.Value;
                throw;
            }
        }
    }
}
