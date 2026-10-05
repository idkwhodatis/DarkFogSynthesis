using System;
using System.Threading;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class MutationPersistenceTests
    {
        internal static void Run(Action<bool, string> check)
        {
            foreach (int failure in new[] { -1, 0, 1 })
            {
                var state = Ready(out object session);
                int mutate = 0, verify = 0, reports = 0;
                bool containmentClosed = true;
                var boundary = new SessionFailureBoundary(state,
                    () => containmentClosed &= !state.CanPersist(session),
                    _ => containmentClosed &= state.IsBlocked && !state.CanPersist(session),
                    _ => { reports++; containmentClosed &= !state.CanPersist(session); });
                var result = boundary.TryMutate(session, () => true, () =>
                {
                    mutate++;
                    check(!state.CanPersist(session) && state.TryBeginPersistence(session) == null,
                        "Mutation callback must not enter persistence");
                    check(boundary.TryMutate(session, () => true, () => mutate++, () => { }) == SessionMutationOutcome.Refused,
                        "Nested mutation must be refused without releasing its caller");
                    check(!state.CanPersist(session), "Nested refusal released the outer scope");
                    if (failure == 0) throw new Exception("native failure");
                }, () =>
                {
                    verify++;
                    check(!state.CanPersist(session) && state.TryBeginPersistence(session) == null,
                        "Verification callback must not enter persistence");
                    if (failure == 1) throw new Exception("verification failure");
                });
                check(mutate == 1 && verify == (failure == 0 ? 0 : 1), "Unexpected native/verify calls");
                check(containmentClosed && result == (failure < 0 ? SessionMutationOutcome.Completed : SessionMutationOutcome.Blocked),
                    "Failure containment must precede temporary lease release");
                check(state.CanPersist(session) == (failure < 0) && state.IsBlocked == (failure >= 0),
                    "Successful mutation must reopen persistence, failed mutation must retain its block");
                check(reports == (failure < 0 ? 0 : 1), "Unexpected reporting side effect");
                if (failure >= 0)
                {
                    var replacement = new object(); state.BeginSession(replacement); state.CompleteValidatedSession(replacement);
                    using (var write = state.TryBeginPersistence(replacement)) check(write != null, "Fresh validated SP recovery remained blocked");
                }
            }

            var s = Ready(out object id);
            var b = new SessionFailureBoundary(s, () => { }, _ => { }, _ => { });
            int changed = 0;
            var outer = s.TryBeginPersistence(id);
            var inner = s.TryBeginPersistence(id);
            check(outer != null && inner != null, "Nested ordinary saves must remain permitted");
            if (outer == null || inner == null) throw new InvalidOperationException("Missing nested save leases");
            inner.Dispose(); inner.Dispose();
            check(b.TryMutate(id, () => true, () => changed++, () => { }) == SessionMutationOutcome.Refused && changed == 0,
                "A nested write released its parent's mutation exclusion");
            outer.Dispose(); outer.Dispose();
            check(b.TryMutate(id, () => true, () => changed++, () => { }) == SessionMutationOutcome.Completed && changed == 1,
                "Write lease leaked or successful SP mutation was refused");

            foreach (bool throws in new[] { false, true })
            {
                var result = b.TryMutate(id, () => { if (throws) throw new Exception("preflight"); return false; }, () => changed++, () => changed++);
                check(result == SessionMutationOutcome.Refused && changed == 1 && !s.IsBlocked && s.CanPersist(id),
                    "Read-only refusal must not alter single-player state");
            }
            check(b.TryMutate(new object(), () => true, () => changed++, () => { }) == SessionMutationOutcome.Refused && changed == 1,
                "Mutation admitted an unvalidated session identity");
            check(b.TryMutate(id, () => { s.EndSession(id); return true; }, () => changed++, () => { }) == SessionMutationOutcome.Refused && changed == 1,
                "Preflight lost the session but mutation was still admitted");

            foreach (bool endSession in new[] { false, true })
            {
                var state = Ready(out object session);
                var boundary = new SessionFailureBoundary(state, () => { }, _ => { }, _ => { });
                var result = boundary.TryMutate(session, () => true, () =>
                {
                    if (endSession) state.EndSession(session);
                    else state.BlockSession(session, "callback rejection");
                }, () => { });
                check(result == SessionMutationOutcome.Blocked && !state.CanPersist(session),
                    "A callback invalidated the session but mutation reported success");
            }
            for (int i = 0; i < 32; i++) ConcurrentAdmissions(check);
        }

        private static SessionCompatibilityState Ready(out object session)
        {
            var state = new SessionCompatibilityState(); session = new object();
            state.BeginSession(session); state.CompleteValidatedSession(session); return state;
        }

        private static void ConcurrentAdmissions(Action<bool, string> check)
        {
            var state = Ready(out object session);
            var boundary = new SessionFailureBoundary(state, () => { }, _ => { }, _ => { });
            using var start = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            using var attempted = new CountdownEvent(2);
            bool wrote = false, mutated = false;
            Exception? writerError = null, mutationError = null;
            var writer = new Thread(() =>
            {
                try
                {
                    start.Wait();
                    using var write = state.TryBeginPersistence(session);
                    wrote = write != null;
                    attempted.Signal(); release.Wait();
                }
                catch (Exception error) { writerError = error; }
            });
            var mutator = new Thread(() =>
            {
                try
                {
                    start.Wait();
                    bool entered = false;
                    boundary.TryMutate(session, () => true, () =>
                    {
                        entered = mutated = true; attempted.Signal(); release.Wait();
                    }, () => { });
                    if (!entered) attempted.Signal();
                }
                catch (Exception error) { mutationError = error; }
            });
            writer.Start(); mutator.Start(); start.Set();
            bool finished = attempted.Wait(TimeSpan.FromSeconds(10));
            release.Set(); writer.Join(); mutator.Join();
            check(finished && writerError == null && mutationError == null, "Concurrent admission hung or threw");
            check(wrote != mutated, "Save and mutation must never both be admitted at once");
            check(state.CanPersist(session) && !state.IsBlocked, "Concurrent refusal leaked a lease or blocked a healthy session");
            using var control = state.TryBeginPersistence(session);
            check(control != null, "Successful concurrent completion did not reopen ordinary persistence");
        }
    }
}
