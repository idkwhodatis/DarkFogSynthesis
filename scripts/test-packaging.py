#!/usr/bin/env python3
"""Packaging guard tests. No game installation or synthetic runtime DLL required."""
import contextlib
import copy
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import uuid
import zipfile

import package


# Use committed original art and metadata as ordinary validation fixtures.
# No game binaries, game data or DLL-shaped substitute is created or loaded.
DISTRIBUTION_FIXTURE = {name: (package.ROOT / name).read_bytes()
                        for name in (*package.ASSET_CHECK_FILES, "manifest.json", "README.md", "LICENSE", "docs/CHANGELOG.md")}


def seed_distribution(root):
    for name, content in DISTRIBUTION_FIXTURE.items():
        path = root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)


@contextlib.contextmanager
def candidate_fixture():
    """Exercise provenance with harmless text; never fabricate/load a runtime DLL.

    Only the PE-output detector is replaced. All capture, hash, audit, identity,
    release and evidence validation runs on ordinary temporary files.
    """
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        seed_distribution(root)
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
                lines = ["schema|2", "invocation|" + invocation, "project|" + project, "configuration|Release",
                         "referenceMode|installed-local", "targetFramework|" + framework, "sdk|8.0.100", "msbuild|17.8.0"]
                for name, path in refs.items():
                    lines.append(f"reference|{path}|{name}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null|{package.digest(path)}")
                lines += [f"inventory|{root / name}|{checksum}" for name, checksum in package.production_inventory().items()]
                lines += [f"compile|{code}|{package.digest(code)}", f"output|{paths[project + '.dll']}|{package.digest(paths[project + '.dll'])}", "completed|" + invocation]
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
            evidence.parent.mkdir(parents=True, exist_ok=True)
            evidence.write_text("Pure packaging test fixture. This is not game evidence.")
            acceptance = {"schemaVersion": 3, "releaseEligible": True, "runtimeValidated": True, "approvedForRelease": True,
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


def candidate_cli(root, paths, acceptance, output_dir, *, cwd, evidence_mutations=()):
    """Run the real CLI parser and packager in a fresh process on text fixtures.

    As in candidate_fixture, only runtime-output discovery is substituted. The
    subprocess still runs every provenance, acceptance, asset and ZIP guard.
    Optional race injection changes only an evidence fixture after a real
    validator returns; it never replaces a guard or creates game evidence.
    Nothing here creates an actual runtime build or target-game test result.
    """
    bootstrap = """
import json
from pathlib import Path
import sys
sys.path.insert(0, sys.argv[1])
import package
package.ROOT = Path(sys.argv[2])
paths = {name: Path(path) for name, path in json.loads(sys.argv[3]).items()}
package.runtime_paths = lambda configuration: paths
def inject_evidence_mutation(phase, relative, replacement):
    original = getattr(package, phase)
    def validate_then_mutate(*args, **kwargs):
        result = original(*args, **kwargs)
        (package.ROOT / relative).write_text(replacement)
        return result
    setattr(package, phase, validate_then_mutate)
for mutation in json.loads(sys.argv[4]):
    inject_evidence_mutation(*mutation)
sys.argv = [package.__file__, *sys.argv[5:]]
raise SystemExit(package.main())
"""
    return subprocess.run([sys.executable, "-c", bootstrap, str(Path(package.__file__).parent),
                           str(root), json.dumps({name: str(path) for name, path in paths.items()}),
                           json.dumps(evidence_mutations),
                           "--channel", "release", "--acceptance", str(acceptance),
                           "--output-dir", str(output_dir)], cwd=cwd, capture_output=True, text=True)


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

    def test_project_path_rejects_symlinks_before_canonicalizing_dot_segments(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "nested").mkdir()
            (root / "acceptance.json").write_text("harmless fixture")
            (root / "linked").symlink_to(root / "nested", target_is_directory=True)
            # resolve() would erase the forbidden linked component from this
            # otherwise in-project path. Reject it before resolving or reading.
            with patch.object(package, "ROOT", root), \
                 patch.object(Path, "resolve", side_effect=AssertionError("Must reject before resolving")), \
                 patch.object(Path, "read_bytes", side_effect=AssertionError("Must reject before reading")), \
                 self.assertRaisesRegex(ValueError, "Symlink"):
                package.validate_release(root / "linked/../acceptance.json")

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

    def test_release_cli_accepts_relative_dot_and_absolute_acceptance_paths(self):
        with candidate_fixture() as (root, paths, _, build, _, acceptance_path, _):
            expected = acceptance_path.read_bytes()
            nested = root / "docs/compatibility/cli-acceptance.json"
            nested.write_bytes(expected)
            cases = (
                ("root-relative", root, "acceptance.json"),
                ("nested-relative", root, "docs/compatibility/cli-acceptance.json"),
                ("dot-relative", root, "./docs/compatibility/../compatibility/cli-acceptance.json"),
                ("absolute", root.parent, str(nested)),
                ("nested-cwd", root / "docs", "compatibility/cli-acceptance.json"),
            )
            for name, cwd, argument in cases:
                with self.subTest(path_form=name):
                    output = root / "out" / name
                    result = candidate_cli(root, paths, argument, output, cwd=cwd)
                    self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                    with zipfile.ZipFile(output / "DarkFogSynthesis-0.1.0-release.zip") as archive:
                        self.assertIsNone(archive.testzip())
                        self.assertEqual(archive.read("RELEASE-ACCEPTANCE.json"), expected)
                        self.assertEqual(archive.read(nested.relative_to(root).as_posix()), expected)
                        status = json.loads(archive.read("PACKAGE-STATUS.json"))
                        self.assertEqual(status["buildIdentity"], build["buildIdentity"])
                        self.assertEqual(status["files"]["RELEASE-ACCEPTANCE.json"],
                                         package.hashlib.sha256(expected).hexdigest())
                        recorded = json.loads(archive.read("BUILD-STATUS.json"))
                        self.assertEqual(recorded["runtimeExecution"], "not_executed")
                        self.assertFalse(recorded["installedGameValidated"])

    def test_release_cli_still_refuses_checked_in_unverified_acceptance(self):
        with tempfile.TemporaryDirectory() as tmp:
            output = Path(tmp) / "out"
            result = subprocess.run([sys.executable, str(Path(package.__file__).absolute()),
                                     "--channel", "release", "--acceptance",
                                     "docs/compatibility/acceptance-status.json", "--output-dir", str(output)],
                                    cwd=package.ROOT, capture_output=True, text=True)
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertIn("Packaging refused: Release blocked:", result.stderr)
            self.assertNotIn("Traceback", result.stderr)
            self.assertFalse(output.exists())

    def test_release_cli_refuses_evidence_omitted_by_source_allowlist(self):
        excluded = [f"review.{extension}" for extension in ("log", "jpg", "jpeg", "dll", "exe", "dsv", "moddsv", "zip")]
        excluded += [f"{folder}/review.txt" for folder in ("bin", "obj", "__pycache__", ".git")]
        # Exact report references must be ZIP/inventory keys, not aliases.
        excluded += ["./guard-fixture.txt"]
        with candidate_fixture() as (root, paths, _, _, acceptance, acceptance_path, _):
            for index, name in enumerate(excluded):
                with self.subTest(evidence=name):
                    relative = "docs/compatibility/evidence/" + name
                    evidence = root / relative
                    evidence.parent.mkdir(parents=True, exist_ok=True)
                    evidence.write_text("Ordinary omitted-file fixture; not target-game evidence.")
                    changed = copy.deepcopy(acceptance)
                    # A valid first reference must not hide an omitted later one.
                    changed["checks"][-1]["evidence"].append(relative)
                    acceptance_path.write_text(json.dumps(changed))
                    output = root / "out" / str(index)
                    result = candidate_cli(root, paths, acceptance_path, output, cwd=root)
                    self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                    self.assertIn("absent from the final allowlisted package inventory", result.stderr)
                    self.assertIn(relative, result.stderr)
                    self.assertFalse(output.exists())

    def test_release_cli_preserves_every_evidence_reference_and_inventory_hash(self):
        with candidate_fixture() as (root, paths, _, _, acceptance, acceptance_path, _):
            expected = {acceptance["checks"][0]["evidence"][0]: b"Repeated ordinary text fixture, not game evidence."}
            for name, content in (("notes.md", b"# Ordinary fixture"), ("data.json", b'{"fixture":true}'),
                                  ("nested/capture.png", DISTRIBUTION_FIXTURE["icon.png"]), ("upper.TXT", b"Uppercase extension fixture")):
                expected["docs/compatibility/evidence/" + name] = content
            for relative, content in expected.items():
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)
            acceptance["checks"][-1]["evidence"] = list(expected)
            acceptance_path.write_text(json.dumps(acceptance))
            output = root / "out"
            result = candidate_cli(root, paths, acceptance_path, output, cwd=root)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            with zipfile.ZipFile(output / "DarkFogSynthesis-0.1.0-release.zip") as archive:
                report = json.loads(archive.read("RELEASE-ACCEPTANCE.json"))
                status = json.loads(archive.read("PACKAGE-STATUS.json"))
                self.assertTrue(status["releaseAcceptanceValidated"])
                for check in report["checks"]:
                    for relative in check["evidence"]:
                        self.assertEqual(archive.namelist().count(relative), 1)
                        self.assertEqual(archive.read(relative), expected[relative])
                        self.assertTrue(archive.read(relative))
                        self.assertEqual(status["files"][relative], package.hashlib.sha256(expected[relative]).hexdigest())

    def test_release_cli_refuses_evidence_truncated_after_validation_before_freeze(self):
        for repair_live_file in (False, True):
            with self.subTest(repair_live_file=repair_live_file), candidate_fixture() as (root, paths, _, _, acceptance, acceptance_path, _):
                relative = acceptance["checks"][0]["evidence"][0]
                mutations = [("validate_release", relative, "")]
                if repair_live_file:
                    mutations.append(("validate_asset_snapshot", relative, "Repaired live file, but the frozen evidence is empty"))
                output = root / "out"
                result = candidate_cli(root, paths, acceptance_path, output, cwd=root, evidence_mutations=mutations)
                self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                self.assertIn("Empty frozen release evidence", result.stderr)
                self.assertEqual((root / relative).read_text(), mutations[-1][2])
                self.assertFalse(output.exists())

    def test_release_cli_uses_frozen_evidence_despite_later_truncation_or_edit(self):
        for replacement in ("", "Different later checkout bytes"):
            with self.subTest(replacement=replacement), candidate_fixture() as (root, paths, _, _, acceptance, acceptance_path, _):
                relative = acceptance["checks"][0]["evidence"][0]
                expected = (root / relative).read_bytes()
                output = root / "out"
                result = candidate_cli(root, paths, acceptance_path, output, cwd=root,
                                       evidence_mutations=[("validate_asset_snapshot", relative, replacement)])
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertEqual((root / relative).read_text(), replacement)
                with zipfile.ZipFile(output / "DarkFogSynthesis-0.1.0-release.zip") as archive:
                    self.assertEqual(archive.read(relative), expected)
                    status = json.loads(archive.read("PACKAGE-STATUS.json"))
                    self.assertEqual(status["files"][relative], package.hashlib.sha256(expected).hexdigest())

    def test_release_cli_preserves_evidence_path_and_nonempty_guards(self):
        with candidate_fixture() as (root, paths, _, _, acceptance, acceptance_path, _):
            evidence = root / acceptance["checks"][0]["evidence"][0]
            empty = evidence.with_name("empty.txt")
            empty.write_bytes(b"")
            linked = evidence.with_name("linked.txt")
            cases = (
                ("docs/compatibility/evidence/missing.txt", "Missing or non-file"),
                (empty.relative_to(root).as_posix(), "Missing release evidence"),
                ("docs/CHANGELOG.md", "must be beneath"),
                ("docs/compatibility/evidence/../evidence/guard-fixture.txt", "must be beneath"),
                (str(evidence), "must be beneath"),
                (linked.relative_to(root).as_posix(), "Symlink"),
            )
            for index, (relative, refusal) in enumerate(cases):
                with self.subTest(evidence=relative):
                    if relative == linked.relative_to(root).as_posix():
                        linked.symlink_to(evidence)
                    changed = copy.deepcopy(acceptance)
                    changed["checks"][-1]["evidence"].append(relative)
                    acceptance_path.write_text(json.dumps(changed))
                    output = root / "out" / str(index)
                    result = candidate_cli(root, paths, acceptance_path, output, cwd=root)
                    self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                    self.assertIn(refusal, result.stderr)
                    self.assertFalse(output.exists())

    def test_release_cli_rejects_symlinked_and_outside_acceptance_paths(self):
        with candidate_fixture() as (root, paths, _, _, _, acceptance_path, _), tempfile.TemporaryDirectory() as tmp:
            outside = Path(tmp) / "acceptance.json"
            outside.write_bytes(acceptance_path.read_bytes())
            (root / "linked-acceptance.json").symlink_to(acceptance_path)
            (root / "linked-docs").symlink_to(root / "docs", target_is_directory=True)
            cases = (
                ("linked-file", "linked-acceptance.json", "Symlink"),
                ("linked-ancestor", "linked-docs/../acceptance.json", "Symlink"),
                ("outside-absolute", str(outside), "escapes the project root"),
                ("outside-relative", os.path.relpath(outside, root), "escapes the project root"),
            )
            for name, argument, refusal in cases:
                with self.subTest(path_form=name):
                    output = root / "out" / name
                    result = candidate_cli(root, paths, argument, output, cwd=root)
                    self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                    self.assertIn(refusal, result.stderr)
                    self.assertFalse(output.exists())

    def test_release_rejects_missing_and_partial_tested_build(self):
        with candidate_fixture() as (_, _, _, _, acceptance, acceptance_path, _):
            for tested in (None, {}, {"buildIdentity": acceptance["testedBuild"]["buildIdentity"]}):
                with self.subTest(tested=tested):
                    acceptance_path.write_text(json.dumps(dict(acceptance, testedBuild=tested)))
                    with self.assertRaisesRegex(ValueError, "exact tested candidate"):
                        package.validate_release(acceptance_path)

    def test_acceptance_cannot_relabel_binaries_references_configuration_or_identity(self):
        with candidate_fixture() as (_, _, _, _, acceptance, acceptance_path, _):
            for field in ("buildIdentity", "configuration", "referenceMode", "assemblies", "references", "compilerInputs", "distributionFiles"):
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

    def test_new_production_members_cannot_rerecord_old_binaries(self):
        # The inventory includes resource/import extensions excluded from the
        # source ZIP allowlist, and binds both projects to the same snapshot.
        additions = ("src/DarkFogSynthesis/NewDefault.cs", "src/DarkFogSynthesis.Core/NewDefault.cs",
                     "src/DarkFogSynthesis/NewDefault.resx", "src/DarkFogSynthesis/Localization/New.json",
                     "src/DarkFogSynthesis.Core/unknown.payload",
                     "src/DarkFogSynthesis.Core/Nested/obj/New.cs", "src/DarkFogSynthesis.Core/Nested/bin/New.cs",
                     "src/DarkFogSynthesis.Core/__pycache__/New.cs", "src/DarkFogSynthesis.Core/artifacts/New.cs",
                     "build/imports/new.props",
                     "build/imports/new.targets", "Local.Build.props", ".editorconfig")
        for name in additions:
            with self.subTest(name=name), candidate_fixture() as (root, _, _, _, _, _, rebuild):
                report = root / "artifacts/build-report.json"
                before = report.read_bytes()
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("New member after compilation, never compiled")
                with self.assertRaisesRegex(ValueError, "Production source inventory"):
                    package.record_build("Release", "installed-local")
                self.assertEqual(report.read_bytes(), before)
                with self.assertRaises(ValueError):
                    package.validate_build("Release")
                with self.assertRaises(ValueError):
                    package.package("experimental", "Release", None, root / "refused")
                self.assertFalse((root / "refused").exists())
                # A fresh capture, rather than reusing/relabeling a report,
                # restores eligibility; these remain harmless text fixtures.
                candidate = rebuild()
                package.validate_build("Release")
                self.assertNotEqual(json.loads(before)["buildIdentity"], candidate["buildIdentity"])

    def test_inventory_deletions_and_renames_preserve_previous_report(self):
        for name in ("src/DarkFogSynthesis.Core/Extra.cs", "src/DarkFogSynthesis/New.resx", "build/extra.targets"):
            for operation in ("delete", "rename"):
                with self.subTest(name=name, operation=operation), candidate_fixture() as (root, _, _, _, _, _, rebuild):
                    path = root / name
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_text("An inventory member not individually captured as compile input")
                    rebuild()
                    report = root / "artifacts/build-report.json"
                    before = report.read_bytes()
                    if operation == "delete":
                        path.unlink()
                    else:
                        path.rename(path.with_name("Renamed" + path.suffix))
                    with self.assertRaises(ValueError):
                        package.record_build("Release", "installed-local")
                    self.assertEqual(report.read_bytes(), before)

    def test_inventory_is_required_for_each_project_even_after_report_relabel(self):
        for project, framework in (("DarkFogSynthesis", "net472"), ("DarkFogSynthesis.Core", "netstandard2.0")):
            for mutation in ("legacy-schema", "missing-inventory", "partial-inventory", "duplicate-inventory"):
                with self.subTest(project=project, mutation=mutation), candidate_fixture() as (root, _, _, _, _, _, _):
                    report = root / "artifacts/build-report.json"
                    before = report.read_bytes()
                    sidecar = root / "src" / project / "bin/Release" / framework / (project + ".build-inputs.txt")
                    lines = sidecar.read_text().splitlines()
                    if mutation == "legacy-schema":
                        lines[0] = "schema|1"
                    elif mutation == "missing-inventory":
                        lines = [line for line in lines if not line.startswith("inventory|")]
                    elif mutation == "partial-inventory":
                        lines.remove(next(line for line in lines if line.startswith("inventory|")))
                    else:
                        lines.append(next(line for line in lines if line.startswith("inventory|")))
                    sidecar.write_text("\n".join(lines) + "\n")
                    with self.assertRaises(ValueError):
                        package.record_build("Release", "installed-local")
                    self.assertEqual(report.read_bytes(), before)

    def test_unobserved_linked_compiler_sources_and_resources_are_refused(self):
        for kind in ("compile", "resource"):
            with self.subTest(kind=kind), candidate_fixture() as (root, _, _, _, _, _, _):
                linked = root / "tests/Unobserved.cs"
                linked.parent.mkdir(parents=True)
                linked.write_text("Harmless unobserved wildcard member fixture")
                sidecar = root / "src/DarkFogSynthesis/bin/Release/net472/DarkFogSynthesis.build-inputs.txt"
                with sidecar.open("a") as stream:
                    stream.write(f"{kind}|{linked}|{package.digest(linked)}\n")
                with self.assertRaisesRegex(ValueError, "outside the observed production inventory"):
                    package.record_build("Release", "installed-local")

    def test_arbitrary_generated_source_cannot_bypass_inventory_membership(self):
        with candidate_fixture() as (root, _, _, _, _, _, _):
            linked = root / "src/DarkFogSynthesis/obj/Unobserved.cs"
            linked.parent.mkdir(parents=True)
            linked.write_text("Not a supported SDK-generated assembly attribute file")
            sidecar = root / "src/DarkFogSynthesis/bin/Release/net472/DarkFogSynthesis.build-inputs.txt"
            with sidecar.open("a") as stream:
                stream.write(f"generated|{linked}|{package.digest(linked)}\ncompile|{linked}|{package.digest(linked)}\n")
            with self.assertRaisesRegex(ValueError, "Unsupported generated compiler input"):
                package.record_build("Release", "installed-local")

    def test_inventory_metadata_is_private_and_nonproduction_edits_are_independent(self):
        with candidate_fixture() as (root, _, _, build, _, _, _):
            expected = package.inventory_fingerprint(package.production_inventory())
            for captured in build["compilerInputs"].values():
                self.assertEqual(captured["sourceInventorySha256"], expected)
            self.assertNotIn(str(root), json.dumps(build))
            for name in ("docs/extra.md", "docs/compatibility/evidence/extra.txt", "tests/Extra.cs",
                         "scripts/extra.py", ".gitattributes", "src/DarkFogSynthesis/obj/ignored.cs",
                         "src/DarkFogSynthesis/bin/ignored.txt"):
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("Nonproduction or generated file")
            self.assertEqual(expected, package.inventory_fingerprint(package.production_inventory()))
            package.validate_build("Release")

    def test_production_inventory_reuses_frozen_packaged_bytes(self):
        with candidate_fixture() as (root, _, _, _, _, _, _):
            snapshot = package.freeze_sources()
            before = package.production_inventory(snapshot)
            (root / "src/Fixture.cs").write_text("Later live source content")
            self.assertEqual(before, package.production_inventory(snapshot))
            self.assertNotEqual(before, package.production_inventory())

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
            with self.subTest(relative=relative), candidate_fixture() as (root, _, _, _, _, _, _), tempfile.TemporaryDirectory() as tmp:
                outside = Path(tmp) / "harmless-target"
                link = root / relative
                is_directory = link.is_dir()
                link.rename(outside)
                link.symlink_to(outside, target_is_directory=is_directory)
                with self.assertRaisesRegex(ValueError, "Symlink"):
                    package.package("experimental", "Release", None, root / "out")

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
            seed_distribution(root)
            code = root / "src/Code.cs"
            code.parent.mkdir(parents=True)
            code.write_text("// source snapshot A")
            script = root / "scripts/generate-assets.py"
            script.write_text("# harmless fixture, never executed")
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

    def test_distribution_files_are_required_in_candidate_identity(self):
        with candidate_fixture() as (root, _, _, build, _, _, _):
            self.assertEqual(build["schemaVersion"], 3)
            self.assertEqual(set(build["distributionFiles"]), set(package.DISTRIBUTION_FILES))
            for name in package.DISTRIBUTION_FILES:
                changed = copy.deepcopy(build)
                changed["distributionFiles"][name] = "0" * 64
                self.assertNotEqual(package.build_identity(changed), build["buildIdentity"])
            for value in (None, {}, {"manifest.json": build["distributionFiles"]["manifest.json"]}):
                with self.subTest(value=value):
                    changed = dict(build, distributionFiles=value)
                    (root / "artifacts/build-report.json").write_text(json.dumps(changed))
                    with self.assertRaisesRegex(ValueError, "Distribution assets or manifest"):
                        package.validate_build("Release")

    def test_legacy_schema_cannot_qualify_runtime_build_or_acceptance(self):
        with candidate_fixture() as (root, _, _, build, acceptance, acceptance_path, _):
            acceptance_path.write_text(json.dumps(dict(acceptance, schemaVersion=2)))
            with self.assertRaisesRegex(ValueError, "Release blocked"):
                package.validate_release(acceptance_path)
            (root / "artifacts/build-report.json").write_text(json.dumps(dict(build, schemaVersion=2)))
            with self.assertRaisesRegex(ValueError, "stale or incomplete"):
                package.validate_build("Release")

    def test_distribution_edits_invalidate_current_acceptance(self):
        for name in package.DISTRIBUTION_FILES:
            with self.subTest(name=name), candidate_fixture() as (root, _, _, _, _, acceptance_path, _):
                path = root / name
                path.write_bytes(path.read_bytes() + b"changed after acceptance")
                with self.assertRaises(ValueError):
                    package.validate_release(acceptance_path)

    def test_valid_manifest_and_asset_manifest_edits_require_new_acceptance(self):
        for name in ("manifest.json", "assets/generated/assets-manifest.json"):
            with self.subTest(name=name), candidate_fixture() as (root, _, _, original, acceptance, acceptance_path, rebuild):
                path = root / name
                path.write_bytes(path.read_bytes() + b"\n")
                candidate = rebuild()
                self.assertNotEqual(original["distributionFiles"][name], candidate["distributionFiles"][name])
                self.assertNotEqual(original["buildIdentity"], candidate["buildIdentity"])
                package.validate_build("Release")
                # Even relabeling the top-level source fingerprint cannot make
                # prior target-game evidence approve the new external files.
                acceptance["sourceFingerprint"] = candidate["sourceFingerprint"]
                acceptance_path.write_text(json.dumps(acceptance))
                with self.assertRaisesRegex(ValueError, "exact tested candidate"):
                    package.validate_release(acceptance_path)

    def test_record_build_refuses_invalid_frozen_png_and_keeps_previous_report(self):
        with candidate_fixture() as (root, _, _, _, _, _, _):
            before = (root / "artifacts/build-report.json").read_bytes()
            (root / "assets/generated/energy-analysis.png").write_bytes(b"not a PNG")
            with self.assertRaisesRegex(ValueError, "Frozen asset validation failed"):
                package.record_build("Release", "installed-local")
            self.assertEqual((root / "artifacts/build-report.json").read_bytes(), before)

    def test_release_refuses_distribution_mutation_after_validation_before_snapshot(self):
        for name in package.DISTRIBUTION_FILES:
            with self.subTest(name=name), candidate_fixture() as (root, _, _, _, _, acceptance_path, _):
                original_validate = package.validate_release

                def mutate_after_validation(*args):
                    result = original_validate(*args)
                    (root / name).write_bytes(b"invalid replacement after candidate validation")
                    return result

                with patch.object(package, "validate_release", side_effect=mutate_after_validation), \
                     self.assertRaisesRegex(ValueError, "changed while packaging"):
                    package.package("release", "Release", acceptance_path, root / "out")
                self.assertFalse((root / "out").exists())

    def test_package_uses_validated_frozen_assets_despite_edits_before_contents_read(self):
        for channel in ("source-only", "experimental", "release"):
            with self.subTest(channel=channel), candidate_fixture() as (root, _, _, candidate, _, acceptance_path, _):
                originals = {name: (root / name).read_bytes() for name in package.DISTRIBUTION_FILES}
                original_validate = package.validate_asset_snapshot

                def mutate_after_asset_validation(snapshot):
                    original_validate(snapshot)
                    for name in package.DISTRIBUTION_FILES:
                        (root / name).write_bytes(b"invalid later mutable checkout content")

                with patch.object(package, "validate_asset_snapshot", side_effect=mutate_after_asset_validation):
                    target = package.package(channel, "Release", acceptance_path, root / "out")
                with zipfile.ZipFile(target) as archive:
                    status = json.loads(archive.read("PACKAGE-STATUS.json"))
                    for name, expected in originals.items():
                        if channel == "source-only":
                            entry = "DarkFogSynthesis-0.1.0-source/" + name
                        elif name == "assets/generated/assets-manifest.json":
                            entry = "ASSET-AUDIT.json"
                        elif name.startswith("assets/generated/"):
                            entry = "BepInEx/plugins/DarkFogSynthesis/assets/" + Path(name).name
                        else:
                            entry = name
                        self.assertEqual(archive.read(entry), expected)
                        self.assertEqual(status["files"][entry], package.hashlib.sha256(expected).hexdigest())
                    if channel != "source-only":
                        self.assertEqual(status["buildIdentity"], candidate["buildIdentity"])

    def test_source_package_rejects_invalid_frozen_manifest_and_png(self):
        for name in ("manifest.json", "assets/generated/energy-analysis.png"):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                seed_distribution(root)
                (root / name).write_bytes(b"invalid file")
                with patch.object(package, "ROOT", root), self.assertRaises(ValueError):
                    package.package("source-only", "Release", None, root / "out")
                self.assertFalse((root / "out").exists())

    def test_atomic_publication_refuses_collision_created_at_publish_time(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            target = root / "package.zip"
            original_link = os.link

            def collision_wins(source, destination):
                Path(destination).write_bytes(b"other process won this destination")
                return original_link(source, destination)

            with patch.object(package.os, "link", side_effect=collision_wins), self.assertRaisesRegex(ValueError, "already exists"):
                package.publish_archive({"ordinary.txt": b"fixture"}, {}, target)
            self.assertEqual(target.read_bytes(), b"other process won this destination")
            self.assertEqual(list(root.iterdir()), [target])

    def test_atomic_publication_never_replaces_symlink_or_directory(self):
        for kind in ("symlink", "directory", "dangling-symlink"):
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                target, unrelated = root / "package.zip", root / "unrelated.txt"
                unrelated.write_bytes(b"preserve unrelated bytes")
                if kind == "directory": target.mkdir()
                else: target.symlink_to(unrelated if kind == "symlink" else root / "missing")
                with self.assertRaisesRegex(ValueError, "already exists"):
                    package.publish_archive({"ordinary.txt": b"fixture"}, {}, target)
                self.assertEqual(unrelated.read_bytes(), b"preserve unrelated bytes")
                self.assertTrue(target.is_dir() if kind == "directory" else target.is_symlink())
                self.assertFalse(list(root.glob(".darkfog-package-*")))

    def test_failed_archive_write_and_validation_leave_no_partial_destination(self):
        for method in ("writestr", "testzip"):
            with self.subTest(method=method), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                target = root / "package.zip"
                unrelated = root / "unrelated.txt"
                unrelated.write_bytes(b"preserve")
                with patch.object(package.zipfile.ZipFile, method, side_effect=OSError("injected archive failure")), \
                     self.assertRaisesRegex(OSError, "injected archive failure"):
                    package.publish_archive({"ordinary.txt": b"fixture"}, {}, target)
                self.assertFalse(target.exists())
                self.assertEqual(list(root.iterdir()), [unrelated])
                self.assertEqual(unrelated.read_bytes(), b"preserve")

    def test_publication_failure_cleanup_never_unlinks_replaced_destination(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            target = root / "package.zip"
            original_link = os.link

            def replace_destination_then_fail(source, destination):
                original_link(source, destination)
                replacement = root / "replacement.txt"
                replacement.write_bytes(b"replacement owned by another process")
                replacement.replace(destination)
                raise OSError("injected post-publication failure")

            with patch.object(package.os, "link", side_effect=replace_destination_then_fail), \
                 self.assertRaisesRegex(OSError, "injected post-publication failure"):
                package.publish_archive({"ordinary.txt": b"fixture"}, {}, target)
            self.assertEqual(target.read_bytes(), b"replacement owned by another process")
            self.assertEqual(list(root.iterdir()), [target])

    def test_archive_is_verified_before_destination_becomes_visible(self):
        with tempfile.TemporaryDirectory() as tmp:
            target = Path(tmp) / "package.zip"
            original_test = zipfile.ZipFile.testzip

            def verify_private_archive(archive):
                self.assertFalse(target.exists())
                return original_test(archive)

            with patch.object(package.zipfile.ZipFile, "testzip", autospec=True, side_effect=verify_private_archive):
                checksum = package.publish_archive({"ordinary.txt": b"fixture"}, {}, target)
            self.assertEqual(checksum, package.digest(target))



if __name__ == "__main__":
    unittest.main(verbosity=2)
