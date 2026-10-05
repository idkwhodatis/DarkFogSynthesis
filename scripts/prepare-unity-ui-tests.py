#!/usr/bin/env python3
"""Prepare an isolated Unity 2022.3 EditMode project; does not install or launch Unity.

Copies the exact production geometry helper, not a reimplementation or a game-shaped stub.
The chosen output directory must not exist. No game or plugin assemblies are needed.
"""
import argparse
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]


def prepare(output):
    # Refuse destinations in this checkout so build inventories cannot be changed by this helper.
    output = Path(output).resolve()
    if output == ROOT or ROOT in output.parents:
        raise ValueError('Choose a new directory outside the repository')
    output.mkdir(parents=True, exist_ok=False)
    editor = output / 'Assets/Tests/Editor'
    editor.mkdir(parents=True)
    for source in (ROOT / 'src/DarkFogSynthesis/Diagnostics/RectMaskClipCapture.cs',
                   ROOT / 'tests/DarkFogSynthesis.Unity.Tests/RectMaskClipCaptureTests.cs'):
        shutil.copyfile(source, editor / source.name)
    (editor / 'DarkFogSynthesis.UiGeometry.Tests.asmdef').write_text(json.dumps({
        'name': 'DarkFogSynthesis.UiGeometry.Tests', 'references': ['UnityEngine.UI'],
        'optionalUnityReferences': ['TestAssemblies'], 'includePlatforms': ['Editor']
    }, indent=2) + '\n', encoding='utf-8')
    (output / 'Packages').mkdir()
    (output / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {
        'com.unity.ugui': '1.0.0', 'com.unity.test-framework': '1.1.33'
    }}, indent=2) + '\n', encoding='utf-8')
    print('Prepared actual Unity EditMode tests (NOT EXECUTED): ' + str(output))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    prepare(parser.parse_args().output)
