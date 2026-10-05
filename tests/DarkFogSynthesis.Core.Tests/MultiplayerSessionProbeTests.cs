using System;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class MultiplayerSessionProbeTests
    {
        public static class LivePeer
        {
            internal static bool Active, Throw;
            public static bool IsActive => Throw ? throw new InvalidOperationException("Unavailable peer") : Active;
        }
        public static class WrongType { public static string IsActive => "false"; }
        public static class PrivateGetter { private static bool IsActive => false; }
        public sealed class InstanceGetter { public bool IsActive => false; }

        internal static void Run(Action<bool, string> check)
        {
            bool installed = false;
            int lookups = 0;
            Type? type = typeof(LivePeer);
            var probe = new MultiplayerSessionProbe(() => installed, () => { lookups++; return type; });
            LivePeer.Active = false; LivePeer.Throw = false;
            check(probe.Observe() == MultiplayerSessionStatus.NotInstalled && probe.AllowsSinglePlayer,
                "No Nebula must retain the single-player path");
            probe.EnsureSinglePlayer();
            check(lookups == 0, "Absent peer (including API-only profiles) must not resolve optional types");
            installed = true;
            check(probe.Observe() == MultiplayerSessionStatus.SinglePlayer && probe.BlockingReason == null,
                "Nebula installed but inactive must allow single-player");
            probe.EnsureSinglePlayer();
            check(lookups == 1, "Only the getter is cached");
            for (int i = 0; i < 10; ++i)
            {
                LivePeer.Active = true;
                check(probe.Observe() == MultiplayerSessionStatus.Active && !probe.AllowsSinglePlayer,
                    "Host/client activity must not reuse a cached false value");
                Refused(probe, check);
                LivePeer.Active = false;
                check(probe.Observe() == MultiplayerSessionStatus.SinglePlayer && probe.AllowsSinglePlayer,
                    "Returning to SP must not be blocked by cached multiplayer activity");
            }
            check(lookups == 1, "Activity transitions must use the bound getter without type scans");
            LivePeer.Throw = true;
            check(probe.Observe() == MultiplayerSessionStatus.Unavailable, "Getter failure is unknown, not SP");
            Refused(probe, check);
            LivePeer.Throw = false;
            check(probe.AllowsSinglePlayer, "A transient read failure is not permanent startup failure");
            installed = false; LivePeer.Active = true;
            check(probe.Observe() == MultiplayerSessionStatus.NotInstalled && lookups == 1,
                "A detached peer getter is not invoked in profiles without that plugin");
            LivePeer.Active = false; installed = true;

            type = null;
            var retry = new MultiplayerSessionProbe(() => installed, () => type);
            Refused(retry, check);
            type = typeof(LivePeer);
            check(retry.AllowsSinglePlayer, "Missing optional type must be resolved again, not negative-cached");
            foreach (Type invalid in new[] { typeof(WrongType), typeof(PrivateGetter), typeof(InstanceGetter), typeof(object) })
            {
                var unsupported = new MultiplayerSessionProbe(() => true, () => invalid);
                check(unsupported.Observe() == MultiplayerSessionStatus.Unavailable, "Only public static bool getters qualify");
                Refused(unsupported, check);
            }
            check(new MultiplayerSessionProbe(() => throw new Exception(), () => null).Observe() == MultiplayerSessionStatus.Unavailable,
                "Missing registry must not be declared single-player");
            check(new MultiplayerSessionProbe(() => true, () => throw new Exception()).Observe() == MultiplayerSessionStatus.Unavailable,
                "Resolver failure must not escape to a startup crash");
            check(new MultiplayerSessionProbe(() => false, () => throw new Exception()).AllowsSinglePlayer,
                "Optional dependency absence must never call its resolver");

            // The same production boundary used by the lab rechecks immediately before mutation.
            var state = new SessionCompatibilityState();
            object original = new object(), replacement = new object();
            state.BeginSession(original); state.CompleteValidatedSession(original);
            int mutations = 0, pauses = 0;
            var boundary = new SessionFailureBoundary(state, () => pauses++, _ => { }, _ => { });
            var outcome = boundary.TryMutate(original, () => true, () => { probe.EnsureSinglePlayer(); mutations++; }, probe.EnsureSinglePlayer);
            check(outcome == SessionMutationOutcome.Completed && mutations == 1 && pauses == 0 && state.CanPersist(original),
                "Inactive Nebula must leave successful single-player lab actions unchanged");
            outcome = boundary.TryMutate(original, () => { LivePeer.Active = true; return true; },
                () => { probe.EnsureSinglePlayer(); mutations++; }, probe.EnsureSinglePlayer);
            check(outcome == SessionMutationOutcome.Blocked && mutations == 1 && pauses == 1 && !state.CanPersist(original),
                "Activation after preflight must block before the native mutation");
            LivePeer.Active = false;
            check(probe.AllowsSinglePlayer && !state.CanPersist(original), "Leaving MP cannot revive an affected session");
            state.EndSession(original); state.BeginSession(replacement);
            probe.EnsureSinglePlayer(); state.CompleteValidatedSession(replacement);
            check(state.CanPersist(replacement) && !state.IsBlocked && pauses == 1,
                "A fresh validated SP session clears only the session latch without a restart");
        }

        private static void Refused(MultiplayerSessionProbe probe, Action<bool, string> check)
        {
            try { probe.EnsureSinglePlayer(); check(false, "Unsupported session was permitted"); }
            catch (InvalidOperationException error) { check(!string.IsNullOrWhiteSpace(error.Message), "Refusal needs an actionable reason"); }
        }
    }
}
