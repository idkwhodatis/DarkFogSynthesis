using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>
    /// A compatibility failure belongs to an actual session, not to registered content. It remains
    /// latched through teardown and failed replacements; only a different, fully validated session
    /// may clear it. Session identities use reference equality, never a game's value equality.
    /// </summary>
    public sealed class SessionCompatibilityState
    {
        private readonly HashSet<object> blockedSessions = new HashSet<object>(SessionIdentityComparer.Instance);
        private object? pendingSession;
        private object? validatedSession;
        private object? activeBeginToken;

        public string? BlockReason { get; private set; }
        public bool IsBlocked => BlockReason != null;

        /// <summary>Ordinary persistence requires completed validation of this exact live identity.</summary>
        public bool CanPersist(object? session) => session != null && !IsBlocked && pendingSession == null && activeBeginToken == null &&
            ReferenceEquals(validatedSession, session);

        public void BeginSession(object session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (activeBeginToken != null)
                throw new InvalidOperationException("Nested session initialization during native Begin validation is unsupported.");
            if (blockedSessions.Contains(session))
                throw new InvalidOperationException("This session is compatibility-blocked. Load a different valid session before continuing. " + BlockReason);
            pendingSession = session;
        }

        /// <summary>Start one native Begin pipeline; repeated calls must revalidate, nested calls fail closed.</summary>
        public object BeginValidation(object session)
        {
            EnsureCanBegin(session);
            if (activeBeginToken != null)
                throw new InvalidOperationException("Nested native Begin validation is unsupported.");
            pendingSession = session;
            return activeBeginToken = new object();
        }

        /// <summary>Only the owning invocation may release its in-flight barrier.</summary>
        public void EndValidation(object? token)
        {
            if (token != null && ReferenceEquals(activeBeginToken, token)) activeBeginToken = null;
        }

        /// <summary>Reject stale/blocked Begin calls before native initialization side effects.</summary>
        public void EnsureCanBegin(object session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (blockedSessions.Contains(session))
                throw new InvalidOperationException("A blocked session cannot clear its own compatibility failure. " + BlockReason);
            if (!ReferenceEquals(pendingSession, session) &&
                (IsBlocked || !ReferenceEquals(validatedSession, session)))
                throw new InvalidOperationException("The validated session was not prepared by the session lifecycle.");
        }

        /// <summary>Call only after native Begin and all compatibility checks succeeded.</summary>
        public void CompleteValidatedSession(object session)
        {
            EnsureCanBegin(session);
            // Repeated Begin on an already valid session is harmless, but never clears a latch.
            if (!ReferenceEquals(pendingSession, session)) return;
            pendingSession = null;
            validatedSession = session;
            blockedSessions.Clear();
            BlockReason = null;
        }

        public void BlockSession(object? session, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A compatibility failure must have a reason.", nameof(reason));
            if (session != null) blockedSessions.Add(session);
            // Neither the previously running identity nor an in-flight replacement is a genuinely
            // new session after this failure, including failures before the caller knows an owner.
            if (validatedSession != null) blockedSessions.Add(validatedSession);
            if (pendingSession != null) blockedSessions.Add(pendingSession);
            BlockReason = reason;
            validatedSession = null;
        }

        /// <summary>Destroying a blocked session is not evidence that its replacement is safe.</summary>
        public void EndSession(object session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (ReferenceEquals(pendingSession, session))
            {
                pendingSession = null;
                // Abandoning a replacement must not resurrect the previous validation after
                // shared progression may already have been restored/changed for the new load.
                validatedSession = null;
            }
            if (ReferenceEquals(validatedSession, session)) validatedSession = null;
        }

        private sealed class SessionIdentityComparer : IEqualityComparer<object>
        {
            internal static readonly SessionIdentityComparer Instance = new SessionIdentityComparer();
            public new bool Equals(object? left, object? right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
