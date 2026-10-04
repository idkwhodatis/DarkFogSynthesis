#!/usr/bin/env python3
"""Allowlist-only source or runtime packaging. Never packages game/dependency DLLs."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import zipfile

# Preserve lexical ancestors so a symlink used to reach the project cannot be
# hidden by resolving __file__ before the input-path guard runs.
ROOT = Path(__file__).absolute().parents[1]
OWN_DLLS = ("DarkFogSynthesis.dll", "DarkFogSynthesis.Core.dll")
CHECK_IDS = tuple(f"{prefix}{n:02}" for prefix, count in (("D", 4), ("R", 6), ("T", 6), ("S", 7), ("U", 5), ("I", 3), ("C", 3), ("V", 1)) for n in range(1, count + 1))
TOP_FILES = ("README.md", "LICENSE", "manifest.json", "icon.png", "Directory.Build.props", "Directory.Build.targets", "Local.Build.props.example", "global.json", ".gitignore", "DarkFogSynthesis_Implementation_Plan_ZH.md")
SOURCE_ROOTS = ("src", "tests", "scripts", "docs", "assets", ".github")
# Logical project paths form part of the tested candidate, including metadata
# used outside the compiled DLLs. The generated package icon is bound by the
# source fingerprint and verified to be byte-identical to the root icon.
DISTRIBUTION_FILES = ("manifest.json", "icon.png", "assets/generated/assets-manifest.json",
                      "assets/generated/energy-analysis.png", "assets/generated/information-topology.png")
ASSET_CHECK_FILES = ("scripts/generate-assets.py", "icon.png", "assets/generated/assets-manifest.json",
                     *(f"assets/{folder}/{name}.{extension}" for folder, extension in (("source", "svg"), ("generated", "png"))
                       for name in ("energy-analysis", "information-topology", "package-icon")))
SOURCE_EXTENSIONS = {".cs", ".csproj", ".md", ".json", ".ps1", ".py", ".svg", ".png", ".yml", ".yaml", ".txt"}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def reject_symlink_components(path: Path) -> None:
    # Check ancestors before resolving or reading the leaf. is_file/is_dir alone
    # would follow a symlinked root, directory, or file outside the project.
    for component in (*reversed(path.absolute().parents), path.absolute()):
        if component.is_symlink():
            raise ValueError(f"Symlink package/build input is forbidden: {component}")


def project_path(path: Path, *, file: bool = True) -> Path:
    reject_symlink_components(ROOT)
    reject_symlink_components(path)
    # Keep lexical components until the symlink checks finish, then use one
    # absolute spelling for containment and frozen-snapshot lookups. CLI paths
    # are relative to the caller's working directory, not necessarily ROOT.
    resolved = path.resolve()
    if not resolved.is_relative_to(ROOT.resolve()):
        raise ValueError(f"Package/build input escapes the project root: {path}")
    if file and not resolved.is_file():
        raise ValueError(f"Missing or non-file project input: {path}")
    return resolved


def tree_files(directory: Path, *, exclude_build: bool = False):
    project_path(directory, file=False)
    for current, folders, names in os.walk(directory, followlinks=False):
        if exclude_build:
            folders[:] = [name for name in folders if name not in {"bin", "obj", "__pycache__", ".git"}]
        for name in folders:
            project_path(Path(current) / name, file=False)
        for name in names:
            yield project_path(Path(current) / name)


def source_files() -> dict[str, Path]:
    project_path(ROOT, file=False)
    result = {}
    for name in TOP_FILES:
        path = project_path(ROOT / name, file=False)
        if path.is_file():
            result[name] = path
    for folder in SOURCE_ROOTS:
        for path in tree_files(ROOT / folder, exclude_build=True):
            relative = path.relative_to(ROOT.resolve())
            if path.suffix.lower() in SOURCE_EXTENSIONS:
                result[relative.as_posix()] = path
    return dict(sorted(result.items()))


def is_build_source(name: str) -> bool:
    return name.startswith(("src/", "assets/source/", "assets/generated/")) or name in {"Directory.Build.props", "Directory.Build.targets", "manifest.json", "global.json"}


def source_fingerprint_from_bytes(files: dict[str, bytes]) -> str:
    """Fingerprint a single frozen source snapshot, excluding docs/evidence."""
    entries = []
    for name, content in sorted(files.items()):
        if is_build_source(name):
            entries.append(f"{name}\0{hashlib.sha256(content).hexdigest()}\n")
    return hashlib.sha256("".join(entries).encode("utf-8")).hexdigest()


def source_fingerprint() -> str:
    # Evidence and docs change during acceptance; bind provenance to build inputs.
    return source_fingerprint_from_bytes({name: path.read_bytes() for name, path in source_files().items() if is_build_source(name)})


def freeze_sources() -> dict[str, bytes]:
    return {name: project_path(path).read_bytes() for name, path in source_files().items()}


def distribution_hashes(snapshot: dict[str, bytes]) -> dict[str, str]:
    return {name: hashlib.sha256(snapshot[name]).hexdigest() for name in DISTRIBUTION_FILES}


def validate_asset_snapshot(snapshot: dict[str, bytes]) -> None:
    """Run the existing image/pixel/hash validator only against frozen bytes.

    This private staging tree has no links to mutable checkout inputs. Its
    checker, SVGs, PNGs and asset manifest all come from the same snapshot used
    for candidate recording or packaging; nothing is re-read from the checkout.
    """
    with tempfile.TemporaryDirectory(prefix="darkfog-assets-") as temporary:
        stage = Path(temporary)
        for name in ASSET_CHECK_FILES:
            path = stage / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(snapshot[name])
        try:
            subprocess.run([sys.executable, str(stage / "scripts/generate-assets.py"), "--check"],
                           check=True, capture_output=True, text=True)
        except subprocess.CalledProcessError as error:
            raise ValueError(f"Frozen asset validation failed: {error.stderr.strip()}") from error


def validate_manifest(content: bytes | None = None) -> dict:
    manifest = json.loads(project_path(ROOT / "manifest.json").read_bytes() if content is None else content)
    if set(manifest) != {"name", "version_number", "website_url", "description", "dependencies"}:
        raise ValueError("Unexpected or missing Thunderstore manifest fields")
    if not re.fullmatch(r"[A-Za-z0-9_]{1,128}", manifest["name"]):
        raise ValueError("Invalid Thunderstore name")
    if not re.fullmatch(r"\d+\.\d+\.\d+", manifest["version_number"]):
        raise ValueError("Thunderstore version must be Major.Minor.Patch")
    if not 1 <= len(manifest["description"]) <= 250:
        raise ValueError("Manifest description must be 1..250 characters")
    if not manifest["dependencies"] or any(not re.fullmatch(r"[A-Za-z0-9_]+-[A-Za-z0-9_]+-\d+\.\d+\.\d+", d) for d in manifest["dependencies"]):
        raise ValueError("Invalid dependency string")
    return manifest


def runtime_paths(configuration: str) -> dict[str, Path]:
    directory = ROOT / "src/DarkFogSynthesis/bin" / configuration / "net472"
    if any(path.name == "DarkFogSynthesis.resources.dll" for path in tree_files(directory)):
        raise ValueError("Unexpected localization satellite DLL; clean and rebuild the culture-neutral main assembly.")
    result = {name: directory / name for name in OWN_DLLS}
    for name, path in result.items():
        project_path(path, file=False)
        if not path.is_file() or path.stat().st_size < 1024 or path.read_bytes()[:2] != b"MZ":
            raise ValueError(f"Missing genuine build output: {name}. Build with lawful local game references, or choose source-only.")
    return result


def validate_resource_audit(paths: dict[str, Path]) -> dict:
    audit_path = ROOT / "artifacts/resource-audit.json"
    project_path(audit_path, file=False)
    if not audit_path.is_file():
        raise ValueError("No compiled-resource audit. Run Build.ps1 successfully before recording or packaging a runtime build.")
    audit = json.loads(audit_path.read_text(encoding="utf-8"))
    if audit.get("schemaVersion") != 1 or audit.get("satelliteFree") is not True or audit.get("assemblySha256") != digest(paths["DarkFogSynthesis.dll"]):
        raise ValueError("Compiled-resource audit is incomplete or does not match the runtime DLL.")
    expected = []
    for language in ("en-US", "zh-CN"):
        source_name = f"Strings.{language}.json"
        source = project_path(ROOT / "src/DarkFogSynthesis/Localization" / source_name)
        expected.append({
            "name": "DarkFogSynthesis.Localization." + source_name,
            "sourceName": source_name,
            "sha256": digest(source),
            "keyCount": len(json.loads(source.read_text(encoding="utf-8"))),
        })
    if audit.get("resources") != expected:
        raise ValueError("Compiled-resource audit does not match both approved localization sources.")
    return audit


def compiler_provenance(configuration: str, reference_mode: str, paths: dict[str, Path]) -> dict:
    """Read build-time captures; verify every captured compiler input still exists unchanged.

    Paths only occur in ignored local sidecars. Published provenance uses actual
    metadata identities and hashes, never paths guessed from dependency names.
    """
    references, captures = {}, {}
    for project, framework in (("DarkFogSynthesis", "net472"), ("DarkFogSynthesis.Core", "netstandard2.0")):
        sidecar = project_path(ROOT / "src" / project / "bin" / configuration / framework / f"{project}.build-inputs.txt")
        fields, resolved, inputs, outputs = {}, {}, {}, []
        for line in sidecar.read_text(encoding="utf-8-sig").splitlines():
            parts = line.split("|")
            kind = parts[0]
            if kind in {"reference", "input", "output"}:
                if len(parts) != (4 if kind == "reference" else 3):
                    raise ValueError("Malformed compiler input capture; rebuild using ordinary unambiguous file paths.")
                captured_path, expected = Path(parts[1]), parts[-1].lower()
                if not captured_path.is_absolute() or not re.fullmatch(r"[0-9a-f]{64}", expected):
                    raise ValueError("Compiler input capture has no absolute path or valid checksum.")
                reject_symlink_components(captured_path)
                if not captured_path.is_file() or digest(captured_path) != expected:
                    raise ValueError(f"Missing or changed compiled {kind}: {captured_path.name}. Rebuild and repeat acceptance.")
                if kind == "reference":
                    identity = parts[2]
                    if not re.fullmatch(r"[^,]+, Version=\d+\.\d+\.\d+\.\d+, Culture=[^,]+, PublicKeyToken=[^,]+", identity) or identity in resolved:
                        raise ValueError("Missing or duplicate resolved assembly identity in compiler capture.")
                    resolved[identity] = expected
                elif kind == "input":
                    inputs[str(captured_path)] = expected
                else:
                    project_path(captured_path)
                    outputs.append(expected)
            else:
                if len(parts) != 2 or kind in fields or not parts[1]:
                    raise ValueError("Incomplete or duplicate compiler build capture fields.")
                fields[kind] = parts[1]
        if (set(fields) != {"schema", "invocation", "project", "configuration", "referenceMode", "targetFramework", "sdk", "msbuild", "completed"}
                or fields.get("schema") != "1" or fields.get("project") != project
                or fields.get("configuration") != configuration or fields.get("referenceMode") != reference_mode
                or fields.get("targetFramework") != framework
                or not re.fullmatch(r"[0-9a-f-]{36}", fields.get("invocation", ""))
                or fields.get("completed") != fields.get("invocation") or not resolved or not inputs
                or outputs != [digest(paths[project + ".dll"])]):
            raise ValueError("Incomplete, stale or mismatched compiler build capture. Rebuild the candidate.")
        references[project] = dict(sorted(resolved.items()))
        captures[project] = {"invocationId": fields["invocation"], "targetFramework": framework,
                             "sdkVersion": fields["sdk"], "msbuildVersion": fields["msbuild"],
                             "captureSha256": digest(sidecar)}
    required = {"Assembly-CSharp", "BepInEx", "0Harmony", "CommonAPI", "LDBTool", "System.Web.Extensions",
                "UnityEngine", "UnityEngine.CoreModule"}
    if not required.issubset({identity.split(",", 1)[0] for identity in references["DarkFogSynthesis"]}):
        raise ValueError("Compiler capture lacks required resolved game/framework references.")
    return {"references": references, "compilerInputs": captures}


BUILD_BINDING_FIELDS = ("sourceFingerprint", "configuration", "referenceMode", "assemblies", "references", "compilerInputs", "distributionFiles")


def build_identity(report: dict) -> str:
    binding = {name: report[name] for name in BUILD_BINDING_FIELDS}
    return hashlib.sha256(json.dumps(binding, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def tested_build(report: dict) -> dict:
    return {name: report[name] for name in ("buildIdentity", *BUILD_BINDING_FIELDS)}


def validate_audited_reference(report: dict, public_api_audit: dict) -> None:
    game_hashes = [value for identity, value in report["references"]["DarkFogSynthesis"].items() if identity.startswith("Assembly-CSharp,")]
    if game_hashes != [public_api_audit["gameReferenceSha256"]]:
        raise ValueError("Public API audit game reference does not match the actual resolved compiler reference.")


def record_build(configuration: str, reference_mode: str) -> None:
    paths = runtime_paths(configuration)
    resource_audit = validate_resource_audit(paths)
    public_api_audit = validate_public_api_audit(paths)
    snapshot = freeze_sources()
    validate_manifest(snapshot["manifest.json"])
    validate_asset_snapshot(snapshot)
    report = {
        "schemaVersion": 3,
        "runtimeBuildCompleted": True,
        "referenceMode": reference_mode,
        "runtimeExecution": "not_executed",
        "installedGameValidated": False,
        "configuration": configuration,
        "sourceFingerprint": source_fingerprint_from_bytes(snapshot),
        "distributionFiles": distribution_hashes(snapshot),
        "assemblies": {name: digest(path) for name, path in paths.items()},
        **compiler_provenance(configuration, reference_mode, paths),
        "resourceAudit": resource_audit,
        "publicApiAudit": public_api_audit,
        "notice": "Build.ps1 records this after pure tests, runtime compilation, compiled-resource validation and public API accessibility audit succeed. These checks do not establish game compatibility.",
    }
    validate_reference_mode(report)
    validate_audited_reference(report, public_api_audit)
    report["buildIdentity"] = build_identity(report)
    target = project_path(ROOT / "artifacts/build-report.json", file=False)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Recorded runtime build provenance: {target.relative_to(ROOT.resolve())}")


def validate_reference_mode(report: dict, channel: str | None = None) -> str:
    mode = report.get("referenceMode")
    if mode not in {"installed-local", "reference-assembly-smoke"}:
        raise ValueError("Build report must identify installed-local or reference-assembly-smoke references.")
    if channel == "release" and mode != "installed-local":
        raise ValueError("Release blocked: reference-assembly smoke compilation is experimental only.")
    return mode


def validate_public_api_audit(paths: dict[str, Path]) -> dict:
    audit_path = ROOT / "artifacts/public-api-audit.json"
    project_path(audit_path, file=False)
    if not audit_path.is_file():
        raise ValueError("No public API accessibility audit. Run Build.ps1 successfully before recording or packaging a runtime build.")
    audit = json.loads(audit_path.read_text(encoding="utf-8"))
    if (audit.get("schemaVersion") != 1 or audit.get("status") != "passed"
            or audit.get("runtimeExecution") != "not_executed"
            or audit.get("nonpublicAccesses") != 0 or audit.get("unresolvedReferences") != 0
            or audit.get("assemblySha256") != digest(paths["DarkFogSynthesis.dll"])
            or not re.fullmatch(r"[0-9a-f]{64}", audit.get("gameReferenceSha256", ""))
            or any(not isinstance(audit.get(key), int) or audit[key] <= 0 for key in ("directGameMemberSites", "uniqueGameMembers", "gameTypes"))):
        raise ValueError("Public API accessibility audit is incomplete, failed or does not match the runtime DLL.")
    return audit


def validate_build(configuration: str, *, with_report: bool = False):
    paths = runtime_paths(configuration)
    report_path = ROOT / "artifacts/build-report.json"
    project_path(report_path, file=False)
    if not report_path.is_file():
        raise ValueError("No runtime build provenance. Run Build.ps1 successfully before packaging.")
    report_bytes = report_path.read_bytes()
    report = json.loads(report_bytes)
    validate_reference_mode(report)
    snapshot = freeze_sources()
    if report.get("schemaVersion") != 3 or report.get("runtimeBuildCompleted") is not True or report.get("configuration") != configuration or report.get("sourceFingerprint") != source_fingerprint_from_bytes(snapshot):
        raise ValueError("Runtime build provenance is stale or incomplete. Rebuild the current inputs.")
    if report.get("distributionFiles") != distribution_hashes(snapshot):
        raise ValueError("Distribution assets or manifest do not match the tested candidate. Rebuild and repeat acceptance.")
    if report.get("assemblies") != {name: digest(path) for name, path in paths.items()}:
        raise ValueError("Runtime DLL hashes do not match the successful build report.")
    provenance = compiler_provenance(configuration, report["referenceMode"], paths)
    if any(report.get(key) != value for key, value in provenance.items()) or report.get("buildIdentity") != build_identity(report):
        raise ValueError("Build identity or resolved compiler references are stale or incomplete. Rebuild the candidate.")
    if report.get("resourceAudit") != validate_resource_audit(paths):
        raise ValueError("Build provenance lacks the matching compiled-resource audit. Rebuild and audit the current inputs.")
    if report.get("publicApiAudit") != validate_public_api_audit(paths):
        raise ValueError("Build provenance lacks the matching public API accessibility audit. Rebuild and audit the current inputs.")
    validate_audited_reference(report, report["publicApiAudit"])
    return (paths, report, report_bytes) if with_report else paths


def validate_release(acceptance_path: Path | None, configuration: str = "Release") -> tuple:
    if acceptance_path is None:
        raise ValueError("Release requires an explicit --acceptance report. Experimental/source-only packages make no release claim.")
    acceptance_bytes = project_path(acceptance_path).read_bytes()
    report = json.loads(acceptance_bytes)
    if report.get("schemaVersion") != 3 or report.get("releaseEligible") is not True or report.get("runtimeValidated") is not True or report.get("approvedForRelease") is not True:
        raise ValueError("Release blocked: acceptance has not explicitly passed and been approved.")
    if report.get("sourceFingerprint") != source_fingerprint():
        raise ValueError("Release blocked: acceptance is not bound to the current build inputs.")
    paths, candidate, candidate_bytes = validate_build(configuration, with_report=True)
    validate_reference_mode(candidate, "release")
    if report.get("testedBuild") != tested_build(candidate):
        raise ValueError("Release blocked: acceptance does not identify the exact tested candidate build, DLLs, resolved references and distribution files.")
    environment = report.get("environment", {})
    if not environment.get("dspVersion") or not environment.get("unityVersion") or not environment.get("runtimeFramework"):
        raise ValueError("Release requires the actual DSP, Unity and framework versions.")
    for name in ("BepInEx", "LDBTool", "CommonAPI", "DSPModSave"):
        dep = environment.get("dependencies", {}).get(name, {})
        if not dep.get("packageVersion") or not dep.get("assemblyVersion") or not re.fullmatch(r"[0-9a-f]{64}", dep.get("sha256", "")):
            raise ValueError(f"Release requires recorded package/assembly version and checksum for {name}.")
        # DSPModSave is a runtime-only transitive dependency, not a compiler
        # reference. Its installed identity remains mandatory target-game evidence.
        if name != "DSPModSave":
            matches = [(identity, value) for identity, value in candidate["references"]["DarkFogSynthesis"].items() if identity.startswith(name + ",")]
            if len(matches) != 1 or matches[0][1] != dep["sha256"] or f", Version={dep['assemblyVersion']}," not in matches[0][0]:
                raise ValueError(f"Release environment differs from the resolved compiler dependency: {name}.")
    checks = report.get("checks", [])
    by_id = {check.get("id"): check for check in checks}
    if len(by_id) != len(checks) or set(by_id) != set(CHECK_IDS):
        raise ValueError("Release requires every acceptance ID exactly once (D01 through V01).")
    for key in CHECK_IDS:
        check = by_id[key]
        if check.get("status") != "passed" or check.get("execution") != "target_game" or not check.get("evidence"):
            raise ValueError(f"Release blocked: {key} has not passed in the target game with evidence.")
        if key == "I03" and check.get("onlineAuthorizationRecorded") is not True:
            raise ValueError("Release blocked: I03 must record explicit authorization for the online test.")
        for relative in check["evidence"]:
            path = Path(relative)
            if path.is_absolute() or ".." in path.parts or not path.parts[:3] == ("docs", "compatibility", "evidence"):
                raise ValueError(f"Release evidence must be beneath docs/compatibility/evidence: {relative}")
            full = project_path(ROOT / path)
            if full.stat().st_size == 0:
                raise ValueError(f"Missing release evidence: {relative}")
    # Retain exactly what was validated. The packager must not re-read another
    # build/acceptance report and silently treat it as this validated candidate.
    return paths, candidate, candidate_bytes, report, acceptance_bytes


def validate_release_evidence_inventory(report: dict, contents: dict[str, bytes]) -> None:
    """Bind every evidence reference to the exact bytes that will enter the ZIP.

    The earlier filesystem checks cannot establish archive membership or size:
    the source allowlist may omit a file, or it may change before freezing.
    Never add excluded files or re-read the mutable checkout to satisfy this gate.
    """
    for check in report["checks"]:
        for relative in check["evidence"]:
            if relative not in contents:
                raise ValueError(f"Release evidence is absent from the final allowlisted package inventory: {relative}")
            if not contents[relative]:
                raise ValueError(f"Empty frozen release evidence: {relative}")


def publish_archive(contents: dict[str, bytes], notice: dict, target: Path) -> str:
    """Verify a private ZIP, then publish with atomic create-if-absent semantics.

    No partial archive is exposed at the destination. link() never replaces a
    collision winner (including symlinks/directories), unlike rename/replace.
    Failure cleanup touches only our private staging tree, never target: checking
    target's identity and then unlinking it would introduce another pathname race.
    """
    with tempfile.TemporaryDirectory(prefix=".darkfog-package-", dir=target.parent) as temporary:
        staged = Path(temporary) / "archive.zip"
        with staged.open("x+b") as stream:
            with zipfile.ZipFile(stream, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
                for name, content in (*contents.items(), ("PACKAGE-STATUS.json", (json.dumps(notice, indent=2) + "\n").encode("utf-8"))):
                    info = zipfile.ZipInfo(name, date_time=(2026, 10, 4, 0, 0, 0))
                    info.compress_type = zipfile.ZIP_DEFLATED
                    info.external_attr = 0o100644 << 16
                    archive.writestr(info, content)
            stream.seek(0)
            with zipfile.ZipFile(stream) as archive:
                if archive.testzip() is not None:
                    raise ValueError("Archive integrity check failed")
            stream.seek(0)
            checksum = hashlib.sha256(stream.read()).hexdigest()
            stream.flush()
            os.fsync(stream.fileno())
        try:
            os.link(staged, target)
        except FileExistsError as error:
            raise ValueError(f"Output already exists; select a new output directory instead of silently replacing it: {target}") from error
    return checksum


def package(channel: str, configuration: str, acceptance: Path | None, output_dir: Path) -> Path:
    # Enumerate and reject linked roots before any source/asset checker reads them.
    sources = source_files()
    reference_mode = None
    candidate = None
    candidate_bytes = None
    accepted_report = None
    acceptance_bytes = None
    if channel != "source-only":
        if channel == "release":
            dlls, candidate, candidate_bytes, accepted_report, acceptance_bytes = validate_release(acceptance, configuration)
        else:
            dlls, candidate, candidate_bytes = validate_build(configuration, with_report=True)
        reference_mode = validate_reference_mode(candidate, channel)

    # Freeze before validation. Reuse these bytes when constructing the archive,
    # so neither the manifest nor runtime PNGs can change between check and use.
    snapshot = {name: project_path(path).read_bytes() for name, path in sources.items()}
    if candidate and (candidate["sourceFingerprint"] != source_fingerprint_from_bytes(snapshot)
                      or candidate["distributionFiles"] != distribution_hashes(snapshot)):
        raise ValueError("Candidate sources, distribution assets or manifest changed while packaging; rebuild and repeat acceptance.")
    manifest = validate_manifest(snapshot["manifest.json"])
    validate_asset_snapshot(snapshot)
    if channel == "source-only":
        prefix = f"DarkFogSynthesis-{manifest['version_number']}-source/"
        files = {prefix + name: path for name, path in sources.items()}
    else:
        files = {name: ROOT / name for name in ("manifest.json", "README.md", "icon.png", "LICENSE")}
        files["BUILD-STATUS.json"] = ROOT / "artifacts/build-report.json"
        files["RESOURCE-AUDIT.json"] = ROOT / "artifacts/resource-audit.json"
        files["PUBLIC-API-AUDIT.json"] = ROOT / "artifacts/public-api-audit.json"
        files["ASSET-AUDIT.json"] = ROOT / "assets/generated/assets-manifest.json"
        if channel == "release":
            files["RELEASE-ACCEPTANCE.json"] = project_path(acceptance)
        for name, path in dlls.items():
            files[f"BepInEx/plugins/DarkFogSynthesis/{name}"] = path
        for name in ("energy-analysis.png", "information-topology.png"):
            files[f"BepInEx/plugins/DarkFogSynthesis/assets/{name}"] = ROOT / "assets/generated" / name
        for name, path in sources.items():
            if name.startswith("docs/") or name.startswith("assets/source/") or name == "assets/README.md":
                files[name] = path
        files["CHANGELOG.md"] = ROOT / "docs/CHANGELOG.md"
    contents = {}
    for name, path in sorted(files.items()):
        if path.suffix.lower() in {".dll", ".exe", ".so", ".dylib"} and (channel == "source-only" or path.name not in OWN_DLLS):
            raise ValueError(f"Refusing foreign binary: {name}")
        path = project_path(path)
        relative = path.relative_to(ROOT.resolve()).as_posix()
        contents[name] = snapshot[relative] if relative in snapshot else path.read_bytes()
    hashes = {name: hashlib.sha256(content).hexdigest() for name, content in contents.items()}
    package_fingerprint = candidate["sourceFingerprint"] if candidate else source_fingerprint_from_bytes(snapshot)
    if candidate:
        for name, expected in candidate["assemblies"].items():
            if hashes[f"BepInEx/plugins/DarkFogSynthesis/{name}"] != expected:
                raise ValueError("Runtime DLL changed while packaging; retry only after the build is stable.")
        if (contents["BUILD-STATUS.json"] != candidate_bytes
                or json.loads(contents["RESOURCE-AUDIT.json"]) != candidate["resourceAudit"]
                or json.loads(contents["PUBLIC-API-AUDIT.json"]) != candidate["publicApiAudit"]
                or (accepted_report is not None and (contents["RELEASE-ACCEPTANCE.json"] != acceptance_bytes
                    or accepted_report["testedBuild"] != tested_build(candidate)))):
            raise ValueError("Build/acceptance evidence changed while packaging; retry after the inputs are stable.")
    if accepted_report is not None:
        validate_release_evidence_inventory(accepted_report, contents)
        print("Release acceptance fields and frozen package evidence validated; their factual correctness remains the tester's responsibility.")
    output_dir.mkdir(parents=True, exist_ok=True)
    target = output_dir / f"DarkFogSynthesis-{manifest['version_number']}-{channel}.zip"
    notice = {
        "channel": channel,
        "installable": channel != "source-only",
        "releaseAcceptanceValidated": channel == "release",
        "referenceMode": reference_mode,
        "sourceFingerprint": package_fingerprint,
        "buildIdentity": candidate["buildIdentity"] if candidate else None,
        "warning": "SOURCE ONLY: not an installable mod." if channel == "source-only" else (("EXPERIMENTAL REFERENCE-ASSEMBLY SMOKE BUILD: no installed game has been validated; isolated test profile and copied saves only." if reference_mode == "reference-assembly-smoke" else "EXPERIMENTAL: game/save/integrity compatibility unverified; use a separate profile and copied saves.") if channel == "experimental" else "Validated only for the recorded acceptance environment."),
        "files": hashes,
    }
    checksum = publish_archive(contents, notice, target)
    print(f"Created {channel} archive: {target}; sha256={checksum}; {len(files)} allowlisted files")
    return target


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--channel", choices=("source-only", "experimental", "release"), default="source-only")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--acceptance", type=Path)
    parser.add_argument("--reference-mode", choices=("installed-local", "reference-assembly-smoke"), default="installed-local", help="Classification recorded by the build wrapper; never implies gameplay validation")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "artifacts/packages")
    parser.add_argument("--record-build", action="store_true", help=argparse.SUPPRESS)
    parser.add_argument("--fingerprint", action="store_true")
    parser.add_argument("--tested-build", action="store_true", help="Print the validated candidate binding to record before target-game acceptance; does not approve it")
    args = parser.parse_args()
    if args.tested_build:
        _, report, _ = validate_build(args.configuration, with_report=True)
        print(json.dumps(tested_build(report), indent=2))
    elif args.fingerprint:
        print(source_fingerprint())
    elif args.record_build:
        record_build(args.configuration, args.reference_mode)
    else:
        package(args.channel, args.configuration, args.acceptance, args.output_dir)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError, json.JSONDecodeError) as error:
        print(f"Packaging refused: {error}", file=sys.stderr)
        raise SystemExit(1)
