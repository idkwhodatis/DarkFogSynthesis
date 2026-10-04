using HarmonyLib;

namespace DarkFogSynthesis.Compatibility
{
    [HarmonyPatch]
    internal static class OwnedBufferImportGuard
    {
        // LDBTool's Import sanitizer can replace incompatible arrays with zero-filled arrays.
        // Validate our exact registered recipe identities before that known postfix, not only at GameMain.Begin.
        [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.Import)), HarmonyPostfix]
        [HarmonyPriority(Priority.First), HarmonyBefore("me.xiaoye97.plugin.Dyson.LDBTool")]
        private static void CheckAssembler(ref AssemblerComponent __instance)
            => Plugin.Instance?.ValidateImportedAssembler(__instance);

        [HarmonyPatch(typeof(LabComponent), nameof(LabComponent.Import)), HarmonyPostfix]
        [HarmonyPriority(Priority.First), HarmonyBefore("me.xiaoye97.plugin.Dyson.LDBTool")]
        private static void CheckLab(ref LabComponent __instance)
            => Plugin.Instance?.ValidateImportedLab(__instance);
    }
}
