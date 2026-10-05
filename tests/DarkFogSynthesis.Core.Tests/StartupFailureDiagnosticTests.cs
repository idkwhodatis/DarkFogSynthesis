using System;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class StartupFailureDiagnosticTests
    {
        internal static void Run(Action<bool, string> check)
        {
            foreach (bool ready in new[] { false, true })
            foreach (bool throws in new[] { false, true })
            foreach (string message in new[] { "", " \t\n", "original failure" })
            {
                var state = new StartupSafetyState();
                if (ready) { state.Initialize(() => { }, () => { }); state.MarkContentReady(); }
                bool closedBeforeGetter = false;
                var error = new DiagnosticException(() =>
                {
                    closedBeforeGetter = !state.AllowsGameOperations && state.FailureReason != null;
                    // A nested diagnostic failure must not replace the first failure.
                    state.Fail(new Exception("nested failure"));
                }, message, throws);
                state.Fail(error);
                check(closedBeforeGetter && error.Reads == 1 && !state.AllowsGameOperations && !string.IsNullOrWhiteSpace(state.FailureReason),
                    "Startup failure gate must close before a virtual Message getter and survive its exception");
                check(state.FailureReason!.StartsWith(nameof(DiagnosticException) + ":", StringComparison.Ordinal),
                    "Original startup exception identity was lost");
                string first = state.FailureReason!;
                var later = new DiagnosticException(() => { }, "later failure", true);
                state.Fail(later);
                check(later.Reads == 0 && state.FailureReason == first, "Sticky failure must not read later diagnostic getters");
                if (!throws && !string.IsNullOrWhiteSpace(message)) check(first.Contains(message), "Useful diagnostic text was discarded");
            }
        }

        private sealed class DiagnosticException : Exception
        {
            private readonly Action observe;
            private readonly string text;
            private readonly bool throws;
            internal int Reads;
            internal DiagnosticException(Action observe, string text, bool throws)
            { this.observe = observe; this.text = text; this.throws = throws; }
            public override string Message
            {
                get
                {
                    Reads++; observe();
                    return throws ? throw new InvalidOperationException("message getter failed") : text;
                }
            }
        }
    }
}
