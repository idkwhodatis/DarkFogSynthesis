using System;

namespace DarkFogSynthesis.Core.Diagnostics
{
    /// <summary>No state transition or side effect is allowed on the disabled path.</summary>
    public static class OptionalDiagnostic
    {
        public static bool TryCapture(bool enabled, Action capture, Action<Exception> warn)
        {
            if (!enabled) return false;
            try { capture(); return true; }
            catch (Exception error)
            {
                try { warn(error); } catch (Exception) { }
                return false;
            }
        }
    }
}
