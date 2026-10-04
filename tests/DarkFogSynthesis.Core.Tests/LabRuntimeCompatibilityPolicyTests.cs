using System;
using DarkFogSynthesis.Core.Compatibility;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class LabRuntimeCompatibilityPolicyTests
    {
        internal static void Run(Action<bool, string> assert)
        {
            assert(LabRuntimeCompatibilityPolicy.GetBlockingReason(Array.Empty<string>(), Array.Empty<string>()) == null,
                "An empty peer inventory has no known LabOpt blocker.");
            string? pluginReason = LabRuntimeCompatibilityPolicy.GetBlockingReason(new[] { "org.soardev.labopt" }, Array.Empty<string>());
            assert(pluginReason != null && pluginReason.Contains("every LabOpt version"),
                "Exact pinned BepInEx GUID blocks without a version exemption.");
            assert(LabRuntimeCompatibilityPolicy.GetBlockingReason(Array.Empty<string>(), new[] { "LabOpt.LabOptPatch" }) != null,
                "Actual LabOpt patch type blocks even without a registered plugin GUID.");
            assert(LabRuntimeCompatibilityPolicy.GetBlockingReason(new[] { "some.other.plugin" },
                new[] { "OtherMod.LabPatches", "DarkFogSynthesis.Compatibility.NativeRecipeCompatibility" }) == null,
                "Known unrelated patches are not mistaken for LabOpt.");
            foreach (string other in new[] { "LabOpt", "org.soardev.labopt.extra", " org.soardev.labopt", "ORG.SOARDEV.LABOPT" })
                assert(LabRuntimeCompatibilityPolicy.GetBlockingReason(new[] { other }, Array.Empty<string>()) == null,
                    "No display-name, prefix, trimming or case-insensitive plugin guesses: " + other);
            foreach (string other in new[] { "LabOpt.LabOptPatchExtra", "OtherMod.LabOptPatch", "labopt.laboptpatch", "org.soardev.labopt" })
                assert(LabRuntimeCompatibilityPolicy.GetBlockingReason(Array.Empty<string>(), new[] { other }) == null,
                    "Patch declaring type is exact, not a guessed Harmony owner: " + other);
            Reject(() => LabRuntimeCompatibilityPolicy.GetBlockingReason(null!, Array.Empty<string>()), assert,
                "Missing plugin inventory fails closed.", typeof(ArgumentNullException));
            Reject(() => LabRuntimeCompatibilityPolicy.GetBlockingReason(Array.Empty<string>(), null!), assert,
                "Missing patch inventory fails closed.", typeof(ArgumentNullException));
            Reject(() => LabRuntimeCompatibilityPolicy.GetBlockingReason(new[] { "" }, Array.Empty<string>()), assert,
                "Unknown plugin identity fails closed.");
            Reject(() => LabRuntimeCompatibilityPolicy.GetBlockingReason(Array.Empty<string>(), new[] { "" }), assert,
                "Unknown patch identity fails closed.");
        }

        private static void Reject(Action action, Action<bool, string> assert, string description, Type? expected = null)
        {
            try { action(); }
            catch (Exception error)
            {
                assert(error.GetType() == (expected ?? typeof(InvalidOperationException)), description);
                return;
            }
            assert(false, description);
        }
    }
}
