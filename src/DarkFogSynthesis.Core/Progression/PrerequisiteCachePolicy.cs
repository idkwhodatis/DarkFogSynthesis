using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DarkFogSynthesis.Core.Progression
{
    public enum PrerequisiteCacheMode { ExplicitOnly, Combined }

    /// <summary>Representation of preTechArray, independent of the owned forward-edge planner.</summary>
    public static class PrerequisiteCachePolicy
    {
        public static PrerequisiteCacheMode Resolve(IEnumerable<int> explicitIds, IEnumerable<int> implicitIds,
            IEnumerable<int> cachedIds, PrerequisiteCacheMode? retainedMode = null)
        {
            int[] explicitList = Copy(explicitIds), implicitList = Copy(implicitIds), cache = Copy(cachedIds);
            bool explicitOnly = cache.SequenceEqual(explicitList);
            bool combined = cache.SequenceEqual(explicitList.Concat(implicitList).Distinct());
            // An owned edit can make the two representations identical. Never re-infer a mode
            // from that ambiguous state, or silently accept a later incompatible cache rewrite.
            if (retainedMode.HasValue)
            {
                bool matches = retainedMode.Value == PrerequisiteCacheMode.ExplicitOnly ? explicitOnly :
                    retainedMode.Value == PrerequisiteCacheMode.Combined && combined;
                if (!matches) throw new InvalidOperationException("Prerequisite cache no longer matches its retained representation.");
                return retainedMode.Value;
            }
            if (explicitOnly) return PrerequisiteCacheMode.ExplicitOnly;
            if (combined) return PrerequisiteCacheMode.Combined;
            throw new InvalidOperationException("Unknown prerequisite cache representation.");
        }

        public static int[] Rebuild(IEnumerable<int> explicitIds, IEnumerable<int> implicitIds, PrerequisiteCacheMode mode)
        {
            int[] explicitList = Copy(explicitIds), implicitList = Copy(implicitIds);
            if (mode == PrerequisiteCacheMode.ExplicitOnly) return explicitList;
            if (mode == PrerequisiteCacheMode.Combined) return explicitList.Concat(implicitList).Distinct().ToArray();
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        private static int[] Copy(IEnumerable<int> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            int[] copy = values.ToArray();
            if (copy.Any(id => id <= 0)) throw new ArgumentException("Prerequisite IDs must be positive.", nameof(values));
            return copy;
        }
    }
    /// <summary>Immutable cache-mode ownership, published only with a successfully applied forward plan.</summary>
    public sealed class PrerequisiteCacheModes
    {
        public static PrerequisiteCacheModes Empty { get; } = new PrerequisiteCacheModes(new Dictionary<int, PrerequisiteCacheMode>());
        public IReadOnlyDictionary<int, PrerequisiteCacheMode> Entries { get; }
        private PrerequisiteCacheModes(Dictionary<int, PrerequisiteCacheMode> modes)
            => Entries = new ReadOnlyDictionary<int, PrerequisiteCacheMode>(modes);

        public PrerequisiteCacheMode Resolve(int tech, IEnumerable<int> explicitIds, IEnumerable<int> implicitIds, IEnumerable<int> cache)
            => PrerequisiteCachePolicy.Resolve(explicitIds, implicitIds, cache,
                Entries.TryGetValue(tech, out var retained) ? (PrerequisiteCacheMode?)retained : null);

        public PrerequisiteCacheModes Retain(OwnedPrerequisiteChanges nextOwned, IReadOnlyDictionary<int, PrerequisiteCacheMode> observed)
        {
            if (nextOwned == null) throw new ArgumentNullException(nameof(nextOwned));
            if (observed == null) throw new ArgumentNullException(nameof(observed));
            var next = new Dictionary<int, PrerequisiteCacheMode>();
            foreach (var entry in nextOwned.Entries)
            {
                if (!observed.TryGetValue(entry.Tech.Value, out var mode) && !Entries.TryGetValue(entry.Tech.Value, out mode))
                    throw new InvalidOperationException("An owned prerequisite edit has no observed cache mode: " + entry.Tech);
                if (mode != PrerequisiteCacheMode.ExplicitOnly && mode != PrerequisiteCacheMode.Combined)
                    throw new ArgumentOutOfRangeException(nameof(observed));
                if (Entries.TryGetValue(entry.Tech.Value, out var retained) && retained != mode)
                    throw new InvalidOperationException("An owned prerequisite cache cannot change representation.");
                next.Add(entry.Tech.Value, mode);
            }
            return new PrerequisiteCacheModes(next);
        }
    }

}
