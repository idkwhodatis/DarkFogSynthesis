using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Progression;

namespace DarkFogSynthesis.Core.Tests
{
    /// <summary>Tests production pure observations/policy, without game objects or replacement game assemblies.</summary>
    internal static class LiveProgressionPolicyTests
    {
        private static readonly (int Child, int Parent)[] Synthesis = { (1951, 1826), (1952, 1808) };
        private static readonly (int Child, int Parent)[] Industrial = { (1901, 1312), (1902, 1203), (1903, 1417), (1904, 1145) };
        private static readonly (int Child, int Parent)[] Combat = { (1901, 1820), (1902, 1811), (1903, 1809), (1904, 1818) };
        private static readonly (bool Peace, bool Extend)[] Modes = { (false, false), (false, true), (true, false), (true, true) };

        internal static void Run(Action<bool, string> assert)
        {
            foreach (var mode in Modes)
            {
                bool active = mode.Peace || mode.Extend;
                var graph = Graph(active);
                var modes = LiveProgressionPolicy.CaptureSynthesisPreCacheModes(graph.Select);
                Validate(graph, mode.Peace, mode.Extend, modes);
                assert(modes.Count == 0, "Explicit-only native pre-caches do not invent implicit cache membership.");
                assert(graph.Select(1952)!.ImplicitPrerequisites.Contains(1124), "The implicit industrial research condition remains in every mode.");
                VerifyAvailability(graph, mode.Peace, mode.Extend, assert);
                foreach (var edge in Synthesis.Concat(Industrial).Concat(active ? Combat : Array.Empty<(int Child, int Parent)>()))
                    VerifyExplicitEdge(graph, edge, mode.Peace, mode.Extend, assert);
                ExtraForeignRelationshipsRemainValid(mode.Peace, mode.Extend, assert);
                UnrelatedUnavailableBranchRemainsValid(mode.Peace, mode.Extend, assert);
            }
            InactiveCombatIsNotRequired(assert);
            ImplicitCacheSemantics(assert);
            LateEditsAreRechecked(assert);
            Reject(() => LiveProgressionPolicy.ValidateSynthesis(null!), assert, "Null synthesis lookup is rejected.", typeof(ArgumentNullException));
            Reject(() => LiveProgressionPolicy.ValidateHidden(null!, false, false), assert, "Null hidden lookup is rejected even in inactive mode.", typeof(ArgumentNullException));
            Reject(() => LiveProgressionPolicy.CaptureSynthesisPreCacheModes(null!), assert, "Null cache-mode lookup is rejected.", typeof(ArgumentNullException));
        }

        private static void VerifyAvailability(GraphState original, bool peace, bool extend, Action<bool, string> assert)
        {
            var required = new[] { 1951, 1952, 1826, 1808, 1124 }.Concat(Industrial.SelectMany(e => new[] { e.Child, e.Parent }))
                .Concat(peace || extend ? Combat.Select(e => e.Parent) : Array.Empty<int>()).Distinct();
            foreach (int id in required)
            {
                var graph = original.Copy();
                var state = graph.Select(id)!;
                graph.States.Remove(id);
                Reject(() => Validate(graph, peace, extend), assert, "A missing required technology fails closed: " + id);
                graph.States[id] = Clone(state, published: false);
                Reject(() => Validate(graph, peace, extend), assert, "An unpublished required technology fails closed: " + id);
                graph.States[id] = Clone(state, obsolete: true);
                Reject(() => Validate(graph, peace, extend), assert, "An obsolete required technology fails closed: " + id);
                graph.States[id] = Clone(state, id: id + 50000);
                Reject(() => Validate(graph, peace, extend), assert, "A lookup returning a different ID fails closed: " + id);
                graph.States[id] = state;
                Validate(graph, peace, extend);
                assert(ReferenceEquals(graph.Select(id), state), "Validation preserves the original observed technology instance.");
            }
        }

        private static void VerifyExplicitEdge(GraphState original, (int Child, int Parent) edge,
            bool peace, bool extend, Action<bool, string> assert)
        {
            var graph = original.Copy();
            var child = graph.Select(edge.Child)!;
            var parent = graph.Select(edge.Parent)!;
            string relation = edge.Child + " <- " + edge.Parent;
            graph.States[edge.Child] = Clone(child, explicitIds: child.ExplicitPrerequisites.Where(id => id != edge.Parent));
            Reject(() => Validate(graph, peace, extend), assert, "A removed required edge is rejected while its parent stays published: " + relation);
            assert(graph.Select(edge.Parent)!.Published && !graph.Select(edge.Parent)!.Obsolete,
                "The late-edge regression keeps its parent available, independently of the missing edge.");
            graph.States[edge.Child] = Clone(graph.Select(edge.Child)!, implicitIds: child.ImplicitPrerequisites.Concat(new[] { edge.Parent }));
            Reject(() => Validate(graph, peace, extend), assert, "Moving a required explicit edge into implicit prerequisites is not silently accepted: " + relation);
            graph.States[edge.Child] = Clone(child, preCache: child.PreCache.Where(entry => entry.Id != edge.Parent));
            Reject(() => Validate(graph, peace, extend), assert, "Missing required forward native cache reference is rejected: " + relation);
            graph.States[edge.Child] = Clone(child, preCache: child.PreCache.Select(entry => entry.Id == edge.Parent ? new LiveTechnologyReference(edge.Parent, new object()) : entry));
            Reject(() => Validate(graph, peace, extend), assert, "A same-ID stale forward cache object is rejected: " + relation);
            graph.States[edge.Child] = Clone(child, preCache: child.PreCache.Concat(new[] { new LiveTechnologyReference(edge.Parent, new object()) }));
            Reject(() => Validate(graph, peace, extend), assert, "A correct forward object cannot conceal an extra same-ID stale object: " + relation);
            graph.States[edge.Child] = Clone(child, preCache: child.PreCache.Select(entry => entry.Id == edge.Parent ? new LiveTechnologyReference(edge.Parent + 50000, parent.Identity) : entry));
            Reject(() => Validate(graph, peace, extend), assert, "Correct identity with an incorrect cache ID is rejected: " + relation);
            graph.States[edge.Child] = Clone(child, preCache: child.PreCache.Concat(new[] { new LiveTechnologyReference(0, null) }));
            Reject(() => Validate(graph, peace, extend), assert, "A null native cache entry cannot hide beside a valid required forward reference: " + relation);
            graph.States[edge.Child] = Clone(child, preCache: child.PreCache.Concat(new[] { new LiveTechnologyReference(9001, new object()) }));
            Reject(() => Validate(graph, peace, extend), assert, "A stale foreign forward cache entry is rejected: " + relation);
            graph.States[edge.Child] = child;
            graph.States[edge.Parent] = Clone(parent, postCache: parent.PostCache.Where(entry => entry.Id != edge.Child));
            Reject(() => Validate(graph, peace, extend), assert, "Missing required reverse native cache reference is rejected: " + relation);
            graph.States[edge.Parent] = Clone(parent, postCache: parent.PostCache.Select(entry => entry.Id == edge.Child ? new LiveTechnologyReference(edge.Child, new object()) : entry));
            Reject(() => Validate(graph, peace, extend), assert, "A same-ID stale reverse cache object is rejected: " + relation);
            graph.States[edge.Parent] = Clone(parent, postCache: parent.PostCache.Concat(new[] { new LiveTechnologyReference(edge.Child, new object()) }));
            Reject(() => Validate(graph, peace, extend), assert, "A correct reverse object cannot conceal a stale duplicate: " + relation);
            graph.States[edge.Parent] = Clone(parent, postCache: parent.PostCache.Concat(new[] { new LiveTechnologyReference(0, null) }));
            Reject(() => Validate(graph, peace, extend), assert, "A null native cache entry cannot hide beside a valid required reverse reference: " + relation);
            graph.States[edge.Parent] = Clone(parent, postCache: parent.PostCache.Concat(new[] { new LiveTechnologyReference(9001, new object()) }));
            Reject(() => Validate(graph, peace, extend), assert, "A stale foreign reverse cache entry is rejected: " + relation);
            graph.States[edge.Parent] = parent;
            Validate(graph, peace, extend);
            assert(ReferenceEquals(graph.Select(edge.Child), child) && ReferenceEquals(graph.Select(edge.Parent), parent),
                "Read-only validation leaves the restored relationship unchanged: " + relation);
        }

        private static void ExtraForeignRelationshipsRemainValid(bool peace, bool extend, Action<bool, string> assert)
        {
            var graph = Graph(peace || extend);
            foreach (int id in new[] { 1951, 1952, 1901, 1902, 1903, 1904 })
            {
                graph.AddExplicit(id, 9001, prepend: true);
                var tech = graph.Select(id)!;
                graph.States[id] = Clone(tech, implicitIds: tech.ImplicitPrerequisites.Concat(new[] { 9002 }));
            }
            foreach (int id in new[] { 1826, 1808, 1312, 1203, 1417, 1145 }.Concat(peace || extend ? Combat.Select(e => e.Parent) : Array.Empty<int>()))
            {
                var parent = graph.Select(id)!;
                graph.States[id] = Clone(parent, postCache: parent.PostCache.Concat(new[] { graph.Reference(9002) }));
            }
            var before = graph.States.ToDictionary(pair => pair.Key, pair => pair.Value);
            var modes = LiveProgressionPolicy.CaptureSynthesisPreCacheModes(graph.Select);
            Validate(graph, peace, extend, modes);
            assert(before.All(pair => ReferenceEquals(graph.Select(pair.Key), pair.Value)),
                "Legitimate added foreign explicit/implicit/reverse relationships are accepted without replacing any technology.");
            assert(graph.Select(1951)!.ExplicitPrerequisites.SequenceEqual(new[] { 9001, 1826 }),
                "Foreign prerequisite insertion and existing ordering remain intact.");
        }

        private static void UnrelatedUnavailableBranchRemainsValid(bool peace, bool extend, Action<bool, string> assert)
        {
            var graph = Graph(peace || extend);
            var parent = graph.Select(1826)!;
            graph.States[1826] = Clone(parent, postCache: parent.PostCache.Concat(new[] { graph.Reference(9002) }));
            graph.States[9002] = Clone(graph.Select(9002)!, published: false, obsolete: true);
            Validate(graph, peace, extend);
            assert(!graph.Select(9002)!.Published && graph.Select(9002)!.Obsolete,
                "An unrelated current-LDB unpublished/obsolete descendant remains valid; availability is scoped to required technologies.");
            var damaged = Clone(graph.Select(1826)!, postCache: parent.PostCache.Concat(new[] { new LiveTechnologyReference(9002, new object()) }));
            graph.States[1826] = damaged;
            Reject(() => Validate(graph, peace, extend), assert,
                "An unrelated unpublished descendant must still have its current-LDB reference, not a stale same-ID object.");
            assert(ReferenceEquals(graph.Select(1826), damaged), "Unrelated stale branch rejection does not rewrite the parent's cache.");
        }

        private static void InactiveCombatIsNotRequired(Action<bool, string> assert)
        {
            var graph = Graph(false);
            foreach (var edge in Combat) graph.States.Remove(edge.Parent);
            Validate(graph, false, false);
            assert(Combat.All(edge => graph.Select(edge.Parent) == null), "Inactive non-Peace/default mode does not require optional combat parents, edges or caches.");
            foreach (var mode in Modes.Where(mode => mode.Peace || mode.Extend))
                Reject(() => Validate(graph, mode.Peace, mode.Extend), assert, "Each active mode requires its combat gate.");
            foreach (var edge in Combat)
                graph.States[edge.Parent] = State(edge.Parent, published: false, obsolete: true);
            Validate(graph, false, false);
            assert(Combat.All(edge => !graph.Select(edge.Parent)!.Published), "Inactive mode does not re-enable or reject irrelevant disabled combat parents.");
        }

        private static void ImplicitCacheSemantics(Action<bool, string> assert)
        {
            var graph = Graph(true);
            var explicitOnlyModes = LiveProgressionPolicy.CaptureSynthesisPreCacheModes(graph.Select);
            Validate(graph, true, false, explicitOnlyModes);
            assert(graph.Select(1124)!.PostCache.Count == 0, "Native explicit-only behavior does not require a synthetic implicit reverse link.");
            var child = graph.Select(1952)!;
            graph.States[1952] = Clone(child, implicitIds: Array.Empty<int>());
            Reject(() => Validate(graph, true, false), assert, "A frozen implicit ID cannot disappear while its parent remains available.");
            graph.States[1952] = Clone(child, preCache: child.PreCache.Concat(new[] { graph.Reference(1124) }));
            var combinedModes = LiveProgressionPolicy.CaptureSynthesisPreCacheModes(graph.Select);
            assert(combinedModes.SequenceEqual(new[] { 1952 }), "Observed combined native pre-cache shape is retained for final validation.");
            Validate(graph, true, false, combinedModes);
            graph.States[1952] = child;
            Reject(() => Validate(graph, true, false, combinedModes), assert, "A late missing implicit pre-cache reference cannot change the captured native cache contract.");
            graph.States[1952] = Clone(child, preCache: child.PreCache.Concat(new[] { new LiveTechnologyReference(1124, new object()) }));
            Reject(() => Validate(graph, true, false, combinedModes), assert, "Same-ID stale implicit pre-cache is rejected under a combined contract.");
            Reject(() => Validate(graph, true, false, explicitOnlyModes), assert, "A present stale implicit cache entry is rejected even in an explicit-only contract.");
            Reject(() => LiveProgressionPolicy.CaptureSynthesisPreCacheModes(graph.Select), assert, "Binding refuses stale same-ID cache references.");
            graph.States[1952] = Clone(child, preCache: Array.Empty<LiveTechnologyReference>());
            Reject(() => LiveProgressionPolicy.CaptureSynthesisPreCacheModes(graph.Select), assert, "Binding refuses an unknown native cache shape.");
            graph.States[1952] = child;
            Validate(graph, true, false, explicitOnlyModes);
        }

        private static void LateEditsAreRechecked(Action<bool, string> assert)
        {
            var graph = Graph(true);
            Validate(graph, true, false);
            var child = graph.Select(1901)!;
            graph.States[1901] = Clone(child, explicitIds: new[] { 1312 });
            var damaged = graph.Select(1901);
            Reject(() => Validate(graph, true, false), assert, "A subsequent validation detects removal of 1901 <- 1820 despite an earlier successful validation.");
            assert(ReferenceEquals(graph.Select(1901), damaged) && !graph.Select(1901)!.ExplicitPrerequisites.Contains(1820),
                "Rejected validation does not repair the deleted edge, replace caches, or mutate the observed graph.");
            graph.States[1901] = child;
            Validate(graph, true, false);
            assert(ReferenceEquals(graph.Select(1901), child), "Restoring the live graph is recognized without a cached failure.");
        }

        private static void Validate(GraphState graph, bool peace, bool extend, IReadOnlyCollection<int>? modes = null)
        {
            LiveProgressionPolicy.ValidateSynthesis(graph.Select, modes);
            LiveProgressionPolicy.ValidateHidden(graph.Select, peace, extend);
        }

        private static GraphState Graph(bool combat)
        {
            var graph = new GraphState();
            foreach (int id in Synthesis.Concat(Industrial).Concat(Combat).SelectMany(e => new[] { e.Child, e.Parent }).Concat(new[] { 1124, 9001, 9002 }).Distinct())
                graph.States[id] = State(id);
            foreach (var edge in Synthesis.Concat(Industrial).Concat(combat ? Combat : Array.Empty<(int Child, int Parent)>())) graph.AddExplicit(edge.Child, edge.Parent);
            graph.States[1952] = Clone(graph.Select(1952)!, implicitIds: new[] { 1124 });
            return graph;
        }

        private static LiveTechnologyState State(int id, bool published = true, bool obsolete = false) =>
            new LiveTechnologyState(id, new object(), published, obsolete, Array.Empty<int>(), Array.Empty<int>(),
                Array.Empty<LiveTechnologyReference>(), Array.Empty<LiveTechnologyReference>());

        private static LiveTechnologyState Clone(LiveTechnologyState source, int? id = null, bool? published = null, bool? obsolete = null,
            IEnumerable<int>? explicitIds = null, IEnumerable<int>? implicitIds = null,
            IEnumerable<LiveTechnologyReference>? preCache = null, IEnumerable<LiveTechnologyReference>? postCache = null) =>
            new LiveTechnologyState(id ?? source.Id, source.Identity, published ?? source.Published, obsolete ?? source.Obsolete,
                explicitIds ?? source.ExplicitPrerequisites, implicitIds ?? source.ImplicitPrerequisites,
                preCache ?? source.PreCache, postCache ?? source.PostCache);

        private static void Reject(Action action, Action<bool, string> assert, string message, Type? exceptionType = null)
        {
            try { action(); }
            catch (Exception error)
            {
                assert(error.GetType() == (exceptionType ?? typeof(InvalidOperationException)), message + " Correct exception type.");
                return;
            }
            assert(false, message);
        }

        private sealed class GraphState
        {
            internal readonly Dictionary<int, LiveTechnologyState> States = new Dictionary<int, LiveTechnologyState>();
            internal LiveTechnologyState? Select(int id) => States.TryGetValue(id, out var state) ? state : null;
            internal LiveTechnologyReference Reference(int id) => new LiveTechnologyReference(id, Select(id)!.Identity);
            internal GraphState Copy()
            {
                var copy = new GraphState();
                foreach (var pair in States) copy.States.Add(pair.Key, pair.Value);
                return copy;
            }
            internal void AddExplicit(int childId, int parentId, bool prepend = false)
            {
                var child = Select(childId)!;
                var parent = Select(parentId)!;
                States[childId] = Clone(child,
                    explicitIds: prepend ? new[] { parentId }.Concat(child.ExplicitPrerequisites) : child.ExplicitPrerequisites.Concat(new[] { parentId }),
                    preCache: prepend ? new[] { Reference(parentId) }.Concat(child.PreCache) : child.PreCache.Concat(new[] { Reference(parentId) }));
                States[parentId] = Clone(parent, postCache: parent.PostCache.Concat(new[] { Reference(childId) }));
            }
        }
    }
}
