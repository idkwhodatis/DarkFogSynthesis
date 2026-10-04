#!/usr/bin/env python3
"""Packaging guard tests. No game installation or synthetic runtime DLL required."""
import contextlib
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

    def test_source_archive_is_deterministic_noninstallable_and_auditable(self):
        with tempfile.TemporaryDirectory() as tmp, contextlib.redirect_stdout(io.StringIO()):
            first = package.package("source-only", "Release", None, Path(tmp) / "one")
            second = package.package("source-only", "Release", None, Path(tmp) / "two")
            self.assertEqual(first.read_bytes(), second.read_bytes())
            with zipfile.ZipFile(first) as archive:
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
