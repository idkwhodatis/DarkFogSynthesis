using System;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>Production completion boundary shared with real Harmony pipeline regressions.</summary>
    public static class SessionBeginCompletion
    {
        public static Exception? Finish(bool runOriginal, Exception? error, Func<bool> validate,
            Action complete, Action<Exception> abort)
        {
            // A prefix can cancel native Begin without throwing. Finalizers still run in HarmonyX.
            // An unknown replacement initializer is not a supported successful native session.
            if (error == null && !runOriginal)
                error = new InvalidOperationException("Native GameMain.Begin was skipped by another patch. This session was not validated; load a different valid session after correcting the conflict.");
            if (error == null)
            {
                try
                {
                    if (!validate()) return null; // The validator already latched its conflict.
                    complete();
                }
                catch (Exception failure) { error = failure; }
            }
            if (error != null) abort(error);
            return error;
        }
    }
}
