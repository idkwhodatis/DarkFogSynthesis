#!/usr/bin/env python3
"""Validate the actual generated Unity project files. Does not install or execute Unity."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).with_name('prepare-unity-ui-tests.py')
ROOT = SCRIPT.resolve().parents[1]
spec = importlib.util.spec_from_file_location('prepare_unity', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class UnityProject(unittest.TestCase):
    def test_generated_assembly_and_packages(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'fixture'
            module.prepare(output)
            editor = output / 'Assets/Tests/Editor'
            assembly = json.loads((editor / 'DarkFogSynthesis.UiGeometry.Tests.asmdef').read_text())
            # Independent contract: an assembly name is NOT the UPM package identifier.
            self.assertEqual(assembly['references'], ['UnityEngine.UI'])
            self.assertEqual(assembly['includePlatforms'], ['Editor'])
            self.assertEqual(assembly['optionalUnityReferences'], ['TestAssemblies'])
            self.assertEqual(assembly['name'], 'DarkFogSynthesis.UiGeometry.Tests')
            packages = json.loads((output / 'Packages/manifest.json').read_text())
            self.assertEqual(packages['dependencies'], {
                'com.unity.ugui': '1.0.0', 'com.unity.test-framework': '1.1.33'})
            self.assertFalse(list(output.rglob('*.dll')))

    def test_exact_production_sources_and_no_source_edits(self):
        sources = [ROOT / 'src/DarkFogSynthesis/Diagnostics/RectMaskClipCapture.cs',
                   ROOT / 'tests/DarkFogSynthesis.Unity.Tests/RectMaskClipCaptureTests.cs']
        before = [p.read_bytes() for p in sources]
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'fixture'
            module.prepare(output)
            for source, contents in zip(sources, before):
                self.assertEqual((output / 'Assets/Tests/Editor' / source.name).read_bytes(), contents)
                self.assertEqual(source.read_bytes(), contents)

    def test_existing_destination_not_overwritten(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'fixture'
            module.prepare(output)
            before = {str(p.relative_to(output)): p.read_bytes() for p in output.rglob('*') if p.is_file()}
            with self.assertRaises(FileExistsError):
                module.prepare(output)
            self.assertEqual({str(p.relative_to(output)): p.read_bytes() for p in output.rglob('*') if p.is_file()}, before)

    def test_destination_in_checkout_refused(self):
        with self.assertRaises(ValueError):
            module.prepare(ROOT / 'artifacts/unity-project-must-not-be-created')
        with self.assertRaises(ValueError):
            module.prepare(ROOT)
        self.assertFalse((ROOT / 'artifacts/unity-project-must-not-be-created').exists())

    def test_cli_creates_correct_project_from_another_working_directory(self):
        with tempfile.TemporaryDirectory() as folder:
            result = subprocess.run([sys.executable, str(SCRIPT.resolve()), 'new-project'],
                                    cwd=folder, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('NOT EXECUTED', result.stdout)
            assembly = json.loads((Path(folder) / 'new-project/Assets/Tests/Editor/DarkFogSynthesis.UiGeometry.Tests.asmdef').read_text())
            self.assertEqual(assembly['references'], ['UnityEngine.UI'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
