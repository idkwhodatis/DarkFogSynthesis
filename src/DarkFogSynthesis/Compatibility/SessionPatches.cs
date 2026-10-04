using System;
using HarmonyLib;

namespace DarkFogSynthesis.Compatibility
{
    /// <summary>Actual game-data imports only. Save-list/header previews must never change shared prototypes.</summary>
    [HarmonyPatch]
    internal static class SessionPatches
    {
        private static readonly object TransitionLock = new object();
        private static GameData? session;
        private static GameData? transition;
        private static bool? sessionPeaceMode;
        [ThreadStatic] private static GameData? importing;
        [ThreadStatic] private static bool descriptorApplied;

        [HarmonyPatch(typeof(GameData), nameof(GameData.Import)), HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void BeforeImport(GameData __instance, out bool __state)
        {
            __state = false;
            try
            {
                Plugin.Instance.EnsureRegistryReady();
                BeginTransition(__instance);
            }
            catch (Exception error) { Plugin.Instance.RejectSession(__instance, error); throw; }
            __state = true;
            importing = __instance;
            descriptorApplied = false;
        }

        [HarmonyPatch(typeof(GameDesc), nameof(GameDesc.Import)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void AfterDescriptor(GameDesc __instance)
        {
            if (importing == null || descriptorApplied) return;
            Plugin.Instance.ApplyMode(__instance.isPeaceMode);
            sessionPeaceMode = __instance.isPeaceMode;
            descriptorApplied = true;
        }

        [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.Import)), HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void BeforeHistoryImport(GameHistoryData __instance)
        {
            if (importing != null && !descriptorApplied)
                throw new InvalidOperationException("Unsupported DSP load order: save mode was not read before history. DarkFogSynthesis stopped this load without changing the save file.");
            if (importing != null && __instance.gameData != null && !ReferenceEquals(__instance.gameData, importing))
                throw new InvalidOperationException("A nested history import belongs to a different game-data instance.");
        }

        [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.Import)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void AfterHistoryImport(GameHistoryData __instance)
        {
            if (importing == null || !descriptorApplied) return;
            SaveReconciler.Reconcile(__instance);
            __instance.VerifyTechQueue();
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.Import)), HarmonyFinalizer]
        private static Exception? EndImport(GameData __instance, bool __state, Exception? __exception)
        {
            if (!__state) return __exception;
            importing = null;
            descriptorApplied = false;
            EndTransition(__instance, __exception);
            return __exception;
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.NewGame)), HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void BeforeNewGame(GameData __instance, GameDesc _gameDesc, out bool __state)
        {
            __state = false;
            try
            {
                Plugin.Instance.EnsureRegistryReady();
                BeginTransition(__instance);
            }
            catch (Exception error) { Plugin.Instance.RejectSession(__instance, error); throw; }
            __state = true;
            Plugin.Instance.ApplyMode(_gameDesc.isPeaceMode);
            sessionPeaceMode = _gameDesc.isPeaceMode;
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.NewGame)), HarmonyFinalizer]
        private static Exception? EndNewGame(GameData __instance, bool __state, bool __result, Exception? __exception)
        {
            if (!__state) return __exception;
            EndTransition(__instance, __exception);
            if (__exception == null && !__result) RestoreSession(__instance);
            return __exception;
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.SetForNewGame)), HarmonyPostfix]
        private static void AfterNewHistory(GameData __instance)
        {
            if (ReferenceEquals(session, __instance) && sessionPeaceMode.HasValue && __instance.history != null)
                SaveReconciler.Reconcile(__instance.history);
        }

        [HarmonyPatch(typeof(GameMain), nameof(GameMain.Begin)), HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void BeforeBegin()
        {
            Plugin.Instance.EnsureRegistryReady();
            // Do not repair a late/unrecognized path after native history/queue initialization already ran.
            if (!ReferenceEquals(session, GameMain.data) || !sessionPeaceMode.HasValue ||
                GameMain.data.gameDesc == null || sessionPeaceMode.Value != GameMain.data.gameDesc.isPeaceMode)
                throw new InvalidOperationException("Unsupported or changed session initialization order. The mode policy must be applied before history/queue initialization.");
            Plugin.Instance.EnsureSessionCanBegin(GameMain.data);
            Plugin.Instance.ValidateActiveProgression(GameMain.data);
            Plugin.Instance.ValidateLoadedMachines(GameMain.data);
            SaveReconciler.Reconcile(GameMain.data.history);
            GameMain.data.history.VerifyTechQueue();
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.Destroy)), HarmonyPostfix]
        private static void OnSessionDestroyed(GameData __instance) => RestoreSession(__instance);

        [HarmonyPatch(typeof(GameMain), nameof(GameMain.Begin)), HarmonyFinalizer, HarmonyPriority(Priority.Last)]
        private static Exception? BeginFailed(Exception? __exception)
        {
            // All original/postfix work must finish before a replacement can release either latch.
            // In particular, a foreign late postfix may disable required technologies or replace lab hooks.
            if (__exception == null)
            {
                try
                {
                    if (!Plugin.Instance.DiagnoseLateConflicts()) return null;
                    Plugin.Instance.CompleteValidatedSession(GameMain.data);
                    SafeRemovalService.OnNewSession(GameMain.data);
                }
                catch (Exception error) { __exception = error; }
            }
            if (__exception != null)
            {
                if (GameMain.data != null && GameMain.isRunning) GameMain.Pause();
                Plugin.Instance.AbortSession(GameMain.data, __exception);
            }
            return __exception;
        }

        private static void BeginTransition(GameData data)
        {
            lock (TransitionLock)
            {
                if (transition != null) throw new InvalidOperationException("Concurrent/nested game-data initialization is unsupported.");
                Plugin.Instance.Progression.Restore();
                Plugin.Instance.BeginSession(data);
                transition = data;
                session = data;
                sessionPeaceMode = null;
            }
        }

        private static void EndTransition(GameData data, Exception? error)
        {
            lock (TransitionLock)
            {
                if (ReferenceEquals(transition, data)) transition = null;
                if (error != null && ReferenceEquals(session, data))
                {
                    Plugin.Instance.AbortSession(data, error);
                    Plugin.Instance.EndSession(data);
                    session = null;
                    sessionPeaceMode = null;
                }
            }
        }

        private static void RestoreSession(GameData data)
        {
            lock (TransitionLock)
            {
                // A menu preview or old GameData can be destroyed after another save was prepared.
                if (!ReferenceEquals(session, data)) return;
                Plugin.Instance.Progression.Restore();
                Plugin.Instance.EndSession(data);
                session = null;
                sessionPeaceMode = null;
                if (ReferenceEquals(transition, data)) transition = null;
                if (ReferenceEquals(importing, data)) { importing = null; descriptorApplied = false; }
            }
        }
    }
}
