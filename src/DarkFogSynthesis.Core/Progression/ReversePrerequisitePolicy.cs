using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Progression
{
    public sealed class OwnedReversePrerequisiteChange
    {
        internal OwnedReversePrerequisiteChange(TechId child, IEnumerable<TechId> baseline, IEnumerable<TechId> expected)
        {
            Child = child; Baseline = FrozenList.Copy(baseline); Expected = FrozenList.Copy(expected);
        }
        public TechId Child { get; }
        public IReadOnlyList<TechId> Baseline { get; }
        public IReadOnlyList<TechId> Expected { get; }
    }

    public sealed class ReversePrerequisitePlan
    {
        internal ReversePrerequisitePlan(IEnumerable<TechId> before, IEnumerable<TechId> after,
            OwnedReversePrerequisiteChange? ownedChange, string? conflict)
        {
            Before = FrozenList.Copy(before); After = FrozenList.Copy(after); OwnedChange = ownedChange; Conflict = conflict;
        }
        public IReadOnlyList<TechId> Before { get; }
        public IReadOnlyList<TechId> After { get; }
        public OwnedReversePrerequisiteChange? OwnedChange { get; }
        public string? Conflict { get; }
        public bool CanApply => Conflict == null;
        public bool HasEdit => !Before.SequenceEqual(After);
    }

    /// <summary>Own a reverse-cache link only when we actually appended it, independently of forward-edge ownership.</summary>
    public static class ReversePrerequisitePolicy
    {
        public static ReversePrerequisitePlan Plan(IReadOnlyList<TechId> current, TechId child,
            OwnedReversePrerequisiteChange? previousChange, bool ownsForwardEdgeAfter, bool forwardEdgeExistsAfter)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (child.Value <= 0) throw new ArgumentOutOfRangeException(nameof(child));
            if (ownsForwardEdgeAfter && !forwardEdgeExistsAfter)
                throw new ArgumentException("An owned forward edge must also exist.", nameof(ownsForwardEdgeAfter));
            if (previousChange != null && (previousChange.Child != child || !current.SequenceEqual(previousChange.Expected)))
                return new ReversePrerequisitePlan(current,current,previousChange,
                    "The owned reverse-cache link changed; its ownership is ambiguous. No changes were applied.");

            if (previousChange != null)
            {
                if (forwardEdgeExistsAfter)
                    // A foreign duplicate forward edge keeps the reverse link alive. Relinquish our ownership
                    // when our forward edge is gone, so a later session cannot remove that foreign link.
                    return new ReversePrerequisitePlan(current,current,ownsForwardEdgeAfter ? previousChange : null,null);
                var after = current.ToList();
                after.RemoveAt(previousChange.Baseline.Count); // Exact expected sequence has already been checked.
                return new ReversePrerequisitePlan(current,after,null,null);
            }

            if (!ownsForwardEdgeAfter || current.Contains(child))
                return new ReversePrerequisitePlan(current,current,null,null);

            var appended = current.Concat(new[] {child}).ToArray();
            return new ReversePrerequisitePlan(current,appended,new OwnedReversePrerequisiteChange(child,current,appended),null);
        }
    }
}
