using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DarkFogSynthesis.Core.Compatibility;
using HarmonyLib;

namespace DarkFogSynthesis.Persistence.Tests
{
    // Actual HarmonyX 2.7 detours on harmless methods. No DSP/Unity/native save execution.
    internal static class Program
    {
        private static SessionCompatibilityState state = null!;
        private static SessionFailureBoundary boundary = null!;
        private static MultiplayerSessionProbe peer = null!;
        private static object session = null!;
        private static int assertions, nativeSaves, nativeMutations, legacyWrites, depth;
        private static bool restricted, installed;
        private static string? permitted;
        private static Action? callback;
        private static Exception? saveFailure;
        private static readonly string[] Saves = { nameof(Named), nameof(Auto), nameof(Errored), nameof(LastExit) };
        private static readonly Exception Injected = new InvalidOperationException("native save failure");

        private static int Main()
        {
            var patcher = new HarmonyLib.Harmony("DarkFogSynthesis.Persistence.Tests");
            try
            {
                foreach (string name in Saves)
                {
                    var original = Method(typeof(Program), name);
                    Type legacy = name == nameof(Named) ? typeof(LegacyNamed) : typeof(LegacyAutomatic);
                    patcher.Patch(original,
                        prefix: new HarmonyMethod(Method(legacy, "Before")) { priority = Priority.First },
                        finalizer: new HarmonyMethod(Method(legacy, "After")));
                    patcher.Patch(original,
                        prefix: new HarmonyMethod(Method(typeof(LeasePatch), "Before")) { priority = Priority.Last },
                        finalizer: new HarmonyMethod(Method(typeof(LeasePatch), "After")) { priority = Priority.Last });
                }
                foreach (bool hasPeer in new[] { false, true })
                {
                    foreach (int fail in new[] { -1, 0, 1 }) MutationCallbacks(hasPeer, fail);
                    foreach (string save in Saves) AdmittedSave(hasPeer, save);
                    MaintenancePermit(hasPeer);
                    PreflightAndRecovery(hasPeer);
                }
                Console.WriteLine("PASS: " + assertions + " persistence/Harmony assertions; absent/inactive Nebula, four save shapes, callbacks, nested saves and recovery.");
                Console.WriteLine("Boundary: harmless managed saves only; no game saves, Unity/DSP or integrity approval.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
            finally { patcher.UnpatchSelf(); }
        }

        private static MethodInfo Method(Type t, string name) => t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
        private static void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
        private static void Fresh(bool hasPeer)
        {
            state = new SessionCompatibilityState(); session = new object();
            state.BeginSession(session); state.CompleteValidatedSession(session);
            boundary = new SessionFailureBoundary(state, () => { }, _ => { }, _ => { });
            installed = hasPeer; Peer.Active = false;
            peer = new MultiplayerSessionProbe(() => installed, () => typeof(Peer));
            nativeSaves = nativeMutations = legacyWrites = depth = 0;
            restricted = false; permitted = null; callback = null; saveFailure = null;
        }

        public static class Peer { public static bool Active; public static bool IsActive => Active; }

        [MethodImpl(MethodImplOptions.NoInlining)] private static bool Named(string name) => Body();
        [MethodImpl(MethodImplOptions.NoInlining)] private static bool Auto() => Body();
        [MethodImpl(MethodImplOptions.NoInlining)] private static bool Errored() => Body();
        [MethodImpl(MethodImplOptions.NoInlining)] private static bool LastExit() => Body();
        private static bool Body()
        {
            Check(legacyWrites > 0, "Existing save accounting was not preserved");
            nativeSaves++; depth++;
            try { callback?.Invoke(); if (saveFailure != null) throw saveFailure; return true; }
            finally { depth--; }
        }
        private static bool Invoke(string name) => name switch {
            nameof(Named) => Named("ordinary"), nameof(Auto) => Auto(),
            nameof(Errored) => Errored(), nameof(LastExit) => LastExit(), _ => throw new ArgumentException(name) };

        private static bool AdmitLegacy(bool named, string? requested, ref bool result, out bool token)
        {
            token = false;
            if (!SessionPersistencePolicy.AllowsWrite(state.CanPersist(session) && peer.AllowsSinglePlayer,
                restricted, named, requested, permitted)) { result = false; return false; }
            legacyWrites++; token = true; return true;
        }
        private sealed class LegacyNamed
        {
            private static bool Before(string __0, ref bool __result, out bool __state) => AdmitLegacy(true, __0, ref __result, out __state);
            private static Exception? After(bool __state, Exception? __exception) { if (__state) legacyWrites--; return __exception; }
        }
        private sealed class LegacyAutomatic
        {
            private static bool Before(ref bool __result, out bool __state) => AdmitLegacy(false, null, ref __result, out __state);
            private static Exception? After(bool __state, Exception? __exception) { if (__state) legacyWrites--; return __exception; }
        }
        private sealed class LeasePatch
        {
            // Exact production additional-prefix/finalizer parameter shapes, including reference __state.
            private static bool Before(ref bool __result, out IDisposable? __state)
            {
                __state = null;
                if (state.CanPersist(session) && peer.AllowsSinglePlayer) __state = boundary.TryBeginPersistence(session);
                if (__state != null) return true;
                __result = false; return false;
            }
            private static Exception? After(IDisposable? __state, Exception? __exception) { __state?.Dispose(); return __exception; }
        }
        private static void RefuseAll()
        {
            int before = nativeSaves;
            foreach (string name in Saves) Check(!Invoke(name), "Unverified lab operation admitted " + name);
            Check(nativeSaves == before && legacyWrites == 0, "Refusal ran a native save or leaked legacy accounting");
        }
        private static SessionMutationOutcome Mutate(Action mutate, Action verify) => boundary.TryMutate(session,
            () => { peer.EnsureSinglePlayer(); return true; }, mutate, verify);

        private static void MutationCallbacks(bool hasPeer, int fail)
        {
            Fresh(hasPeer);
            var outcome = Mutate(() => {
                nativeMutations++; RefuseAll();
                Check(Mutate(() => nativeMutations++, () => { }) == SessionMutationOutcome.Refused, "Nested mutation was not refused");
                RefuseAll(); if (fail == 0) throw new Exception("native lab failure");
            }, () => { RefuseAll(); if (fail == 1) throw new Exception("verification failure"); });
            Check(nativeMutations == 1 && nativeSaves == 0, "Mutation callbacks escaped the persistence barrier");
            Check(outcome == (fail < 0 ? SessionMutationOutcome.Completed : SessionMutationOutcome.Blocked), "Wrong mutation result");
            if (fail >= 0) { Check(state.IsBlocked, "Failure did not latch"); RefuseAll(); }
            else foreach (string name in Saves) Check(Invoke(name), "Successful single-player selection did not reopen " + name);
        }
        private static void AdmittedSave(bool hasPeer, string name)
        {
            Fresh(hasPeer);
            int attempts = 0;
            callback = () => {
                attempts++;
                Check(Mutate(() => nativeMutations++, () => { }) == SessionMutationOutcome.Refused,
                    "An admitted save did not exclude mutation");
                if (depth == 1) Check(Named("delegated"), "Nested save delegation was broken");
            };
            Check(Invoke(name) && attempts == 2 && nativeSaves == 2 && nativeMutations == 0 && legacyWrites == 0,
                "Nested save accounting changed");
            callback = null;
            Check(Mutate(() => nativeMutations++, () => { }) == SessionMutationOutcome.Completed, "Completed save leaked its lease");
            saveFailure = Injected;
            Exception? observed = null;
            try { Invoke(name); } catch (Exception error) { observed = error; }
            Check(ReferenceEquals(observed, Injected) && legacyWrites == 0, "Save finalizer changed exception or accounting");
            saveFailure = null;
            Check(Mutate(() => nativeMutations++, () => { }) == SessionMutationOutcome.Completed, "Failed save leaked its lease");
        }
        private static void MaintenancePermit(bool hasPeer)
        {
            Fresh(hasPeer); restricted = true; permitted = "backup";
            Check(Named("backup"), "Existing authorized maintenance save was broken");
            RefuseAll();
            Check(Mutate(RefuseAll, () => Check(!Named("backup"), "Maintenance permit bypassed mutation exclusion")) == SessionMutationOutcome.Completed,
                "Maintenance fixture verification failed");
            Check(Named("backup") && legacyWrites == 0, "Maintenance permit did not recover after lease release");
        }
        private static void PreflightAndRecovery(bool hasPeer)
        {
            Fresh(hasPeer);
            foreach (bool throws in new[] { false, true })
            {
                Check(boundary.TryMutate(session, () => { if (throws) throw new Exception("preflight"); return false; },
                    () => nativeMutations++, () => { }) == SessionMutationOutcome.Refused, "Preflight refusal changed");
                Check(Named("after-refusal") && !state.IsBlocked && nativeMutations == 0, "Read-only refusal damaged SP state");
            }
            Check(Mutate(() => throw new Exception("uncertain lab mutation"), () => { }) == SessionMutationOutcome.Blocked, "Failure did not block");
            RefuseAll();
            state.EndSession(session); session = new object(); state.BeginSession(session); state.CompleteValidatedSession(session);
            Check(Named("recovery") && Mutate(() => nativeMutations++, () => { }) == SessionMutationOutcome.Completed,
                "Fresh validated single-player session did not recover");
        }
    }
}
