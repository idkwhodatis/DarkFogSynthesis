using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using DarkFogSynthesis.Core.Compatibility;
using HarmonyLib;

namespace DarkFogSynthesis.Harmony.Tests
{
    /// <summary>Actual detours over harmless export/reset methods; no DSP types, buffers or disk saves.</summary>
    internal static class RemovalTargetHarmonyTests
    {
        private static Action<int>? afterExport;
        private static Action<int>? afterReset;
        private static int[] exports = Array.Empty<int>();
        private static int[] resets = Array.Empty<int>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static byte[] Export(int index) { exports[index]++; return new[] { (byte)index }; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Reset(int index) { resets[index]++; }
        private static void ExportPostfix(int __0) => afterExport?.Invoke(__0);
        private static void ResetPostfix(int __0) => afterReset?.Invoke(__0);
        private static MethodInfo Method(string name) => typeof(RemovalTargetHarmonyTests).GetMethod(name,
            BindingFlags.NonPublic | BindingFlags.Static)!;

        internal static void Run(Action<bool, string> check, bool drift)
        {
            var harmony = new HarmonyLib.Harmony("darkfogsynthesis.tests.per-target-guard");
            try
            {
                harmony.Patch(Method(nameof(Export)), postfix: new HarmonyMethod(Method(nameof(ExportPostfix))));
                harmony.Patch(Method(nameof(Reset)), postfix: new HarmonyMethod(Method(nameof(ResetPostfix))));
                foreach (bool lab in new[] { false, true })
                foreach (bool duringExport in new[] { false, true })
                foreach (string change in drift ? new[] { "recipe", "entity", "progress", "location" } : new[] { "none" })
                {
                    exports = new int[2]; resets = new int[2];
                    var expected = new[] { new RemovalTargetIdentity(1, 10, 48102, 0, false, lab),
                        new RemovalTargetIdentity(2, 20, 48102, 0, false, lab) };
                    var live = (RemovalTargetIdentity[])expected.Clone();
                    bool hasState = false, locationMatches = true;
                    int reports = 0, enrolled = 0, checks = 0, callbacks = 0;
                    void Change()
                    {
                        callbacks++;
                        live[1] = new RemovalTargetIdentity(2, change == "entity" ? 21 : 20,
                            change == "recipe" ? 1 : 48102, 0, false, lab);
                        hasState = change == "progress";
                        locationMatches = change != "location";
                    }
                    afterExport = index => { if (duringExport && index == 1) Change(); };
                    afterReset = index => { if (!duringExport && index == 0) Change(); };
                    Exception? failure = null;
                    try
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            int index = i;
                            RemovalTargetGuard.Reset(() => {
                                checks++;
                                if (index == 1 && !locationMatches) throw new InvalidOperationException("Fixture location changed");
                                RemovalTargetGuard.EnsureUnchanged(expected[index], live[index], live[index].RecipeId == 48102,
                                    index == 1 && hasState);
                            }, () => {
                                byte[] bytes = Export(index);
                                return () => { GC.KeepAlive(bytes); };
                            }, _ => enrolled++, () => Reset(index));
                        }
                        reports++;
                    }
                    catch (InvalidOperationException error) { failure = error; }
                    finally { afterExport = null; afterReset = null; }
                    string label = lab + "/export=" + duringExport + "/" + change;
                    check(callbacks == 1, "Actual detour callback was not exercised: " + label);
                    check((failure != null) == drift && reports == (drift ? 0 : 1), "Changed machine was declared cleaned: " + label);
                    check(resets[0] == 1 && resets[1] == (drift ? 0 : 1), "Later changed target reached reset: " + label);
                    check(exports[1] == (!drift || duringExport ? 1 : 0), "Changed target reached export: " + label);
                    check(enrolled == (drift ? 1 : 2), "Stale target rollback enrollment: " + label);
                    check(checks == (drift && !duringExport ? 3 : 4), "Missing target revalidation: " + label);
                    check(live[1].RecipeId == (change == "recipe" ? 1 : 48102) &&
                        live[1].EntityId == (change == "entity" ? 21 : 20) && hasState == (change == "progress"),
                        "Refusal must not repair a peer's later target changes");
                }
            }
            finally { afterExport = null; afterReset = null; harmony.UnpatchSelf(); }
        }
    }
}
