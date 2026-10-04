#!/usr/bin/env python3
"""Exercise real Core compilation and production provenance validation in a copy.

No game references, game-shaped stubs, plugin execution or installed configuration
are needed. This test never builds or mutates the caller's checkout.
"""
from __future__ import annotations

import argparse
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile

import package


def run(dotnet: str) -> None:
    source_root = package.ROOT
    source = package.freeze_sources()
    with tempfile.TemporaryDirectory(prefix="darkfog-real-capture-") as temporary:
        root = Path(temporary) / "project"
        root.mkdir()
        for name, data in source.items():
            target = root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        project = root / "src/DarkFogSynthesis.Core"
        output = project / "bin/Release/netstandard2.0/DarkFogSynthesis.Core.dll"
        sidecar = output.with_name("DarkFogSynthesis.Core.build-inputs.txt")
        marker = project / "UncompiledDefaultItemProbe.cs"
        assertions = 0

        def check(condition: bool, message: str) -> None:
            nonlocal assertions
            assertions += 1
            if not condition:
                raise AssertionError(message)

        def build(*, failure: str | None = None, extra: tuple[str, ...] = ()) -> None:
            result = subprocess.run(
                [dotnet, "build", str(project / "DarkFogSynthesis.Core.csproj"),
                 "--configuration", "Release", "--nologo",
                 "-p:DarkFogReferenceMode=reference-assembly-smoke", *extra],
                cwd=root, capture_output=True, text=True, timeout=600,
                env={**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
            log = result.stdout + result.stderr
            if failure is None:
                check(result.returncode == 0, "Real Core build failed:\n" + log)
            else:
                check(result.returncode != 0 and failure in log,
                      "Expected compiler refusal was not observed:\n" + log)

        def validate():
            inventory = package.production_inventory()
            try:
                return package.validate_compiler_capture(
                    "DarkFogSynthesis.Core", "netstandard2.0", "Release",
                    "reference-assembly-smoke", output, inventory)
            except ValueError as error:
                captured = {Path(parts[1]).relative_to(root).as_posix(): parts[2].lower()
                            for line in sidecar.read_text(encoding="utf-8-sig").splitlines()
                            if (parts := line.split("|"))[0] == "inventory"}
                raise ValueError(
                    f"{error} Missing inventory: {sorted(inventory.keys() - captured.keys())}; "
                    f"extra inventory: {sorted(captured.keys() - inventory.keys())}; "
                    f"changed inventory: {sorted(k for k in captured.keys() & inventory.keys() if captured[k] != inventory[k])}"
                ) from error

        def refusal(label: str) -> None:
            try:
                validate()
            except ValueError:
                check(True, label)
            else:
                raise AssertionError("Capture accepted " + label)

        try:
            package.ROOT = root
            build()
            validate()
            check(output.is_file(), "Core DLL missing after actual build")
            baseline_dll = hashlib.sha256(output.read_bytes()).hexdigest()
            baseline_capture = sidecar.read_bytes()

            # Deliberately uncompilable and default-included. No build occurs
            # between adding it and asking the production validator to qualify
            # the old output, which is the reported re-recording failure window.
            marker.write_text("#error DARKFOG_UNCOMPILED_INPUT_PROBE\n", encoding="utf-8")
            refusal("new default-included source without compilation")
            check(hashlib.sha256(output.read_bytes()).hexdigest() == baseline_dll,
                  "Validation unexpectedly changed the real DLL")
            check(sidecar.read_bytes() == baseline_capture,
                  "Validation unexpectedly changed compiler provenance")

            build(failure="DARKFOG_UNCOMPILED_INPUT_PROBE")
            refusal("failed compilation with an old DLL still on disk")
            marker.write_text(
                "namespace DarkFogSynthesis.Core { public static class BuildCaptureProbeMarker {} }\n",
                encoding="utf-8")
            build()
            validate()
            check(b"BuildCaptureProbeMarker" in output.read_bytes(),
                  "Repaired source marker is absent from real PE metadata")
            check(str(marker) in sidecar.read_text(encoding="utf-8-sig"),
                  "New source is absent from the successful compiler capture")

            # A provenance-generating build must actually run the compiler, not
            # stamp changed bytes around an incrementally skipped old output.
            compiled_source = marker.read_bytes()
            stat = marker.stat()
            marker.write_text("#error DARKFOG_PRESERVED_MTIME_PROBE\n", encoding="utf-8")
            os.utime(marker, ns=(stat.st_atime_ns, stat.st_mtime_ns))
            build(failure="DARKFOG_PRESERVED_MTIME_PROBE")
            refusal("failed compilation after a preserved-timestamp source edit")
            marker.write_bytes(compiled_source)
            os.utime(marker, ns=(stat.st_atime_ns, stat.st_mtime_ns))
            build()
            validate()

            # Files appearing after initial SDK Compile-item evaluation must
            # not enter an accepted inventory without entering compilation.
            project_file = project / "DarkFogSynthesis.Core.csproj"
            original_project = project_file.read_bytes()
            injected = project / "InjectedDuringBuild.cs"
            for scheduling in ('BeforeTargets="CaptureDarkFogCompilerInputs"',
                               'AfterTargets="CoreCompile"'):
                hook = (
                    f'<Target Name="InjectInventoryProbe" {scheduling}>'
                    '<WriteLinesToFile File="$(MSBuildProjectDirectory)/InjectedDuringBuild.cs" '
                    'Lines="#error DARKFOG_BUILD_WINDOW_PROBE" Overwrite="true" />'
                    '</Target>\n'
                ).encode("utf-8")
                project_file.write_bytes(original_project.replace(b"</Project>", hook + b"</Project>"))
                build(failure="inventory")
                check(injected.is_file(), "The mutation target did not execute")
                check("completed|" not in sidecar.read_text(encoding="utf-8-sig"),
                      "A changing inventory received a completed capture")
                refusal("source mutation in the " + scheduling + " window")
                injected.unlink()
                project_file.write_bytes(original_project)
                build()
                validate()

            build(failure="SkipCompilerExecution", extra=("-p:SkipCompilerExecution=true",))
            refusal("explicitly skipped compiler execution")
            build()
            validate()

            # Membership changes must be detected even for extensions absent
            # from the packaging allowlist and for wildcard build imports.
            for relative in ("src/DarkFogSynthesis.Core/Added.resx", "build/Added.props"):
                added = root / relative
                added.parent.mkdir(parents=True, exist_ok=True)
                added.write_text("<root />\n", encoding="utf-8")
                refusal("added inventory member " + relative)
                added.unlink()
                validate()

            renamed = marker.with_name("RenamedProbe.cs")
            marker.rename(renamed)
            refusal("renamed compiled source")
            renamed.rename(marker)
            validate()
            saved = marker.read_bytes()
            marker.unlink()
            refusal("removed compiled source")
            marker.write_bytes(saved)
            validate()
            print(f"PASS: genuine Core build/capture regression, {assertions} assertions; "
                  "uncompiled additions, compiler failure/recovery, preserved timestamps, "
                  "build-window mutations, skipped compiler, resources/imports, rename/delete.")
            print("Scope: isolated real Core compilation and production metadata guards; no DSP, plugin execution or saves.")
        finally:
            package.ROOT = source_root


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet", help=".NET SDK executable")
    run(parser.parse_args().dotnet)
