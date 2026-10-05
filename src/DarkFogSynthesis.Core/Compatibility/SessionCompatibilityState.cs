using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>
    /// A compatibility failure belongs to an actual session, not to registered content. It remains
    /// latched through teardown and failed replacements; only a different, fully validated session
    /// may clear it. Session identities use reference equality, never a game's value equality.
    /// </summary>
    public sealed class SessionCompatibilityState
    {
        // Admission only: never hold this lock while calling native code or user callbacks.
        private readonly object gate = new object();
        private readonly HashSet<object> blockedSessions = new HashSet<object>(SessionIdentityComparer.Instance);
        private object? pendingSession;
        private object? validatedSession;
        private object? activeBeginToken;
        private OperationLease? activeMutation;
        private int activeWrites;
        private string? blockReason;

        public string? BlockReason { get { lock (gate) return blockReason; } }
        public bool IsBlocked { get { lock (gate) return blockReason != null; } }

        private bool IsValidated(object? session) => session != null && blockReason == null &&
            pendingSession == null && activeBeginToken == null && ReferenceEquals(validatedSession, session);

        /// <summary>Ordinary persistence also waits for this session's native mutation to be verified.</summary>
        public bool CanPersist(object? session)
        {
            lock (gate) return IsValidated(session) && activeMutation == null;
        }

        /// <summary>
        /// Atomically admit a write or refuse it. Other startup/maintenance/peer checks remain the
        /// caller's responsibility. Nested native saves are allowed; every invocation owns a lease.
        /// </summary>
        public IDisposable? TryBeginPersistence(object? session)
        {
            lock (gate)
            {
                if (!IsValidated(session) || activeMutation != null) return null;
                var lease = new OperationLease(this, false);
                activeWrites++;
                return lease;
            }
        }

        /// <summary>Never wait for an admitted save: refuse selection and leave the healthy session intact.</summary>
        internal IDisposable? TryBeginMutation(object? session)
        {
            lock (gate)
            {
                if (!IsValidated(session) || activeMutation != null || activeWrites != 0) return null;
                return activeMutation = new OperationLease(this, true);
            }
        }

        internal void EnsureMutationCanComplete(object? session, IDisposable lease)
        {
            lock (gate)
                if (!ReferenceEquals(activeMutation, lease) || !IsValidated(session))
                    throw new InvalidOperationException("The native mutation lost its validated session or acquired a compatibility failure.");
        }

        private void RequireNoMutation()
        {
            if (activeMutation != null)
                throw new InvalidOperationException("Session initialization cannot run during an unverified native mutation.");
        }

        public void BeginSession(object session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            lock (gate)
            {
                RequireNoMutation();
                if (activeBeginToken != null)
                    throw new InvalidOperationException("Nested session initialization during native Begin validation is unsupported.");
                if (blockedSessions.Contains(session))
                    throw new InvalidOperationException("This session is compatibility-blocked. Load a different valid session before continuing. " + blockReason);
                pendingSession = session;
            }
        }

        /// <summary>Start one native Begin pipeline; repeated calls must revalidate, nested calls fail closed.</summary>
        public object BeginValidation(object session)
        {
            lock (gate)
            {
                EnsureCanBegin(session);
                if (activeBeginToken != null)
                    throw new InvalidOperationException("Nested native Begin validation is unsupported.");
                pendingSession = session;
                return activeBeginToken = new object();
            }
        }

        /// <summary>Only the owning invocation may release its in-flight barrier.</summary>
        public void EndValidation(object? token)
        {
            lock (gate)
                if (token != null && ReferenceEquals(activeBeginToken, token)) activeBeginToken = null;
        }

        /// <summary>Reject stale/blocked Begin calls before native initialization side effects.</summary>
        public void EnsureCanBegin(object session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            lock (gate)
            {
                RequireNoMutation();
                if (blockedSessions.Contains(session))
                    throw new InvalidOperationException("A blocked session cannot clear its own compatibility failure. " + blockReason);
                if (!ReferenceEquals(pendingSession, session) &&
                    (blockReason != null || !ReferenceEquals(validatedSession, session)))
                    throw new InvalidOperationException("The validated session was not prepared by the session lifecycle.");
            }
        }

        /// <summary>Call only after native Begin and all compatibility checks succeeded.</summary>
        public void CompleteValidatedSession(object session)
        {
            lock (gate)
            {
                EnsureCanBegin(session);
                if (!ReferenceEquals(pendingSession, session)) return;
                pendingSession = null;
                validatedSession = session;
                blockedSessions.Clear();
                blockReason = null;
            }
        }

        public void BlockSession(object? session, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A compatibility failure must have a reason.", nameof(reason));
            lock (gate)
            {
                if (session != null) blockedSessions.Add(session);
                if (validatedSession != null) blockedSessions.Add(validatedSession);
                if (pendingSession != null) blockedSessions.Add(pendingSession);
                blockReason = reason;
                validatedSession = null;
            }
        }

        /// <summary>Destroying a blocked session is not evidence that its replacement is safe.</summary>
        public void EndSession(object session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            lock (gate)
            {
                if (ReferenceEquals(pendingSession, session))
                {
                    pendingSession = null;
                    validatedSession = null;
                }
                if (ReferenceEquals(validatedSession, session)) validatedSession = null;
                // Never release someone else's operation lease on teardown or a failed replacement.
            }
        }

        private sealed class OperationLease : IDisposable
        {
            private SessionCompatibilityState? owner;
            private readonly bool mutation;
            internal OperationLease(SessionCompatibilityState owner, bool mutation)
            { this.owner = owner; this.mutation = mutation; }

            public void Dispose()
            {
                var state = Interlocked.Exchange(ref owner, null);
                if (state == null) return;
                lock (state.gate)
                {
                    if (mutation)
                    {
                        if (ReferenceEquals(state.activeMutation, this)) state.activeMutation = null;
                    }
                    else state.activeWrites--;
                }
            }
        }

        private sealed class SessionIdentityComparer : IEqualityComparer<object>
        {
            internal static readonly SessionIdentityComparer Instance = new SessionIdentityComparer();
            public new bool Equals(object? left, object? right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
