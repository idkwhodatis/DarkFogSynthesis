using System;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>A process-lifetime, fail-closed gate independent of localization, configuration and game types.</summary>
    public sealed class StartupSafetyState
    {
        private bool started;
        private volatile bool guardsVerified;
        private volatile bool initializationComplete;
        private volatile bool contentReady;
        private string? failureReason;

        public bool GuardsVerified => guardsVerified;
        public string? FailureReason => System.Threading.Volatile.Read(ref failureReason);
        public bool AllowsGameOperations => guardsVerified && initializationComplete && contentReady && FailureReason == null;

        /// <summary>No initializer (including content callback subscription) runs before every critical hook verifies.</summary>
        public void Initialize(Action installAndVerifyGuards, Action initialize)
        {
            if (installAndVerifyGuards == null) throw new ArgumentNullException(nameof(installAndVerifyGuards));
            if (initialize == null) throw new ArgumentNullException(nameof(initialize));
            if (started) throw new InvalidOperationException("DarkFogSynthesis startup cannot be retried in the same process; restart the game.");
            started = true;
            try
            {
                installAndVerifyGuards();
                guardsVerified = true;
                initialize();
                EnsureNotFailed();
                initializationComplete = true;
            }
            catch (Exception error) { Fail(error); throw; }
        }

        public void EnsureInitializationComplete()
        {
            EnsureNotFailed();
            if (!guardsVerified || !initializationComplete)
                throw new InvalidOperationException("DarkFogSynthesis initialization and critical guard verification did not finish.");
        }

        public void MarkContentReady()
        {
            EnsureInitializationComplete();
            contentReady = true;
        }

        public void EnsureGameOperationsAllowed()
        {
            EnsureNotFailed();
            if (!AllowsGameOperations)
                throw new InvalidOperationException("DarkFogSynthesis cannot safely enter this save: guarded prototype initialization did not finish.");
        }

        /// <summary>Failure is sticky, including after disposal; a later callback cannot reopen the gate.</summary>
        public void Fail(Exception error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            System.Threading.Interlocked.CompareExchange(ref failureReason, error.GetType().Name + ": " + error.Message, null);
        }

        private void EnsureNotFailed()
        {
            if (FailureReason != null)
                throw new InvalidOperationException("DarkFogSynthesis is blocked for this process; restart after correcting the error: " + FailureReason);
        }
    }

    /// <summary>
    /// These exact static methods are installed as the runtime Harmony prefixes. They neither reference nor
    /// initialize any game, localization, configuration or plugin object, so tests can execute the real decisions.
    /// </summary>
    public static class StartupGuardEntrypoints
    {
        public static StartupSafetyState State { get; } = new StartupSafetyState();

        public static void BeforeSessionEntry() => State.EnsureGameOperationsAllowed();
        public static bool BeforeLoad(ref bool __result) => AllowBooleanOperation(ref __result);
        public static bool BeforeSave(ref bool __result) => AllowBooleanOperation(ref __result);
        public static bool BeforeResume() => State.AllowsGameOperations;

        private static bool AllowBooleanOperation(ref bool result)
        {
            if (State.AllowsGameOperations) return true;
            result = false;
            return false;
        }
    }
}
