using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    /// <summary>Runs the exact production prefix decisions, not substitute game methods or Ready-only assertions.</summary>
    internal static class StartupGuardTests
    {
        internal static void Run(Action<bool, string> assert)
        {
            SuccessfulStartupWaitsForContent(assert);
            foreach (string failure in new[] { "configuration", "localization", "partial optional patch installation", "callback subscription" })
                InitializationFailureBlocksEntrypoints(failure, assert);
            CriticalFailureRetainsSurvivors(assert);
            MissingOwnershipIsNotSuccess(assert);
            LateBindingFailureAndTeardownStayClosed(assert);
            InvalidCoverageFailsClosed(assert);
        }

        private static void SuccessfulStartupWaitsForContent(Action<bool, string> assert)
        {
            WithEntrypoints(gate =>
            {
                gate.AssertBlocked(assert, "Before initialization");
                var order = new List<string>();
                gate.Initialize(() => { order.Add("critical"); gate.AssertBlocked(assert, "During guard installation"); },
                    () => { order.Add("configuration/localization/ordinary patches/callbacks"); gate.AssertBlocked(assert, "During initialization"); });
                assert(order.SequenceEqual(new[] { "critical", "configuration/localization/ordinary patches/callbacks" }),
                    "Critical coverage verification precedes every optional initializer and content subscription.");
                gate.AssertBlocked(assert, "Before prototypes finish binding");
                gate.MarkContentReady();
                gate.AssertAllowed(assert);
            });
        }

        private static void InitializationFailureBlocksEntrypoints(string failure, Action<bool, string> assert)
        {
            WithEntrypoints(gate =>
            {
                var stages = new List<string>();
                bool contentCallbacksUsable = false;
                Reject(() => gate.Initialize(() => stages.Add("verified critical guards"), () =>
                {
                    foreach (string stage in new[] { "configuration", "localization", "partial optional patch installation", "callback subscription" })
                    {
                        stages.Add(stage);
                        if (stage == failure) throw new InvalidOperationException(failure + " failed");
                    }
                    contentCallbacksUsable = true;
                }), assert, failure + " propagates and latches a fatal startup error.");
                assert(stages[0] == "verified critical guards" && !contentCallbacksUsable,
                    failure + " occurs behind verified guards and cannot reach usable content subscriptions.");
                gate.AssertBlocked(assert, "After " + failure);
                Reject(gate.MarkContentReady, assert, "A stale content callback cannot reopen the gate after " + failure + ".");
                gate.AssertBlocked(assert, "After rejected stale callback");
            });
        }

        private static void CriticalFailureRetainsSurvivors(Action<bool, string> assert)
        {
            WithEntrypoints(gate =>
            {
                var installed = new HashSet<string>();
                var verified = new List<string>();
                bool initialized = false;
                CriticalGuardStep Step(string name, bool failInstall) => new CriticalGuardStep(name, () =>
                {
                    if (failInstall) throw new InvalidOperationException("native detour installation failed");
                    installed.Add(name);
                }, () =>
                {
                    verified.Add(name);
                    if (!installed.Contains(name)) throw new InvalidOperationException("expected owner/prefix missing");
                });
                Reject(() => gate.Initialize(() => CriticalGuardInstallation.InstallAndVerify(new[] {
                    Step("load survivor", false), Step("missing session barrier", true),
                    Step("save survivor", false), Step("resume survivor", false)
                }), () => initialized = true), assert, "A partial critical installation is fatal, not clean coverage.");
                assert(installed.SetEquals(new[] { "load survivor", "save survivor", "resume survivor" }) && verified.Count == 4,
                    "Every independent guard is attempted and verified after an earlier failure; survivors are retained.");
                assert(!initialized, "Critical installation failure never runs configuration, localization, patches or content subscriptions.");
                // This calls the actual prefix methods that surviving native hooks reference; no game detour is executed.
                gate.AssertBlocked(assert, "Surviving critical hooks after partial installation");
            });
        }

        private static void MissingOwnershipIsNotSuccess(Action<bool, string> assert)
        {
            WithEntrypoints(gate =>
            {
                bool initialized = false;
                int checkedCount = 0;
                Reject(() => gate.Initialize(() => CriticalGuardInstallation.InstallAndVerify(new[] {
                    new CriticalGuardStep("foreign or missing prefix", () => { }, () => {
                        checkedCount++;
                        throw new InvalidOperationException("Patch returned without the exact expected owner and prefix");
                    }),
                    new CriticalGuardStep("later save prefix", () => { }, () => checkedCount++)
                }), () => initialized = true), assert, "A nonthrowing patch operation does not replace owner/prefix verification.");
                assert(!initialized && checkedCount == 2, "Missing ownership blocks initialization while remaining coverage is still inspected.");
                gate.AssertBlocked(assert, "After critical ownership verification failure");
            });
        }

        private static void LateBindingFailureAndTeardownStayClosed(Action<bool, string> assert)
        {
            foreach (string failure in new[] { "prototype binding failed", "plugin was disabled", "guard ownership was lost" })
                WithEntrypoints(gate =>
                {
                    gate.Initialize(() => { }, () => { });
                    gate.MarkContentReady();
                    gate.AssertAllowed(assert);
                    gate.Fail(new InvalidOperationException(failure));
                    gate.AssertBlocked(assert, "After " + failure);
                    Reject(gate.MarkContentReady, assert, "Repeated binding cannot clear fatal state.");
                    Reject(() => gate.Initialize(() => { }, () => { }), assert, "Reinitialization cannot clear process-lifetime failure.");
                    gate.AssertBlocked(assert, "After rejected reinitialization");
                });
        }

        private static void InvalidCoverageFailsClosed(Action<bool, string> assert)
        {
            Reject(() => CriticalGuardInstallation.InstallAndVerify(Array.Empty<CriticalGuardStep>()), assert, "Empty guard coverage is never accepted.");
            Reject(() => CriticalGuardInstallation.Verify(new CriticalGuardStep[] { null! }), assert, "Uninspectable guard coverage is never accepted.");
        }

        private static void Reject(Action action, Action<bool, string> assert, string message)
        {
            try { action(); }
            catch (InvalidOperationException) { assert(true, message); return; }
            catch (AggregateException) { assert(true, message); return; }
            catch (ArgumentException) { assert(true, message); return; }
            assert(false, message);
        }

        private static void WithEntrypoints(Action<Entrypoints> test)
        {
            // A fresh copy of the real Core assembly gives each case a new process-lifetime static gate.
            // Only our Core code executes; this harness has no game assemblies, game stubs or Harmony engine.
            var context = new AssemblyLoadContext("startup-guard-case-" + Guid.NewGuid(), true);
            try { test(new Entrypoints(context.LoadFromAssemblyPath(typeof(StartupGuardEntrypoints).Assembly.Location))); }
            finally { context.Unload(); }
        }

        private sealed class Entrypoints
        {
            private readonly Type hooks;
            private readonly object state;
            internal Entrypoints(Assembly assembly)
            {
                hooks = assembly.GetType(typeof(StartupGuardEntrypoints).FullName!, true)!;
                state = hooks.GetProperty(nameof(StartupGuardEntrypoints.State))!.GetValue(null)!;
            }
            internal void Initialize(Action guards, Action initialize) => Call(state.GetType(), state, nameof(StartupSafetyState.Initialize), guards, initialize);
            internal void MarkContentReady() => Call(state.GetType(), state, nameof(StartupSafetyState.MarkContentReady));
            internal void Fail(Exception error) => Call(state.GetType(), state, nameof(StartupSafetyState.Fail), error);
            internal void AssertBlocked(Action<bool, string> assert, string context)
            {
                Reject(() => Call(hooks, null, nameof(StartupGuardEntrypoints.BeforeSessionEntry)), assert, context + ": actual Import/Begin prefix throws before native entry.");
                foreach (string method in new[] { nameof(StartupGuardEntrypoints.BeforeLoad), nameof(StartupGuardEntrypoints.BeforeSave) })
                {
                    object[] args = { true };
                    bool runOriginal = (bool)Call(hooks, null, method, args)!;
                    assert(!runOriginal && !(bool)args[0], context + ": actual " + method + " prefix skips the native method and returns failure.");
                }
                assert(!(bool)Call(hooks, null, nameof(StartupGuardEntrypoints.BeforeResume))!, context + ": actual resume prefix skips native resume.");
            }
            internal void AssertAllowed(Action<bool, string> assert)
            {
                Call(hooks, null, nameof(StartupGuardEntrypoints.BeforeSessionEntry));
                foreach (string method in new[] { nameof(StartupGuardEntrypoints.BeforeLoad), nameof(StartupGuardEntrypoints.BeforeSave) })
                {
                    object[] args = { true };
                    assert((bool)Call(hooks, null, method, args)! && (bool)args[0], "Successful binding allows native " + method + " without replacing its result.");
                }
                assert((bool)Call(hooks, null, nameof(StartupGuardEntrypoints.BeforeResume))!, "Successful binding allows native resume.");
            }
            private static object? Call(Type type, object? target, string name, params object[] args)
            {
                try { return type.GetMethod(name)!.Invoke(target, args); }
                catch (TargetInvocationException error) when (error.InnerException != null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                    throw;
                }
            }
        }
    }
}
