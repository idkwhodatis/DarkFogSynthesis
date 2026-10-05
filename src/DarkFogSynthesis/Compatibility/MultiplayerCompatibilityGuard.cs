using System;
using BepInEx.Bootstrap;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Compatibility
{
    /// <summary>No optional assembly is loaded, patched or referenced by this read-only adapter.</summary>
    internal static class MultiplayerCompatibilityGuard
    {
        // NebulaModTeam/nebula 5ecd2b4168c8e4df72a301894d004aee5629961b:
        // NebulaWorld.Multiplayer.IsActive is Session != null, including host/join setup.
        // Use that live getter, not NebulaIsInstalled or the API's later session-change notification.
        private static readonly MultiplayerSessionProbe Probe = new MultiplayerSessionProbe(
            () => Chainloader.PluginInfos.ContainsKey(MultiplayerSessionProbe.NebulaPluginGuid),
            () => {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    if (assembly.GetName().Name == MultiplayerSessionProbe.NebulaAssemblyName)
                        return assembly.GetType(MultiplayerSessionProbe.NebulaTypeName, false);
                return null;
            });

        internal static bool AllowsSinglePlayer => Probe.AllowsSinglePlayer;
        internal static string? BlockingReason => Probe.BlockingReason;
        internal static void EnsureSinglePlayer() => Probe.EnsureSinglePlayer();
    }
}
