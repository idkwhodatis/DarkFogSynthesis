using System;
using System.Collections.Generic;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>A named, read-only reason that a peer's persistent data prevents candidate cleanup.</summary>
    public sealed class PeerPersistenceBlocker
    {
        internal PeerPersistenceBlocker(string pluginGuid, string displayName, string reason)
        {
            PluginGuid = pluginGuid; DisplayName = displayName; Reason = reason;
        }
        public string PluginGuid { get; }
        public string DisplayName { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// Conservative known-peer blockers only. Absence of a blocker does not establish that every other
    /// mod's persistent references have been inspected. This policy never reads or changes peer state.
    /// </summary>
    public static class PeerPersistenceGuard
    {
        public const string MoreMegaStructureGuid = "Gnimaerd.DSP.plugin.MoreMegaStructure";
        private static readonly PeerPersistenceBlocker MoreMegaStructure = new PeerPersistenceBlocker(
            MoreMegaStructureGuid, "MoreMegaStructure",
            "MoreMegaStructure is loaded; its separate mod-save data can retain DarkFogSynthesis recipe IDs and is not covered by this cleanup. " +
            "Candidate cleanup is blocked. Keep DarkFogSynthesis installed and retain the untouched backup. / " +
            "MoreMegaStructure 已加载；其独立 Mod 存档可能保留 DarkFogSynthesis 的配方 ID，当前清理未覆盖。" +
            "已阻止生成卸载候选存档。请保留 DarkFogSynthesis 和未修改的备份。");

        /// <summary>
        /// Match the verified plugin GUID exactly using ordinal, case-sensitive comparison, without trimming,
        /// prefix matching or display-name guessing. There is no version exemption: an installed known peer
        /// blocks even when its version is unknown. An invalid registry snapshot is rejected, not certified clean.
        /// </summary>
        public static IReadOnlyList<PeerPersistenceBlocker> FindBlockers(IEnumerable<string> installedPluginGuids)
        {
            if (installedPluginGuids == null) throw new ArgumentNullException(nameof(installedPluginGuids));
            bool found = false;
            foreach (string guid in installedPluginGuids)
            {
                if (string.IsNullOrWhiteSpace(guid))
                    throw new ArgumentException("The loaded plugin GUID inventory contains an empty entry.", nameof(installedPluginGuids));
                if (string.Equals(guid, MoreMegaStructureGuid, StringComparison.Ordinal)) found = true;
            }
            return FrozenList.Copy(found ? new[] { MoreMegaStructure } : Array.Empty<PeerPersistenceBlocker>());
        }
    }
}
