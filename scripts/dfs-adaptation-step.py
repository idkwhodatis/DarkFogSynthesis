#!/usr/bin/env python3
"""Temporary bounded Patch C integration. No save, discovery or native research-state edits."""
from pathlib import Path
import os
import subprocess
os.chdir(Path(__file__).resolve().parents[1])
excluded=['scripts/dfs-adaptation*','.github/workflows/dfs-adaptation.yml','scripts/check-ui-layout.py','scripts/test-ui-layout.py','src/DarkFogSynthesis/Diagnostics/UiLayoutCapture.cs','src/DarkFogSynthesis/Localization/Strings.en-US.json','src/DarkFogSynthesis/Localization/Strings.zh-CN.json']
subprocess.run(['git','diff','--exit-code','18346e0c6b83fb4912cc0f4691572e1766d662e9','HEAD','--','.',*(':(exclude)'+p for p in excluded)],check=True)
changed=[]
def edit(name,old,new):
    p=Path(name);s=p.read_text(encoding='utf-8')
    if s.count(old)!=1:raise ValueError('Unexpected source anchor: '+name+' '+old[:60])
    p.write_text(s.replace(old,new),encoding='utf-8',newline='\n');changed.append(name)
edit('src/DarkFogSynthesis/Diagnostics/CompatibilityReport.cs','bool? peaceOverride = null)','bool? peaceOverride = null, object? uiLayout = null)')
edit('src/DarkFogSynthesis/Diagnostics/CompatibilityReport.cs','machines = inspectLive ? ObserveMachines(data!) : Array.Empty<object>()','machines = inspectLive ? ObserveMachines(data!) : Array.Empty<object>(),\n                uiLayout')
edit('src/DarkFogSynthesis/Plugin.cs','        private bool showDiagnostics = true;','        private bool showDiagnostics = true;\n        private float uiCaptureAt = -1f;')
edit('src/DarkFogSynthesis/Plugin.cs','        private void OnGUI()','''        // A single explicit capture request gives the user time to return the pointer to a
        // hovered/expanded node. No enumeration, hashing or I/O occurs without that request.
        private void LateUpdate()
        {
            if (uiCaptureAt < 0f || UnityEngine.Time.realtimeSinceStartup < uiCaptureAt) return;
            uiCaptureAt = -1f;
            try { status = "UI observation saved: " + CompatibilityReport.Export(registry?.Ready == true, Ready,
                BlockingReason, "manual", GameMain.data, nonPeaceAtStartup, null, UiLayoutCapture.Capture()); }
            catch (Exception error) { status = "UI observation unavailable."; WarnDiagnostic(error); }
            showDiagnostics = true;
        }

        private void OnGUI()''')
edit('src/DarkFogSynthesis/Plugin.cs','            if (Ready && GameMain.data != null && !GameMain.isLoading && !cleanupCandidate)','''            if (GUILayout.Button("Capture UI in 3 seconds / 3 秒后采集 UI 边界"))
            {
                uiCaptureAt = UnityEngine.Time.realtimeSinceStartup + 3f;
                showDiagnostics = false;
            }
            if (Ready && GameMain.data != null && !GameMain.isLoading && !cleanupCandidate)''')
edit('src/DarkFogSynthesis/Compatibility/NativeRecipeCompatibility.cs','                bool unlocked = history!.RecipeUnlocked(ProtoIds.DarkFogMatrix.Value);','''                bool unlocked = history!.RecipeUnlocked(ProtoIds.DarkFogMatrix.Value);
                if (!unlocked)
                {
                    string technology = (LDB.techs.Select(ProtoIds.InformationTopology.Value)?.Name ??
                        ProtoIds.StringKey("tech.information_topology.name")).Translate();
                    button.tips.tipText += "\\n" + string.Format("dark_fog_synthesis.lab.requires_technology".Translate(), technology);
                }''')
edit('scripts/Build.ps1','    & $Python scripts/test-runtime-snapshots.py',"    & $Python scripts/test-ui-layout.py\n    if ($LASTEXITCODE -ne 0) { throw 'UI layout diagnostic regressions failed.' }\n    & $Python scripts/test-runtime-snapshots.py")
edit('scripts/test-ui-layout.py','    def test_cli_create_only(self):','''    def test_silicon_neuron_text_matches_approved_revision(self):
        root=Path(__file__).resolve().parents[1]
        for lang,expected,obsolete in [('en-US','2 titanium alloy','particle broadband'),('zh-CN','钛合金 ×2','粒子宽带')]:
            data=json.loads((root/f'src/DarkFogSynthesis/Localization/Strings.{lang}.json').read_text(encoding='utf-8'))
            text=data['dark_fog_synthesis.recipe.silicon_neuron.description']
            self.assertIn(expected,text);self.assertNotIn(obsolete,text)
    def test_cli_create_only(self):''')
p=Path('docs/compatibility/content-adaptation.md')
with p.open('a',encoding='utf-8') as stream:
    stream.write('''

## Patch C: native locked feedback and manual rendered bounds

The separate lab choice remains gated by native `RecipeUnlocked`; a locked choice now names the required localized Information Topology research. Both dictionaries have the same extra format key, and Silicon Neuron prose now matches Titanium Alloy x2. No research slot, item discovery flag, native click-index array or gameplay value is changed.

Use the diagnostics window's **Capture UI in 3 seconds / 3 秒后采集 UI 边界** button. The window hides and a single main-thread capture runs after the delay, allowing the pointer to return to a hovered node. This is an explicit one-shot observation, not background polling. Existing lifecycle traces never enumerate UI. The existing manual schema-3 report gains `uiLayout` with screen-space rectangles, ancestor masks, viewport, canvas scale, page grouping, focus value and language. Capture failures remain diagnostics only. Native private UI fields are read through explicit reflection rather than publicized direct access.

```sh
python scripts/check-ui-layout.py normal.json --kind technology --state normal
python scripts/check-ui-layout.py hovered.json --kind technology --tech-id 1951 --state hover
python scripts/check-ui-layout.py expanded.json --kind technology --tech-id 1952 --state expanded
python scripts/check-ui-layout.py locked-lab.json --kind lab-choice --state locked
```

The checker refuses absent/partial snapshots and missing/duplicate targets, and reports clipping and same-page/control-group overlaps. Return 0 describes only that observed frame's rectangles, never all languages/scales, pixel visibility, connector clearance or click behavior. Candidate positions and vanilla controls have not been moved: collect real target-game evidence before changing them. Normal/hover/expanded, both languages, supported scales and lab open/close/recreation remain actual game acceptance work. The fixture suite checks geometry and bilingual text only.
''')
changed.append(str(p))
subprocess.run(['git','add','--',*set(changed)],check=True)
with open(os.environ['GITHUB_OUTPUT'],'a',encoding='utf-8') as stream:stream.write('message=Add native research feedback and rendered UI observation tools\n')
