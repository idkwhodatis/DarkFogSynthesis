using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DarkFogSynthesis.Core.Compatibility;
using HarmonyLib;

namespace DarkFogSynthesis.Harmony.Tests
{
    // Real Harmony patches over harmless, no-inline methods. No DSP/Unity/BepInEx assembly
    // or game-type stub is loaded. Fixture quarantine is only an observable callback marker;
    // it does not claim to test SafeRemovalService's game-backed cleanup implementation.
    internal static class Program
    {
        private static readonly HarmonyLib.Harmony Patcher = new HarmonyLib.Harmony("DarkFogSynthesis.Harmony.Tests");
        private static Context current = null!;
        private static int assertions;
        [ThreadStatic] private static MaintenanceSessionGuard? restoringPreflightSession;

        private static int Main()
        {
            Console.WriteLine("Harmless-method Harmony pipeline on " + RuntimeInformation.FrameworkDescription);
            foreach (Type type in new[] { typeof(HarmonyLib.Harmony), typeof(MonoMod.RuntimeDetour.Detour),
                typeof(MonoMod.Utils.DynamicMethodDefinition), typeof(Mono.Cecil.AssemblyDefinition) })
                Console.WriteLine(type.Assembly.FullName);
            try
            {
                PatchSaves();
                Patcher.Patch(Method(nameof(HarmlessResume)), prefix: Patch(nameof(BeforeResume)));
                Patcher.Patch(Method(nameof(HarmlessResume)), prefix: Patch(nameof(ForeignResumePrefix), Priority.First));
                // Pin this case: __runOriginal works with a void prefix, not only a bool prefix.
                PatchBegin(false);
                Run("void-prefix-only finalizer observes original completion", VoidPrefixOnly);
                RemoveBeginPatches();
                PatchBegin(true);
                Run("skipped original fails closed with null incoming exception", SkippedOriginal);
                Run("prefix/native/postfix writes wait for finalizer validation", NormalCompletion);
                Run("native failure preserves original exception and quarantine", NativeFailure);
                Run("late postfix failure cannot commit earlier callback writes", LatePostfixFailure);
                Run("prefix failure aborts without original or completion", PrefixFailure);
                Run("late validation rejection retains compatibility and quarantine", LateValidationFailure);
                Run("repeated Begin reopens pending validation", RepeatedBegin);
                Run("nested Begin cannot release its caller validation barrier", NestedBegin);
                Run("completion callbacks remain unwritable until token release", CompletionCallbackWrites);
                Run("pending replacement blocks previously validated identity", PendingReplacement);
                Run("abandoned replacement cannot revive earlier validation", AbandonedReplacement);
                Run("same blocked identity cannot clear either fixture latch", SameBlockedIdentity);
                Run("different validated replacement clears both fixture latches", ValidReplacement);
                Run("maintenance permit is exact, named, and subordinate to readiness", MaintenancePermit);
                Run("save state injection balances writes on native failure", SaveFailure);
                Run("maintenance backup callbacks cannot resume before quarantine", MaintenanceResumeCallback);
                Run("callback pause/session changes refuse cleanup before mutation", MaintenanceCallbackChanges);
                Run("failed preflight resumes only its captured validated session", FailedPreflightResume);
                Run("ordinary native Begin retains pending-session Resume", PendingBeginResume);
                Run("validated replacement callbacks retain active maintenance quarantine", MaintenanceQuarantineReplacement);
                Run("foreign Resume prefixes cannot redirect failed-preflight restoration", FailedPreflightResumeReplacement);
                Run("candidate-save history mutation rejects success while retaining quarantine", CandidateSaveHistoryMutation);
                Run("unchanged candidate-save history passes both preservation checks", CandidateSaveHistoryPreserved);
                Console.WriteLine("PASS: " + assertions + " assertions across 24 real Harmony pipeline cases");
                Console.WriteLine("Boundary: no game integration, actual disk saves, cleanup, or uninstall tested.");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
            finally
            {
                // Harmony 2.7 cannot regenerate a __runOriginal finalizer with zero prefixes.
                // Remove that finalizer before UnpatchSelf removes its last prefix.
                RemoveBeginPatches();
                Patcher.Unpatch(Method(nameof(HarmlessNamedSave)), SaveMethod(typeof(NamedSavePatch), "EndSave"));
                foreach (string name in AutomaticSaveNames)
                    Patcher.Unpatch(Method(name), SaveMethod(typeof(AutomaticSavePatch), "EndSave"));
                Patcher.UnpatchSelf();
            }
        }

        private static void Run(string name, Action test)
        {
            test();
            Console.WriteLine("PASS: " + name);
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private static MethodInfo Method(string name) => typeof(Program).GetMethod(name,
            BindingFlags.NonPublic | BindingFlags.Static)!;

        private static HarmonyMethod Patch(string name, int priority = Priority.Normal) =>
            new HarmonyMethod(Method(name)) { priority = priority };

        private static void PatchBegin(bool includeForeignPatches)
        {
            Patcher.Patch(Method(nameof(HarmlessBegin)), prefix: Patch(nameof(BeforeBegin), int.MaxValue),
                finalizer: Patch(nameof(FinishBegin), Priority.Last));
            if (includeForeignPatches)
                Patcher.Patch(Method(nameof(HarmlessBegin)), prefix: Patch(nameof(ForeignPrefix)),
                    postfix: Patch(nameof(ForeignPostfix), Priority.Last));
        }

        private static void RemoveBeginPatches()
        {
            Patcher.Unpatch(Method(nameof(HarmlessBegin)), Method(nameof(FinishBegin)));
            Patcher.Unpatch(Method(nameof(HarmlessBegin)), HarmonyPatchType.All, Patcher.Id);
        }

        private static readonly string[] AutomaticSaveNames = {
            nameof(HarmlessAutoSave), nameof(HarmlessErrorAutoSave), nameof(HarmlessLastExitSave) };

        private static MethodInfo SaveMethod(Type type, string name) =>
            type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

        private static void PatchSaves()
        {
            // Exact production parameter shapes and separate declaring-type __state slots.
            // Pinned Harmony 2.7 does not support the later __args injection API.
            Patcher.Patch(Method(nameof(HarmlessNamedSave)),
                prefix: new HarmonyMethod(SaveMethod(typeof(NamedSavePatch), "BeginSave")) { priority = Priority.First },
                finalizer: new HarmonyMethod(SaveMethod(typeof(NamedSavePatch), "EndSave")));
            foreach (string name in AutomaticSaveNames)
                Patcher.Patch(Method(name),
                    prefix: new HarmonyMethod(SaveMethod(typeof(AutomaticSavePatch), "BeginSave")) { priority = Priority.First },
                    finalizer: new HarmonyMethod(SaveMethod(typeof(AutomaticSavePatch), "EndSave")));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HarmlessBegin()
        {
            current.NativeBeginCalls++;
            if (current.ExerciseBeginResume) HarmlessResume();
            if (current.ExerciseCallbackSaves) AttemptAllSaves();
            if (current.FailurePoint == "native") throw current.Failure;
        }

        private static void BeforeBegin(out object? __state)
        {
            __state = null;
            __state = current.State.BeginValidation(current.Identity);
            if (current.FailurePoint == "prefix") throw current.Failure;
        }

        private static bool ForeignPrefix()
        {
            TryNestedBegin("prefix");
            if (current.ExerciseCallbackSaves) AttemptAllSaves();
            return !current.SkipOriginal;
        }

        private static void ForeignPostfix()
        {
            TryNestedBegin("postfix");
            if (current.ExerciseCallbackSaves) AttemptAllSaves();
            if (current.FailurePoint == "postfix") throw current.Failure;
        }

        private static Exception? FinishBegin(object? __state, bool __runOriginal, Exception? __exception)
        {
            current.FinalizerCalls++;
            current.ObservedRunOriginal = __runOriginal;
            current.ObservedException = __exception;
            if (__state == null && __exception == null)
                __exception = new InvalidOperationException("Native Begin has no matching validation entry. This session cannot be promoted.");
            try
            {
                return SessionBeginCompletion.Finish(__runOriginal, __exception,
                    () => {
                        current.ValidationCalls++;
                        if (current.AcceptLateValidation) return true;
                        current.State.BlockSession(current.Identity, "Harmless late validation rejection");
                        return false;
                    },
                    () => {
                        current.State.CompleteValidatedSession(current.Identity);
                        current.CompletionCalls++;
                        if (current.ExerciseCompletionSaves) AttemptAllSaves();
                        if (MaintenanceSessionGuard.CanReleaseQuarantineAfterValidation(current.Cleaning,
                            current.QuarantinedIdentity, current.Identity))
                        {
                            current.QuarantinedIdentity = null;
                            current.QuarantineReleaseCalls++;
                        }
                    },
                    error => {
                        current.AbortCalls++;
                        current.State.BlockSession(current.Identity, error.Message);
                        current.State.EndSession(current.Identity);
                    });
            }
            finally { current.State.EndValidation(__state); }
        }

        private static void TryNestedBegin(string stage)
        {
            if (current.NestedAtStage != stage || current.NestedTriggered) return;
            current.NestedTriggered = true;
            current.NestedException = CaptureBegin();
            AttemptAllSaves();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HarmlessNamedSave(string? name) { return RecordNativeSave(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HarmlessAutoSave() { return RecordNativeSave(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HarmlessLastExitSave() { return RecordNativeSave(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HarmlessErrorAutoSave() { return RecordNativeSave(); }

        private static bool RecordNativeSave()
        {
            Check(current.WritesInProgress == 1, "Save original did not retain its granted write state");
            current.NativeSaveCalls++;
            current.SaveCallback?.Invoke();
            if (current.ThrowFromSave) throw current.Failure;
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HarmlessResume() { current.NativeResumeCalls++; current.Paused = false; }

        private static bool BeforeResume()
        {
            lock (current.SaveGate)
            {
                if (!MaintenanceSessionGuard.AllowsResume(current.StartupReady && !current.State.IsBlocked,
                    current.Cleaning, current.QuarantinedIdentity != null)) return false;
                return restoringPreflightSession == null || restoringPreflightSession.CanRestoreRunningSession(current.Identity,
                    current.History, current.Player, current.Loading, current.StartupReady && current.State.CanPersist(current.Identity));
            }
        }

        private static void ForeignResumePrefix() => current.ResumePrefixCallback?.Invoke();

        private static class NamedSavePatch
        {
            private static bool BeginSave(string __0, ref bool __result, out bool __state) =>
                BeforeSave(true, __0, ref __result, out __state);
            private static Exception? EndSave(bool __state, Exception? __exception) => FinishSave(__state, __exception);
        }

        private static class AutomaticSavePatch
        {
            private static bool BeginSave(ref bool __result, out bool __state) =>
                BeforeSave(false, null, ref __result, out __state);
            private static Exception? EndSave(bool __state, Exception? __exception) => FinishSave(__state, __exception);
        }

        private static Exception? FinishSave(bool state, Exception? error)
        {
            current.SaveFinalizerCalls++;
            if (state) --current.WritesInProgress;
            return error;
        }

        private static bool BeforeSave(bool isNamed, string? requestedName, ref bool result, out bool state)
        {
            state = false;
            current.SaveAttempts++;
            bool allowed = SessionPersistencePolicy.AllowsWrite(
                current.StartupReady && current.State.CanPersist(current.Identity),
                current.Cleaning || current.QuarantinedIdentity != null, isNamed,
                requestedName, current.PermittedName);
            if (!allowed) { result = false; current.RefusedSaves++; return false; }
            current.WritesInProgress++;
            state = true;
            return true;
        }

        private static void AttemptAllSaves()
        {
            Check(!HarmlessNamedSave("ordinary"), "A pending callback named save ran");
            Check(!HarmlessAutoSave(), "A pending callback autosave ran");
            Check(!HarmlessLastExitSave(), "A pending callback last-exit save ran");
            Check(!HarmlessErrorAutoSave(), "A pending callback errored autosave ran");
            Check(current.WritesInProgress == 0 && current.SaveAttempts == current.SaveFinalizerCalls,
                "Rejected save changed write count or skipped its state finalizer");
        }

        private static Exception? CaptureBegin()
        {
            try { HarmlessBegin(); return null; }
            catch (Exception error) { return error; }
        }

        private static void NewContext(bool quarantine = false)
        {
            current = new Context();
            current.State.BeginSession(current.Identity);
            if (quarantine) current.QuarantinedIdentity = new object();
        }

        private static void VoidPrefixOnly()
        {
            NewContext();
            HarmlessBegin();
            Check(current.NativeBeginCalls == 1 && current.FinalizerCalls == 1, "Void prefix pipeline did not execute once");
            Check(current.ObservedRunOriginal && current.ObservedException == null, "Void prefix must inject true/null");
            Check(current.CompletionCalls == 1 && current.State.CanPersist(current.Identity), "Void-prefix-only session did not validate");
        }

        private static void SkippedOriginal()
        {
            NewContext(true);
            object quarantine = current.QuarantinedIdentity!;
            current.SkipOriginal = true;
            current.ExerciseCallbackSaves = true;
            Exception? error = CaptureBegin();
            Check(error is InvalidOperationException && error.Message.Contains("skipped", StringComparison.Ordinal), "Canceled Begin must fail explicitly");
            Check(current.NativeBeginCalls == 0 && current.FinalizerCalls == 1, "Canceled Begin body ran or finalizer count changed");
            Check(!current.ObservedRunOriginal && current.ObservedException == null, "Harmony did not inject false/null on prefix cancellation");
            Check(current.ValidationCalls == 0 && current.CompletionCalls == 0 && current.AbortCalls == 1, "Canceled Begin completed validation");
            Check(current.NativeSaveCalls == 0 && current.State.IsBlocked, "Canceled Begin permitted persistence");
            Check(ReferenceEquals(current.QuarantinedIdentity, quarantine) && current.QuarantineReleaseCalls == 0, "Canceled Begin released fixture quarantine");
        }

        private static void NormalCompletion()
        {
            NewContext(true);
            current.ExerciseCallbackSaves = true;
            HarmlessBegin();
            Check(current.SaveAttempts == 12 && current.RefusedSaves == 12 && current.NativeSaveCalls == 0,
                "Prefix, original, and postfix must each refuse all four save paths");
            Check(current.NativeBeginCalls == 1 && current.FinalizerCalls == 1 && current.CompletionCalls == 1,
                "Normal Begin must complete once");
            Check(current.ObservedRunOriginal && current.ObservedException == null, "Normal finalizer arguments changed");
            Check(current.QuarantinedIdentity == null && current.QuarantineReleaseCalls == 1, "Validated replacement did not clear fixture quarantine");
            Check(HarmlessNamedSave("ordinary") && HarmlessAutoSave() && HarmlessLastExitSave() && HarmlessErrorAutoSave(), "Validated session cannot save");
            Check(current.NativeSaveCalls == 4 && current.WritesInProgress == 0 && current.SaveAttempts == current.SaveFinalizerCalls,
                "Validated save originals or paired state finalizers did not run");
        }

        private static void AssertFailure(string point)
        {
            NewContext(true);
            object quarantine = current.QuarantinedIdentity!;
            current.ExerciseCallbackSaves = true;
            current.FailurePoint = point;
            Exception? observed = CaptureBegin();
            Check(ReferenceEquals(observed, current.Failure) && ReferenceEquals(current.ObservedException, current.Failure),
                "Native/prefix/postfix exception identity was replaced");
            Check(current.FinalizerCalls == 1 && current.AbortCalls == 1 && current.CompletionCalls == 0,
                "Failed Begin did not abort exactly once");
            Check(current.ValidationCalls == 0 && current.State.IsBlocked && !current.State.CanPersist(current.Identity),
                "Failed Begin reached successful validation");
            Check(ReferenceEquals(current.QuarantinedIdentity, quarantine) && current.QuarantineReleaseCalls == 0,
                "Failed Begin released fixture quarantine");
            Check(current.NativeSaveCalls == 0, "A callback save escaped before the later failure");
            AttemptAllSaves();
        }

        private static void NativeFailure() => AssertFailure("native");
        private static void LatePostfixFailure() => AssertFailure("postfix");
        private static void PrefixFailure()
        {
            AssertFailure("prefix");
            Check(current.NativeBeginCalls == 0, "Prefix failure allowed the original");
        }

        private static void LateValidationFailure()
        {
            NewContext(true);
            object quarantine = current.QuarantinedIdentity!;
            current.ExerciseCallbackSaves = true;
            current.AcceptLateValidation = false;
            Check(CaptureBegin() == null, "Validator-latched rejection should preserve the null exception contract");
            Check(current.ValidationCalls == 1 && current.CompletionCalls == 0 && current.State.IsBlocked,
                "Late validation rejection did not remain blocked");
            Check(ReferenceEquals(current.QuarantinedIdentity, quarantine), "Late validation rejection released fixture quarantine");
            Check(current.NativeSaveCalls == 0, "Late rejection committed a pending callback save");
            AttemptAllSaves();
        }

        private static void RepeatedBegin()
        {
            NewContext();
            HarmlessBegin();
            Check(current.State.CanPersist(current.Identity), "First Begin must validate identity");
            current.ExerciseCallbackSaves = true;
            HarmlessBegin();
            Check(current.SaveAttempts == 12 && current.RefusedSaves == 12 && current.NativeSaveCalls == 0,
                "Repeated Begin reused stale validation for a callback write");
            Check(current.CompletionCalls == 2 && current.State.CanPersist(current.Identity), "Repeated Begin never reopened validated persistence");
        }

        private static void NestedBegin()
        {
            foreach (string stage in new[] { "prefix", "postfix" })
            {
                NewContext(true);
                object quarantine = current.QuarantinedIdentity!;
                current.NestedAtStage = stage;
                current.ExerciseCallbackSaves = true;
                Check(CaptureBegin() is InvalidOperationException, "Outer Begin ignored nested failure");
                Check(current.NestedException is InvalidOperationException &&
                    current.NestedException.Message.Contains("Nested", StringComparison.Ordinal), "Nested invocation acquired a validation ticket");
                Check(current.NativeBeginCalls == 1 && current.FinalizerCalls == 2, "Nested original executed or finalizers were lost");
                Check(current.AbortCalls == 2 && current.CompletionCalls == 0 && current.State.IsBlocked,
                    "Nested invocation promoted either session");
                Check(current.NativeSaveCalls == 0 && ReferenceEquals(current.QuarantinedIdentity, quarantine),
                    "Nested invocation released persistence or fixture quarantine");
                // The rejected inner invocation could not release the outer ticket. The outer
                // finally must still release it so a later independent identity can validate.
                current.NestedAtStage = null;
                current.ExerciseCallbackSaves = false;
                current.Identity = new object();
                current.State.BeginSession(current.Identity);
                HarmlessBegin();
                Check(current.State.CanPersist(current.Identity) && current.CompletionCalls == 1,
                    "The owning outer finalizer leaked its validation barrier");
            }
        }

        private static void CompletionCallbackWrites()
        {
            NewContext();
            current.ExerciseCompletionSaves = true;
            HarmlessBegin();
            Check(current.SaveAttempts == 4 && current.RefusedSaves == 4 && current.NativeSaveCalls == 0,
                "Completion callback escaped before the finalizer released its ticket");
            Check(HarmlessAutoSave(), "Ticket release did not reopen validated persistence");
        }

        private static void PendingReplacement()
        {
            NewContext();
            HarmlessBegin();
            object validated = current.Identity;
            object replacement = new object();
            current.State.BeginSession(replacement);
            AttemptAllSaves();
            current.Identity = replacement;
            AttemptAllSaves();
            Check(!current.State.CanPersist(validated) && !current.State.CanPersist(replacement), "Pending replacement left an identity writable");
            HarmlessBegin();
            Check(HarmlessNamedSave("replacement"), "Completed replacement did not become writable");
        }

        private static void AbandonedReplacement()
        {
            NewContext();
            HarmlessBegin();
            object validated = current.Identity;
            object replacement = new object();
            current.State.BeginSession(replacement);
            current.State.EndSession(replacement);
            AttemptAllSaves();
            Check(!current.State.CanPersist(validated), "Abandoned replacement revived old validation");
            current.Identity = replacement;
            AttemptAllSaves();
            Check(!current.State.CanPersist(replacement), "Abandoned replacement remained writable");
        }

        private static void SameBlockedIdentity()
        {
            NewContext();
            current.QuarantinedIdentity = current.Identity;
            current.SkipOriginal = true;
            Check(CaptureBegin() is InvalidOperationException, "Canceled session must block first");
            current.SkipOriginal = false;
            Check(CaptureBegin() is InvalidOperationException, "Same blocked identity restarted");
            Check(current.NativeBeginCalls == 0 && current.CompletionCalls == 0 && current.State.IsBlocked,
                "Same blocked identity cleared the compatibility latch");
            Check(ReferenceEquals(current.QuarantinedIdentity, current.Identity) && current.QuarantineReleaseCalls == 0,
                "Same blocked identity cleared fixture quarantine");
            AttemptAllSaves();
        }

        private static void ValidReplacement()
        {
            SameBlockedIdentity();
            object blocked = current.Identity;
            current.Identity = new object();
            current.State.BeginSession(current.Identity);
            HarmlessBegin();
            Check(current.CompletionCalls == 1 && !current.State.IsBlocked, "Different validated replacement did not clear compatibility latch");
            Check(current.QuarantinedIdentity == null && current.QuarantineReleaseCalls == 1, "Different validated replacement did not clear fixture quarantine");
            Check(!current.State.CanPersist(blocked) && HarmlessAutoSave(), "Wrong identity remained writable after replacement");
        }

        private static void MaintenancePermit()
        {
            NewContext();
            HarmlessBegin();
            current.Cleaning = true;
            current.PermittedName = "Exact-Clean-Copy";
            Check(HarmlessNamedSave("Exact-Clean-Copy"), "Exact maintenance named save was denied");
            Check(!HarmlessNamedSave("exact-clean-copy") && !HarmlessNamedSave("other") && !HarmlessNamedSave(null),
                "Maintenance permit was not exact and ordinal");
            Check(!HarmlessAutoSave() && !HarmlessLastExitSave() && !HarmlessErrorAutoSave(), "Unnamed maintenance save escaped");
            current.Cleaning = false;
            current.QuarantinedIdentity = current.Identity;
            Check(HarmlessNamedSave("Exact-Clean-Copy"), "Exact permit should apply while quarantined");
            Check(!HarmlessAutoSave(), "Fixture quarantine permitted autosave");
            current.PermittedName = null;
            Check(!HarmlessNamedSave("Exact-Clean-Copy"), "Expired maintenance permit was reusable");
            current.PermittedName = "Exact-Clean-Copy";
            current.State.BeginSession(current.Identity);
            Check(!HarmlessNamedSave("Exact-Clean-Copy"), "Maintenance permit bypassed pending validation");
            current.State.CompleteValidatedSession(current.Identity);
            current.StartupReady = false;
            Check(!HarmlessNamedSave("Exact-Clean-Copy"), "Maintenance permit bypassed startup readiness");
            current.StartupReady = true;
            current.State.BlockSession(current.Identity, "Harmless failure");
            Check(!HarmlessNamedSave("Exact-Clean-Copy"), "Maintenance permit bypassed compatibility latch");
            Check(current.NativeSaveCalls == 2, "Unexpected maintenance save original executed");
        }

        private static void SaveFailure()
        {
            NewContext();
            HarmlessBegin();
            current.ThrowFromSave = true;
            Exception? observed = null;
            try { HarmlessNamedSave("native-save-failure"); }
            catch (Exception error) { observed = error; }
            Check(ReferenceEquals(observed, current.Failure), "Save finalizer replaced original exception");
            Check(current.NativeSaveCalls == 1 && current.WritesInProgress == 0 && current.SaveFinalizerCalls == 1,
                "Save exception leaked granted write state");
        }

        private static MaintenanceSessionGuard PrepareMaintenance()
        {
            NewContext();
            HarmlessBegin();
            var guard = new MaintenanceSessionGuard(current.Identity, current.History, current.Player, current.Paused);
            current.Paused = true;
            current.Cleaning = true;
            current.PermittedName = "maintenance-backup";
            return guard;
        }

        private static void RequirePausedMaintenance(MaintenanceSessionGuard guard) =>
            guard.EnsurePausedSession(current.Identity, current.History, current.Player, current.Paused,
                current.Loading, current.StartupReady && current.State.CanPersist(current.Identity));

        private static void MaintenanceResumeCallback()
        {
            var guard = PrepareMaintenance();
            current.SaveCallback = HarmlessResume;
            Check(current.QuarantinedIdentity == null && current.State.CanPersist(current.Identity),
                "The callback case must use a validated session before quarantine");
            Check(HarmlessNamedSave("maintenance-backup"), "Maintenance backup's exact permit stopped working");
            RequirePausedMaintenance(guard);
            Check(current.NativeResumeCalls == 0 && current.Paused && current.WritesInProgress == 0,
                "A native save callback resumed cleaning before quarantine");
            current.Cleaning = false;
            current.QuarantinedIdentity = current.Identity;
            HarmlessResume();
            Check(current.NativeResumeCalls == 0 && current.Paused, "Quarantined candidate resumed");
        }

        private static void CandidateSaveHistoryPreserved() => VerifyCandidateHistory("none");

        private static void CandidateSaveHistoryMutation()
        {
            foreach (string change in new[] { "recipe", "tech-remove", "tech-value", "queue", "current" })
                VerifyCandidateHistory(change);
        }

        private static void VerifyCandidateHistory(string change)
        {
            var guard = PrepareMaintenance();
            object identity = current.Identity, history = current.History, player = current.Player;
            current.QuarantinedIdentity = identity;
            current.PermittedName = "maintenance-candidate";
            var expectedRecipes = new[] { 10, 11 };
            var expectedTechs = new Dictionary<int, long> { [100] = 10, [101] = 20 };
            var expectedQueue = new[] { 100, 101, 0 };
            var recipes = new HashSet<int>(expectedRecipes);
            var techs = new Dictionary<int, long>(expectedTechs);
            var queue = expectedQueue.ToList();
            int activeTech = 100, reports = 0, checks = 0, callbacks = 0;
            current.SaveCallback = () => {
                callbacks++;
                switch (change)
                {
                    case "recipe": recipes.Remove(10); break;
                    case "tech-remove": techs.Remove(101); break;
                    case "tech-value": techs[101]++; break;
                    case "queue": queue.Reverse(); break;
                    case "current": activeTech = 101; break;
                }
            };
            Exception? observed = null;
            try
            {
                RemovalHistoryGuard.VerifyAcrossSave(() => {
                    checks++;
                    RequirePausedMaintenance(guard);
                    RemovalHistoryGuard.EnsurePreserved(expectedRecipes, expectedTechs, expectedQueue, 100,
                        recipes, techs, queue, activeTech);
                }, () => Check(HarmlessNamedSave("maintenance-candidate"), "Candidate save original was refused"));
                reports++;
            }
            catch (InvalidOperationException error) { observed = error; }
            finally { current.PermittedName = null; current.Cleaning = false; }
            Check(callbacks == 1 && checks == 2 && current.NativeSaveCalls == 1 && current.WritesInProgress == 0,
                "Both-side preservation check must surround an actual Harmony-patched save callback");
            Check(ReferenceEquals(current.Identity, identity) && ReferenceEquals(current.History, history) &&
                ReferenceEquals(current.Player, player), "This case must not depend on replacing native session identities");
            Check((observed == null) == (change == "none") && reports == (change == "none" ? 1 : 0),
                "Candidate-save callback " + change + " gave an incorrect preservation result");
            Check(ReferenceEquals(current.QuarantinedIdentity, identity), "History verification released quarantine");
            if (change == "tech-remove") Check(!techs.ContainsKey(101), "Guard repaired another mod's history");
            if (change == "recipe") Check(!recipes.Contains(10), "Guard repaired another mod's unlocks");
            HarmlessResume();
            Check(current.Paused && current.NativeResumeCalls == 0 && !HarmlessAutoSave() &&
                !HarmlessNamedSave("maintenance-candidate"), "Expired maintenance permit or quarantine was bypassed");
        }

        private static void MaintenanceCallbackChanges()
        {
            foreach (string change in new[] { "pause", "session", "history", "player", "loading", "validation" })
            {
                var guard = PrepareMaintenance();
                current.SaveCallback = () => {
                    switch (change)
                    {
                        case "pause": current.Paused = false; break;
                        case "session":
                            current.Identity = new object();
                            current.State.BeginSession(current.Identity);
                            current.State.CompleteValidatedSession(current.Identity);
                            break;
                        case "history": current.History = new object(); break;
                        case "player": current.Player = new object(); break;
                        case "loading": current.Loading = true; break;
                        case "validation": current.State.BeginSession(current.Identity); break;
                    }
                };
                Check(HarmlessNamedSave("maintenance-backup"), "Harmless callback save did not execute");
                int mutations = 0;
                Exception? refusal = null;
                try { RequirePausedMaintenance(guard); mutations++; }
                catch (InvalidOperationException error) { refusal = error; }
                Check(refusal != null && mutations == 0,
                    "Callback " + change + " change escaped the production pre-mutation boundary check");
                Check(current.NativeResumeCalls == 0,
                    "Direct field-change injection must not be mistaken for a guarded Resume call");
            }
        }

        private static void TryRestoreFailedPreflight(MaintenanceSessionGuard guard)
        {
            bool restoreRunning;
            lock (current.SaveGate)
            {
                current.Cleaning = false;
                restoreRunning = guard.CanRestoreRunningSession(current.Identity, current.History, current.Player, current.Loading,
                    current.StartupReady && current.State.CanPersist(current.Identity));
            }
            if (restoreRunning)
            {
                var previous = restoringPreflightSession;
                restoringPreflightSession = guard;
                try { HarmlessResume(); }
                finally { restoringPreflightSession = previous; }
            }
        }

        private static void FailedPreflightResume()
        {
            var guard = PrepareMaintenance();
            HarmlessResume();
            Check(current.NativeResumeCalls == 0 && current.Paused, "Maintenance cleared before failed-preflight restoration");
            TryRestoreFailedPreflight(guard);
            Check(!current.Cleaning && !current.Paused && current.NativeResumeCalls == 1,
                "A failed preflight did not restore its captured originally running session");

            foreach (string change in new[] { "session", "pending", "loading", "history", "player", "startup", "quarantine", "previously-paused" })
            {
                guard = PrepareMaintenance();
                switch (change)
                {
                    case "session":
                        current.Identity = new object();
                        current.State.BeginSession(current.Identity);
                        current.State.CompleteValidatedSession(current.Identity);
                        break;
                    case "pending": current.State.BeginSession(current.Identity); break;
                    case "loading": current.Loading = true; break;
                    case "history": current.History = new object(); break;
                    case "player": current.Player = new object(); break;
                    case "startup": current.StartupReady = false; break;
                    case "quarantine": current.QuarantinedIdentity = current.Identity; break;
                    case "previously-paused":
                        guard = new MaintenanceSessionGuard(current.Identity, current.History, current.Player, true);
                        break;
                }
                TryRestoreFailedPreflight(guard);
                Check(!current.Cleaning && current.Paused && current.NativeResumeCalls == 0,
                    "Failed preflight incorrectly resumed " + change + " context");
            }
        }

        private static void PendingBeginResume()
        {
            NewContext();
            current.Paused = true;
            current.ExerciseBeginResume = true;
            current.ExerciseCallbackSaves = true;
            HarmlessBegin();
            Check(current.NativeResumeCalls == 1 && !current.Paused && current.RefusedSaves == 12,
                "Ordinary native Begin must retain Resume while callback persistence stays closed");
        }

        private static void MaintenanceQuarantineReplacement()
        {
            var guard = PrepareMaintenance();
            object quarantined = current.QuarantinedIdentity = current.Identity;
            current.SaveCallback = () => {
                current.Identity = new object();
                current.State.BeginSession(current.Identity);
                HarmlessBegin();
            };
            Check(HarmlessNamedSave("maintenance-backup"), "Harmless replacement callback did not execute");
            Check(current.State.CanPersist(current.Identity) && ReferenceEquals(current.QuarantinedIdentity, quarantined) &&
                current.QuarantineReleaseCalls == 0, "Replacement callback cleared active cleanup quarantine");
            bool refused = false;
            try { RequirePausedMaintenance(guard); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "A late replaced-session boundary allowed cleanup success or rollback");
            current.Cleaning = false;
            HarmlessResume();
            Check(current.NativeResumeCalls == 0 && current.Paused, "Failed cleanup quarantine did not survive barrier release");
            current.Identity = new object();
            current.State.BeginSession(current.Identity);
            HarmlessBegin();
            Check(current.QuarantinedIdentity == null && current.QuarantineReleaseCalls == 1,
                "Normal later validated replacement could not release maintenance quarantine");
        }

        private static void FailedPreflightResumeReplacement()
        {
            foreach (bool throwFromPrefix in new[] { false, true })
            {
                var guard = PrepareMaintenance();
                object captured = current.Identity;
                int foreignCalls = 0;
                current.ResumePrefixCallback = () => {
                    foreignCalls++;
                    current.Identity = new object();
                    current.State.BeginSession(current.Identity);
                    current.State.CompleteValidatedSession(current.Identity);
                    if (throwFromPrefix) throw current.Failure;
                };
                Exception? observed = null;
                try { TryRestoreFailedPreflight(guard); }
                catch (Exception error) { observed = error; }
                Check(throwFromPrefix ? ReferenceEquals(observed, current.Failure) : observed == null,
                    "Restoration changed a foreign Resume prefix exception");
                Check(foreignCalls == 1 && !ReferenceEquals(current.Identity, captured) && current.State.CanPersist(current.Identity),
                    "The actual earlier Harmony prefix must install a different fully validated session");
                Check(!current.Cleaning && current.Paused && current.NativeResumeCalls == 0,
                    "A foreign prefix redirected automatic preflight Resume to its replacement session");
                current.ResumePrefixCallback = null;
                HarmlessResume();
                Check(!current.Paused && current.NativeResumeCalls == 1,
                    "The automatic-restoration scope leaked into a later ordinary Resume");
            }
        }

        private sealed class Context
        {
            internal readonly SessionCompatibilityState State = new SessionCompatibilityState();
            internal readonly Exception Failure = new InvalidOperationException("Harmless injected failure");
            internal object Identity = new object();
            internal object History = new object(), Player = new object();
            internal readonly object SaveGate = new object();
            internal object? QuarantinedIdentity;
            internal Action? SaveCallback, ResumePrefixCallback;
            internal bool Paused, Loading, ExerciseBeginResume;
            internal int NativeResumeCalls;
            internal bool SkipOriginal, ExerciseCallbackSaves, ExerciseCompletionSaves, Cleaning, NestedTriggered, ThrowFromSave;
            internal bool AcceptLateValidation = true, StartupReady = true;
            internal string? FailurePoint, PermittedName, NestedAtStage;
            internal int NativeBeginCalls, FinalizerCalls, ValidationCalls, CompletionCalls, AbortCalls;
            internal int NativeSaveCalls, SaveAttempts, RefusedSaves, QuarantineReleaseCalls, WritesInProgress, SaveFinalizerCalls;
            internal bool ObservedRunOriginal;
            internal Exception? ObservedException, NestedException;
        }
    }
}
