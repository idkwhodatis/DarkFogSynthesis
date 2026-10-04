using System;

namespace DarkFogSynthesis.Core.Compatibility
{
    public enum SessionMutationOutcome { Refused, Completed, Blocked }

    /// <summary>
    /// Shared production boundary for native mutation and session-abort failures. The latch is
    /// committed before optional pause/UI/logging callbacks. It never invents a native rollback.
    /// </summary>
    public sealed class SessionFailureBoundary
    {
        private readonly SessionCompatibilityState state;
        private readonly Action pause;
        private readonly Action<string> present;
        private readonly Action<Exception> report;

        public SessionFailureBoundary(SessionCompatibilityState state, Action pause,
            Action<string> present, Action<Exception> report)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.pause = pause ?? throw new ArgumentNullException(nameof(pause));
            this.present = present ?? throw new ArgumentNullException(nameof(present));
            this.report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public static string FailureReason(Exception? error)
        {
            // Exception.Message is virtual: a diagnostic getter is not a prerequisite to safety.
            try
            {
                string? message = error?.Message;
                if (!string.IsNullOrWhiteSpace(message)) return message!;
            }
            catch (Exception) { }
            return "Session compatibility failed (" + (error?.GetType().FullName ?? "unknown error") +
                "). Load a different validated session before continuing.";
        }

        public void Reject(object? session, Exception error)
        {
            string reason = FailureReason(error);
            state.BlockSession(session, reason);
            BestEffort(pause);
            BestEffort(() => present(reason));
            Report(error);
        }

        public void Abort(object? session, Exception error, Action restore, Action cleanup)
        {
            try { Reject(session, error); }
            finally
            {
                try { BestEffort(restore); }
                finally { BestEffort(cleanup); }
            }
        }

        public SessionMutationOutcome TryMutate(object? session, Func<bool> preflight,
            Action mutate, Action verify)
        {
            if (preflight == null) throw new ArgumentNullException(nameof(preflight));
            if (mutate == null) throw new ArgumentNullException(nameof(mutate));
            if (verify == null) throw new ArgumentNullException(nameof(verify));
            if (state.IsBlocked) return SessionMutationOutcome.Refused;
            // Preflight must be read-only. A refusal/failure here has made no native mutation.
            try { if (!preflight()) return SessionMutationOutcome.Refused; }
            catch (Exception error) { Report(error); return SessionMutationOutcome.Refused; }
            try
            {
                // Even the first native call may throw after changing state. Completion is unknown
                // until every native call and the whole-stack postcondition have returned normally.
                mutate();
                verify();
                return SessionMutationOutcome.Completed;
            }
            catch (Exception error)
            {
                Reject(session, error);
                return SessionMutationOutcome.Blocked;
            }
        }

        public void BestEffort(Action action)
        {
            try { action(); }
            catch (Exception error) { Report(error); }
        }

        private void Report(Exception error)
        {
            try { report(error); }
            catch (Exception) { } // A broken diagnostic sink must not interrupt containment/cleanup.
        }
    }
}
