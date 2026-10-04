#!/usr/bin/env python3
"""Packaging guard tests. No game installation or synthetic runtime DLL required."""
import contextlib
import copy
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import package


class PackagingGuards(unittest.TestCase):
    def test_manifest_format(self):
        self.assertEqual(package.validate_manifest()["name"], "DarkFogSynthesis")

    def test_source_allowlist_excludes_binaries_and_local_configuration(self):
        files = package.source_files()
        self.assertNotIn("Local.Build.props", files)
        self.assertFalse(any(Path(name).suffix.lower() in {".dll", ".pdb", ".dsv", ".moddsv"} for name in files))
        self.assertFalse(any("bin" in Path(name).parts or "obj" in Path(name).parts or ".git" in Path(name).parts for name in files))

    def test_source_allowlist_excludes_symlinks(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "src").mkdir()
            (root / "secret.txt").write_text("not package content")
            (root / "src/leak.txt").symlink_to(root / "secret.txt")
            with patch.object(package, "ROOT", root):
                self.assertNotIn("src/leak.txt", package.source_files())

    def test_source_allowlist_excludes_bin_obj_descendants(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for name in ("src/Test/bin/Release/config.json", "src/Test/obj/project.assets.json", "src/Test/Code.cs"):
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("test fixture")
            with patch.object(package, "ROOT", root):
                self.assertEqual(set(package.source_files()), {"src/Test/Code.cs"})

    def test_release_requires_explicit_acceptance(self):
        with self.assertRaisesRegex(ValueError, "explicit"):
            package.validate_release(None)

    def test_checked_in_report_cannot_create_release(self):
        with self.assertRaisesRegex(ValueError, "blocked"):
            package.validate_release(package.ROOT / "docs/compatibility/acceptance-status.json")

    def test_release_rejects_stale_fingerprint(self):
        report = json.loads((package.ROOT / "docs/compatibility/acceptance-status.json").read_text())
        report.update(releaseEligible=True, runtimeValidated=True, approvedForRelease=True, sourceFingerprint="stale")
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "acceptance.json"
            path.write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, "current build"):
                package.validate_release(path)

    def test_runtime_requires_actual_build_outputs(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(package, "ROOT", Path(tmp)):
            with self.assertRaisesRegex(ValueError, "Missing genuine build output"):
                package.validate_build("Release")

    def test_reference_smoke_cannot_be_release(self):
        with self.assertRaisesRegex(ValueError, "experimental only"):
            package.validate_reference_mode({"referenceMode": "reference-assembly-smoke"}, "release")

    def test_reference_smoke_classification_is_preserved_for_experimental(self):
        self.assertEqual(package.validate_reference_mode({"referenceMode": "reference-assembly-smoke"}, "experimental"), "reference-assembly-smoke")

    def test_build_provenance_cannot_omit_reference_classification(self):
        with self.assertRaisesRegex(ValueError, "must identify"):
            package.validate_reference_mode({})

    def test_runtime_rejects_localization_satellite(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            satellite = root / "src/DarkFogSynthesis/bin/Release/net472/en-US/DarkFogSynthesis.resources.dll"
            satellite.parent.mkdir(parents=True)
            satellite.write_bytes(b"not an assembly; directory guard only")
            with patch.object(package, "ROOT", root), self.assertRaisesRegex(ValueError, "satellite"):
                package.runtime_paths("Release")

    def test_resource_audit_requires_matching_dll_and_both_source_dictionaries(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "artifacts").mkdir()
            locale_dir = root / "src/DarkFogSynthesis/Localization"
            locale_dir.mkdir(parents=True)
            # Ordinary test data, never a loadable or packaged runtime DLL.
            plugin = root / "plugin-test-data.txt"
            plugin.write_text("audit hash binding fixture")
            paths = {"DarkFogSynthesis.dll": plugin}
            expected = []
            for language in ("en-US", "zh-CN"):
                source_name = f"Strings.{language}.json"
                source = locale_dir / source_name
                source.write_text('{"test":"value"}')
                expected.append({"name": "DarkFogSynthesis.Localization." + source_name,
                                 "sourceName": source_name, "sha256": package.digest(source), "keyCount": 1})
            valid = {"schemaVersion": 1, "satelliteFree": True, "assemblySha256": package.digest(plugin), "resources": expected}
            target = root / "artifacts/resource-audit.json"
            with patch.object(package, "ROOT", root):
                with self.assertRaisesRegex(ValueError, "No compiled-resource"):
                    package.validate_resource_audit(paths)
                target.write_text(json.dumps(valid))
                self.assertEqual(package.validate_resource_audit(paths), valid)
                mutations = [
                    ("assemblySha256", "stale", "runtime DLL"),
                    ("satelliteFree", False, "incomplete"),
                    ("resources", expected[:1], "both approved"),
                    ("resources", expected + expected[:1], "both approved"),
                ]
                changed_source = copy.deepcopy(expected)
                changed_source[0]["sha256"] = "stale"
                mutations.append(("resources", changed_source, "both approved"))
                for key, value, message in mutations:
                    with self.subTest(key=key, value=value):
                        changed = copy.deepcopy(valid)
                        changed[key] = value
                        target.write_text(json.dumps(changed))
                        with self.assertRaisesRegex(ValueError, message):
                            package.validate_resource_audit(paths)

    def test_public_api_audit_requires_exact_binary_and_successful_metadata_inspection(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "artifacts").mkdir()
            plugin = root / "plugin-test-data.txt"
            plugin.write_text("audit hash binding fixture")
            paths = {"DarkFogSynthesis.dll": plugin}
            valid = {"schemaVersion": 1, "status": "passed", "assemblySha256": package.digest(plugin),
                     "gameReferenceSha256": "a" * 64, "nonpublicAccesses": 0, "unresolvedReferences": 0,
                     "runtimeExecution": "not_executed", "directGameMemberSites": 3, "uniqueGameMembers": 2, "gameTypes": 1}
            target = root / "artifacts/public-api-audit.json"
            with patch.object(package, "ROOT", root):
                with self.assertRaisesRegex(ValueError, "No public API"):
                    package.validate_public_api_audit(paths)
                target.write_text(json.dumps(valid))
                self.assertEqual(package.validate_public_api_audit(paths), valid)
                for key, value in (("status", "failed"), ("assemblySha256", "stale"), ("nonpublicAccesses", 1),
                                   ("unresolvedReferences", 1), ("gameReferenceSha256", ""), ("gameTypes", 0),
                                   ("runtimeExecution", "target_game")):
                    with self.subTest(key=key):
                        target.write_text(json.dumps(dict(valid, **{key: value})))
                        with self.assertRaisesRegex(ValueError, "incomplete, failed"):
                            package.validate_public_api_audit(paths)

    def test_direct_record_build_cannot_skip_resource_audit(self):
        with patch.object(package, "runtime_paths", return_value={}), \
                patch.object(package, "validate_resource_audit", side_effect=ValueError("required resource audit")), \
                patch.object(package, "validate_public_api_audit") as access:
            with self.assertRaisesRegex(ValueError, "required resource"):
                package.record_build("Release", "reference-assembly-smoke")
            access.assert_not_called()

    def test_direct_record_build_cannot_skip_accessibility_audit(self):
        with patch.object(package, "runtime_paths", return_value={}), \
                patch.object(package, "validate_resource_audit", return_value={}), \
                patch.object(package, "validate_public_api_audit", side_effect=ValueError("required accessibility audit")):
            with self.assertRaisesRegex(ValueError, "required accessibility"):
                package.record_build("Release", "reference-assembly-smoke")

    def test_source_archive_is_deterministic_noninstallable_and_auditable(self):
        with tempfile.TemporaryDirectory() as tmp, contextlib.redirect_stdout(io.StringIO()):
            first = package.package("source-only", "Release", None, Path(tmp) / "one")
            second = package.package("source-only", "Release", None, Path(tmp) / "two")
            self.assertEqual(first.read_bytes(), second.read_bytes())
            with zipfile.ZipFile(first) as archive:
                sdk_file = next(name for name in archive.namelist() if name.endswith('/global.json'))
                self.assertEqual('8.0.100', json.loads(archive.read(sdk_file))['sdk']['version'])
                status = json.loads(archive.read("PACKAGE-STATUS.json"))
                self.assertFalse(status["installable"])
                self.assertFalse(status["releaseAcceptanceValidated"])
                self.assertFalse(any(name.startswith("BepInEx/") or name.lower().endswith(".dll") for name in archive.namelist()))
                for name, expected_hash in status["files"].items():
                    self.assertEqual(package.hashlib.sha256(archive.read(name)).hexdigest(), expected_hash)
            with self.assertRaisesRegex(ValueError, "already exists"):
                package.package("source-only", "Release", None, Path(tmp) / "one")


if __name__ == "__main__":
    unittest.main(verbosity=2)
