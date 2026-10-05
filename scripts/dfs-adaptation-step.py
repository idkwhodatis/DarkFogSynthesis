#!/usr/bin/env python3
"""Temporary Patch B integration; no gameplay/state changes beyond observation."""
from pathlib import Path
import os
import shutil
import subprocess
os.chdir(Path(__file__).resolve().parents[1])
excluded=['scripts/dfs-adaptation*','.github/workflows/dfs-adaptation.yml','.github/workflows/ci.yml','scripts/compare-runtime-snapshots.py','scripts/test-runtime-snapshots.py','src/DarkFogSynthesis.Core/Diagnostics/OptionalDiagnostic.cs','tests/DarkFogSynthesis.Core.Tests/OptionalDiagnosticTests.cs']
subprocess.run(['git','diff','--exit-code','a28d95b2bcef75620b32659adb2e98b254dd18e5','HEAD','--','.',*(':(exclude)'+p for p in excluded)],check=True)
changed=[]
def edit(name,old,new):
    p=Path(name);s=p.read_text(encoding='utf-8')
    if s.count(old)!=1:raise ValueError('Unexpected source anchor: '+name+' '+old[:60])
    p.write_text(s.replace(old,new),encoding='utf-8',newline='\n');changed.append(name)
report='src/DarkFogSynthesis/Diagnostics/CompatibilityReport.cs'
shutil.copyfile('scripts/dfs-adaptation-report.cs.txt',report);changed.append(report)
edit('src/DarkFogSynthesis/Plugin.cs','using DarkFogSynthesis.Core.Definitions;','using DarkFogSynthesis.Core.Definitions;\nusing DarkFogSynthesis.Core.Diagnostics;')
edit('src/DarkFogSynthesis/Plugin.cs','        private bool nonPeaceAtStartup;','        private bool nonPeaceAtStartup;\n        private ConfigEntry<bool>? traceSnapshots;')
edit('src/DarkFogSynthesis/Plugin.cs','                    nonPeaceAtStartup = nonPeaceSetting.Value;','''                    nonPeaceAtStartup = nonPeaceSetting.Value;
                    // Optional diagnostics cannot prevent startup when their configuration is unavailable.
                    try { traceSnapshots = Config.Bind("Diagnostics", "TraceSnapshots", false,
                        "Opt-in phase-labelled prototype snapshots. No game acceptance is implied; restart to capture registration. / 按阶段导出原型诊断，默认关闭。重启以捕获注册阶段。"); }
                    catch (Exception diagnosticError) { WarnDiagnostic(diagnosticError); }''')
edit('src/DarkFogSynthesis/Plugin.cs','                registry.Register();','                TraceSnapshot("pre-register");\n                registry.Register();')
edit('src/DarkFogSynthesis/Plugin.cs','CompatibilityReport.Export(registry.Ready, Ready, BlockingReason)','CompatibilityReport.Export(registry.Ready, Ready, BlockingReason, "post-bind", null, nonPeaceAtStartup)')
edit('src/DarkFogSynthesis/Plugin.cs','''        internal void ApplyMode(bool peace)
        {
            EnsureRegistryReady();
            Progression.Apply(peace, nonPeaceAtStartup);
            cleanupCandidate = false;
        }''','''        internal void ApplyMode(bool peace, GameData data)
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
        }''')
edit('src/DarkFogSynthesis/Plugin.cs','() => Progression?.Restore(), cleanup','() => RestoreProgression(data), cleanup')
edit('src/DarkFogSynthesis/Plugin.cs','CompatibilityReport.Export(registry?.Ready == true, Ready, BlockingReason)','CompatibilityReport.Export(registry?.Ready == true, Ready, BlockingReason, "manual", GameMain.data, nonPeaceAtStartup)')
edit('src/DarkFogSynthesis/Compatibility/SessionPatches.cs','Plugin.Instance.ApplyMode(__instance.isPeaceMode);','Plugin.Instance.ApplyMode(__instance.isPeaceMode, importing);')
edit('src/DarkFogSynthesis/Compatibility/SessionPatches.cs','Plugin.Instance.ApplyMode(_gameDesc.isPeaceMode);','Plugin.Instance.ApplyMode(_gameDesc.isPeaceMode, __instance);')
edit('src/DarkFogSynthesis/Compatibility/SessionPatches.cs','            Plugin.Instance.ValidateLoadedMachines(GameMain.data);','            Plugin.Instance.ValidateLoadedMachines(GameMain.data);\n            Plugin.Instance.TraceSnapshot("execution-cache-ready", GameMain.data);')
edit('src/DarkFogSynthesis/Compatibility/SessionPatches.cs','            finally { Plugin.Instance.EndSessionValidation(__state); }','''            finally
            {
                Plugin.Instance.EndSessionValidation(__state);
                if (Plugin.Instance.Ready) Plugin.Instance.TraceSnapshot("session-ready", GameMain.data);
            }''')
edit('src/DarkFogSynthesis/Compatibility/SessionPatches.cs','                Plugin.Instance.Progression.Restore();','                Plugin.Instance.RestoreProgression(session);')
edit('src/DarkFogSynthesis/Compatibility/SessionPatches.cs','try { Plugin.Instance.Progression.Restore(); }','try { Plugin.Instance.RestoreProgression(data); }')
edit('tests/DarkFogSynthesis.Core.Tests/Program.cs','                ("D02 fixed namespace and content counts", FixedIds),','''                ("B01 opt-in diagnostics remain outside session state transitions",
                    () => OptionalDiagnosticTests.Run((condition,message) => { assertions++; if (!condition) throw new Exception(message); })),
                ("D02 fixed namespace and content counts", FixedIds),''')
edit('scripts/Build.ps1','    & $Python scripts/test-packaging.py',"    & $Python scripts/test-runtime-snapshots.py\n    if ($LASTEXITCODE -ne 0) { throw 'Runtime snapshot comparator regressions failed.' }\n    & $Python scripts/test-packaging.py")
p=Path('docs/compatibility/content-adaptation.md')
with p.open('a',encoding='utf-8') as stream:
    stream.write('''

## Patch B: phase-aware observations and comparison

`Diagnostics.TraceSnapshots=false` invokes no exporter at the new lifecycle checkpoints. Enable it before startup for `pre-register`, `pre-mode`, `post-mode`, `execution-cache-ready`, `session-ready`, and `post-restore`. The existing warning-only automatic export is labelled `post-bind`, not execution-cache readiness. `session-ready` follows release of the native Begin token. Schema 3 includes random process IDs, weakly held per-session IDs, startup policy, copied arrays, item crafting lists/fallbacks and technology caches. Save names and installation paths are not exported. Manual export while paused in a validated current session additionally observes owned machines and special-item discovery; other stages explicitly omit those live observations.

Commands:

```sh
python scripts/compare-runtime-snapshots.py before.json after.json --phase registration
python scripts/compare-runtime-snapshots.py pre-mode.json post-mode.json --phase mode
python scripts/compare-runtime-snapshots.py post-mode.json post-restore.json --phase restore --baseline pre-mode.json
python scripts/compare-runtime-snapshots.py a.json b.json --phase equal --output new-report.json
```

The independent oracle includes Titanium Alloy (1107) ×2. Restoration compares with the same session's pre-mode, already-registered baseline, not pre-registration. Existing foreign fallbacks and prerequisites are preserved. Duplicate IDs, missing baselines, malformed caches and differing process/build environments are refused. Exit 0 is only a structural match of exported fields; exit 1 reports drift; exit 2 refuses input. No acceptance flag or save is written. Other mods/framework changes between checkpoints deliberately appear as drift and are not automatically attributed to DFS. Native object-identity checks remain authoritative. Output reports are create-only. No background or production-tick scan was added.
''')
changed.append(str(p))
subprocess.run(['git','add','--',*set(changed)],check=True)
with open(os.environ['GITHUB_OUTPUT'],'a',encoding='utf-8') as stream:stream.write('message=Add opt-in lifecycle snapshots and strict structural comparison\n')
