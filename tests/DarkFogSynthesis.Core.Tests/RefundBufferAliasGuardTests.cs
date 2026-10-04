using System;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class RefundBufferAliasGuardTests
    {
        internal static void Run(Action<bool, string> assert)
        {
            var separate = new RefundBufferAliasGuard();
            separate.Observe(new[] { 5 }, "lab[1]", "produced", true);
            separate.Observe(new[] { 5 }, "lab[2]", "produced", true);
            assert(true, "Independent arrays with identical quantities are distinct inventories.");

            foreach (string field in new[] { "produced", "served", "incServed", "matrixServed", "matrixIncServed" })
            {
                var guard = new RefundBufferAliasGuard();
                var shared = new[] { 5, 2 };
                guard.Observe(shared, "factory[0].lab[1]", field, true);
                Reject(() => guard.Observe(shared, "factory[1].lab[2]", field, true), assert,
                    field + " shared across factories is rejected by original reference identity.");
                assert(shared[0] == 5 && shared[1] == 2, "Alias rejection never drains or modifies " + field + ".");
            }

            var crossField = new RefundBufferAliasGuard();
            var crossFieldBuffer = new[] { 1 };
            crossField.Observe(crossFieldBuffer, "lab[1]", "served", true);
            Reject(() => crossField.Observe(crossFieldBuffer, "lab[1]", "incServed", true), assert,
                "Sharing across mutable fields in one machine is also unsupported.");

            var zeroFilled = new RefundBufferAliasGuard();
            var zeros = new int[3];
            zeroFilled.Observe(zeros, "lab[1]", "served", true);
            Reject(() => zeroFilled.Observe(zeros, "lab[2]", "served", true), assert,
                "Positive-length zero-filled arrays still have shared mutable ownership.");

            var empty = new RefundBufferAliasGuard();
            for (int i = 0; i < 100; ++i)
            {
                empty.Observe(Array.Empty<int>(), "lab[" + i + "]", "produced", true);
                empty.Observe(Array.Empty<int>(), "lab[" + i + "]", "served", true);
                empty.Observe(Array.Empty<int>(), "lab[" + i + "]", "incServed", true);
                empty.Observe(null, "lab[" + i + "]", "matrixServed", true);
            }
            assert(true, "Shared empty singleton arrays and absent buffers produce no spurious alias failure.");

            foreach (bool foreignFirst in new[] { false, true })
            {
                var mixed = new RefundBufferAliasGuard();
                var buffer = new[] { 7 };
                mixed.Observe(buffer, "machine[1]", "produced", !foreignFirst);
                Reject(() => mixed.Observe(buffer, "machine[2]", "produced", foreignFirst), assert,
                    "Owned/foreign shared buffer rejected regardless of scan ordering.");
            }
            var foreign = new RefundBufferAliasGuard();
            var foreignBuffer = new[] { 7 };
            foreign.Observe(foreignBuffer, "foreign[1]", "produced", false);
            foreign.Observe(foreignBuffer, "foreign[2]", "produced", false);
            assert(true, "Unrelated foreign-only sharing is outside this refund plan.");
            Reject(() => foreign.Observe(foreignBuffer, "owned[3]", "produced", true), assert,
                "An owned machine still conflicts after multiple foreign aliases were observed.");

            // Snapshot copying must happen after this original-array preflight. Two snapshots would
            // otherwise turn one five-item buffer into two apparently independent five-item buffers.
            var snapshotGuard = new RefundBufferAliasGuard();
            var original = new[] { 5 };
            snapshotGuard.Observe(original, "lab[1]", "produced", true);
            Reject(() => snapshotGuard.Observe(original, "lab[2]", "produced", true), assert,
                "Shared source is rejected before defensive copies can hide ownership.");
            assert(original[0] == 5, "Preflight leaves the original quantity intact.");
            Reject(() => new RefundBufferAliasGuard().Observe(new[] { 1 }, "", "produced", true), assert,
                "Missing machine identity is rejected.", typeof(ArgumentException));
            Reject(() => new RefundBufferAliasGuard().Observe(new[] { 1 }, "lab[1]", "", true), assert,
                "Missing field identity is rejected.", typeof(ArgumentException));
        }

        private static void Reject(Action action, Action<bool, string> assert, string description, Type? expected = null)
        {
            try { action(); }
            catch (Exception error)
            {
                assert(error.GetType() == (expected ?? typeof(InvalidOperationException)), description);
                return;
            }
            assert(false, description);
        }
    }
}
