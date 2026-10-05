#!/usr/bin/env python3
"""Temporary bounded integration step for the user's approved master changes.
Removed after the final normal CI run is scheduled. Never executes DSP or touches saves.
"""
from pathlib import Path
import os
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
os.chdir(ROOT)
changed = []

def replace(path, old, new):
    p = ROOT / path
    text = p.read_text(encoding='utf-8')
    if text.count(old) != 1:
        raise ValueError(f'Expected one unchanged anchor in {path}: {old!r}')
    p.write_text(text.replace(old, new), encoding='utf-8', newline='\n')
    changed.append(path)

replace('src/DarkFogSynthesis.Core/Definitions/VanillaIds.cs',
        '            public static readonly ItemId ParticleBroadband = new ItemId(1402);',
        '            public static readonly ItemId ParticleBroadband = new ItemId(1402);\n            public static readonly ItemId TitaniumAlloy = new ItemId(1107);')
replace('src/DarkFogSynthesis.Core/Definitions/FrozenContent.cs',
        'A(VanillaIds.Items.MicrocrystallineComponent, 2), A(VanillaIds.Items.ParticleBroadband, 1), A(VanillaIds.Items.CrystalSilicon, 2)',
        'A(VanillaIds.Items.MicrocrystallineComponent, 2), A(VanillaIds.Items.TitaniumAlloy, 2), A(VanillaIds.Items.CrystalSilicon, 2)')
replace('tests/DarkFogSynthesis.Core.Tests/Program.cs',
        '("silicon_neuron", 48103, 5202, 1, 240, 1312, new[] { (1302,2), (1402,1), (1113,2) })',
        '("silicon_neuron", 48103, 5202, 1, 240, 1312, new[] { (1302,2), (1107,2), (1113,2) })')
for path in ['README.md', 'DarkFogSynthesis_Implementation_Plan_ZH.md']:
    p = ROOT / path
    text = p.read_text(encoding='utf-8')
    lines = []
    for line in text.splitlines():
        if '硅基神经元' in line or 'silicon neuron' in line.lower() or 'silicon_neuron' in line:
            line = re.sub(r'粒子宽带\s*[×x*]\s*1', '钛合金 ×2', line)
            line = re.sub(r'Particle [Bb]roadband\s*[×x*]\s*1', 'Titanium alloy ×2', line)
        lines.append(line)
    note = '\n\n## Approved Silicon Neuron recipe revision / 已批准的配方修改\n\nSilicon Neuron now requires Microcrystalline Component ×2, Titanium Alloy ×2, and Crystal Silicon ×2. Output (one), recipe ID 48103, unlock technology 1312 and 240-tick base time are unchanged.\n\n硅基神经元：微晶元件 ×2；钛合金 ×2；晶格硅 ×2。此修改覆盖原计划中的粒子宽带 ×1。\n\n**Experimental-save upgrade boundary:** no migration of buffered/in-flight material from the previous ingredient layout is claimed. Before upgrading, use the old build to drain/reset Silicon Neuron machines and finish/cancel related handcraft jobs, then retain an untouched backup. New ingredients are not a license to reinterpret old particle-broadband buffers as titanium alloy. Verify copied saves in the target game; release acceptance remains unexecuted.\n'
    p.write_text('\n'.join(lines) + note, encoding='utf-8', newline='\n')
    changed.append(path)
path = 'docs/compatibility/content-adaptation.md'
p = ROOT / path
p.write_text('''# Incremental content adaptation\n\nApproved against b30dce929ac6b2872cd49b527b06692c786a3231.\n\nThe explicit recipe revision supersedes only Silicon Neuron's Particle Broadband ×1 input: use Titanium Alloy (1107) ×2. Other quantities, research costs, identifiers, machines and progression policy remain unchanged.\n\n## Ordered work\n\n1. Recipe definition, independent oracle and upgrade warning.\n2. Patch A: resolve both icons and all recipe slots, and create detached mutable argument arrays before the first custom prototype is queued; retain existing lifecycle/failure guards.\n3. Patch B: extend the existing report with opt-in, phase-labelled snapshots and deterministic structural comparison; diagnostics must never affect readiness or write saves.\n4. Patch C: add native-unlock-based locked feedback and opt-in rendered rectangle capture/comparison; do not claim measured layout or move vanilla nodes without target-game evidence.\n5. Patch D: provide the exact-build target-game runbook using existing acceptance IDs; research consumption, acquisition/discovery, production, stacking and save/reload are unexecuted until actually observed. Add no speculative discovery hook.\n\n## Preserved boundaries\n\nUse the existing CommonAPI/LDBTool registration and native research engine. No new scientific matrix, save sidecar, blanket cache reset, automatic unlock, full refund operation or release approval. Prior compile-baseline reports describe their historical build, not the new runtime.\n\nThe source-comparison rationale and pinned upstream revisions remain in [mod-source-comparison.md](mod-source-comparison.md). This document tracks implementation, not target-game acceptance.\n''', encoding='utf-8')
changed.append(path)
subprocess.run(['git', 'add', '--', *changed], check=True)
with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as output:
    output.write('message=Replace Silicon Neuron broadband with two titanium alloy\n')
