using System;
using DarkFogSynthesis.Core.Diagnostics;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class OptionalDiagnosticTests
    {
        internal static void Run(Action<bool, string> check)
        {
            int captures = 0, warnings = 0;
            var state = new SessionCompatibilityState();
            var session = new object();
            state.BeginSession(session);
            state.CompleteValidatedSession(session);
            bool result = OptionalDiagnostic.TryCapture(false, () => captures++, _ => warnings++);
            check(!result && captures == 0 && warnings == 0, "Disabled diagnostics invoked a callback");
            result = OptionalDiagnostic.TryCapture(true, () => captures++, _ => warnings++);
            check(result && captures == 1 && warnings == 0, "Successful capture failed");
            result = OptionalDiagnostic.TryCapture(true, () => { captures++; throw new Exception("write failed"); }, _ => warnings++);
            check(!result && captures == 2 && warnings == 1 && state.CanPersist(session), "Diagnostic failure changed readiness or escaped");
            result = OptionalDiagnostic.TryCapture(true, () => { throw new Exception("write failed"); }, _ => { throw new Exception("logger failed"); });
            check(!result && state.CanPersist(session) && !state.IsBlocked, "Broken logger changed the healthy session");
        }
    }
}
