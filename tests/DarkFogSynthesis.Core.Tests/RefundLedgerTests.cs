using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class RefundLedgerTests
    {
        internal static void Run(Action<bool, string> assert)
        {
            var served = new[] { 3, 2, 1 };
            var inc = new[] { 7, 1, 0 }; // Deliberately mixed, with a remainder that an average level would lose.
            var produced = new[] { 5 };
            var snapshot = Snapshot(served, inc, produced);
            served[0] = 999; inc[0] = 999; produced[0] = 999;
            var plan = RefundLedger.Create(new[] { snapshot });
            assert(plan.MachineCount == 1 && plan.Packets.Count == 4 && plan.Totals.Count == 4, "Ledger counts every nonempty input/output packet.");
            var input = plan.Totals.Single(p => p.ItemId == 1128);
            assert(input.Count == 3 && input.Inc == 7, "Snapshot is defensive and preserves mixed proliferation remainders.");
            var product = plan.Totals.Single(p => p.ItemId == 5206);
            assert(product.Count == 5 && product.Inc == 0, "Native produced buffers carry no new proliferation points.");
            assert(plan.Packets.Sum(p => p.Count) == 11 && plan.Packets.Sum(p => p.Inc) == 8, "Input and product quantities remain separate.");
            var combined = RefundLedger.Create(new[] { snapshot, snapshot });
            assert(combined.MachineCount == 2 && combined.Packets.Count == 8, "Batch retains separate machine packets.");
            assert(combined.Totals.Single(p => p.ItemId == 1128).Count == 6
                && combined.Totals.Single(p => p.ItemId == 1128).Inc == 14, "Batch totals aggregate counts and exact points.");
            assert(RefundLedger.Create(Array.Empty<RefundBufferSnapshot>()).Packets.Count == 0, "An explicitly empty batch is empty.");
            assert(RefundLedger.Create(new[] { Snapshot() }).Packets.Count == 0, "Known empty machine buffers produce no packets.");
            Reject(() => ((IList<RefundPacket>)plan.Packets).Add(input), assert, "Packets are immutable.", typeof(NotSupportedException));
            Reject(() => ((IList<int>)snapshot.Served!)[0] = 2, assert, "Snapshot arrays are immutable.", typeof(NotSupportedException));
            Reject(() => RefundLedger.Create(null!), assert, "Unknown batch rejected.", typeof(ArgumentNullException));
            Reject(() => RefundLedger.Create(new RefundBufferSnapshot[] { null! }), assert, "Unknown machine rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(recipe: 1) }), assert, "Vanilla/unowned recipes rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(served: new[] { -1, 0, 0 }) }), assert, "Negative inputs rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(inc: new[] { -1, 0, 0 }) }), assert, "Negative points rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(inc: new[] { 1, 0, 0 }) }), assert, "Points without items rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(new[] { 1, 0, 0 }, new[] { 11, 0, 0 }) }), assert, "Unsupported points are rejected rather than clamped.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(produced: new[] { -1 }) }), assert, "Negative products rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(served: new[] { 0, 0 }) }), assert, "Short buffer rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(inc: new[] { 0, 0, 0, 0 }) }), assert, "Long buffer rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(produced: Array.Empty<int>()) }), assert, "Missing product buffer rejected.");
            Reject(() => RefundLedger.Create(new[] { new RefundBufferSnapshot(48101, null, new[] { 1, 1, 1 },
                new[] { 5206 }, new[] { 2 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 0 }) }), assert, "Null native recipe metadata rejected.");
            Reject(() => RefundLedger.Create(new[] { new RefundBufferSnapshot(48101, new[] { 1110, 1109, 1128 }, new[] { 1, 1, 1 },
                new[] { 5206 }, new[] { 2 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 0 }) }), assert, "Changed native recipe ordering rejected.");
            Reject(() => RefundLedger.Create(new[] { new RefundBufferSnapshot(48101, new[] { 1128, 1109, 1110 }, new[] { 1, 1, 1 },
                new[] { 5206 }, new[] { 3 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 0 }) }), assert, "Changed native recipe batch counts rejected.");
            foreach (int value in new[] { -1, 1 })
            {
                Reject(() => RefundLedger.Create(new[] { Snapshot(time: value) }), assert, "Nonzero production time rejected.");
                Reject(() => RefundLedger.Create(new[] { Snapshot(extraTime: value) }), assert, "Nonzero extra time rejected.");
                Reject(() => RefundLedger.Create(new[] { Snapshot(cycles: value) }), assert, "Nonzero cycle count rejected.");
                Reject(() => RefundLedger.Create(new[] { Snapshot(extraCycles: value) }), assert, "Nonzero extra-cycle count rejected.");
                Reject(() => RefundLedger.Create(new[] { Snapshot(hash: value) }), assert, "Nonzero research bytes rejected.");
                Reject(() => RefundLedger.Create(new[] { Snapshot(extraHash: value) }), assert, "Nonzero extra research bytes rejected.");
            }
            Reject(() => RefundLedger.Create(new[] { Snapshot(replicating: true) }), assert, "Replicating cycle rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(research: true) }), assert, "Research-mode lab rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(tech: 1951) }), assert, "Lab technology reference rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(matrix: new[] { 0, 1 }) }), assert, "Fractional research matrix buffer rejected.");
            Reject(() => RefundLedger.Create(new[] { Snapshot(matrixInc: new[] { 1 }) }), assert, "Research proliferation points rejected.");
            var maximum = Snapshot(new[] { int.MaxValue, 0, 0 });
            assert(RefundLedger.Create(new[] { maximum }).Totals.Single().Count == int.MaxValue, "No premature overflow in supported packet count.");
            Reject(() => RefundLedger.Create(new[] { maximum, Snapshot(new[] { 1, 0, 0 }) }), assert, "Aggregate item overflow rejected.", typeof(OverflowException));
            var maximumInc = Snapshot(new[] { int.MaxValue / 2, 0, 0 }, new[] { int.MaxValue, 0, 0 });
            Reject(() => RefundLedger.Create(new[] { maximumInc, Snapshot(new[] { 1, 0, 0 }, new[] { 1, 0, 0 }) }), assert, "Aggregate proliferation overflow rejected.", typeof(OverflowException));
            var repeat = RefundLedger.Create(new[] { snapshot });
            assert(repeat.Totals.Select(p => (p.ItemId, p.Count, p.Inc)).SequenceEqual(plan.Totals.Select(p => (p.ItemId, p.Count, p.Inc))),
                "Repeated planning is deterministic and never drains a snapshot.");
        }

        private static RefundBufferSnapshot Snapshot(int[]? served = null, int[]? inc = null, int[]? produced = null,
            int recipe = 48101, int time = 0, int extraTime = 0, int cycles = 0, int extraCycles = 0,
            bool replicating = false, bool research = false, int tech = 0, int hash = 0, int extraHash = 0,
            int[]? matrix = null, int[]? matrixInc = null)
            => new RefundBufferSnapshot(recipe, new[] { 1128, 1109, 1110 }, new[] { 1, 1, 1 }, new[] { 5206 }, new[] { 2 },
                served ?? new[] { 0, 0, 0 }, inc ?? new[] { 0, 0, 0 }, produced ?? new[] { 0 },
                time, extraTime, cycles, extraCycles, replicating, research, tech, hash, extraHash, matrix, matrixInc);

        private static void Reject(Action action, Action<bool, string> assert, string description, Type? expected = null)
        {
            try { action(); }
            catch (Exception error)
            {
                assert(error.GetType() == (expected ?? typeof(InvalidOperationException)), description + " Exact rejection type.");
                return;
            }
            assert(false, description);
        }
    }
}
