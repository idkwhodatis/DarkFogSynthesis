using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using DarkFogSynthesis.Core.Compatibility;
using HarmonyLib;

namespace DarkFogSynthesis.Compatibility
{
    /// <summary>Lifecycle-only peer check; never call reflection/Harmony discovery from per-lab production hooks.</summary>
    internal static class RuntimeCompatibilityGuard
    {
        internal static void ValidateSupportedPeers()
        {
            string? reason = GetBlockingReason();
            if (reason != null) throw new InvalidOperationException(reason);
        }

        internal static string? GetBlockingReason()
        {
            try
            {
                var registry = Chainloader.PluginInfos;
                if (registry == null) throw new InvalidOperationException("The loaded plugin registry is unavailable.");
                var guids = new List<string>();
                foreach (var entry in registry)
                {
                    // Examine the actual metadata as well as the key. Do not infer identity from a display name.
                    guids.Add(entry.Key);
                    if (entry.Value == null || entry.Value.Metadata == null)
                        throw new InvalidOperationException("The loaded plugin registry contains unavailable metadata.");
                    guids.Add(entry.Value.Metadata.GUID);
                }
                string? reason = LabRuntimeCompatibilityPolicy.GetBlockingReason(guids, Array.Empty<string>());
                if (reason != null) return reason;

                var patchTypes = new List<string>();
                InspectProductionCallSite(new[] { typeof(long), typeof(bool) }, patchTypes);
                InspectProductionCallSite(new[] { typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int) }, patchTypes);
                return LabRuntimeCompatibilityPolicy.GetBlockingReason(guids, patchTypes);
            }
            catch (Exception error)
            {
                // Metadata failure must never be interpreted as a clean peer inventory.
                return "Lab runtime compatibility could not be inspected; session entry is blocked: " +
                    error.GetType().Name + ": " + error.Message;
            }
        }

        private static void InspectProductionCallSite(Type[] parameters, ICollection<string> patchTypes)
        {
            MethodInfo? method = typeof(FactorySystem).GetMethod(nameof(FactorySystem.GameTickLabProduceMode),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null);
            if (method == null) throw new InvalidOperationException("A required FactorySystem.GameTickLabProduceMode overload is unavailable.");
            Patches? patches = Harmony.GetPatchInfo(method);
            if (patches == null) return; // Harmony documents null as no registered patches for this method.
            InspectPatchMethods(patches.Prefixes, patchTypes);
            InspectPatchMethods(patches.Postfixes, patchTypes);
            InspectPatchMethods(patches.Transpilers, patchTypes);
            InspectPatchMethods(patches.Finalizers, patchTypes);
            InspectPatchMethods(patches.ILManipulators, patchTypes);
        }

        private static void InspectPatchMethods(IEnumerable<Patch> patches, ICollection<string> patchTypes)
        {
            foreach (Patch patch in patches)
            {
                string? typeName = patch.PatchMethod?.DeclaringType?.FullName;
                if (string.IsNullOrWhiteSpace(typeName))
                    throw new InvalidOperationException("A lab production call-site patch has no inspectable declaring type.");
                // LabOpt calls CreateAndPatchAll(typeof(LabOptPatch)) without an explicit owner ID.
                // Its generated Harmony owner is not the BepInEx GUID; match the actual patch type instead.
                patchTypes.Add(typeName!);
            }
        }
    }
}
