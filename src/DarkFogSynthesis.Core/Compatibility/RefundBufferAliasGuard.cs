using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>
    /// Checks live mutable buffer ownership before defensive snapshots erase reference identity.
    /// One instance covers the entire paused inventory scan, including machines outside the refund plan.
    /// This does not validate buffer contents or make native refunds safe.
    /// </summary>
    public sealed class RefundBufferAliasGuard
    {
        private readonly Dictionary<int[], BufferOwner> owners =
            new Dictionary<int[], BufferOwner>(BufferReferenceComparer.Instance);

        /// <summary>
        /// Register an original native array, never a copy. Any positive-length shared array involving
        /// a refund target is unsupported, even if currently zero-filled. Null/zero-length arrays cannot
        /// hold a refundable quantity and are ignored; shape validation remains the caller's responsibility.
        /// Immutable recipe metadata must not be registered here.
        /// </summary>
        public void Observe(int[]? buffer, string machine, string field, bool includedInRefund)
        {
            if (string.IsNullOrWhiteSpace(machine)) throw new ArgumentException("Machine identity is required.", nameof(machine));
            if (string.IsNullOrWhiteSpace(field)) throw new ArgumentException("Buffer field is required.", nameof(field));
            if (buffer == null || buffer.Length == 0) return;
            if (owners.TryGetValue(buffer, out BufferOwner previous))
            {
                if (previous.IncludedInRefund || includedInRefund)
                    throw new InvalidOperationException("Unsupported shared native buffer: " + previous.Machine + "." + previous.Field +
                        " and " + machine + "." + field + " reference the same mutable array. " +
                        "Refund planning was refused before copying buffers; shared ownership needs a tested adapter.");
                return;
            }
            owners.Add(buffer, new BufferOwner(machine, field, includedInRefund));
        }

        private readonly struct BufferOwner
        {
            internal readonly string Machine, Field;
            internal readonly bool IncludedInRefund;
            internal BufferOwner(string machine, string field, bool includedInRefund)
            { Machine = machine; Field = field; IncludedInRefund = includedInRefund; }
        }

        private sealed class BufferReferenceComparer : IEqualityComparer<int[]>
        {
            internal static readonly BufferReferenceComparer Instance = new BufferReferenceComparer();
            public bool Equals(int[]? x, int[]? y) => ReferenceEquals(x, y);
            public int GetHashCode(int[] obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
