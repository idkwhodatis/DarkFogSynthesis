using System;
using System.Collections.Generic;
using System.Reflection;
using DarkFogSynthesis.Core.Compatibility;
using HarmonyLib;

namespace DarkFogSynthesis.Compatibility
{
    /// <summary>
    /// Additional save admission only; existing startup and maintenance guards remain installed.
    /// A captured lease excludes lab mutation until this exact save invocation has finished.
    /// </summary>
    [HarmonyPatch]
    internal static class NativeMutationPersistenceGuard
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets() => new[] {
            AccessTools.Method(typeof(GameSave), nameof(GameSave.SaveCurrentGame), new[] { typeof(string) }),
            AccessTools.Method(typeof(GameSave), nameof(GameSave.AutoSave), Type.EmptyTypes),
            AccessTools.Method(typeof(GameSave), nameof(GameSave.AutoSaveAfterErrored), Type.EmptyTypes),
            AccessTools.Method(typeof(GameSave), nameof(GameSave.SaveAsLastExit), Type.EmptyTypes) };

        [HarmonyPrefix, HarmonyPriority(Priority.Last)]
        private static bool BeforeSave(ref bool __result, out IDisposable? __state)
        {
            __state = null;
            var plugin = Plugin.Instance;
            if (plugin != null && !plugin.IsPersistenceBlocked)
                __state = plugin.FailureBoundary.TryBeginPersistence(GameMain.data);
            if (__state != null) return true;
            __result = false;
            return false;
        }

        [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
        private static Exception? AfterSave(IDisposable? __state, Exception? __exception)
        {
            __state?.Dispose();
            return __exception;
        }
    }
}
