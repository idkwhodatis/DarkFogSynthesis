#!/usr/bin/env python3
"""Allowlist-only source or runtime packaging. Never packages game/dependency DLLs."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OWN_DLLS = ("DarkFogSynthesis.dll", "DarkFogSynthesis.Core.dll")
CHECK_IDS = tuple(f"{prefix}{n:02}" for prefix, count in (("D", 4), ("R", 6), ("T", 6), ("S", 7), ("U", 5), ("I", 3), ("C", 3), ("V", 1)) for n in range(1, count + 1))
TOP_FILES = ("README.md", "LICENSE", "manifest.json", "icon.png", "Directory.Build.props", "Local.Build.props.example", ".gitignore", "DarkFogSynthesis_Implementation_Plan_ZH.md")
SOURCE_ROOTS = ("src", "tests", "scripts", "docs", "assets", ".github")
SOURCE_EXTENSIONS = {".cs", ".csproj", ".md", ".json", ".ps1", ".py", ".svg", ".png", ".yml", ".yaml", ".txt"}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_files() -> dict[str, Path]:
    result = {}
    for name in TOP_FILES:
        path = ROOT / name
        if path.is_file() and not path.is_symlink():
            result[name] = path
    for folder in SOURCE_ROOTS:
        for path in (ROOT / folder).rglob("*"):
            relative = path.relative_to(ROOT)
            if not path.is_file() or path.is_symlink() or any(p in {"bin", "obj", "__pycache__", ".git"} for p in relative.parts):
                continue
            if path.suffix.lower() in SOURCE_EXTENSIONS:
                result[relative.as_posix()] = path
    return dict(sorted(result.items()))


def source_fingerprint() -> str:
    # Evidence and docs change during acceptance; bind provenance to build inputs.
    entries = []
    for name, path in source_files().items():
        if name.startswith(("src/", "assets/source/", "assets/generated/")) or name in {"Directory.Build.props", "manifest.json"}:
            entries.append(f"{name}\0{digest(path)}\n")
    return hashlib.sha256("".join(entries).encode("utf-8")).hexdigest()


def validate_manifest() -> dict:
    manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
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
    if any(directory.rglob("DarkFogSynthesis.resources.dll")):
        raise ValueError("Unexpected localization satellite DLL; clean and rebuild the culture-neutral main assembly.")
    result = {name: directory / name for name in OWN_DLLS}
    for name, path in result.items():
        if not path.is_file() or path.is_symlink() or path.stat().st_size < 1024 or path.read_bytes()[:2] != b"MZ":
            raise ValueError(f"Missing genuine build output: {name}. Build with lawful local game references, or choose source-only.")
    return result


def validate_resource_audit(paths: dict[str, Path]) -> dict:
    audit_path = ROOT / "artifacts/resource-audit.json"
    if not audit_path.is_file() or audit_path.is_symlink():
        raise ValueError("No compiled-resource audit. Run Build.ps1 successfully before recording or packaging a runtime build.")
    audit = json.loads(audit_path.read_text(encoding="utf-8"))
    if audit.get("schemaVersion") != 1 or audit.get("satelliteFree") is not True or audit.get("assemblySha256") != digest(paths["DarkFogSynthesis.dll"]):
        raise ValueError("Compiled-resource audit is incomplete or does not match the runtime DLL.")
    expected = []
    for language in ("en-US", "zh-CN"):
        source_name = f"Strings.{language}.json"
        source = ROOT / "src/DarkFogSynthesis/Localization" / source_name
        expected.append({
            "name": "DarkFogSynthesis.Localization." + source_name,
            "sourceName": source_name,
            "sha256": digest(source),
            "keyCount": len(json.loads(source.read_text(encoding="utf-8"))),
        })
    if audit.get("resources") != expected:
        raise ValueError("Compiled-resource audit does not match both approved localization sources.")
    return audit


def record_build(configuration: str, reference_mode: str) -> None:
    paths = runtime_paths(configuration)
    resource_audit = validate_resource_audit(paths)
    public_api_audit = validate_public_api_audit(paths)
    report = {
        "schemaVersion": 1,
        "runtimeBuildCompleted": True,
        "referenceMode": reference_mode,
        "runtimeExecution": "not_executed",
        "installedGameValidated": False,
        "configuration": configuration,
        "sourceFingerprint": source_fingerprint(),
        "assemblies": {name: digest(path) for name, path in paths.items()},
        "resourceAudit": resource_audit,
        "publicApiAudit": public_api_audit,
        "notice": "Build.ps1 records this after pure tests, runtime compilation, compiled-resource validation and public API accessibility audit succeed. These checks do not establish game compatibility.",
    }
    target = ROOT / "artifacts/build-report.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Recorded runtime build provenance: {target.relative_to(ROOT)}")


def validate_reference_mode(report: dict, channel: str | None = None) -> str:
    mode = report.get("referenceMode")
    if mode not in {"installed-local", "reference-assembly-smoke"}:
        raise ValueError("Build report must identify installed-local or reference-assembly-smoke references.")
    if channel == "release" and mode != "installed-local":
        raise ValueError("Release blocked: reference-assembly smoke compilation is experimental only.")
    return mode


def validate_public_api_audit(paths: dict[str, Path]) -> dict:
    audit_path = ROOT / "artifacts/public-api-audit.json"
    if not audit_path.is_file() or audit_path.is_symlink():
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


def validate_build(configuration: str) -> dict[str, Path]:
    paths = runtime_paths(configuration)
    report_path = ROOT / "artifacts/build-report.json"
    if not report_path.is_file():
        raise ValueError("No runtime build provenance. Run Build.ps1 successfully before packaging.")
    report = json.loads(report_path.read_text(encoding="utf-8"))
    validate_reference_mode(report)
    if report.get("runtimeBuildCompleted") is not True or report.get("configuration") != configuration or report.get("sourceFingerprint") != source_fingerprint():
        raise ValueError("Runtime build provenance is stale or incomplete. Rebuild the current inputs.")
    if report.get("assemblies") != {name: digest(path) for name, path in paths.items()}:
        raise ValueError("Runtime DLL hashes do not match the successful build report.")
    if report.get("resourceAudit") != validate_resource_audit(paths):
        raise ValueError("Build provenance lacks the matching compiled-resource audit. Rebuild and audit the current inputs.")
    if report.get("publicApiAudit") != validate_public_api_audit(paths):
        raise ValueError("Build provenance lacks the matching public API accessibility audit. Rebuild and audit the current inputs.")
    return paths


def validate_release(acceptance_path: Path | None) -> None:
    if acceptance_path is None:
        raise ValueError("Release requires an explicit --acceptance report. Experimental/source-only packages make no release claim.")
    report = json.loads(acceptance_path.read_text(encoding="utf-8"))
    if report.get("schemaVersion") != 1 or report.get("releaseEligible") is not True or report.get("runtimeValidated") is not True or report.get("approvedForRelease") is not True:
        raise ValueError("Release blocked: acceptance has not explicitly passed and been approved.")
    if report.get("sourceFingerprint") != source_fingerprint():
        raise ValueError("Release blocked: acceptance is not bound to the current build inputs.")
    environment = report.get("environment", {})
    if not environment.get("dspVersion") or not environment.get("unityVersion") or not environment.get("runtimeFramework"):
        raise ValueError("Release requires the actual DSP, Unity and framework versions.")
    for name in ("BepInEx", "LDBTool", "CommonAPI", "DSPModSave"):
        dep = environment.get("dependencies", {}).get(name, {})
        if not dep.get("packageVersion") or not dep.get("assemblyVersion") or not re.fullmatch(r"[0-9a-f]{64}", dep.get("sha256", "")):
            raise ValueError(f"Release requires recorded package/assembly version and checksum for {name}.")
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
            full = ROOT / path
            if not full.is_file() or full.is_symlink() or any(parent.is_symlink() for parent in full.parents if parent != ROOT) or full.stat().st_size == 0:
                raise ValueError(f"Missing release evidence: {relative}")
    print("Release acceptance fields and evidence files validated; their factual correctness remains the tester's responsibility.")


def package(channel: str, configuration: str, acceptance: Path | None, output_dir: Path) -> Path:
    manifest = validate_manifest()
    subprocess.run([sys.executable, str(ROOT / "scripts/generate-assets.py"), "--check"], check=True)
    files: dict[str, Path] = {}
    reference_mode = None
    if channel == "source-only":
        prefix = f"DarkFogSynthesis-{manifest['version_number']}-source/"
        files = {prefix + name: path for name, path in source_files().items()}
    else:
        if channel == "release":
            validate_release(acceptance)
        dlls = validate_build(configuration)
        build_report_path = ROOT / "artifacts/build-report.json"
        reference_mode = validate_reference_mode(json.loads(build_report_path.read_text(encoding="utf-8")), channel)
        files = {name: ROOT / name for name in ("manifest.json", "README.md", "icon.png", "LICENSE")}
        files["BUILD-STATUS.json"] = build_report_path
        files["RESOURCE-AUDIT.json"] = ROOT / "artifacts/resource-audit.json"
        files["PUBLIC-API-AUDIT.json"] = ROOT / "artifacts/public-api-audit.json"
        for name, path in dlls.items():
            files[f"BepInEx/plugins/DarkFogSynthesis/{name}"] = path
        for name in ("energy-analysis.png", "information-topology.png"):
            files[f"BepInEx/plugins/DarkFogSynthesis/assets/{name}"] = ROOT / "assets/generated" / name
        for name, path in source_files().items():
            if name.startswith("docs/") or name.startswith("assets/source/") or name == "assets/README.md":
                files[name] = path
        files["CHANGELOG.md"] = ROOT / "docs/CHANGELOG.md"
    for name, path in files.items():
        if path.suffix.lower() in {".dll", ".exe", ".so", ".dylib"} and (channel == "source-only" or path.name not in OWN_DLLS):
            raise ValueError(f"Refusing foreign binary: {name}")
        if not path.is_file() or path.is_symlink():
            raise ValueError(f"Missing, non-file or symlink package entry: {name}")
    output_dir.mkdir(parents=True, exist_ok=True)
    target = output_dir / f"DarkFogSynthesis-{manifest['version_number']}-{channel}.zip"
    if target.exists():
        raise ValueError(f"Output already exists; select a new output directory instead of silently replacing it: {target}")
    # Stable ZIP metadata: repeated packaging of identical input creates identical bytes.
    with zipfile.ZipFile(target, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, path in sorted(files.items()):
            info = zipfile.ZipInfo(name, date_time=(2026, 10, 4, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, path.read_bytes())
        notice = {
            "channel": channel,
            "installable": channel != "source-only",
            "releaseAcceptanceValidated": channel == "release",
            "referenceMode": reference_mode,
            "sourceFingerprint": source_fingerprint(),
            "warning": "SOURCE ONLY: not an installable mod." if channel == "source-only" else (("EXPERIMENTAL REFERENCE-ASSEMBLY SMOKE BUILD: no installed game has been validated; isolated test profile and copied saves only." if reference_mode == "reference-assembly-smoke" else "EXPERIMENTAL: game/save/integrity compatibility unverified; use a separate profile and copied saves.") if channel == "experimental" else "Validated only for the recorded acceptance environment."),
            "files": {name: digest(path) for name, path in sorted(files.items())},
        }
        info = zipfile.ZipInfo("PACKAGE-STATUS.json", date_time=(2026, 10, 4, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o100644 << 16
        archive.writestr(info, json.dumps(notice, indent=2) + "\n")
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise ValueError("Archive integrity check failed")
    print(f"Created {channel} archive: {target}; sha256={digest(target)}; {len(files)} allowlisted files")
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
    args = parser.parse_args()
    if args.fingerprint:
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
