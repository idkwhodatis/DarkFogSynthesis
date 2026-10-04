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
using DarkFogSynthesis.Core.Progression;
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
        private readonly ContentRegistry registry = new ContentRegistry();
        internal RuntimeProgression Progression { get; } = new RuntimeProgression();
        internal bool Ready => registry.Ready && fatal == null && !SafeRemovalService.IsQuarantined;
        private Harmony harmony = null!;
        private ConfigEntry<bool> nonPeaceSetting = null!;
        private bool nonPeaceAtStartup;
        private string? fatal;
        private string status = "Experimental build. Gameplay, saves, achievements and integrity are not yet validated. Use an isolated profile and copied saves only.";
        private bool showDiagnostics = true;
        private bool confirmRemoval;
        private bool cleanupCandidate;
        private Rect window = new Rect(20, 20, 560, 260);

        private void Awake()
        {
            Instance = this;
            nonPeaceSetting = Config.Bind("Progression", "ApplyCombatPrerequisitesInNonPeaceMode", false,
                "Also apply the four added combat prerequisites to non-Peace saves. Peace saves always use them. Restart required. / 是否在非和平存档中也启用四项额外战斗前置；和平存档始终启用。修改后重启游戏。");
            nonPeaceAtStartup = nonPeaceSetting.Value;
            try
            {
                Localization.Strings.Register();
                LDBTool.PreAddDataAction += RegisterContent;
                ProtoRegistry.onLoadingFinished += BindContent;
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(Plugin).Assembly);
                Logger.LogWarning(status);
            }
            catch (Exception error) { Fail(error); }
        }

        private void RegisterContent()
        {
            try { registry.Register(); }
            catch (Exception error) { Fail(error); throw; }
        }

        private void BindContent()
        {
            try
            {
                registry.BindAndValidate();
                status = "Registered 2 technologies and 6 recipes. This game layout, discovery behavior and save cleanup remain unverified; use copied diagnostic saves only.";
                Logger.LogInfo(status);
                Logger.LogInfo("Runtime diagnostic snapshot: " + CompatibilityReport.Export(Ready, fatal));
            }
            catch (Exception error) { Fail(error); throw; }
        }

        internal void EnsureReady()
        {
            if (!registry.Ready || fatal != null) throw new InvalidOperationException("DarkFogSynthesis cannot safely enter this save: " + (fatal ?? "prototype initialization did not finish"));
            registry.Validate();
            registry.ValidateExecutionCache();
            NativeRecipeCompatibility.ValidateSupportedMatrixRegistry();
        }

        internal void ApplyMode(bool peace)
        {
            EnsureReady();
            Progression.Apply(peace, nonPeaceAtStartup);
            cleanupCandidate = false;
        }

        internal void ValidateLoadedMachines(GameData data) => registry.ValidateSavedRecipeCaches(data);
        internal void ValidateImportedAssembler(AssemblerComponent machine) => registry.ValidateImportedAssembler(machine);
        internal void ValidateImportedLab(LabComponent machine) => registry.ValidateImportedLab(machine);

        internal void AbortSession(Exception error)
        {
            try { Progression.Restore(); }
            catch (Exception restoreError) { Logger.LogError(restoreError); }
            Fail(error);
        }

        internal void DiagnoseLateConflicts()
        {
            NativeRecipeCompatibility.ValidateSupportedMatrixRegistry();
            var required = FrozenContent.Technologies.SelectMany(t => t.ExplicitPrerequisites.Concat(t.ImplicitPrerequisites));
            if (GameMain.data != null && ProgressionPolicy.ShouldApply(GameMain.data.gameDesc.isPeaceMode, nonPeaceAtStartup))
                required = required.Concat(FrozenContent.CombatPrerequisites.Select(e => e.RequiredCombatTech));
            var unavailable = required.Distinct().Where(id => LDB.techs.Select(id.Value) == null ||
                !LDB.techs.Select(id.Value).Published || LDB.techs.Select(id.Value).IsObsolete).Select(id => id.ToString()).ToArray();
            if (unavailable.Length == 0) return;
            status = "CONFLICT: another mod disabled required technologies: " + string.Join(", ", unavailable) +
                ". Review that mod's combat-technology settings, then restart. No technologies or other mod settings were changed. / 前置科技被其他 Mod 禁用，请检查配置并重启。";
            showDiagnostics = true;
            Logger.LogError(status);
            GameMain.Pause();
        }

        private void Fail(Exception error)
        {
            fatal = error.Message;
            status = "BLOCKED: " + fatal;
            showDiagnostics = true;
            Logger.LogError(error);
        }

        private void OnGUI()
        {
            if (!showDiagnostics)
            {
                if (GUI.Button(new Rect(12, 12, 190, 28), "Dark Fog Synthesis [test]")) showDiagnostics = true;
                return;
            }
            window = GUILayout.Window(195148101, window, DrawDiagnostics, "Dark Fog Synthesis 0.1.0 · EXPERIMENTAL");
        }

        private void DrawDiagnostics(int id)
        {
            GUILayout.Label(status);
            if (nonPeaceSetting.Value != nonPeaceAtStartup) GUILayout.Label("Progression setting changed: restart the game for it to take effect.");
            if (GUILayout.Button("Export runtime diagnostics / 导出运行时诊断"))
            {
                try { status = "Diagnostics saved: " + CompatibilityReport.Export(Ready, fatal); Logger.LogInfo(status); }
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
            LDBTool.PreAddDataAction -= RegisterContent;
            ProtoRegistry.onLoadingFinished -= BindContent;
            try { Progression.Restore(); } catch (Exception error) { Logger.LogError(error); }
            NativeRecipeCompatibility.Dispose();
            harmony?.UnpatchSelf();
            Localization.Strings.Dispose();
        }
    }
}
