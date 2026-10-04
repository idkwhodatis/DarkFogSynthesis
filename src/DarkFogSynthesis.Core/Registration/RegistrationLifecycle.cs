using System;

namespace DarkFogSynthesis.Core.Registration
{
    /// <summary>Optional diagnostic I/O must never invalidate successfully registered content.</summary>
    public static class RegistrationLifecycle
    {
        public static void BindAndDiagnose(Action bind, Action<Exception> bindingFailed,
            Action diagnose, Action<Exception> diagnosticWarning)
        {
            if (bind == null) throw new ArgumentNullException(nameof(bind));
            if (bindingFailed == null) throw new ArgumentNullException(nameof(bindingFailed));
            if (diagnose == null) throw new ArgumentNullException(nameof(diagnose));
            if (diagnosticWarning == null) throw new ArgumentNullException(nameof(diagnosticWarning));
            try { bind(); }
            catch (Exception error) { bindingFailed(error); throw; }

            // Deliberately outside the registration-failure boundary, including warning reporting.
            try { diagnose(); }
            catch (Exception error) { diagnosticWarning(error); }
        }
    }
}
