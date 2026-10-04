using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    /// <summary>Failure injection through the exact production boundary; no game-type substitutes.</summary>
    internal static class SessionFailureBoundaryTests
    {
        internal static void AbortFailures(Action<bool, string> assert)
        {
            foreach (Exception failure in new Exception[] {
                new InvalidOperationException(""), new InvalidOperationException(" \t\r\n"),
                new InvalidOperationException("native import failed"), new UnreadableMessageException() })
            for (int brokenSinks = 0; brokenSinks < 8; brokenSinks++)
            foreach (bool brokenRestore in new[] { false, true })
            foreach (bool brokenCleanup in new[] { false, true })
            {
                var state = new SessionCompatibilityState();
                object session = new object();
                state.BeginSession(session);
                state.CompleteValidatedSession(session);
                var order = new List<string>();
                var observations = new List<bool>();
                int mask = brokenSinks;
                var boundary = new SessionFailureBoundary(state,
                    () => { order.Add("pause"); observations.Add(state.IsBlocked); if ((mask & 1) != 0) throw new Exception("pause failed"); },
                    reason => { order.Add("present"); observations.Add(state.IsBlocked && !string.IsNullOrWhiteSpace(reason)); if ((mask & 2) != 0) throw new Exception("UI failed"); },
                    _ => { order.Add("log"); if ((mask & 4) != 0) throw new Exception("logger failed"); });
                boundary.Abort(session, failure,
                    () => { order.Add("restore"); observations.Add(state.IsBlocked); if (brokenRestore) throw new Exception("restore failed"); },
                    () => { order.Add("cleanup"); state.EndSession(session); if (brokenCleanup) throw new Exception("cleanup failed"); });
                assert(observations.Count == 3 && observations.All(value => value), "Pause, presentation and restore observe the established nonblank latch; assertions run outside swallowed callbacks.");
                assert(state.IsBlocked && !string.IsNullOrWhiteSpace(state.BlockReason), "Every abort remains blocked despite blank messages and broken diagnostic sinks.");
                assert(order.Count(x => x == "restore") == 1 && order.Count(x => x == "cleanup") == 1 &&
                    order.IndexOf("restore") < order.IndexOf("cleanup"), "Restoration and transition cleanup each run once, in order, despite other failures.");
                assert(order.IndexOf("pause") < order.IndexOf("present"), "Pause is attempted before optional presentation.");
                AssertBlockedIdentity(state, session, assert);
                object replacement = new object();
                state.BeginSession(replacement);
                assert(state.IsBlocked, "Preparing a replacement cannot clear failure containment.");
                state.CompleteValidatedSession(replacement);
                assert(!state.IsBlocked, "Only a different fully validated session clears the failure.");
            }
            assert(SessionFailureBoundary.FailureReason(new Exception("useful reason")) == "useful reason", "Useful native error text is retained.");
            assert(!string.IsNullOrWhiteSpace(SessionFailureBoundary.FailureReason(null)), "Missing exception diagnostics also have a visible fallback.");
            var once = new OneReadMessageException();
            assert(SessionFailureBoundary.FailureReason(once) == "first read" && once.Reads == 1, "A virtual Message getter is read only once.");
        }

        internal static void NativeMutationFailures(Action<bool, string> assert)
        {
            // Three mutator stages correspond to SetFunction, SyncLabFunctions and SyncLabForceAccMode.
            // These counters test boundary ordering/containment, not those game methods' semantics.
            for (int failingStage = 0; failingStage < 4; failingStage++)
            foreach (bool diagnosticsThrow in new[] { false, true })
            {
                var state = new SessionCompatibilityState();
                object session = new object();
                state.BeginSession(session); state.CompleteValidatedSession(session);
                int touched = 0, verified = 0, paused = 0;
                bool pauseObservedLatch = false;
                var boundary = new SessionFailureBoundary(state,
                    () => { paused++; pauseObservedLatch = state.IsBlocked; },
                    _ => { if (diagnosticsThrow) throw new Exception("UI failed"); },
                    _ => { if (diagnosticsThrow) throw new Exception("log failed"); });
                int stage = failingStage;
                SessionMutationOutcome result = boundary.TryMutate(session, () => true, () =>
                {
                    for (int i = 0; i < 3; i++) { touched++; if (i == stage) throw new Exception(""); }
                }, () => { verified++; if (stage == 3) throw new Exception("whole-stack postcondition failed"); });
                assert(result == SessionMutationOutcome.Blocked && state.IsBlocked && paused == 1 && pauseObservedLatch,
                    "A throw from any native stage or postcondition blocks the session exactly once.");
                assert(touched == Math.Min(stage + 1, 3) && verified == (stage == 3 ? 1 : 0),
                    "No later native work or speculative rollback follows uncertain completion.");
                int beforeRetry = touched;
                assert(boundary.TryMutate(session, () => true, () => touched++, () => { }) == SessionMutationOutcome.Refused && touched == beforeRetry,
                    "A blocked session cannot retry a selection through the boundary.");
                AssertBlockedIdentity(state, session, assert);
            }

            foreach (bool preflightThrows in new[] { false, true })
            {
                var state = new SessionCompatibilityState();
                int nativeCalls = 0, pauseCalls = 0, presentCalls = 0;
                var boundary = new SessionFailureBoundary(state, () => { pauseCalls++; throw new Exception("pause must not run"); },
                    _ => { presentCalls++; throw new Exception("present must not run"); }, _ => throw new Exception("logging failed"));
                assert(boundary.TryMutate(new object(), () => { if (preflightThrows) throw new Exception("preflight failed"); return false; },
                    () => nativeCalls++, () => nativeCalls++) == SessionMutationOutcome.Refused,
                    "Read-only preflight refusal/failure stays a refusal.");
                assert(nativeCalls == 0 && pauseCalls == 0 && presentCalls == 0 && !state.IsBlocked, "No mutation or failure latch is invented for a read-only preflight refusal.");
            }

            var successState = new SessionCompatibilityState();
            var successOrder = new List<string>();
            var success = new SessionFailureBoundary(successState, () => successOrder.Add("pause"),
                _ => successOrder.Add("present"), _ => { successOrder.Add("log"); throw new Exception("logger failed"); });
            var outcome = success.TryMutate(new object(), () => { successOrder.Add("preflight"); return true; },
                () => successOrder.Add("native"), () => successOrder.Add("verified"));
            assert(outcome == SessionMutationOutcome.Completed && successOrder.SequenceEqual(new[] { "preflight", "native", "verified" }),
                "Successful native completion is verified before returning without any safety side effect.");
            success.BestEffort(() => { successOrder.Add("UI cleanup"); throw new Exception("button unavailable"); });
            assert(!successState.IsBlocked && !successOrder.Contains("pause"), "UI-only failure after verified completion cannot mislabel native success as a partial mutation.");
        }

        private static void AssertBlockedIdentity(SessionCompatibilityState state, object session, Action<bool, string> assert)
        {
            try { state.EnsureCanBegin(session); assert(false, "A blocked identity must fail pre-Begin validation."); }
            catch (InvalidOperationException) { assert(true, "A blocked identity fails pre-Begin validation."); }
            try { state.BeginSession(session); assert(false, "A blocked identity must not reimport itself."); }
            catch (InvalidOperationException) { assert(true, "A blocked identity cannot reimport itself."); }
        }

        private sealed class UnreadableMessageException : Exception
        {
            public override string Message => throw new InvalidOperationException("message getter failed");
        }
        private sealed class OneReadMessageException : Exception
        {
            internal int Reads;
            public override string Message => ++Reads == 1 ? "first read" : throw new Exception("second read");
        }
    }
}
