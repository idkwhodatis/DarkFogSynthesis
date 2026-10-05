using System;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>
    /// Captures the identities owned by one maintenance operation. Boundary checks detect changed
    /// state; they do not prevent foreign code from writing pause/session fields directly.
    /// </summary>
    public sealed class MaintenanceSessionGuard
    {
        private readonly object session;
        private readonly object history;
        private readonly object player;
        private readonly bool wasPaused;

        public MaintenanceSessionGuard(object session, object history, object player, bool wasPaused)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.history = history ?? throw new ArgumentNullException(nameof(history));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.wasPaused = wasPaused;
        }

        /// <summary>Native Begin may resume before persistence validation, except during maintenance.</summary>
        public static bool AllowsResume(bool resumeReady, bool cleaning, bool quarantined) =>
            resumeReady && !cleaning && !quarantined;

        /// <summary>Only a completed replacement outside an active cleanup may release quarantine.</summary>
        public static bool CanReleaseQuarantineAfterValidation(bool cleaning, object? quarantinedSession, object? validatedSession) =>
            !cleaning && quarantinedSession != null && validatedSession != null &&
            !ReferenceEquals(quarantinedSession, validatedSession);

        public void EnsurePausedSession(object? currentSession, object? currentHistory, object? currentPlayer,
            bool isPaused, bool isLoading, bool persistenceReady)
        {
            if (!Matches(currentSession, currentHistory, currentPlayer) || !isPaused || isLoading || !persistenceReady)
                throw new InvalidOperationException("The captured maintenance session is no longer current, paused, and validated. Refusing further cleanup.");
        }

        /// <summary>Call only after clearing maintenance, and only when no cleanup mutation began.</summary>
        public bool CanRestoreRunningSession(object? currentSession, object? currentHistory, object? currentPlayer,
            bool isLoading, bool persistenceReady) =>
            !wasPaused && !isLoading && persistenceReady && Matches(currentSession, currentHistory, currentPlayer);

        private bool Matches(object? currentSession, object? currentHistory, object? currentPlayer) =>
            ReferenceEquals(session, currentSession) && ReferenceEquals(history, currentHistory) &&
            ReferenceEquals(player, currentPlayer);
    }
}
