using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.LabHookBenchmark
{
    // Calls the real Core overloads, not game substitutes or a copied candidate implementation.
    internal static class Program
    {
        private const int Iterations = 10_000_000;
        private const int Samples = 7;
        private static readonly int[] Expected = { 6001, 6002, 6003, 6004, 6005, 6006 };

        private static void Main()
        {
            Console.WriteLine($"Runtime={Environment.Version}; OS={Environment.OSVersion}; " +
                $"tieredCompilation={Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "default"}; " +
                $"iterations={Iterations}; samples={Samples}; StopwatchFrequency={Stopwatch.Frequency}");
            var counted = new CountingList(Expected);
            if (!NativeMatrixContract.IsSupported(counted)) throw new InvalidOperationException("Valid registry rejected.");
            Console.WriteLine($"Accepted interface-list work: Count reads={counted.CountReads}, index reads={counted.IndexReads}");

            var random = new Random(195148102);
            var mixed = new int[4096][];
            for (int index = 0; index < mixed.Length; ++index)
            {
                mixed[index] = (index & 3) switch
                {
                    0 => new[] { 6001, 6002, 6003, 6004, 6005, 6006 },
                    1 => new[] { 6001, 6002, 6003, 6004, 6005, 5201 },
                    2 => new[] { 5201, 6002, 6003, 6004, 6005, 6006 },
                    _ => new[] { 6001, 6002, 6003, 6004, 6005 }
                };
            }
            for (int index = mixed.Length - 1; index > 0; --index)
            {
                int other = random.Next(index + 1);
                (mixed[index], mixed[other]) = (mixed[other], mixed[index]);
            }
            Run("valid", new[] { Expected });
            Run("mixed4096", mixed);
            Console.WriteLine("Scope: isolated Core helper on .NET 8. Not Unity Mono, a game tick, frame time, or FPS measurement.");
        }

        private static void Run(string label, int[][] values)
        {
            // Arrays are allocated before measurement; the mixed case is evenly divided among
            // accepted IDs, late mismatch, early mismatch, and wrong length, then deterministically shuffled.
            foreach (int[] ids in values)
                if (Baseline(ids) != ids.SequenceEqual(Expected) || Fast(ids) != ids.SequenceEqual(Expected))
                    throw new InvalidOperationException("Overload result differs from the independent oracle.");
            long checksum = 0;
            long warmExpected = ExpectedCount(values, 1_000_000);
            for (int warmup = 0; warmup < 5; ++warmup)
            {
                checksum += Measure(false, values, 1_000_000, warmExpected).Checksum;
                checksum += Measure(true, values, 1_000_000, warmExpected).Checksum;
            }
            var baseline = new List<double>();
            var candidate = new List<double>();
            long expected = ExpectedCount(values, Iterations);
            for (int sample = 0; sample < Samples; ++sample)
            {
                // Reverse execution order on alternate samples to reduce order effects.
                for (int pass = 0; pass < 2; ++pass)
                {
                    bool fast = ((sample + pass) & 1) != 0;
                    var measured = Measure(fast, values, Iterations, expected);
                    checksum += measured.Checksum;
                    (fast ? candidate : baseline).Add(measured.Nanoseconds);
                    Console.WriteLine($"{label} sample={sample} {(fast ? "array" : "interface")} " +
                        $"ns/op={measured.Nanoseconds:F3} allocatedBytes={measured.Allocated} checksum={measured.Checksum}");
                }
            }
            baseline.Sort();
            candidate.Sort();
            Console.WriteLine($"{label} median interface={baseline[Samples / 2]:F3}ns array={candidate[Samples / 2]:F3}ns " +
                $"ratio={baseline[Samples / 2] / candidate[Samples / 2]:F3}x totalChecksum={checksum}");
        }

        private static long ExpectedCount(int[][] values, int count)
        {
            long total = (long)(count / values.Length) * values.Count(ids => ids.SequenceEqual(Expected));
            for (int index = 0; index < count % values.Length; ++index)
                if (values[index].SequenceEqual(Expected)) ++total;
            return total;
        }

        private static (double Nanoseconds, long Allocated, long Checksum) Measure(bool fast, int[][] values, int count, long expected)
        {
            if ((values.Length & (values.Length - 1)) != 0) throw new ArgumentException("Dataset length must be a power of two.");
            long before = GC.GetAllocatedBytesForCurrentThread();
            long checksum = 0;
            int mask = values.Length - 1;
            long started = Stopwatch.GetTimestamp();
            if (fast)
            {
                for (int index = 0; index < count; ++index) if (Fast(values[index & mask])) ++checksum;
            }
            else
            {
                for (int index = 0; index < count; ++index) if (Baseline(values[index & mask])) ++checksum;
            }
            long elapsed = Stopwatch.GetTimestamp() - started;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (checksum != expected) throw new InvalidOperationException("Measured checksum differs from the independent oracle.");
            return (elapsed * 1_000_000_000.0 / Stopwatch.Frequency / count, allocated, checksum);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Baseline(int[] values) => NativeMatrixContract.IsSupported((IReadOnlyList<int>)values);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Fast(int[] values) => NativeMatrixContract.IsSupported(values);

        private sealed class CountingList : IReadOnlyList<int>
        {
            private readonly int[] values;
            internal int CountReads;
            internal int IndexReads;
            internal CountingList(int[] values) { this.values = values; }
            public int Count { get { ++CountReads; return values.Length; } }
            public int this[int index] { get { ++IndexReads; return values[index]; } }
            public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)values).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
