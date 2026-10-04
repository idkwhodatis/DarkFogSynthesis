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
import uuid
import zipfile

import package


@contextlib.contextmanager
def candidate_fixture():
    """Exercise provenance with harmless text; never fabricate/load a runtime DLL.

    Only the PE-output detector is replaced. All capture, hash, audit, identity,
    release and evidence validation runs on ordinary temporary files.
    """
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        paths = {}
        for name in package.OWN_DLLS:
            paths[name] = root / (name + ".fixture.txt")
            paths[name].write_text("ordinary checksum fixture: " + name)
        (root / "artifacts").mkdir()
        refs = {}
        for name in ("Assembly-CSharp", "BepInEx", "0Harmony", "CommonAPI", "LDBTool", "System.Web.Extensions", "UnityEngine", "UnityEngine.CoreModule", "netstandard"):
            refs[name] = root / (name + ".reference.txt")
            refs[name].write_text("harmless reference hash fixture: " + name)
        locale = root / "src/DarkFogSynthesis/Localization"
        locale.mkdir(parents=True)
        resources = []
        for language in ("en-US", "zh-CN"):
            name = f"Strings.{language}.json"
            path = locale / name
            path.write_text('{"test":"value"}')
            resources.append({"name": "DarkFogSynthesis.Localization." + name, "sourceName": name,
                              "sha256": package.digest(path), "keyCount": 1})
        code = root / "src/Fixture.cs"
        code.write_text("// Harmless source fingerprint fixture")

        def rebuild():
            for project, framework in (("DarkFogSynthesis", "net472"), ("DarkFogSynthesis.Core", "netstandard2.0")):
                sidecar = root / "src" / project / "bin/Release" / framework / f"{project}.build-inputs.txt"
                sidecar.parent.mkdir(parents=True, exist_ok=True)
                invocation = str(uuid.uuid4())
                lines = ["schema|1", "invocation|" + invocation, "project|" + project, "configuration|Release",
                         "referenceMode|installed-local", "targetFramework|" + framework, "sdk|8.0.100", "msbuild|17.8.0"]
                for name, path in refs.items():
                    lines.append(f"reference|{path}|{name}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null|{package.digest(path)}")
                lines += [f"input|{code}|{package.digest(code)}", f"output|{paths[project + '.dll']}|{package.digest(paths[project + '.dll'])}", "completed|" + invocation]
                sidecar.write_text("\n".join(lines) + "\n")
            plugin_hash = package.digest(paths["DarkFogSynthesis.dll"])
            resource = {"schemaVersion": 1, "satelliteFree": True, "assemblySha256": plugin_hash, "resources": resources}
            api = {"schemaVersion": 1, "status": "passed", "assemblySha256": plugin_hash,
                   "gameReferenceSha256": package.digest(refs["Assembly-CSharp"]), "nonpublicAccesses": 0,
                   "unresolvedReferences": 0, "runtimeExecution": "not_executed", "directGameMemberSites": 3,
                   "uniqueGameMembers": 2, "gameTypes": 1}
            (root / "artifacts/resource-audit.json").write_text(json.dumps(resource))
            (root / "artifacts/public-api-audit.json").write_text(json.dumps(api))
            package.record_build("Release", "installed-local")
            return json.loads((root / "artifacts/build-report.json").read_text())

        with patch.object(package, "ROOT", root), patch.object(package, "runtime_paths", return_value=paths), contextlib.redirect_stdout(io.StringIO()):
            build = rebuild()
            evidence = root / "docs/compatibility/evidence/guard-fixture.txt"
            evidence.parent.mkdir(parents=True)
            evidence.write_text("Pure packaging test fixture. This is not game evidence.")
            acceptance = {"schemaVersion": 2, "releaseEligible": True, "runtimeValidated": True, "approvedForRelease": True,
                          "sourceFingerprint": build["sourceFingerprint"], "testedBuild": package.tested_build(build),
                          "environment": {"dspVersion": "test-fixture", "unityVersion": "test-fixture", "runtimeFramework": "test-fixture",
                                          "dependencies": {name: {"packageVersion": "test-fixture", "assemblyVersion": "1.0.0.0",
                                                                  "sha256": package.digest(refs[name]) if name in refs else "a" * 64}
                                                           for name in ("BepInEx", "LDBTool", "CommonAPI", "DSPModSave")}},
                          "checks": [{"id": key, "status": "passed", "execution": "target_game", "onlineAuthorizationRecorded": True,
                                      "evidence": [evidence.relative_to(root).as_posix()]} for key in package.CHECK_IDS]}
            acceptance_path = root / "acceptance.json"
            acceptance_path.write_text(json.dumps(acceptance))
            yield root, paths, refs, build, acceptance, acceptance_path, rebuild


class PackagingGuards(unittest.TestCase):
    def test_manifest_format(self):
        self.assertEqual(package.validate_manifest()["name"], "DarkFogSynthesis")

    def test_source_allowlist_excludes_binaries_and_local_configuration(self):
        files = package.source_files()
        self.assertNotIn("Local.Build.props", files)
        self.assertFalse(any(Path(name).suffix.lower() in {".dll", ".pdb", ".dsv", ".moddsv"} for name in files))
        self.assertFalse(any("bin" in Path(name).parts or "obj" in Path(name).parts or ".git" in Path(name).parts for name in files))

    def test_source_allowlist_rejects_file_symlinks(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "src").mkdir()
            (root / "secret.txt").write_text("not package content")
            (root / "src/leak.txt").symlink_to(root / "secret.txt")
            with patch.object(package, "ROOT", root), self.assertRaisesRegex(ValueError, "Symlink"):
                package.source_files()

    def test_source_directory_symlinks_are_rejected_before_any_read(self):
        for relative in ("src", "src/nested", "assets/source"):
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp) / "project"
                outside = Path(tmp) / "harmless-fixture"
                root.mkdir(); outside.mkdir()
                (outside / "should-not-read.cs").write_text("temporary fixture only")
                link = root / relative
                link.parent.mkdir(parents=True, exist_ok=True)
                link.symlink_to(outside, target_is_directory=True)
                with patch.object(package, "ROOT", root), patch.object(Path, "read_bytes", side_effect=AssertionError("Must reject before reading")), self.assertRaisesRegex(ValueError, "Symlink"):
                    package.source_files()

    def test_source_project_root_and_ancestor_symlinks_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            (base / "real/project/src").mkdir(parents=True)
            (base / "real/project/src/okay.cs").write_text("temporary fixture only")
            (base / "root-link").symlink_to(base / "real/project", target_is_directory=True)
            (base / "ancestor-link").symlink_to(base / "real", target_is_directory=True)
            for root in (base / "root-link", base / "ancestor-link/project"):
                with self.subTest(root=root), patch.object(package, "ROOT", root), self.assertRaisesRegex(ValueError, "Symlink"):
                    package.source_files()

    def test_project_path_rejects_resolved_escape_and_allows_ordinary_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "project"
            root.mkdir()
            okay, outside = root / "ordinary.txt", Path(tmp) / "outside-fixture.txt"
            okay.write_text("ordinary"); outside.write_text("harmless")
            with patch.object(package, "ROOT", root):
                self.assertEqual(package.project_path(okay), okay)
                with self.assertRaisesRegex(ValueError, "escapes"):
                    package.project_path(root / "../outside-fixture.txt")

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
            with patch.object(package, "ROOT", Path(tmp)), self.assertRaisesRegex(ValueError, "current build"):
                package.validate_release(path)

    def test_runtime_requires_actual_build_outputs(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(package, "ROOT", Path(tmp)):
            with self.assertRaisesRegex(ValueError, "Missing genuine build output"):
                package.validate_build("Release")

    def test_reference_smoke_cannot_be_release(self):
        with self.assertRaisesRegex(ValueError, "experimental only"):
            package.validate_reference_mode({"referenceMode": "reference-assembly-smoke"}, "release")

    def test_release_accepts_only_the_exact_tested_candidate_fixture(self):
        with candidate_fixture() as (_, paths, _, _, _, acceptance_path, _):
            self.assertEqual(package.validate_build("Release"), paths)
            package.validate_release(acceptance_path)

    def test_release_rejects_missing_and_partial_tested_build(self):
        with candidate_fixture() as (_, _, _, _, acceptance, acceptance_path, _):
            for tested in (None, {}, {"buildIdentity": acceptance["testedBuild"]["buildIdentity"]}):
                with self.subTest(tested=tested):
                    acceptance_path.write_text(json.dumps(dict(acceptance, testedBuild=tested)))
                    with self.assertRaisesRegex(ValueError, "exact tested candidate"):
                        package.validate_release(acceptance_path)

    def test_acceptance_cannot_relabel_binaries_references_configuration_or_identity(self):
        with candidate_fixture() as (_, _, _, _, acceptance, acceptance_path, _):
            for field in ("buildIdentity", "configuration", "referenceMode", "assemblies", "references", "compilerInputs"):
                with self.subTest(field=field):
                    changed = copy.deepcopy(acceptance)
                    changed["testedBuild"][field] = "stale"
                    acceptance_path.write_text(json.dumps(changed))
                    with self.assertRaisesRegex(ValueError, "exact tested candidate"):
                        package.validate_release(acceptance_path)

    def test_acceptance_for_build_a_cannot_qualify_build_b_with_same_sources(self):
        for changed_file in ("DarkFogSynthesis.dll", "DarkFogSynthesis.Core.dll", "Assembly-CSharp", "BepInEx", "System.Web.Extensions"):
            with self.subTest(changed_file=changed_file), candidate_fixture() as (_, paths, refs, old, _, acceptance_path, rebuild):
                path = paths.get(changed_file) or refs[changed_file]
                path.write_text("different ordinary fixture bytes")
                candidate = rebuild()
                self.assertEqual(old["sourceFingerprint"], candidate["sourceFingerprint"])
                self.assertNotEqual(old["buildIdentity"], candidate["buildIdentity"])
                package.validate_build("Release")
                with self.assertRaisesRegex(ValueError, "exact tested candidate"):
                    package.validate_release(acceptance_path)

    def test_same_binary_rebuild_has_a_distinct_invocation_identity(self):
        with candidate_fixture() as (_, _, _, old, _, acceptance_path, rebuild):
            candidate = rebuild()
            self.assertEqual(old["assemblies"], candidate["assemblies"])
            self.assertNotEqual(old["buildIdentity"], candidate["buildIdentity"])
            with self.assertRaisesRegex(ValueError, "exact tested candidate"):
                package.validate_release(acceptance_path)

    def test_release_packaging_refuses_build_or_acceptance_change_after_validation(self):
        for mutation in ("build", "acceptance"):
            with self.subTest(mutation=mutation), candidate_fixture() as (root, paths, _, _, acceptance, acceptance_path, rebuild):
                for name in ("README.md", "LICENSE", "icon.png", "manifest.json", "scripts/generate-assets.py",
                             "assets/generated/energy-analysis.png", "assets/generated/information-topology.png", "docs/CHANGELOG.md"):
                    path = root / name
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_text("ordinary temporary package fixture")
                # Adding fixture assets changed the source fingerprint. Freeze a
                # new baseline before arranging the validation/write interleave.
                baseline = rebuild()
                acceptance["sourceFingerprint"] = baseline["sourceFingerprint"]
                acceptance["testedBuild"] = package.tested_build(baseline)
                acceptance_path.write_text(json.dumps(acceptance))
                original_validate = package.validate_release

                def validate_then_change(*args):
                    validated = original_validate(*args)
                    if mutation == "build":
                        paths["DarkFogSynthesis.dll"].write_text("different candidate compiled during packaging")
                        rebuild()
                    else:
                        changed = copy.deepcopy(acceptance)
                        changed["approvedForRelease"] = False
                        acceptance_path.write_text(json.dumps(changed))
                    return validated

                with patch.object(package, "validate_release", side_effect=validate_then_change), \
                     patch.object(package, "validate_manifest", return_value={"version_number": "0.1.0"}), \
                     patch.object(package.subprocess, "run"), self.assertRaisesRegex(ValueError, "changed while packaging"):
                    package.package("release", "Release", acceptance_path, root / "out")
                self.assertFalse((root / "out").exists())

    def test_current_candidate_rejects_changed_or_missing_binaries_and_dependencies(self):
        for name in ("DarkFogSynthesis.dll", "DarkFogSynthesis.Core.dll", "Assembly-CSharp", "BepInEx", "System.Web.Extensions"):
            for missing in (False, True):
                with self.subTest(name=name, missing=missing), candidate_fixture() as (_, paths, refs, _, _, acceptance_path, _):
                    path = paths.get(name) or refs[name]
                    if missing:
                        path.unlink()
                    else:
                        path.write_text("changed after build")
                    with self.assertRaises((ValueError, FileNotFoundError)):
                        package.validate_release(acceptance_path)

    def test_release_environment_dependency_version_and_hash_must_match_compiler(self):
        with candidate_fixture() as (_, _, _, _, acceptance, acceptance_path, _):
            for field, value in (("assemblyVersion", "9.9.9.9"), ("sha256", "b" * 64)):
                changed = copy.deepcopy(acceptance)
                changed["environment"]["dependencies"]["BepInEx"][field] = value
                acceptance_path.write_text(json.dumps(changed))
                with self.assertRaisesRegex(ValueError, "resolved compiler dependency"):
                    package.validate_release(acceptance_path)

    def test_missing_partial_stale_and_changed_build_capture_is_refused(self):
        for mutation in ("missing", "incomplete", "config", "mode", "identity", "input"):
            with self.subTest(mutation=mutation), candidate_fixture() as (root, _, _, _, _, acceptance_path, _):
                sidecar = root / "src/DarkFogSynthesis/bin/Release/net472/DarkFogSynthesis.build-inputs.txt"
                if mutation == "missing":
                    sidecar.unlink()
                elif mutation == "input":
                    (root / "src/Fixture.cs").write_text("source changed after compilation")
                else:
                    content = sidecar.read_text()
                    if mutation == "incomplete": content = "\n".join(line for line in content.splitlines() if not line.startswith("completed|"))
                    if mutation == "config": content = content.replace("configuration|Release", "configuration|Debug")
                    if mutation == "mode": content = content.replace("referenceMode|installed-local", "referenceMode|reference-assembly-smoke")
                    if mutation == "identity": content = content.replace("sdk|8.0.100", "sdk|8.0.200")
                    sidecar.write_text(content)
                with self.assertRaises(ValueError):
                    package.validate_release(acceptance_path)

    def test_report_cannot_substitute_other_reference_or_build_identity(self):
        for field in ("references", "buildIdentity", "compilerInputs"):
            with self.subTest(field=field), candidate_fixture() as (root, _, _, build, _, acceptance_path, _):
                changed = copy.deepcopy(build)
                changed[field] = "invented"
                (root / "artifacts/build-report.json").write_text(json.dumps(changed))
                with self.assertRaisesRegex(ValueError, "identity or resolved"):
                    package.validate_release(acceptance_path)

    def test_api_audit_must_use_the_actual_resolved_game_reference(self):
        with candidate_fixture() as (root, _, _, _, _, _, _):
            path = root / "artifacts/public-api-audit.json"
            audit = json.loads(path.read_text()); audit["gameReferenceSha256"] = "b" * 64
            path.write_text(json.dumps(audit))
            with self.assertRaisesRegex(ValueError, "actual resolved compiler reference"):
                package.record_build("Release", "installed-local")

    def test_runtime_directory_and_file_symlinks_are_rejected_without_reading(self):
        for relative in ("src", "src/DarkFogSynthesis/bin", "src/DarkFogSynthesis/bin/Release/net472", "src/DarkFogSynthesis/bin/Release/net472/DarkFogSynthesis.dll"):
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp) / "project"
                root.mkdir()
                outside = Path(tmp) / "harmless-target"
                is_file = relative.endswith(".dll")
                if is_file: outside.write_text("Not an assembly")
                else: outside.mkdir()
                link = root / relative
                link.parent.mkdir(parents=True, exist_ok=True)
                link.symlink_to(outside, target_is_directory=not is_file)
                with patch.object(package, "ROOT", root), patch.object(Path, "read_bytes", side_effect=AssertionError("Must reject before reading")), self.assertRaisesRegex(ValueError, "Symlink"):
                    package.runtime_paths("Release")

    def test_runtime_package_assets_audits_and_metadata_cannot_use_symlinks(self):
        for relative in ("README.md", "artifacts/resource-audit.json", "assets/generated", "assets/generated/energy-analysis.png"):
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp) / "project"
                root.mkdir()
                for name in ("manifest.json", "README.md", "icon.png", "LICENSE", "scripts/generate-assets.py", "artifacts/build-report.json",
                             "artifacts/resource-audit.json", "artifacts/public-api-audit.json", "assets/generated/energy-analysis.png",
                             "assets/generated/information-topology.png", "docs/CHANGELOG.md"):
                    path = root / name
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_text("ordinary input fixture")
                (root / "artifacts/build-report.json").write_text(json.dumps({"referenceMode": "installed-local", "buildIdentity": "a" * 64}))
                outside = Path(tmp) / "harmless-target"
                link = root / relative
                if link.is_dir():
                    link.rename(outside)
                    link.symlink_to(outside, target_is_directory=True)
                else:
                    link.rename(outside)
                    link.symlink_to(outside)
                with patch.object(package, "ROOT", root), patch.object(package, "source_files", return_value={}), \
                     patch.object(package, "validate_manifest", return_value={"version_number": "0.1.0"}), \
                     patch.object(package, "validate_build", return_value=({}, {"referenceMode": "installed-local", "buildIdentity": "a" * 64}, b"")), patch.object(package.subprocess, "run"), \
                     self.assertRaisesRegex(ValueError, "Symlink"):
                    package.package("experimental", "Release", None, Path(tmp) / "out")

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

    def test_source_package_fingerprint_uses_frozen_bytes_despite_later_source_edit(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "project"
            code = root / "src/Code.cs"
            code.parent.mkdir(parents=True)
            code.write_text("// source snapshot A")
            script = root / "scripts/generate-assets.py"
            script.parent.mkdir(); script.write_text("# harmless fixture, never executed")
            original_zip = zipfile.ZipFile

            def edit_before_zip(*args, **kwargs):
                code.write_text("// later workspace snapshot B")
                return original_zip(*args, **kwargs)

            with patch.object(package, "ROOT", root), patch.object(package, "validate_manifest", return_value={"version_number": "0.1.0"}), \
                 patch.object(package.subprocess, "run"), patch.object(package.zipfile, "ZipFile", side_effect=edit_before_zip), \
                 contextlib.redirect_stdout(io.StringIO()):
                archive_path = package.package("source-only", "Release", None, Path(tmp) / "out")
                with original_zip(archive_path) as archive:
                    status = json.loads(archive.read("PACKAGE-STATUS.json"))
                    prefix = "DarkFogSynthesis-0.1.0-source/"
                    frozen = {name[len(prefix):]: archive.read(name) for name in archive.namelist() if name.startswith(prefix)}
                self.assertEqual(frozen["src/Code.cs"], b"// source snapshot A")
                self.assertEqual(status["sourceFingerprint"], package.source_fingerprint_from_bytes(frozen))
                self.assertNotEqual(status["sourceFingerprint"], package.source_fingerprint())

    def test_runtime_package_fingerprint_uses_validated_candidate_despite_later_source_edit(self):
        with candidate_fixture() as (root, _, _, _, _, _, rebuild):
            for name in ("README.md", "LICENSE", "icon.png", "manifest.json", "scripts/generate-assets.py",
                         "assets/generated/energy-analysis.png", "assets/generated/information-topology.png", "docs/CHANGELOG.md"):
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("ordinary temporary package fixture")
            baseline = rebuild()
            original_zip = zipfile.ZipFile

            def edit_before_zip(*args, **kwargs):
                (root / "src/Fixture.cs").write_text("// later workspace snapshot B")
                return original_zip(*args, **kwargs)

            with patch.object(package, "validate_manifest", return_value={"version_number": "0.1.0"}), \
                 patch.object(package.subprocess, "run"), patch.object(package.zipfile, "ZipFile", side_effect=edit_before_zip):
                archive_path = package.package("experimental", "Release", None, root / "out")
            with original_zip(archive_path) as archive:
                status = json.loads(archive.read("PACKAGE-STATUS.json"))
                build = json.loads(archive.read("BUILD-STATUS.json"))
            self.assertEqual(status["sourceFingerprint"], baseline["sourceFingerprint"])
            self.assertEqual(status["sourceFingerprint"], build["sourceFingerprint"])
            self.assertNotEqual(status["sourceFingerprint"], package.source_fingerprint())


if __name__ == "__main__":
    unittest.main(verbosity=2)
