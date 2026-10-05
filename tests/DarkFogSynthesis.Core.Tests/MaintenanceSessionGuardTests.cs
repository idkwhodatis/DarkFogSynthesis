using System;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class MaintenanceSessionGuardTests
    {
        internal static void Run(Action<bool, string> assert)
        {
            foreach (bool ready in new[] { false, true })
            foreach (bool cleaning in new[] { false, true })
            foreach (bool quarantined in new[] { false, true })
                assert(MaintenanceSessionGuard.AllowsResume(ready, cleaning, quarantined) ==
                    (ready && !cleaning && !quarantined),
                    "Maintenance blocks Resume before quarantine; startup/compatibility refusal remains independent.");

            // These identities deliberately compare equal by value: a different instance must still fail.
            object session = new EqualByValue(), history = new EqualByValue(), player = new EqualByValue();
            object replacement = new EqualByValue();
            assert(!MaintenanceSessionGuard.CanReleaseQuarantineAfterValidation(true, session, replacement),
                "A validated replacement callback cannot clear quarantine while cleanup still owns it.");
            assert(!MaintenanceSessionGuard.CanReleaseQuarantineAfterValidation(false, session, session) &&
                !MaintenanceSessionGuard.CanReleaseQuarantineAfterValidation(false, session, null) &&
                !MaintenanceSessionGuard.CanReleaseQuarantineAfterValidation(false, null, replacement),
                "Only a different nonnull validated session can release an existing quarantine.");
            assert(MaintenanceSessionGuard.CanReleaseQuarantineAfterValidation(false, session, replacement),
                "A later validated replacement still releases quarantine after maintenance ends.");
            foreach (bool previouslyPaused in new[] { false, true })
            foreach (object? currentSession in new[] { null, session, replacement })
            foreach (object? currentHistory in new[] { null, history, replacement })
            foreach (object? currentPlayer in new[] { null, player, replacement })
            foreach (bool paused in new[] { false, true })
            foreach (bool loading in new[] { false, true })
            foreach (bool validated in new[] { false, true })
            {
                var guard = new MaintenanceSessionGuard(session, history, player, previouslyPaused);
                bool same = ReferenceEquals(currentSession, session) && ReferenceEquals(currentHistory, history) &&
                    ReferenceEquals(currentPlayer, player);
                bool refused = false;
                try { guard.EnsurePausedSession(currentSession, currentHistory, currentPlayer, paused, loading, validated); }
                catch (InvalidOperationException) { refused = true; }
                assert(refused == !(same && paused && !loading && validated),
                    "Cleanup requires the captured session/history/player, pause, and completed validation after callbacks.");
                assert(guard.CanRestoreRunningSession(currentSession, currentHistory, currentPlayer, loading, validated) ==
                    (!previouslyPaused && same && !loading && validated),
                    "Failed preflight restores only its originally running, still-current validated session.");
            }

            var pending = new SessionCompatibilityState();
            pending.BeginSession(session);
            assert(!pending.CanPersist(session) && MaintenanceSessionGuard.AllowsResume(!pending.IsBlocked, false, false),
                "Ordinary native Begin can Resume while persistence validation is pending.");
            assert(!MaintenanceSessionGuard.AllowsResume(!pending.IsBlocked, true, false),
                "The independent maintenance barrier still blocks that Resume before quarantine.");
            pending.CompleteValidatedSession(session);
            var captured = new MaintenanceSessionGuard(session, history, player, false);
            pending.BeginSession(replacement);
            assert(!captured.CanRestoreRunningSession(session, history, player, false, pending.CanPersist(session)),
                "A pending replacement cannot cause failed cleanup to resume even the old captured identity.");
        }

        private sealed class EqualByValue
        {
            public override bool Equals(object? other) => other is EqualByValue;
            public override int GetHashCode() => 0;
        }
    }
}
