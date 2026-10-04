using System;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class SessionPersistenceTests
    {
        internal static void Run(Action<bool, string> assert)
        {
            var state = new SessionCompatibilityState();
            object first = new object(), second = new object(), third = new object();
            assert(!state.CanPersist(null) && !state.CanPersist(first), "No session is save-qualified at startup.");
            state.BeginSession(first);
            assert(!state.IsBlocked && !state.CanPersist(first), "A pending first load is not a failure, but must not write.");
            state.CompleteValidatedSession(first);
            assert(state.CanPersist(first) && !state.CanPersist(second), "Only the exact validated live identity may write.");
            object ticket = state.BeginValidation(first);
            assert(!state.CanPersist(first), "Repeated native Begin reopens pending validation instead of reusing stale success.");
            try { state.BeginValidation(first); assert(false, "Nested Begin must be refused."); }
            catch (InvalidOperationException) { assert(true, "Nested Begin cannot acquire another completion ticket."); }
            state.EndValidation(null);
            state.EndValidation(new object());
            state.CompleteValidatedSession(first);
            assert(!state.CanPersist(first), "Completion cannot open persistence while its owning Begin pipeline is active.");
            state.EndValidation(ticket);
            assert(state.CanPersist(first), "The matching invocation releases its barrier only after completion.");
            state.BeginSession(second);
            state.EndSession(second);
            assert(!state.CanPersist(first) && !state.CanPersist(second), "Abandoning a replacement cannot resurrect old validation.");
            state.BeginSession(first);
            state.CompleteValidatedSession(first);
            state.BeginSession(second);
            assert(!state.CanPersist(first) && !state.CanPersist(second), "Replacement preparation closes writes for both old and pending identities.");
            state.EndSession(first);
            assert(!state.CanPersist(second), "Old teardown cannot validate the pending replacement.");
            state.CompleteValidatedSession(second);
            assert(state.CanPersist(second) && !state.CanPersist(first), "Successful replacement qualifies only its own identity.");
            state.BlockSession(second, "late conflict");
            assert(!state.CanPersist(second), "Late failure closes persistence after an earlier successful validation.");
            state.BeginSession(third);
            assert(!state.CanPersist(third), "Preparing recovery does not release the failure latch.");
            state.CompleteValidatedSession(third);
            assert(state.CanPersist(third), "A different fully validated recovery reopens persistence.");
            state.EndSession(third);
            assert(!state.CanPersist(third), "A destroyed session has no persistence qualification.");

            foreach (bool ready in new[] { false, true })
            foreach (bool restricted in new[] { false, true })
            foreach (bool named in new[] { false, true })
            foreach (string? requested in new[] { null, "backup", "Backup", "another" })
            foreach (string? permitted in new[] { null, "backup" })
            {
                bool expected = ready && (!restricted || (named && requested == "backup" && permitted == "backup"));
                assert(SessionPersistencePolicy.AllowsWrite(ready, restricted, named, requested, permitted) == expected,
                    "Maintenance authorization is exact-name/method scoped and never bypasses startup or pending-session refusal.");
            }

            foreach (bool ran in new[] { false, true })
            foreach (bool nativeFailed in new[] { false, true })
            {
                var nativeError = nativeFailed ? new InvalidOperationException("native failure") : null;
                int validations = 0, completions = 0, aborts = 0;
                Exception? captured = null;
                Exception? result = SessionBeginCompletion.Finish(ran, nativeError,
                    () => { validations++; return true; }, () => completions++, error => { aborts++; captured = error; });
                bool success = ran && !nativeFailed;
                assert(validations == (success ? 1 : 0) && completions == (success ? 1 : 0) && aborts == (success ? 0 : 1),
                    "Skipped or failed native Begin cannot reach late validation or completion.");
                assert(success ? result == null : result != null && ReferenceEquals(result, captured), "Failure passes the same exception to containment and Harmony.");
                if (nativeError != null) assert(ReferenceEquals(result, nativeError), "Original native errors are never replaced by the cancellation explanation.");
            }
            int completed = 0, aborted = 0;
            assert(SessionBeginCompletion.Finish(true, null, () => false, () => completed++, _ => aborted++) == null && completed == 0 && aborted == 0,
                "An already-latched late validator refusal cannot promote a session.");
            var lateError = new Exception("late validation failed");
            Exception? seen = null;
            assert(ReferenceEquals(SessionBeginCompletion.Finish(true, null, () => throw lateError, () => completed++, error => seen = error), lateError) &&
                ReferenceEquals(seen, lateError) && completed == 0, "Thrown late checks route through containment without completion.");
        }
    }
}
