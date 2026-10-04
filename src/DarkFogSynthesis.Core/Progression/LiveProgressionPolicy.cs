using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Progression
{
    /// <summary>A cache entry retains both its ID and its actual native object identity.</summary>
    public sealed class LiveTechnologyReference
    {
        public LiveTechnologyReference(int id, object? identity) { Id = id; Identity = identity; }
        public int Id { get; }
        public object? Identity { get; }
    }

    /// <summary>Read-only observations of one live technology; no game types or mutation callbacks.</summary>
    public sealed class LiveTechnologyState
    {
        public LiveTechnologyState(int id, object identity, bool published, bool obsolete,
            IEnumerable<int> explicitPrerequisites, IEnumerable<int> implicitPrerequisites,
            IEnumerable<LiveTechnologyReference> preCache, IEnumerable<LiveTechnologyReference> postCache)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id; Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Published = published; Obsolete = obsolete;
            ExplicitPrerequisites = FrozenList.Copy(explicitPrerequisites);
            ImplicitPrerequisites = FrozenList.Copy(implicitPrerequisites);
            PreCache = FrozenList.Copy(preCache); PostCache = FrozenList.Copy(postCache);
        }
        public int Id { get; }
        public object Identity { get; }
        public bool Published { get; }
        public bool Obsolete { get; }
        public IReadOnlyList<int> ExplicitPrerequisites { get; }
        public IReadOnlyList<int> ImplicitPrerequisites { get; }
        public IReadOnlyList<LiveTechnologyReference> PreCache { get; }
        public IReadOnlyList<LiveTechnologyReference> PostCache { get; }
    }

    /// <summary>
    /// Final read-only contract, separate from the session edit planner. Required relationships are
    /// membership checks, not whole-array equality: additional third-party prerequisites survive.
    /// </summary>
    public static class LiveProgressionPolicy
    {
        // Frozen plan sections 3.1/3.2; independently matched to the author-exported vanilla
        // prototype baseline. These original industrial prerequisites apply in every save mode.
        private static readonly IReadOnlyList<KeyValuePair<TechId, TechId>> industrialPrerequisites = FrozenList.Copy(new[]
        {
            new KeyValuePair<TechId, TechId>(VanillaIds.Techs.DigitalAnalogComputation, VanillaIds.Techs.InformationMatrix),
            new KeyValuePair<TechId, TechId>(VanillaIds.Techs.MatterRecombination, VanillaIds.Techs.QuantumPrinting),
            new KeyValuePair<TechId, TechId>(VanillaIds.Techs.NegentropyRecursion, VanillaIds.Techs.PlaneMetallurgy),
            new KeyValuePair<TechId, TechId>(VanillaIds.Techs.HighDensityControlledAnnihilation, VanillaIds.Techs.ControlledAnnihilation)
        });

        public static IReadOnlyCollection<int> CaptureSynthesisPreCacheModes(Func<int, LiveTechnologyState?> select)
        {
            if (select == null) throw new ArgumentNullException(nameof(select));
            var combinedTechs = new List<int>();
            foreach (var definition in FrozenContent.Technologies)
            {
                var tech = Available(select, definition.Id.Value);
                var ids = tech.PreCache.Select(entry => entry.Id);
                bool explicitOnly = ids.SequenceEqual(tech.ExplicitPrerequisites);
                bool combined = ids.SequenceEqual(tech.ExplicitPrerequisites.Concat(tech.ImplicitPrerequisites).Distinct());
                Require(explicitOnly || combined, "Unknown native preTechArray semantics on synthesis technology " + tech.Id + ".");
                foreach (var entry in tech.PreCache)
                {
                    var current = select(entry.Id);
                    Require(current != null && current.Id == entry.Id && ReferenceEquals(entry.Identity, current.Identity),
                        "Stale native preTechArray while binding synthesis technology " + tech.Id + ".");
                }
                if (!explicitOnly && combined) combinedTechs.Add(tech.Id);
            }
            return combinedTechs.AsReadOnly();
        }

        public static void ValidateSynthesis(Func<int, LiveTechnologyState?> select,
            IReadOnlyCollection<int>? combinedPreCacheTechs = null)
        {
            if (select == null) throw new ArgumentNullException(nameof(select));
            foreach (var definition in FrozenContent.Technologies)
            {
                var child = Available(select, definition.Id.Value);
                foreach (var parent in definition.ExplicitPrerequisites)
                    ValidateExplicit(select, child, parent.Value);
                foreach (var parent in definition.ImplicitPrerequisites)
                {
                    var required = Available(select, parent.Value);
                    Require(child.ImplicitPrerequisites.Contains(parent.Value),
                        "Missing required implicit prerequisite " + parent + " on technology " + child.Id + ".");
                    // CommonAPI registers reverse links for explicit prerequisites only. Do not invent
                    // reciprocal implicit links. Require implicit pre-cache membership only when the
                    // native cache observed at binding actually included implicit prerequisites.
                    bool cached = child.PreCache.Any(entry => entry.Id == parent.Value);
                    if (cached || (combinedPreCacheTechs?.Contains(child.Id) ?? false))
                        RequireReference(select, child.PreCache, required, "preTechArray", child.Id);
                }
            }
        }

        public static void ValidateHidden(Func<int, LiveTechnologyState?> select, bool isPeaceMode,
            bool applyCombatPrerequisitesInNonPeaceMode)
        {
            if (select == null) throw new ArgumentNullException(nameof(select));
            foreach (var edge in industrialPrerequisites)
                ValidateExplicit(select, Available(select, edge.Key.Value), edge.Value.Value);
            if (!ProgressionPolicy.ShouldApply(isPeaceMode, applyCombatPrerequisitesInNonPeaceMode)) return;
            foreach (var edge in FrozenContent.CombatPrerequisites)
                ValidateExplicit(select, Available(select, edge.HiddenTech.Value), edge.RequiredCombatTech.Value);
        }

        private static void ValidateExplicit(Func<int, LiveTechnologyState?> select, LiveTechnologyState child, int parentId)
        {
            var parent = Available(select, parentId);
            Require(child.ExplicitPrerequisites.Contains(parentId),
                "Missing required explicit prerequisite " + parentId + " on technology " + child.Id + ".");
            RequireReference(select, child.PreCache, parent, "preTechArray", child.Id);
            RequireReference(select, parent.PostCache, child, "postTechArray", parent.Id);
        }

        private static LiveTechnologyState Available(Func<int, LiveTechnologyState?> select, int id)
        {
            var tech = select(id);
            Require(tech != null && tech.Id == id && tech.Published && !tech.Obsolete,
                "Required technology " + id + " is missing, unpublished or obsolete. Review the conflicting mod's settings and restart; no technologies were repaired or unlocked.");
            return tech!;
        }

        private static void RequireReference(Func<int, LiveTechnologyState?> select,
            IReadOnlyList<LiveTechnologyReference> cache, LiveTechnologyState required, string name, int owner)
        {
            // Check ID membership and native identity independently. Additional valid foreign links
            // are allowed, but null/stale entries anywhere in this native cache are not safe to use.
            Require(cache.Any(entry => entry.Id == required.Id && ReferenceEquals(entry.Identity, required.Identity)),
                "Required " + name + " reference " + owner + " -> " + required.Id + " is missing or stale (not the current LDB technology).");
            foreach (var entry in cache)
            {
                var current = select(entry.Id);
                Require(current != null && current.Id == entry.Id && ReferenceEquals(entry.Identity, current.Identity),
                    "Technology " + owner + " has a null or stale " + name + " entry: " + entry.Id + ".");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
