using System;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>Shared save-prefix decision. A maintenance permit never bypasses session validation.</summary>
    public static class SessionPersistencePolicy
    {
        public static bool AllowsWrite(bool persistenceReady, bool maintenanceRestricted,
            bool isNamedSave, string? requestedName, string? permittedName) =>
            persistenceReady && (!maintenanceRestricted || (isNamedSave && permittedName != null &&
                string.Equals(requestedName, permittedName, StringComparison.Ordinal)));
    }
}
