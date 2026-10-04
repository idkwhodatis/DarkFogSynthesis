using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Progression
{
    public sealed class OwnedPrerequisiteChange
    {
        internal OwnedPrerequisiteChange(TechId tech, TechId prerequisite, IEnumerable<TechId> baseline)
        {
            Tech = tech; Prerequisite = prerequisite; Baseline = FrozenList.Copy(baseline);
        }
        public TechId Tech { get; }
        public TechId Prerequisite { get; }
        /// <summary>Exact prefix observed before this mod appended its one edge. Never written back wholesale.</summary>
        public IReadOnlyList<TechId> Baseline { get; }
        public int InsertionIndex => Baseline.Count;
    }

    public sealed class OwnedPrerequisiteChanges
    {
        internal OwnedPrerequisiteChanges(IEnumerable<OwnedPrerequisiteChange> entries) { Entries = FrozenList.Copy(entries); }
        public static OwnedPrerequisiteChanges Empty { get; } = new OwnedPrerequisiteChanges(new OwnedPrerequisiteChange[0]);
        public IReadOnlyList<OwnedPrerequisiteChange> Entries { get; }
    }

    public sealed class PrerequisiteEdit
    {
        internal PrerequisiteEdit(TechId tech, IEnumerable<TechId> before, IEnumerable<TechId> after)
        {
            Tech = tech; Before = FrozenList.Copy(before); After = FrozenList.Copy(after);
        }
        public TechId Tech { get; }
        public IReadOnlyList<TechId> Before { get; }
        public IReadOnlyList<TechId> After { get; }
    }

    public sealed class PrerequisiteConflict
    {
        internal PrerequisiteConflict(TechId tech, string reason) { Tech = tech; Reason = reason; }
        public TechId Tech { get; }
        public string Reason { get; }
        public override string ToString() => "Tech " + Tech + ": " + Reason;
    }

    /// <summary>
    /// A transactional value: compare Before with the live graph, apply all edits and rebuild verified native caches,
    /// then adopt OwnedChanges only after success. No game state is mutated by planning.
    /// </summary>
    public sealed class PrerequisitePlan
    {
        internal PrerequisitePlan(IEnumerable<PrerequisiteEdit> edits, OwnedPrerequisiteChanges ownedChanges,
            IEnumerable<PrerequisiteConflict> conflicts)
        {
            Edits = FrozenList.Copy(edits); OwnedChanges = ownedChanges; Conflicts = FrozenList.Copy(conflicts);
        }
        public bool CanApply => Conflicts.Count == 0;
        public IReadOnlyList<PrerequisiteEdit> Edits { get; }
        public OwnedPrerequisiteChanges OwnedChanges { get; }
        public IReadOnlyList<PrerequisiteConflict> Conflicts { get; }
    }

    /// <summary>
    /// Pure edit planner for the four hidden technologies' explicit prerequisite lists only.
    /// No discovery (PreItem/IsHiddenTech), research state, inventory, save mode, or metadata writes exist here.
    /// </summary>
    public static class ProgressionPolicy
    {
        public const bool DefaultApplyCombatPrerequisitesInNonPeaceMode = false;

        public static bool ShouldApply(bool isPeaceMode, bool applyCombatPrerequisitesInNonPeaceMode)
            => isPeaceMode || applyCombatPrerequisitesInNonPeaceMode;

        public static PrerequisitePlan PlanSession(
            IReadOnlyDictionary<TechId, IReadOnlyList<TechId>> current,
            OwnedPrerequisiteChanges previousChanges, bool isPeaceMode,
            bool applyCombatPrerequisitesInNonPeaceMode)
        {
            return Plan(current, previousChanges, ShouldApply(isPeaceMode, applyCombatPrerequisitesInNonPeaceMode), true);
        }

        public static PrerequisitePlan PlanRestore(
            IReadOnlyDictionary<TechId, IReadOnlyList<TechId>> current,
            OwnedPrerequisiteChanges previousChanges)
        {
            return Plan(current, previousChanges, false, false);
        }

        private static PrerequisitePlan Plan(IReadOnlyDictionary<TechId, IReadOnlyList<TechId>> current,
            OwnedPrerequisiteChanges previousChanges, bool apply, bool requireAllTargets)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (previousChanges == null) throw new ArgumentNullException(nameof(previousChanges));
            var work = new Dictionary<TechId, List<TechId>>();
            foreach (var pair in current)
            {
                if (pair.Value == null) throw new ArgumentException("Prerequisite snapshots must not contain null lists.", nameof(current));
                work.Add(pair.Key, pair.Value.ToList());
            }
            var conflicts = new List<PrerequisiteConflict>();
            foreach (var owned in previousChanges.Entries)
            {
                if (!work.TryGetValue(owned.Tech, out var edges))
                {
                    conflicts.Add(new PrerequisiteConflict(owned.Tech, "Cannot restore an owned edge because its technology is missing."));
                    continue;
                }
                // An external actor may already have removed our edge. There is then nothing to remove.
                if (!edges.Contains(owned.Prerequisite)) continue;

                // Never restore a whole baseline: unrelated prerequisites appended later belong to other actors.
                // Reordered/replaced prefixes make ownership ambiguous. Stop atomically rather than remove a
                // possibly third-party edge. This is intentionally conservative, not a general graph merger.
                bool ownSlotStillIdentifiable = edges.Count > owned.InsertionIndex
                    && edges[owned.InsertionIndex] == owned.Prerequisite
                    && edges.Take(owned.InsertionIndex).SequenceEqual(owned.Baseline);
                if (!ownSlotStillIdentifiable)
                {
                    conflicts.Add(new PrerequisiteConflict(owned.Tech,
                        "Prerequisites changed around the owned insertion; ownership is ambiguous. No changes were applied."));
                    continue;
                }
                edges.RemoveAt(owned.InsertionIndex); // One occurrence only, even if another actor appended the same ID.
            }

            if (requireAllTargets)
                foreach (var edge in FrozenContent.CombatPrerequisites)
                    if (!work.ContainsKey(edge.HiddenTech))
                        conflicts.Add(new PrerequisiteConflict(edge.HiddenTech, "Required vanilla hidden technology is missing."));

            if (conflicts.Count > 0)
                return new PrerequisitePlan(new PrerequisiteEdit[0], previousChanges, conflicts);

            var nextOwned = new List<OwnedPrerequisiteChange>();
            if (apply)
                foreach (var edge in FrozenContent.CombatPrerequisites)
                {
                    var list = work[edge.HiddenTech];
                    // A vanilla/third-party preexisting edge is never owned and will never be removed by us.
                    if (list.Contains(edge.RequiredCombatTech)) continue;
                    nextOwned.Add(new OwnedPrerequisiteChange(edge.HiddenTech, edge.RequiredCombatTech, list));
                    list.Add(edge.RequiredCombatTech);
                }

            var edits = new List<PrerequisiteEdit>();
            foreach (var pair in work.OrderBy(pair => pair.Key.Value))
                if (!pair.Value.SequenceEqual(current[pair.Key]))
                    edits.Add(new PrerequisiteEdit(pair.Key, current[pair.Key], pair.Value));
            return new PrerequisitePlan(edits, new OwnedPrerequisiteChanges(nextOwned), new PrerequisiteConflict[0]);
        }
    }
}
