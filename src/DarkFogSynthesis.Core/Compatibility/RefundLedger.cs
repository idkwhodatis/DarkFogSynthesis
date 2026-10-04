using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>An exact whole-item packet. Inc is total proliferation points, never an averaged level.</summary>
    public sealed class RefundPacket
    {
        public int ItemId { get; }
        public int Count { get; }
        public int Inc { get; }
        internal RefundPacket(int itemId, int count, int inc) { ItemId = itemId; Count = count; Inc = inc; }
    }

    /// <summary>A defensive copy of the native production buffer and its execution recipe, for diagnostics only.</summary>
    public sealed class RefundBufferSnapshot
    {
        public int RecipeId { get; }
        public IReadOnlyList<int>? Requires { get; }
        public IReadOnlyList<int>? RequireCounts { get; }
        public IReadOnlyList<int>? Products { get; }
        public IReadOnlyList<int>? ProductCounts { get; }
        public IReadOnlyList<int>? Served { get; }
        public IReadOnlyList<int>? IncServed { get; }
        public IReadOnlyList<int>? Produced { get; }
        public IReadOnlyList<int>? MatrixServed { get; }
        public IReadOnlyList<int>? MatrixIncServed { get; }
        public int Time { get; }
        public int ExtraTime { get; }
        public int CycleCount { get; }
        public int ExtraCycleCount { get; }
        public bool Replicating { get; }
        public bool ResearchMode { get; }
        public int TechId { get; }
        public int HashBytes { get; }
        public int ExtraHashBytes { get; }

        public RefundBufferSnapshot(int recipeId, IReadOnlyList<int>? requires, IReadOnlyList<int>? requireCounts,
            IReadOnlyList<int>? products, IReadOnlyList<int>? productCounts, IReadOnlyList<int>? served,
            IReadOnlyList<int>? incServed, IReadOnlyList<int>? produced, int time = 0, int extraTime = 0,
            int cycleCount = 0, int extraCycleCount = 0, bool replicating = false, bool researchMode = false,
            int techId = 0, int hashBytes = 0, int extraHashBytes = 0,
            IReadOnlyList<int>? matrixServed = null, IReadOnlyList<int>? matrixIncServed = null)
        {
            RecipeId = recipeId;
            Requires = Copy(requires); RequireCounts = Copy(requireCounts);
            Products = Copy(products); ProductCounts = Copy(productCounts);
            Served = Copy(served); IncServed = Copy(incServed); Produced = Copy(produced);
            MatrixServed = Copy(matrixServed); MatrixIncServed = Copy(matrixIncServed);
            Time = time; ExtraTime = extraTime; CycleCount = cycleCount; ExtraCycleCount = extraCycleCount;
            Replicating = replicating; ResearchMode = researchMode; TechId = techId;
            HashBytes = hashBytes; ExtraHashBytes = extraHashBytes;
        }

        private static IReadOnlyList<int>? Copy(IReadOnlyList<int>? source)
            => source == null ? null : Array.AsReadOnly(source.ToArray());
    }

    /// <summary>Immutable expected packets and totals. This is not authorization or proof that native refund calls are safe.</summary>
    public sealed class RefundPlan
    {
        public IReadOnlyList<RefundPacket> Packets { get; }
        public IReadOnlyList<RefundPacket> Totals { get; }
        public int MachineCount { get; }
        internal RefundPlan(IEnumerable<RefundPacket> packets, IEnumerable<RefundPacket> totals, int machineCount)
        {
            Packets = Array.AsReadOnly(packets.ToArray());
            Totals = Array.AsReadOnly(totals.ToArray());
            MachineCount = machineCount;
        }
    }

    /// <summary>
    /// Plans only idle, non-replicating production buffers of this mod's unchanged frozen recipes.
    /// No active-cycle, research, forge, capacity, refund, or vanilla-save claim is made by this pure layer.
    /// </summary>
    public static class RefundLedger
    {
        public static RefundPlan Create(IEnumerable<RefundBufferSnapshot> snapshots)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            var packets = new List<RefundPacket>();
            var totals = new SortedDictionary<int, RefundPacket>();
            int machines = 0;
            foreach (var snapshot in snapshots)
            {
                if (snapshot == null) throw new InvalidOperationException("A production snapshot is unknown.");
                Validate(snapshot);
                machines = checked(machines + 1);
                // Preserve separate input/output packets and all mixed-input remainder points.
                // This order follows the reviewed accounting pattern, not a proven native call trace.
                for (int i = snapshot.Products!.Count - 1; i >= 0; --i)
                    Add(snapshot.Products[i], snapshot.Produced![i], 0, packets, totals);
                for (int i = snapshot.Requires!.Count - 1; i >= 0; --i)
                    Add(snapshot.Requires[i], snapshot.Served![i], snapshot.IncServed![i], packets, totals);
            }
            return new RefundPlan(packets, totals.Values, machines);
        }

        private static void Validate(RefundBufferSnapshot s)
        {
            var definition = FrozenContent.Recipes.FirstOrDefault(r => r.Id.Value == s.RecipeId);
            Require(definition != null, "The recipe is not owned by DarkFogSynthesis.");
            Require(s.Time == 0 && s.ExtraTime == 0 && s.CycleCount == 0 && s.ExtraCycleCount == 0 && !s.Replicating,
                "Active or invalid production progress requires native/manual handling.");
            Require(!s.ResearchMode && s.TechId == 0 && s.HashBytes == 0 && s.ExtraHashBytes == 0
                && Empty(s.MatrixServed) && Empty(s.MatrixIncServed), "Research state is outside the idle-buffer refund subset.");
            Require(s.Requires != null && s.RequireCounts != null && s.Products != null && s.ProductCounts != null
                && s.Served != null && s.IncServed != null && s.Produced != null, "Native buffer/cache arrays are unavailable.");
            Require(s.Requires!.Count == s.RequireCounts!.Count && s.Requires.Count == s.Served!.Count
                && s.Requires.Count == s.IncServed!.Count && s.Products!.Count == s.ProductCounts!.Count
                && s.Products.Count == s.Produced!.Count, "Native buffer/cache arrays do not align.");
            Require(s.Requires.SequenceEqual(definition!.Inputs.Select(i => i.Item.Value))
                && s.RequireCounts.SequenceEqual(definition.Inputs.Select(i => i.Count))
                && s.Products.SequenceEqual(new[] { definition.Output.Item.Value })
                && s.ProductCounts.SequenceEqual(new[] { definition.Output.Count }), "The owned execution recipe differs from frozen content.");
            for (int i = 0; i < s.Served!.Count; ++i)
            {
                int count = s.Served[i], inc = s.IncServed![i];
                Require(count >= 0 && inc >= 0, "Negative input quantity or proliferation points.");
                // Reviewed refund accounting clamps above this bound. Never silently discard such points.
                Require((long)inc <= (long)count * 10, "Input proliferation points exceed the supported native refund bound.");
            }
            foreach (int count in s.Produced!) Require(count >= 0, "Negative product quantity.");
        }

        private static void Add(int itemId, int count, int inc, List<RefundPacket> packets, IDictionary<int, RefundPacket> totals)
        {
            if (count == 0) return;
            var packet = new RefundPacket(itemId, count, inc);
            packets.Add(packet);
            if (totals.TryGetValue(itemId, out var previous))
                totals[itemId] = new RefundPacket(itemId, checked(previous.Count + count), checked(previous.Inc + inc));
            else totals.Add(itemId, packet);
        }

        private static bool Empty(IReadOnlyList<int>? values) => values == null || values.All(v => v == 0);
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    }
}
