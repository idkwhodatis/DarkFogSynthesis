using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class RemovalTargetGuardTests
    {
        internal static void Run(Action<bool, string> check)
        {
            foreach (bool lab in new[] { false, true })
            foreach (string boundary in new[] { "before", "export", "earlier-reset" })
            foreach (string change in new[] { "none", "component", "entity", "recipe", "owned-recipe", "tech", "mode", "matrix-mode", "buffer", "negative-progress", "unowned", "location" })
            {
                var expected = new RemovalTargetIdentity(2, 20, 48102, 0, false, lab);
                var live = expected;
                bool hasState = false, owned = true, locationMatches = true;
                int exports = 0, resets = 0, checks = 0;
                var rollback = new List<Action>();
                var order = new List<string>();
                void Change()
                {
                    live = new RemovalTargetIdentity(change == "component" ? 3 : 2, change == "entity" ? 21 : 20,
                        change == "recipe" ? 1 : change == "owned-recipe" ? 48101 : 48102, change == "tech" ? 1951 : 0,
                        change == "mode", change == "matrix-mode" ? !lab : lab);
                    hasState = change == "buffer" || change == "negative-progress";
                    owned = change != "unowned";
                    locationMatches = change != "location";
                }
                if (boundary == "before") Change();
                if (boundary == "earlier-reset")
                    RemovalTargetGuard.Reset(() => { }, () => () => { }, rollback.Add, Change);
                int previousEnrolled = rollback.Count;
                Exception? error = null;
                try
                {
                    RemovalTargetGuard.Reset(() => {
                        checks++; order.Add("validate");
                        if (!locationMatches) throw new InvalidOperationException("Fixture location replaced");
                        RemovalTargetGuard.EnsureUnchanged(expected, live, owned, hasState);
                    }, () => {
                        exports++; order.Add("export");
                        if (boundary == "export") Change();
                        return () => { };
                    }, restore => { order.Add("enroll"); rollback.Add(restore); }, () => { resets++; order.Add("reset"); });
                }
                catch (InvalidOperationException caught) { error = caught; }
                string label = lab + "/" + boundary + "/" + change;
                bool unchanged = change == "none";
                check((error == null) == unchanged, "Target drift admission: " + label);
                check(resets == (unchanged ? 1 : 0), "Changed target reset: " + label);
                check(rollback.Count == previousEnrolled + (unchanged ? 1 : 0), "Stale target enrolled for rollback: " + label);
                check(exports == (unchanged || boundary == "export" ? 1 : 0), "Changed target exported before validation: " + label);
                check(checks == (unchanged || boundary == "export" ? 2 : 1), "Export boundary must be checked twice: " + label);
                if (unchanged) check(order.SequenceEqual(new[] { "validate", "export", "validate", "enroll", "reset" }), "Reset ordering");
            }
            // Native reset failures still have a rollback snapshot; failed exports never enroll one.
            foreach (string failure in new[] { "validate", "export", "null-rollback", "reset" })
            {
                int enrolled = 0, reset = 0;
                try
                {
                    RemovalTargetGuard.Reset(() => {
                        if (failure == "validate") throw new InvalidOperationException("validate");
                    }, () => {
                        if (failure == "export") throw new InvalidOperationException("export");
                        return failure == "null-rollback" ? null! : (() => { });
                    }, _ => enrolled++, () => { reset++; throw new InvalidOperationException("reset"); });
                    check(false, "Failure swallowed");
                }
                catch (InvalidOperationException) { }
                check(enrolled == (failure == "reset" ? 1 : 0), "Invalid snapshot enrolled: " + failure);
                check(reset == (failure == "reset" ? 1 : 0), "Reset reached on failed preparation: " + failure);
            }
            foreach (var invalid in new[] { new RemovalTargetIdentity(), new RemovalTargetIdentity(-1, 1, 1, 0, false, false),
                new RemovalTargetIdentity(1, 0, 1, 0, false, false) })
            {
                try { RemovalTargetGuard.EnsureUnchanged(invalid, invalid, true, false); check(false, "Invalid target accepted"); }
                catch (InvalidOperationException) { check(true, "Invalid target refused"); }
            }
        }
    }
}
