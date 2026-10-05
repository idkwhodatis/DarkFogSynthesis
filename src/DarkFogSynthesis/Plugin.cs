using System;
using BepInEx;
using BepInEx.Configuration;
using CommonAPI;
using CommonAPI.Systems;
using DarkFogSynthesis.Compatibility;
using DarkFogSynthesis.Diagnostics;
using DarkFogSynthesis.Progression;
using DarkFogSynthesis.Registration;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Diagnostics;
using DarkFogSynthesis.Core.Progression;
using DarkFogSynthesis.Core.Compatibility;
using DarkFogSynthesis.Core.Registration;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using xiaoye97;

namespace DarkFogSynthesis
{
    [BepInPlugin(Guid, "Dark Fog Synthesis", Version)]
    [BepInDependency("me.xiaoye97.plugin.Dyson.LDBTool", "3.0.3")]
    [BepInDependency(CommonAPIPlugin.GUID, "1.6.7")]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(TabSystem))]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "idkwhodatis.darkfogsynthesis";
        public const string Version = "0.1.0";
        internal static Plugin Instance { get; private set; } = null!;
        // No runtime/content constructors run before the localization-independent critical barriers.
        private ContentRegistry registry = null!;
        private readonly SessionCompatibilityState compatibility = new SessionCompatibilityState();
        private SessionFailureBoundary? failureBoundary;
        internal SessionFailureBoundary FailureBoundary => failureBoundary ??= new SessionFailureBoundary(
            compatibility, () => { if (GameMain.data != null && GameMain.isRunning) GameMain.Pause(); },
            reason => { status = "BLOCKED: " + reason; confirmRemoval = false; showDiagnostics = true; },
            error => Logger.LogError(error));
        internal RuntimeProgression Progression { get; private set; } = null!;
        internal bool Ready => registry?.Ready == true && !IsPersistenceBlocked && !SafeRemovalService.IsQuarantined;
        internal bool IsCompatibilityBlocked => compatibility.IsBlocked;
        internal bool IsPersistenceBlocked => !StartupGuardEntrypoints.State.AllowsGameOperations || !compatibility.CanPersist(GameMain.data);
        // Preserve native Begin's existing resume behavior; persistence has a stricter identity gate.
        internal bool IsResumeBlocked => !StartupGuardEntrypoints.State.AllowsGameOperations || compatibility.IsBlocked;
        private string? BlockingReason => fatal ?? compatibility.BlockReason;
        private Harmony harmony = null!;
        private ConfigEntry<bool>? nonPeaceSetting;
        private bool nonPeaceAtStartup;
        private ConfigEntry<bool>? traceSnapshots;
        private string? fatal => StartupGuardEntrypoints.State.FailureReason;
        private string status = "Experimental build. Gameplay, saves, achievements and integrity are not yet validated. Use an isolated profile and copied saves only.";
        private bool showDiagnostics = true;
        private bool confirmRemoval;
        private bool cleanupCandidate;
        private GUI.WindowFunction? drawDiagnostics;
        private Rect window = new Rect(20, 20, 560, 260);

        private void Awake()
        {
            Instance = this;
            try
            {
                StartupGuardEntrypoints.State.Initialize(StartupGuardInstaller.InstallAndVerify, () =>
                {
                    registry = new ContentRegistry();
                    Progression = new RuntimeProgression();
                    nonPeaceSetting = Config.Bind("Progression", "ApplyCombatPrerequisitesInNonPeaceMode", false,
                        "Also apply the four added combat prerequisites to non-Peace saves. Peace saves always use them. Restart required. / 是否在非和平存档中也启用四项额外战斗前置；和平存档始终启用。修改后重启游戏。");
                    nonPeaceAtStartup = nonPeaceSetting.Value;
                    // Optional diagnostics cannot prevent startup when their configuration is unavailable.
                    try { traceSnapshots = Config.Bind("Diagnostics", "TraceSnapshots", false,
                        "Opt-in phase-labelled prototype snapshots. No game acceptance is implied; restart to capture registration. / 按阶段导出原型诊断，默认关闭。重启以捕获注册阶段。"); }
                    catch (Exception diagnosticError) { WarnDiagnostic(diagnosticError); }
                    Localization.Strings.Register();
                    harmony = new Harmony(Guid);
                    harmony.PatchAll(typeof(Plugin).Assembly);
                    StartupGuardInstaller.Verify();
                    // Subscribe only after all initialization/patching succeeds. Both callbacks recheck the
                    // gate, so a partial subscription or a later failure cannot start prototype mutation.
                    LDBTool.PreAddDataAction += RegisterContent;
                    ProtoRegistry.onLoadingFinished += BindContent;
                });
                Logger.LogWarning(status);
            }
            catch (Exception error) { Fail(error); UnsubscribeContentCallbacks(); }
        }

        private void RegisterContent()
        {
            try
            {
                StartupGuardEntrypoints.State.EnsureInitializationComplete();
                StartupGuardInstaller.Verify();
                TraceSnapshot("pre-register");
                registry.Register();
            }
            catch (Exception error) { Fail(error); throw; }
        }

        private void BindContent()
        {
            RegistrationLifecycle.BindAndDiagnose(() => {
                StartupGuardEntrypoints.State.EnsureInitializationComplete();
                StartupGuardInstaller.Verify();
                registry.BindAndValidate();
                StartupGuardEntrypoints.State.MarkContentReady();
                status = "Registered 2 technologies and 6 recipes. This game layout, discovery behavior and save cleanup remain unverified; use copied diagnostic saves only.";
                Logger.LogInfo(status);
            }, Fail,
                () => Logger.LogInfo("Runtime diagnostic snapshot: " + CompatibilityReport.Export(registry.Ready, Ready, BlockingReason, "post-bind", null, nonPeaceAtStartup)),
                error => {
                    status += " Automatic diagnostic export failed: " + error.Message;
                    Logger.LogWarning("Content registration succeeded, but automatic diagnostics could not be exported: " + error);
                });
        }

        internal void EnsureReady()
        {
            EnsureRegistryReady();
            if (IsPersistenceBlocked || SafeRemovalService.IsQuarantined)
                throw new InvalidOperationException("DarkFogSynthesis cannot safely use this session: " +
                    (compatibility.BlockReason ?? "this session is not fully validated or is removal-quarantined; load a different valid session"));
        }

        // Preparing a replacement must remain possible while the old session's block is latched.
        internal void EnsureRegistryReady()
        {
            StartupGuardEntrypoints.State.EnsureGameOperationsAllowed();
            StartupGuardInstaller.Verify();
            if (registry?.Ready != true) throw new InvalidOperationException("DarkFogSynthesis cannot safely enter this save: prototype initialization did not finish");
            registry.Validate();
            registry.ValidateExecutionCache();
            NativeRecipeCompatibility.ValidateSupportedMatrixRegistry();
            RuntimeCompatibilityGuard.ValidateSupportedPeers();
        }

        internal void ApplyMode(bool peace, GameData data)
        {
            EnsureRegistryReady();
            TraceSnapshot("pre-mode", data, peace);
            Progression.Apply(peace, nonPeaceAtStartup);
            TraceSnapshot("post-mode", data, peace);
            cleanupCandidate = false;
        }

        internal void RestoreProgression(GameData? data)
        {
            Progression?.Restore();
            TraceSnapshot("post-restore", data);
        }

        internal void TraceSnapshot(string stage, GameData? data = null, bool? peace = null)
        {
            // No exporter, database scan, reflection or file I/O on the disabled path.
            OptionalDiagnostic.TryCapture(traceSnapshots?.Value == true,
                () => CompatibilityReport.Export(registry?.Ready == true, Ready, BlockingReason, stage, data, nonPeaceAtStartup, peace),
                WarnDiagnostic);
        }

        private void WarnDiagnostic(Exception error)
        {
            try { Logger.LogWarning("Optional diagnostic failed: " + error.GetType().Name); }
            catch (Exception) { } // Reporting failure is never a compatibility failure.
        }

        internal void ValidateLoadedMachines(GameData data) => registry.ValidateSavedRecipeCaches(data);
        internal void ValidateActiveProgression(GameData data) => Progression.ValidateLive(data.gameDesc.isPeaceMode, nonPeaceAtStartup);
        internal void ValidateImportedAssembler(AssemblerComponent machine) => registry.ValidateImportedAssembler(machine);
        internal void ValidateImportedLab(LabComponent machine) => registry.ValidateImportedLab(machine);

        internal void BeginSession(GameData data) => compatibility.BeginSession(data);
        internal void EnsureSessionCanBegin(GameData data) => compatibility.EnsureCanBegin(data);
        internal object BeginSessionValidation(GameData data) => compatibility.BeginValidation(data);
        internal void EndSessionValidation(object? token) => compatibility.EndValidation(token);
        internal void EndSession(GameData data) => compatibility.EndSession(data);
        internal void CompleteValidatedSession(GameData data)
        {
            bool wasBlocked = compatibility.IsBlocked;
            compatibility.CompleteValidatedSession(data);
            if (wasBlocked)
                status = "The new session passed compatibility checks. This experimental build still requires copied diagnostic saves; gameplay and removal remain unverified.";
        }

        internal void RejectSession(GameData? data, Exception error)
        {
            FailureBoundary.Reject(data, error);
        }

        internal void AbortSession(GameData? data, Exception error, Action? cleanup = null)
        {
            FailureBoundary.Abort(data, error, () => RestoreProgression(data), cleanup ?? (() => { }));
        }

        internal bool DiagnoseLateConflicts()
        {
            try
            {
                EnsureRegistryReady();
                if (GameMain.data != null) ValidateActiveProgression(GameMain.data);
                var required = FrozenContent.Technologies.SelectMany(t => t.ExplicitPrerequisites.Concat(t.ImplicitPrerequisites));
                if (GameMain.data != null && ProgressionPolicy.ShouldApply(GameMain.data.gameDesc.isPeaceMode, nonPeaceAtStartup))
                    required = required.Concat(FrozenContent.CombatPrerequisites.Select(e => e.RequiredCombatTech));
                var unavailable = required.Distinct().Where(id => LDB.techs.Select(id.Value) == null ||
                    !LDB.techs.Select(id.Value).Published || LDB.techs.Select(id.Value).IsObsolete).Select(id => id.ToString()).ToArray();
                if (unavailable.Length == 0) return true;
                BlockSession(GameMain.data, "CONFLICT: another mod disabled required technologies: " + string.Join(", ", unavailable) +
                    ". Review that mod's combat-technology settings, then restart. No technologies or other mod settings were changed. / 前置科技被其他 Mod 禁用，请检查配置并重启。");
            }
            catch (Exception error) { RejectSession(GameMain.data, error); }
            return false;
        }

        private void BlockSession(GameData? data, string reason)
        {
            RejectSession(data, new InvalidOperationException(reason));
        }

        private void Fail(Exception error)
        {
            StartupGuardEntrypoints.State.Fail(error);
            status = "BLOCKED: " + fatal;
            confirmRemoval = false;
            showDiagnostics = true;
            Logger.LogError(error);
            // Best effort only: missing critical Harmony hooks cannot be replaced by a status flag or pause.
            // Do not auto-quit or claim shutdown/save safety; keep the incomplete-coverage warning visible.
            try { if (GameMain.data != null && GameMain.isRunning) GameMain.Pause(); }
            catch (Exception pauseError) { Logger.LogError(pauseError); }
        }

        private void OnGUI()
        {
            if (!showDiagnostics)
            {
                if (GUI.Button(new Rect(12, 12, 190, 28), "Dark Fog Synthesis [test]")) showDiagnostics = true;
                return;
            }
            window = GUILayout.Window(195148101, window, drawDiagnostics ??= DrawDiagnostics, "Dark Fog Synthesis 0.1.0 · EXPERIMENTAL");
        }

        private void DrawDiagnostics(int id)
        {
            // Export/preview messages must never hide the persistent reason that resuming is refused.
            if (BlockingReason != null) GUILayout.Label("BLOCKED: " + BlockingReason);
            if (BlockingReason == null || status != "BLOCKED: " + BlockingReason) GUILayout.Label(status);
            if (nonPeaceSetting != null && nonPeaceSetting.Value != nonPeaceAtStartup) GUILayout.Label("Progression setting changed: restart the game for it to take effect.");
            if (GUILayout.Button("Export runtime diagnostics / 导出运行时诊断"))
            {
                try { status = "Diagnostics saved: " + CompatibilityReport.Export(registry?.Ready == true, Ready, BlockingReason, "manual", GameMain.data, nonPeaceAtStartup); Logger.LogInfo(status); }
                catch (Exception error) { status = "Diagnostic export failed: " + error.Message; Logger.LogError(error); }
            }
            if (Ready && GameMain.data != null && !GameMain.isLoading && !cleanupCandidate)
            {
                if (GUILayout.Button("Preview removal blockers / 检查移除阻断项"))
                {
                    try { status = SafeRemovalService.Preview(); }
                    catch (Exception error) { status = "Preflight could not inspect the current session: " + error.Message; Logger.LogError(error); }
                }
                if (!confirmRemoval && GUILayout.Button("Prepare removal candidate… / 准备移除候选存档…")) confirmRemoval = true;
                if (confirmRemoval)
                {
                    GUILayout.Label("Creates a unique backup and separate experimental cleanup save, removes only this mod's IDs. Requires drained machines and cleared custom handcraft tasks/blueprints. Vanilla reload is NOT certified. External blueprint files are untouched.");
                    if (GUILayout.Button("Confirm backup and create candidate / 确认备份并另存候选"))
                    {
                        confirmRemoval = false;
                        try { status = SafeRemovalService.PrepareCandidate(); cleanupCandidate = true; }
                        catch (Exception error) { status = "Cleanup stopped: " + error.Message; Logger.LogError(error); }
                    }
                    if (GUILayout.Button("Cancel / 取消")) confirmRemoval = false;
                }
            }
            if (GUILayout.Button("Hide diagnostics / 收起诊断")) showDiagnostics = false;
            GUI.DragWindow();
        }

        private void OnDestroy()
        {
            // Removing functional patches after startup failure or live disable must never reopen saves.
            // The separate critical owner is deliberately process-lifetime and remains installed/closed.
            Fail(new InvalidOperationException("DarkFogSynthesis was disabled or destroyed. Restart the game before loading, resuming or saving."));
            UnsubscribeContentCallbacks();
            try { Progression?.Restore(); } catch (Exception error) { Logger.LogError(error); }
            NativeRecipeCompatibility.Dispose();
            harmony?.UnpatchSelf();
            Localization.Strings.Dispose();
        }

        private void UnsubscribeContentCallbacks()
        {
            try { LDBTool.PreAddDataAction -= RegisterContent; }
            catch (Exception error) { Logger.LogError(error); }
            try { ProtoRegistry.onLoadingFinished -= BindContent; }
            catch (Exception error) { Logger.LogError(error); }
        }
    }
}
