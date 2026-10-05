using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using BepInEx;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Compatibility;
using HarmonyLib;

namespace DarkFogSynthesis.Compatibility
{
    /// <summary>
    /// Conservative, explicit maintenance operation. Active buffers are a blocker until native refund/capacity
    /// behavior is game-tested. A candidate is NEVER certified vanilla-compatible merely because it serialized.
    /// </summary>
    internal static class SafeRemovalService
    {
        private static readonly HashSet<int> Recipes = new HashSet<int>(FrozenContent.Recipes.Select(r => r.Id.Value));
        private static readonly HashSet<int> Technologies = new HashSet<int>(FrozenContent.Technologies.Select(t => t.Id.Value));
        private static int writesInProgress;
        private static bool cleaning;
        private static GameData? quarantinedSession;
        // Keep the quarantine global while a replacement is loading or has failed; merely replacing
        // GameMain.data is not proof that the next session passed all validation.
        internal static bool IsQuarantined { get { lock (SaveGate) return quarantinedSession != null; } }
        private static readonly object SaveGate = new object();
        [ThreadStatic] private static string? permittedSaveName;
        [ThreadStatic] private static MaintenanceSessionGuard? restoringPreflightSession;

        private static class SaveWriteGuard
        {
            internal static bool BeginSave(bool isNamedSave, string? requestedName, ref bool result, out bool state)
            {
                state = false;
                lock (SaveGate)
                {
                    // The independent critical prefix already rejects fatal/incomplete startup. Keep this
                    // maintenance barrier fail-closed too; compatibility alone does not include init failure.
                    if (!SessionPersistencePolicy.AllowsWrite(Plugin.Instance != null && !Plugin.Instance.IsPersistenceBlocked,
                        cleaning || quarantinedSession != null, isNamedSave, requestedName, permittedSaveName))
                    { result = false; return false; }
                    ++writesInProgress;
                    state = true;
                    return true;
                }
            }

            internal static Exception? EndSave(bool state, Exception? error)
            {
                if (state) lock (SaveGate) --writesInProgress;
                return error;
            }
        }

        // HarmonyX 2.7.0 does not implement __args injection. Use exact supported signatures,
        // with each prefix/finalizer pair sharing its own declaring-type __state slot.
        [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame), new[] { typeof(string) })]
        private static class NamedSaveWriteGuard
        {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static bool BeginSave(string __0, ref bool __result, out bool __state) =>
                SaveWriteGuard.BeginSave(true, __0, ref __result, out __state);

            [HarmonyFinalizer]
            private static Exception? EndSave(bool __state, Exception? __exception) => SaveWriteGuard.EndSave(__state, __exception);
        }

        [HarmonyPatch]
        private static class AutomaticSaveWriteGuard
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> Targets() => new[] {
                AccessTools.Method(typeof(GameSave), nameof(GameSave.AutoSave), Type.EmptyTypes),
                AccessTools.Method(typeof(GameSave), nameof(GameSave.AutoSaveAfterErrored), Type.EmptyTypes),
                AccessTools.Method(typeof(GameSave), nameof(GameSave.SaveAsLastExit), Type.EmptyTypes) };

            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static bool BeginSave(ref bool __result, out bool __state) =>
                SaveWriteGuard.BeginSave(false, null, ref __result, out __state);

            [HarmonyFinalizer]
            private static Exception? EndSave(bool __state, Exception? __exception) => SaveWriteGuard.EndSave(__state, __exception);
        }

        [HarmonyPatch(typeof(GameMain), nameof(GameMain.Resume))]
        private static class ResumeGuard
        {
            [HarmonyPrefix]
            private static bool BeforeResume()
            {
                lock (SaveGate)
                {
                    if (!MaintenanceSessionGuard.AllowsResume(Plugin.Instance != null && !Plugin.Instance.IsResumeBlocked,
                        cleaning, quarantinedSession != null)) return false;
                    // A foreign prefix may replace the session after the restoration decision but
                    // before this native entry. Ordinary Resume retains its weaker Begin semantics.
                    var current = GameMain.data;
                    return restoringPreflightSession == null || restoringPreflightSession.CanRestoreRunningSession(current,
                        current?.history, current?.mainPlayer, GameMain.isLoading, HasCurrentValidatedPlayer(current));
                }
            }
        }

        internal static void OnNewSession(GameData data)
        {
            // Called only after the replacement's native Begin and late compatibility checks succeed.
            lock (SaveGate)
                if (MaintenanceSessionGuard.CanReleaseQuarantineAfterValidation(cleaning, quarantinedSession, data)) quarantinedSession = null;
        }

        internal static string Preview()
        {
            if (GameMain.data == null || GameMain.mainPlayer == null) return "No loaded game to inspect.";
            var scan = Scan(GameMain.data, false);
            string summary = scan.Blockers.Count == 0
                ? $"Candidate preflight: {scan.Assemblers.Count} assembler/smelter and {scan.Labs.Count} lab references. No active buffers found. Vanilla reload and external blueprint safety are still unverified."
                : "Cleanup blocked: " + string.Join("; ", scan.Blockers.Take(12));
            if (!GameMain.isPaused)
                return summary + " Pause the game to inspect read-only idle-buffer totals and package capacity. Automatic refunds remain disabled. / 暂停后可预览闲置缓存账目与背包容量；自动退料仍未启用。";
            try
            {
                var ledger = NativeBufferRefunds.Plan(GameMain.data);
                if (ledger.Packets.Count == 0) return summary + " Idle-buffer refund ledger is empty; no item transfer was performed.";
                var capacity = NativeBufferRefunds.CheckPackageCapacity(GameMain.mainPlayer, ledger);
                return summary + $" Idle-buffer diagnostic: {capacity.RequestedCount} items, {capacity.RequestedInc} proliferation points; whole-batch package fit: {capacity.Fits}. " +
                    capacity.Reason + " This diagnostic does not permit cleanup of occupied machines. / 此预览不解除有缓存设备的清理限制。";
            }
            catch (Exception error)
            {
                return summary + " Refund-capacity diagnostic unavailable: " + error.Message + " No automatic refund was attempted.";
            }
        }

        internal static string PrepareCandidate()
        {
            Plugin.Instance.EnsureReady();
            GameData data;
            GameHistoryData history;
            MaintenanceSessionGuard session;
            lock (SaveGate)
            {
                if (cleaning || quarantinedSession != null || writesInProgress != 0 || GameMain.isLoading || GameMain.data == null || GameMain.mainPlayer == null)
                    throw new InvalidOperationException("A game must be loaded, with no other save or maintenance operation in progress.");
                data = GameMain.data;
                history = data.history;
                session = new MaintenanceSessionGuard(data, history, data.mainPlayer, GameMain.isPaused);
                cleaning = true;
            }
            bool succeeded = false;
            var rollback = new List<Action>();
            string? backupName = null;
            string? candidateName = null;
            try
            {
                GameMain.Pause();
                RequirePausedSession(session);
                var scan = Scan(data, false);
                if (scan.Blockers.Count != 0) throw new InvalidOperationException(string.Join("; ", scan.Blockers));
                string token = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
                backupName = "DFS-backup-" + token;
                candidateName = "DFS-removal-candidate-" + token;
                if (GameSave.SaveExist(backupName) || GameSave.SaveExist(candidateName)) throw new IOException("Unique maintenance save name already exists.");
                SaveNewAndVerify(backupName, session); // First durable copy: nothing has been removed yet.
                RequirePausedSession(session);
                // Recheck after all native save callbacks. No writes start if inventory or references changed.
                var secondScan = Scan(data, false);
                if (secondScan.Blockers.Count != 0 || !scan.SameTargets(secondScan))
                    throw new InvalidOperationException("State changed while the backup was created. Backup is retained; no cleanup was performed.");
                RequirePausedSession(session);

                var originalRecipes = new HashSet<int>(history.recipeUnlocked);
                var originalTechs = new Dictionary<int, TechState>(history.techStates);
                var originalQueue = (int[])history.techQueue.Clone();
                var expectedRecipes = originalRecipes.Where(id => !Recipes.Contains(id)).ToArray();
                var expectedTechs = originalTechs.Where(k => !Technologies.Contains(k.Key)).ToDictionary(k => k.Key, k => k.Value);
                var expectedQueue = originalQueue.Where(id => id != 0 && !Technologies.Contains(id)).ToArray();
                int expectedCurrentTech = history.currentTech;
                void VerifyPreservedHistory()
                {
                    RequirePausedSession(session);
                    RemovalHistoryGuard.EnsurePreserved(expectedRecipes, expectedTechs, expectedQueue, expectedCurrentTech,
                        history.recipeUnlocked, history.techStates, history.techQueue, history.currentTech);
                }
                lock (SaveGate)
                {
                    RequirePausedSession(session);
                    quarantinedSession = data;
                }
                rollback.Add(() =>
                {
                    // Restore only our IDs. Never replace shared collections or undo another mod's unrelated writes.
                    foreach (int recipe in Recipes)
                        if (originalRecipes.Contains(recipe)) history.recipeUnlocked.Add(recipe); else history.recipeUnlocked.Remove(recipe);
                    foreach (int tech in Technologies)
                        if (originalTechs.TryGetValue(tech, out var state)) history.techStates[tech] = state; else history.techStates.Remove(tech);
                });
                foreach (var target in scan.Assemblers)
                {
                    var factory = target.Factory; var system = target.System; int index = target.Identity.ComponentId;
                    RemovalTargetGuard.Reset(() => { RequirePausedSession(session); target.Validate(data); }, () =>
                    {
                        var component = system.assemblerPool[index];
                        int entity = component.entityId;
                        var sign = factory.entitySignPool[entity];
                        byte[] bytes = Serialize(w => component.Export(w));
                        return () =>
                        {
                            // Rollback restores only the machine enrolled before its reset, not a replacement.
                            target.ValidateLocation(data);
                            using (var reader = new BinaryReader(new MemoryStream(bytes))) system.assemblerPool[index].Import(reader);
                            RequirePausedSession(session);
                            target.ValidateLocation(data);
                            factory.entitySignPool[entity] = sign;
                        };
                    }, rollback.Add, () =>
                    {
                        system.assemblerPool[index].SetRecipe(0, factory.entitySignPool);
                        RequirePausedSession(session);
                    });
                }
                foreach (var target in scan.Labs)
                {
                    var factory = target.Factory; var system = target.System; int index = target.Identity.ComponentId;
                    RemovalTargetGuard.Reset(() => { RequirePausedSession(session); target.Validate(data); }, () =>
                    {
                        var component = system.labPool[index];
                        int entity = component.entityId;
                        var sign = factory.entitySignPool[entity];
                        byte[] bytes = Serialize(w => component.Export(w));
                        return () =>
                        {
                            target.ValidateLocation(data);
                            using (var reader = new BinaryReader(new MemoryStream(bytes))) system.labPool[index].Import(reader);
                            RequirePausedSession(session);
                            target.ValidateLocation(data);
                            factory.entitySignPool[entity] = sign;
                        };
                    }, rollback.Add, () =>
                    {
                        system.labPool[index].SetFunction(false, 0, 0, factory.entitySignPool);
                        RequirePausedSession(session);
                    });
                }
                // Queued/current custom research was rejected in preflight. Do not invoke queue APIs whose
                // current-tech/mecha cache side effects cannot yet be safely rolled back on this target.
                foreach (int recipe in Recipes) history.recipeUnlocked.Remove(recipe);
                foreach (int tech in Technologies) history.techStates.Remove(tech);

                var remaining = Scan(data, true);
                if (remaining.Blockers.Count != 0 || remaining.Assemblers.Count != 0 || remaining.Labs.Count != 0)
                    throw new InvalidOperationException("Reference sweep did not finish cleanly: " + string.Join("; ", remaining.Blockers));
                // Saving invokes peer callbacks. Validate the same captured history on BOTH sides;
                // object identity and a custom-ID scan alone cannot establish vanilla preservation.
                RemovalHistoryGuard.VerifyAcrossSave(VerifyPreservedHistory, () => SaveNewAndVerify(candidateName, session));
                RequirePausedSession(session);
                var afterSave = Scan(data, true);
                if (afterSave.Blockers.Count != 0 || afterSave.Assemblers.Count != 0 || afterSave.Labs.Count != 0)
                    throw new InvalidOperationException("Native save callbacks reintroduced known custom references. The candidate is not accepted.");
                VerifyPreservedHistory();
                string reportDir = Path.Combine(Paths.ConfigPath, "DarkFogSynthesis", "diagnostics");
                Directory.CreateDirectory(reportDir);
                File.WriteAllText(Path.Combine(reportDir, "removal-" + token + ".json"), new JavaScriptSerializer().Serialize(new
                {
                    status = "CANDIDATE_ONLY_VANILLA_RELOAD_NOT_TESTED", backupName, candidateName,
                    assemblerReferencesCleared = scan.Assemblers.Count, labReferencesCleared = scan.Labs.Count,
                    vanillaResearchPreserved = true, knownLiveReferencesRemaining = 0,
                    inventoryHandling = "All affected buffers were empty; no item refunds, gifts or inventory edits were performed.",
                    externalBlueprints = "Not read or changed; copied external blueprints containing custom recipe IDs must not be reused without cleanup."
                }));
                RequirePausedSession(session);
                succeeded = true;
                return "Created " + backupName + " and " + candidateName + ". This is an UNVERIFIED removal candidate, not a certified vanilla-compatible save. Quit the game now; test the candidate in a separate vanilla profile. Keep the backup. Simulation remains paused.";
            }
            catch (Exception original)
            {
                var errors = new List<Exception>();
                for (int i = rollback.Count - 1; i >= 0; --i)
                {
                    // Do not invoke native rollback callbacks against a replaced or running session.
                    try { RequirePausedSession(session); } catch (Exception error) { errors.Add(error); break; }
                    try { rollback[i](); } catch (Exception error) { errors.Add(error); }
                }
                if (rollback.Count != 0 && errors.Count == 0)
                    try { RequirePausedSession(session); } catch (Exception error) { errors.Add(error); }
                if (errors.Count != 0) throw new AggregateException("Cleanup failed and in-memory rollback was incomplete. Keep the game paused and reload backup " + backupName + ". Never overwrite the original.", new[] { original }.Concat(errors));
                if (rollback.Count != 0)
                    throw new InvalidOperationException("Cleanup failed; this mod's in-memory edits were rolled back; unrelated callback writes were not undone. The game remains paused. Reload the backup before continuing. Backup: " + backupName + "; candidate (if created, do not treat as successful): " + candidateName + ". " + original.Message, original);
                throw;
            }
            finally
            {
                bool restoreRunning;
                lock (SaveGate)
                {
                    cleaning = false;
                    // Restore a failed preflight only for the same still-valid session, after the
                    // maintenance barrier is down. Never resume a replacement or pending load.
                    var current = GameMain.data;
                    restoreRunning = !succeeded && rollback.Count == 0 && session.CanRestoreRunningSession(current, current?.history,
                        current?.mainPlayer, GameMain.isLoading, HasCurrentValidatedPlayer(current));
                }
                if (restoreRunning)
                {
                    var previous = restoringPreflightSession;
                    restoringPreflightSession = session;
                    try { GameMain.Resume(); }
                    finally { restoringPreflightSession = previous; }
                }
            }
        }

        private static bool HasCurrentValidatedPlayer(GameData? data) => Plugin.Instance != null &&
            !Plugin.Instance.IsPersistenceBlocked && ReferenceEquals(GameMain.mainPlayer, data?.mainPlayer);

        private static void RequirePausedSession(MaintenanceSessionGuard session)
        {
            lock (SaveGate)
            {
                var current = GameMain.data;
                session.EnsurePausedSession(current, current?.history, current?.mainPlayer,
                    GameMain.isPaused, GameMain.isLoading, HasCurrentValidatedPlayer(current));
            }
        }

        private static ScanResult Scan(GameData data, bool requireRemoved)
        {
            var result = new ScanResult();
            // A peer may persist our recipe IDs outside native factories and history. Do not inspect or mutate
            // its private state, and do not imply that removing either plugin would clean that separate data.
            foreach (var blocker in PeerPersistenceGuard.FindBlockers(BepInEx.Bootstrap.Chainloader.PluginInfos.Keys))
                result.Blockers.Add(blocker.Reason);
            if (RemovalSafetyPolicy.HasCustomQueueReferences(data.history.currentTech, data.history.techQueue,
                data.mainPlayer.mecha.forge.tasks.Select(t => t.recipeId).ToArray()))
                result.Blockers.Add("Custom or unknown crafting/research queue state must be cleared with native controls before cleanup.");
            if (Technologies.Contains(data.history.currentTech) || data.history.techQueue.Any(Technologies.Contains))
                result.Blockers.Add("Cancel custom research in the native research queue before cleanup; active-research resource handling is not verified.");
            if (data.mainPlayer.mecha.forge.tasks.Any(t => Recipes.Contains(t.recipeId)))
                result.Blockers.Add("Finish/cancel custom handcraft tasks using the vanilla queue first; reserved-material refunds are not verified.");
            if (Recipes.Contains(BuildingParameters.clipboard.recipeId) || Recipes.Contains(BuildingParameters.template.recipeId))
                result.Blockers.Add("Clear the copied building recipe/template using vanilla controls.");
            var build = data.mainPlayer.controller.actionBuild;
            if (build.active) result.Blockers.Add("Exit build/blueprint mode before preparing a removal candidate.");
            if (HasRecipe(build.blueprintClipboard) || HasRecipe(build.blueprintPasteTool?.blueprint) || HasRecipe(build.blueprintCopyTool?.blueprint))
                result.Blockers.Add("Clear the active blueprint/clipboard. External blueprint files are not modified.");
            for (int f = 0; f < data.factoryCount; ++f)
            {
                var factory = data.factories[f];
                if (factory == null) continue;
                for (int index = 1; index < factory.prebuildCursor; ++index)
                    if (factory.prebuildPool[index].id == index && Recipes.Contains(factory.prebuildPool[index].recipeId))
                        result.Blockers.Add("Pending build on planet " + factory.planetId + " references a custom recipe; complete/cancel it first.");
                var system = factory.factorySystem;
                for (int index = 1; index < system.assemblerCursor; ++index)
                {
                    var assembler = system.assemblerPool[index];
                    if (assembler.id != index || !Recipes.Contains(assembler.recipeId)) continue;
                    result.Assemblers.Add(new RemovalTarget(factory, f, system, false,
                        new RemovalTargetIdentity(assembler.id, assembler.entityId, assembler.recipeId, 0, false, false)));
                    if (RemovalSafetyPolicy.HasProductionState(assembler.time, assembler.extraTime, assembler.cycleCount,
                        assembler.extraCycleCount, assembler.replicating, assembler.served, assembler.incServed, assembler.produced))
                        result.Blockers.Add("Drain/reset assembler " + index + " on planet " + factory.planetId + " with vanilla controls; it contains resources or production progress.");
                }
                for (int index = 1; index < system.labCursor; ++index)
                {
                    var lab = system.labPool[index];
                    if (lab.id != index || (!Recipes.Contains(lab.recipeId) && !Technologies.Contains(lab.techId))) continue;
                    result.Labs.Add(new RemovalTarget(factory, f, system, true,
                        new RemovalTargetIdentity(lab.id, lab.entityId, lab.recipeId, lab.techId, lab.researchMode, lab.matrixMode)));
                    if (RemovalSafetyPolicy.HasProductionState(lab.time, lab.extraTime, lab.cycleCount, lab.extraCycleCount,
                        lab.replicating, lab.served, lab.incServed, lab.produced)
                        || RemovalSafetyPolicy.HasResearchState(lab.hashBytes, lab.extraHashBytes, lab.matrixServed, lab.matrixIncServed))
                        result.Blockers.Add("Drain/reset lab " + index + " on planet " + factory.planetId + " with vanilla controls; it contains resources or progress.");
                }
            }
            if (requireRemoved && (data.history.recipeUnlocked.Any(Recipes.Contains) || data.history.techStates.Keys.Any(Technologies.Contains)
                || data.history.techQueue.Any(Technologies.Contains) || Technologies.Contains(data.history.currentTech))) result.Blockers.Add("Custom history references remain.");
            return result;
        }

        private static bool HasRecipe(BlueprintData? blueprint) => blueprint?.buildings != null && blueprint.buildings.Any(b => Recipes.Contains(b.recipeId));
        private static byte[] Serialize(Action<BinaryWriter> export)
        {
            using (var stream = new MemoryStream()) { using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) export(writer); return stream.ToArray(); }
        }
        private static void SaveNewAndVerify(string name, MaintenanceSessionGuard session)
        {
            RequirePausedSession(session);
            if (GameSave.SaveExist(name)) throw new IOException("Refusing to overwrite a save: " + name);
            RequirePausedSession(session);
            permittedSaveName = name;
            try { if (!GameSave.SaveCurrentGame(name)) throw new IOException("Native save creation failed: " + name); }
            finally { permittedSaveName = null; }
            RequirePausedSession(session);
            var file = new FileInfo(GameSave.SavePath(name));
            if (!file.Exists || file.Length == 0) throw new IOException("Native save did not produce a nonempty file: " + name);
        }

        /// <summary>Captured native location and configuration; each use re-reads the live component.</summary>
        private sealed class RemovalTarget
        {
            internal PlanetFactory Factory { get; }
            internal FactorySystem System { get; }
            internal RemovalTargetIdentity Identity { get; }
            private readonly int factoryIndex;
            private readonly bool isLab;
            private readonly int entityProtoId;
            private readonly int entityModelIndex;

            internal RemovalTarget(PlanetFactory factory, int factoryIndex, FactorySystem system, bool isLab,
                RemovalTargetIdentity identity)
            {
                Factory = factory; System = system; this.factoryIndex = factoryIndex; this.isLab = isLab; Identity = identity;
                if (identity.EntityId <= 0 || identity.EntityId >= factory.entityCursor || identity.EntityId >= factory.entityPool.Length)
                    throw new InvalidOperationException("Removal target has an invalid entity reference.");
                var entity = factory.entityPool[identity.EntityId];
                entityProtoId = entity.protoId; entityModelIndex = entity.modelIndex;
            }

            internal bool SameTarget(RemovalTarget other) => ReferenceEquals(Factory, other.Factory) &&
                ReferenceEquals(System, other.System) && factoryIndex == other.factoryIndex && isLab == other.isLab &&
                Identity.Equals(other.Identity) && entityProtoId == other.entityProtoId && entityModelIndex == other.entityModelIndex;

            internal void ValidateLocation(GameData data)
            {
                int index = Identity.ComponentId, entityId = Identity.EntityId;
                if (factoryIndex < 0 || factoryIndex >= data.factoryCount || factoryIndex >= data.factories.Length ||
                    !ReferenceEquals(data.factories[factoryIndex], Factory) || !ReferenceEquals(Factory.factorySystem, System) ||
                    index <= 0 || (isLab ? index >= System.labCursor || index >= System.labPool.Length :
                        index >= System.assemblerCursor || index >= System.assemblerPool.Length) ||
                    entityId <= 0 || entityId >= Factory.entityCursor || entityId >= Factory.entityPool.Length ||
                    entityId >= Factory.entitySignPool.Length)
                    throw new InvalidOperationException("Removal target factory or pool location changed; refusing to overwrite a replacement.");
                var entity = Factory.entityPool[entityId];
                bool componentMatches = isLab ? System.labPool[index].id == index && System.labPool[index].entityId == entityId :
                    System.assemblerPool[index].id == index && System.assemblerPool[index].entityId == entityId;
                if (!componentMatches || entity.id != entityId || entity.protoId != entityProtoId || entity.modelIndex != entityModelIndex ||
                    (isLab ? entity.labId != index : entity.assemblerId != index))
                    throw new InvalidOperationException("Removal target entity association changed; refusing to overwrite a replacement.");
            }

            internal void Validate(GameData data)
            {
                ValidateLocation(data);
                int index = Identity.ComponentId;
                if (isLab)
                {
                    var lab = System.labPool[index];
                    RemovalTargetGuard.EnsureUnchanged(Identity,
                        new RemovalTargetIdentity(lab.id, lab.entityId, lab.recipeId, lab.techId, lab.researchMode, lab.matrixMode),
                        Recipes.Contains(lab.recipeId) || Technologies.Contains(lab.techId),
                        RemovalSafetyPolicy.HasProductionState(lab.time, lab.extraTime, lab.cycleCount, lab.extraCycleCount,
                            lab.replicating, lab.served, lab.incServed, lab.produced) ||
                        RemovalSafetyPolicy.HasResearchState(lab.hashBytes, lab.extraHashBytes, lab.matrixServed, lab.matrixIncServed));
                }
                else
                {
                    var assembler = System.assemblerPool[index];
                    RemovalTargetGuard.EnsureUnchanged(Identity,
                        new RemovalTargetIdentity(assembler.id, assembler.entityId, assembler.recipeId, 0, false, false),
                        Recipes.Contains(assembler.recipeId),
                        RemovalSafetyPolicy.HasProductionState(assembler.time, assembler.extraTime, assembler.cycleCount,
                            assembler.extraCycleCount, assembler.replicating, assembler.served, assembler.incServed, assembler.produced));
                }
            }
        }

        private sealed class ScanResult
        {
            internal List<string> Blockers { get; } = new List<string>();
            internal List<RemovalTarget> Assemblers { get; } = new List<RemovalTarget>();
            internal List<RemovalTarget> Labs { get; } = new List<RemovalTarget>();
            internal bool SameTargets(ScanResult other) => SameTargets(Assemblers, other.Assemblers) && SameTargets(Labs, other.Labs);
            private static bool SameTargets(List<RemovalTarget> left, List<RemovalTarget> right) => left.Count == right.Count &&
                left.Zip(right, (a, b) => a.SameTarget(b)).All(same => same);
        }
    }
}
