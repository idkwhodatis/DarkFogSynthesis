using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DarkFogSynthesis.Core.Compatibility;
using HarmonyLib;

namespace DarkFogSynthesis.Compatibility
{
    /// <summary>Small localization-independent safety owner, intentionally retained until process exit.</summary>
    internal static class StartupGuardInstaller
    {
        internal const string Owner = Plugin.Guid + ".startup-safety";
        // Run before this mod's ordinary session prefixes (Priority.First) and known load sanitizers.
        private const int GuardPriority = int.MaxValue;

        internal static void InstallAndVerify()
        {
            var harmony = new Harmony(Owner);
            CriticalGuardInstallation.InstallAndVerify(Steps(harmony));
        }

        internal static void Verify()
        {
            try { CriticalGuardInstallation.Verify(Steps(null)); }
            catch (Exception error) { StartupGuardEntrypoints.State.Fail(error); throw; }
        }

        private static IEnumerable<CriticalGuardStep> Steps(Harmony? harmony)
        {
            yield return Step(harmony, typeof(GameSave), nameof(GameSave.LoadCurrentGame), typeof(bool), new[] { typeof(string) }, nameof(StartupGuardEntrypoints.BeforeLoad));
            yield return Step(harmony, typeof(GameSave), nameof(GameSave.LoadCurrentGameInResource), typeof(bool), new[] { typeof(int) }, nameof(StartupGuardEntrypoints.BeforeLoad));
            yield return Step(harmony, typeof(GameData), nameof(GameData.Import), typeof(void), new[] { typeof(BinaryReader) }, nameof(StartupGuardEntrypoints.BeforeSessionEntry));
            yield return Step(harmony, typeof(GameData), nameof(GameData.NewGame), typeof(bool), new[] { typeof(GameDesc) }, nameof(StartupGuardEntrypoints.BeforeLoad));
            yield return Step(harmony, typeof(GameMain), nameof(GameMain.Begin), typeof(void), Type.EmptyTypes, nameof(StartupGuardEntrypoints.BeforeSessionEntry));
            yield return Step(harmony, typeof(GameMain), nameof(GameMain.Resume), typeof(void), Type.EmptyTypes, nameof(StartupGuardEntrypoints.BeforeResume));
            yield return Step(harmony, typeof(GameSave), nameof(GameSave.SaveCurrentGame), typeof(bool), new[] { typeof(string) }, nameof(StartupGuardEntrypoints.BeforeSave));
            yield return Step(harmony, typeof(GameSave), nameof(GameSave.AutoSave), typeof(bool), Type.EmptyTypes, nameof(StartupGuardEntrypoints.BeforeSave));
            yield return Step(harmony, typeof(GameSave), nameof(GameSave.AutoSaveAfterErrored), typeof(bool), Type.EmptyTypes, nameof(StartupGuardEntrypoints.BeforeSave));
            yield return Step(harmony, typeof(GameSave), nameof(GameSave.SaveAsLastExit), typeof(bool), Type.EmptyTypes, nameof(StartupGuardEntrypoints.BeforeSave));
        }

        private static CriticalGuardStep Step(Harmony? harmony, Type type, string name, Type returns, Type[] parameters, string prefixName)
        {
            // Resolution stays inside each independent action: one missing signature must not skip later guards.
            MethodInfo Target()
            {
                var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                    null, parameters, null);
                if (method == null || method.ReturnType != returns || method.DeclaringType != type ||
                    method.IsStatic != (type != typeof(GameData)))
                    throw new MissingMethodException(type.FullName, name);
                return method;
            }
            MethodInfo Prefix() => typeof(StartupGuardEntrypoints).GetMethod(prefixName, BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(StartupGuardEntrypoints).FullName, prefixName);
            return new CriticalGuardStep(type.FullName + "." + name,
                () => {
                    if (harmony == null) throw new InvalidOperationException("No critical patch installer is available.");
                    harmony.Patch(Target(), prefix: new HarmonyMethod(Prefix()) { priority = GuardPriority, before = new[] { Plugin.Guid } });
                },
                () => {
                    var patches = Harmony.GetPatchInfo(Target());
                    MethodInfo expected = Prefix();
                    if (patches == null || !patches.Prefixes.Any(patch => patch.owner == Owner && patch.PatchMethod == expected &&
                        patch.priority == GuardPriority && patch.before.Contains(Plugin.Guid)))
                        throw new InvalidOperationException("The exact critical prefix, owner and early priority could not be verified.");
                });
        }
    }
}
