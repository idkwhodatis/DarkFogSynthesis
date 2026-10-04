using System;
using System.IO;
using System.Security.Cryptography;
using DarkFogSynthesis.Core.Compatibility;
using DarkFogSynthesis.Core.Registration;

namespace DarkFogSynthesis.Core.Tests
{
    /// <summary>Exercises the actual pure boundaries used by Plugin; no substitute game types.</summary>
    internal static class SessionLifecycleTests
    {
        internal static void Run(Action<bool, string> assert)
        {
            DiagnosticFailuresDoNotFailRegistration(assert);
            RegistrationFailureStillFailsClosed(assert);
            SessionBlocksSurviveUntilNewValidatedSession(assert);
            SessionIdentityIsReferenceBased(assert);
            DiagnosticsDoNotClearExistingBlocks(assert);
            InvalidArguments(assert);
        }

        private static void DiagnosticFailuresDoNotFailRegistration(Action<bool, string> assert)
        {
            foreach (Exception failure in new Exception[] {
                new IOException("diagnostic directory is read-only"),
                new UnauthorizedAccessException("assembly file cannot be read"),
                new CryptographicException("assembly hash failed") })
            {
                bool ready = false;
                bool fatal = false;
                int exports = 0;
                int warnings = 0;
                RegistrationLifecycle.BindAndDiagnose(() => ready = true, _ => fatal = true,
                    () => { exports++; throw failure; }, error => {
                        warnings++;
                        assert(ReferenceEquals(error, failure), "The diagnostic warning retains the original exception.");
                    });
                assert(ready && !fatal, "Diagnostic I/O/hash failure preserves successful registration readiness.");
                assert(exports == 1 && warnings == 1, "Failed automatic diagnostics produce one warning, with no retry.");
            }

            int successfulExports = 0;
            int unexpectedFailures = 0;
            RegistrationLifecycle.BindAndDiagnose(() => { }, _ => unexpectedFailures++,
                () => successfulExports++, _ => unexpectedFailures++);
            assert(successfulExports == 1 && unexpectedFailures == 0, "A successful optional diagnostic runs once.");

            bool blocked = false;
            var warningFailure = new InvalidOperationException("logger unavailable");
            Reject(() => RegistrationLifecycle.BindAndDiagnose(() => { }, _ => blocked = true,
                () => throw new IOException("export failed"), _ => throw warningFailure), assert,
                "Even failure of the warning sink stays outside the fatal registration catch.", warningFailure);
            assert(!blocked, "A broken diagnostic warning sink cannot invoke the registration-failure callback.");
        }

        private static void RegistrationFailureStillFailsClosed(Action<bool, string> assert)
        {
            var failure = new InvalidOperationException("prototype identity changed");
            bool ready = false;
            int fatalCalls = 0;
            int diagnostics = 0;
            Reject(() => RegistrationLifecycle.BindAndDiagnose(() => throw failure,
                error => {
                    fatalCalls++;
                    assert(ReferenceEquals(error, failure), "Binding reports the original registration error.");
                }, () => { diagnostics++; ready = true; }, _ => diagnostics++), assert,
                "Binding failures still propagate to the loader.", failure);
            assert(!ready && fatalCalls == 1 && diagnostics == 0,
                "Failed registration remains blocked and never starts optional diagnostics.");
        }

        private static void SessionBlocksSurviveUntilNewValidatedSession(Action<bool, string> assert)
        {
            var state = new SessionCompatibilityState();
            var first = new object();
            var second = new object();
            var third = new object();
            assert(!state.IsBlocked && state.BlockReason == null, "No compatibility failure is invented before a session.");
            state.BeginSession(first);
            state.CompleteValidatedSession(first);
            state.CompleteValidatedSession(first);
            assert(!state.IsBlocked, "Repeated Begin on a valid session is idempotent.");

            state.BlockSession(first, "Required foreign technology is disabled.");
            assert(state.IsBlocked, "A late failure immediately blocks readiness, resume and save permission.");
            assert(state.BlockReason == "Required foreign technology is disabled.", "The blocking reason is retained independently of diagnostics status.");
            Reject(() => state.EnsureCanBegin(first), assert, "Blocked same-object Begin is rejected before native initialization side effects.");
            Reject(() => state.CompleteValidatedSession(first), assert, "Repeated Begin cannot clear the same session's failure.");
            state.EndSession(first);
            assert(state.IsBlocked, "Destroying the blocked session does not clear the latch.");
            Reject(() => state.BeginSession(first), assert, "Reimporting the same blocked identity cannot clear the latch.");
            Reject(() => state.CompleteValidatedSession(second), assert, "A new identity without preparation cannot clear the latch.");

            state.BeginSession(second);
            assert(state.IsBlocked, "Starting a replacement is not evidence that it is valid.");
            state.EndSession(second);
            assert(state.IsBlocked, "A failed/abandoned replacement leaves the previous failure latched.");
            Reject(() => state.CompleteValidatedSession(second), assert, "An abandoned replacement cannot complete without a new initialization.");

            state.BeginSession(second);
            state.BlockSession(second, "Late native matrix validation failed.");
            state.EndSession(second);
            Reject(() => state.BeginSession(second), assert, "A late-failed replacement cannot clear its own failure.");
            Reject(() => state.BeginSession(first), assert, "An earlier failed identity remains blocked after a later failure.");
            assert(state.IsBlocked && state.BlockReason == "Late native matrix validation failed.", "The latest useful failure stays visible.");

            state.BeginSession(third);
            state.EnsureCanBegin(third);
            assert(state.IsBlocked, "Permission to begin a prepared replacement does not prematurely clear the latch.");
            state.EndSession(first);
            assert(state.IsBlocked, "Destroying an old identity cannot validate the pending replacement.");
            Reject(() => state.CompleteValidatedSession(new object()), assert, "A mismatched completion cannot clear the pending replacement's latch.");
            state.CompleteValidatedSession(third);
            assert(!state.IsBlocked && state.BlockReason == null, "Only a different fully validated session releases the latch.");
            state.CompleteValidatedSession(third);
            state.EndSession(first);
            assert(!state.IsBlocked, "Old teardown and repeated valid Begin do not affect the replacement.");

            state.BlockSession(null, "Session initialization failed before an identity was available.");
            assert(state.IsBlocked, "Unknown-session initialization failures fail closed.");
            Reject(() => state.CompleteValidatedSession(third), assert, "An already validated identity cannot erase an unknown-session failure.");
            Reject(() => state.BeginSession(third), assert, "Reinitializing the old validated identity cannot erase an unknown-session failure.");
            var fourth = new object();
            state.BeginSession(fourth);
            state.CompleteValidatedSession(fourth);
            assert(!state.IsBlocked, "A new validated session can recover from an identity-less failure.");

            var failedBeforeImport = new object();
            state.BlockSession(failedBeforeImport, "Peer check rejected session entry before import began.");
            assert(state.IsBlocked, "Entry-time validation failure latches even before preparation starts.");
            Reject(() => state.BeginSession(failedBeforeImport), assert, "A rejected entry identity cannot retry itself into readiness.");
            Reject(() => state.BeginSession(fourth), assert, "A previous running identity cannot clear the failure of a replacement.");
            var replacement = new object();
            state.BeginSession(replacement);
            state.CompleteValidatedSession(replacement);
            assert(!state.IsBlocked, "Entry-time failure still permits a different valid replacement.");
        }

        private static void SessionIdentityIsReferenceBased(Action<bool, string> assert)
        {
            var state = new SessionCompatibilityState();
            var first = new EqualIdentity();
            var second = new EqualIdentity();
            assert(first.Equals(second), "The test identities deliberately compare equal by value.");
            state.BeginSession(first);
            state.BlockSession(first, "First object failed.");
            state.EndSession(first);
            state.BeginSession(second);
            state.CompleteValidatedSession(second);
            assert(!state.IsBlocked, "Distinct session references stay distinct despite overridden value equality.");
        }

        private static void DiagnosticsDoNotClearExistingBlocks(Action<bool, string> assert)
        {
            var state = new SessionCompatibilityState();
            var session = new object();
            state.BeginSession(session);
            state.BlockSession(session, "Foreign disabled flags are unchanged.");
            string reason = state.BlockReason!;
            bool registered = true;
            bool fatal = true;
            foreach (bool failExport in new[] { false, true })
            {
                RegistrationLifecycle.BindAndDiagnose(() => registered = true, _ => fatal = true,
                    () => { if (failExport) throw new IOException("read-only diagnostics path"); }, _ => { });
                assert(registered && fatal, "Diagnostic success/failure cannot erase a preexisting fatal registration state.");
                assert(state.IsBlocked && state.BlockReason == reason,
                    "Diagnostic success/failure cannot erase a session block or its persistent reason.");
            }
        }

        private static void InvalidArguments(Action<bool, string> assert)
        {
            var state = new SessionCompatibilityState();
            Reject(() => state.BeginSession(null!), assert, "Null preparation is rejected.", expectedType: typeof(ArgumentNullException));
            Reject(() => state.EnsureCanBegin(null!), assert, "Null pre-Begin validation is rejected.", expectedType: typeof(ArgumentNullException));
            Reject(() => state.EndSession(null!), assert, "Null teardown is rejected.", expectedType: typeof(ArgumentNullException));
            Reject(() => state.CompleteValidatedSession(null!), assert, "Null completion is rejected.", expectedType: typeof(ArgumentNullException));
            foreach (string? reason in new[] { null, "", " " })
                Reject(() => state.BlockSession(new object(), reason!), assert, "An invisible failure reason is rejected.", expectedType: typeof(ArgumentException));
            assert(!state.IsBlocked, "Invalid failure arguments do not modify state.");
            Action noop = () => { };
            Action<Exception> ignore = _ => { };
            Reject(() => RegistrationLifecycle.BindAndDiagnose(null!, ignore, noop, ignore), assert, "Null bind is rejected.", expectedType: typeof(ArgumentNullException));
            Reject(() => RegistrationLifecycle.BindAndDiagnose(noop, null!, noop, ignore), assert, "Null binding failure callback is rejected.", expectedType: typeof(ArgumentNullException));
            Reject(() => RegistrationLifecycle.BindAndDiagnose(noop, ignore, null!, ignore), assert, "Null diagnostics callback is rejected.", expectedType: typeof(ArgumentNullException));
            Reject(() => RegistrationLifecycle.BindAndDiagnose(noop, ignore, noop, null!), assert, "Null warning callback is rejected.", expectedType: typeof(ArgumentNullException));
        }

        private sealed class EqualIdentity
        {
            public override bool Equals(object? other) => other is EqualIdentity;
            public override int GetHashCode() => 1;
        }

        private static void Reject(Action action, Action<bool, string> assert, string message,
            Exception? expected = null, Type? expectedType = null)
        {
            try { action(); }
            catch (Exception error)
            {
                assert(error.GetType() == (expectedType ?? typeof(InvalidOperationException)) &&
                    (expected == null || ReferenceEquals(error, expected)), message);
                return;
            }
            assert(false, message);
        }
    }
}
