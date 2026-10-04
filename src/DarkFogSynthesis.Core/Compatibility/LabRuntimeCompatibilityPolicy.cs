using System;
using System.Collections.Generic;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>
    /// Exact, source-backed identities of an unqualified lab-runtime replacement. No version exemption
    /// exists until an adapter is tested. An absent match is not certification of arbitrary peer mods.
    /// </summary>
    public static class LabRuntimeCompatibilityPolicy
    {
        // soarqin/DSP_Mods 9fc2723bcaa3b4b0a13e47477f70f2bf697edf98, LabOpt/LabOpt.csproj and LabOpt.cs.
        public const string LabOptPluginGuid = "org.soardev.labopt";
        public const string LabOptPatchType = "LabOpt.LabOptPatch";

        private const string BlockingReason =
            "LabOpt is loaded or patches the lab production call site. Its replacement production path bypasses " +
            "DarkFogSynthesis's LabComponent animation adapter, and its shared lab buffers are unqualified. " +
            "This combination is blocked for every LabOpt version until a tested adapter is available. " +
            "Use a separate compatible profile and an untouched copied save; do not overwrite the affected save. / " +
            "检测到 LabOpt；其生产替换路径绕过本 Mod 的研究站动画适配，且共享缓存尚未验证。" +
            "当前阻止所有 LabOpt 版本的组合。请使用独立兼容配置及未修改的存档副本，勿覆盖受影响存档。";

        public static string? GetBlockingReason(IEnumerable<string> pluginGuids, IEnumerable<string> patchDeclaringTypes)
        {
            if (pluginGuids == null) throw new ArgumentNullException(nameof(pluginGuids));
            if (patchDeclaringTypes == null) throw new ArgumentNullException(nameof(patchDeclaringTypes));
            foreach (string guid in pluginGuids)
            {
                RequireIdentity(guid, "Plugin GUID inventory");
                if (string.Equals(guid, LabOptPluginGuid, StringComparison.Ordinal)) return BlockingReason;
            }
            foreach (string type in patchDeclaringTypes)
            {
                RequireIdentity(type, "Patch declaring-type inventory");
                if (string.Equals(type, LabOptPatchType, StringComparison.Ordinal)) return BlockingReason;
            }
            return null;
        }

        private static void RequireIdentity(string value, string inventory)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(inventory + " contains an unknown identity; compatibility cannot be established.");
        }
    }
}
