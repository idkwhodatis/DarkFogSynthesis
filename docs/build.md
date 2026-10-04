# Build and package contract

## Local references

The plugin compiles against `net472`; Core is `netstandard2.0`; the pure-test executable is `net8.0`. Install the official .NET SDK appropriate for those projects. The .NET Framework reference-assembly package is a compile-time NuGet dependency, not a substitute for a lawful game installation.

`global.json` selects the .NET 8 SDK and permits newer .NET 8 feature bands. This prevents a runner with SDK 10 installed from silently compiling `LangVersion=latest` using a different language version. The SDK pin is included in source packages and build fingerprints; CI prints `dotnet --info` before tests.

`Directory.Build.props` imports the ignored `Local.Build.props`. Copy `Local.Build.props.example` and set:

| Property | Meaning |
|---|---|
| `DSPGameDir` | Root of your own installed Dyson Sphere Program |
| `DSPManagedDir` | Game's `DSPGAME_Data/Managed` folder; may be derived from `DSPGameDir` |
| `BepInExDir` | Installed isolated-profile directory containing `BepInEx.dll` and `0Harmony.dll` |
| `CommonApiDir` | Directory containing installed `CommonAPI.dll` |
| `LdbToolDir` | Directory containing installed `LDBTool.dll` |

Do not commit `Local.Build.props`, game assemblies, dependency binaries, saves, account data, or private installation paths. References have `Private=false`; package generation additionally uses an explicit two-DLL allowlist.

## Commands

```powershell
./scripts/Build.ps1 -CoreOnly
./scripts/Build.ps1 -RegenerateAssets
./scripts/Build.ps1 -DotNet 'path/to/dotnet' -Python 'path/to/python'
./scripts/Package.ps1 -Channel source-only
./scripts/Package.ps1 -Channel experimental
```

`-CoreOnly` runs asset/content checks and the pure-test executable, without claiming runtime compilation. A full build runs the same checks, compiles the plugin against configured local references, runs metadata-only resource and original-API accessibility audits, then records schema-v3 build provenance in `artifacts/build-report.json`. This includes exact plugin/Core hashes, resolved compiler-reference identities and hashes, configuration, reference mode, source fingerprint, distribution-file hashes and an unambiguous build identity. No install/copy-to-game target runs. An incomplete or failed capture cannot qualify as current success provenance.

The default `-ReferenceMode installed-local` means references were supplied from the local installation; it still records `installedGameValidated=false` and `runtimeExecution=not_executed`. For an explicitly identified compile-only reference-assembly experiment, pass `-ReferenceMode reference-assembly-smoke`. That classification is preserved in `BUILD-STATUS.json` and the package notice and can produce an experimental package only. Reference-assembly compilation is not an installed-game test and cannot qualify for `release`. Never relabel a reference-assembly compile as a local game build.

On platforms without PowerShell, the content/test commands in the README and `python scripts/package.py` are usable directly. The packaging engine is Python standard library; texture validation additionally uses Pillow. `scripts/generate-assets.py` uses installed Inkscape only when regeneration is requested, and records its version. No font or extracted game resource is required.

## Package channels and safeguards

- `source-only` (default): source tree beneath a clearly named source directory, plus `PACKAGE-STATUS.json` with `installable=false`; no DLLs or installation claim
- `experimental`: requires both actual project DLLs plus a matching successful build report; includes own PNGs, source SVGs, documentation, license, icon and dependency manifest. No external binaries
- `release`: additionally requires an explicitly supplied schema-v3 acceptance report whose 35 IDs all passed in the recorded target game, whose evidence files exist, whose exact tested build (both DLL hashes, compiler dependencies, distribution files, configuration, reference mode and build identity) matches the current verified candidate, and whose `approvedForRelease`, `runtimeValidated` and `releaseEligible` flags are true

```powershell
./scripts/Package.ps1 -Channel release -AcceptancePath docs/compatibility/acceptance-status.json
```

`-AcceptancePath` / `--acceptance` accepts an absolute path or a path relative to the caller's current working directory, including nested paths and `.` / `..` components. The report must still resolve inside the project. Symlinked files, directories and project-root ancestors are rejected before path normalization; relative paths do not bypass these safeguards.

This command **must fail with the checked-in unverified report**. A compiler configuration called `Release` does not satisfy acceptance. The package guard checks data and evidence existence; it cannot determine whether a human's claimed gameplay evidence is truthful. Record real results, never flip flags merely to make packaging succeed.

All required checks, including online I03, must have real evidence. I03 must explicitly record authorization for the online test. Do not upload experimental save data merely to satisfy a checklist. Evidence belongs under `docs/compatibility/evidence/`; remove personal information before including it in a package. Never put save binaries, credentials or game DLLs there.

A deterministic ZIP includes only allowlisted file types and ignores `bin`, `obj`, private build settings and VCS metadata. Source roots, project-root ancestors, traversed directories and individual inputs must not be symlinks; violations fail closed before reading the linked content. All package inputs, including runtime assets, DLLs, audits and the explicit acceptance report, must resolve beneath the project root. Compiler references may reside outside the project, but are never packaged and must still match their recorded local hashes. Runtime DLLs are explicitly limited to `DarkFogSynthesis.dll` and `DarkFogSynthesis.Core.dll`; their hashes must match current build provenance. Source/asset bytes are frozen before validating the manifest and images. The existing pixel/hash validator runs against a private snapshot, and those exact frozen bytes become ZIP entries and their SHA-256 inventory. A later checkout edit cannot substitute a different PNG or manifest. Runtime packages also carry the validated asset manifest as `ASSET-AUDIT.json`. Entries receive a fixed timestamp.

The complete ZIP is written and verified in a private staging directory inside the output directory, then published using atomic hard-link creation, which fails if the destination already exists. There is no check-then-truncate window, no partial destination archive, and failure cleanup never unlinks the destination, even if another process replaced it. An existing file, directory or symlink is preserved. The output filesystem must support same-filesystem hard links; an unsupported publication fails without falling back to overwrite. Use a fresh `-OutputDirectory` / `--output-dir` on collision.

## Exact-build acceptance binding

`Directory.Build.targets` captures `ReferencePathWithRefAssemblies`, the resolved item list actually passed to the C# compiler, immediately before `CoreCompile` for both production projects. It uses MSBuild's metadata-only `GetAssemblyIdentity` and SHA-256 `GetFileHash` tasks; it does not infer assembly identities from filenames or configured search directories. This covers the game, Unity, BepInEx/Harmony, CommonAPI/LDBTool and framework references, including `System.Web.Extensions`, as well as Core's own compiler references. Source/resource/build-input hashes are captured too. Completion appends the emitted assembly's hash only after a successful `Build` target. A unique invocation ID distinguishes rebuilds even when output bytes are identical.

Ignored `bin/.../*.build-inputs.txt` sidecars retain the actual local paths for revalidation. Do not publish them: they can contain private installation paths. Build/package validation re-hashes the captured files and rejects absent, altered, incomplete or mismatched captures. The public `BUILD-STATUS.json` contains metadata identities and hashes without local paths; its `buildIdentity` is a SHA-256 digest over all binding fields. The API audit's game hash must equal the actual compiler-resolved game reference, not merely any supplied game DLL.

Schema 3 adds the required `distributionFiles` object to both `BUILD-STATUS.json` and `testedBuild`. It maps these exact project-relative paths to SHA-256 hashes:

- `manifest.json`
- `icon.png`
- `assets/generated/assets-manifest.json`
- `assets/generated/energy-analysis.png`
- `assets/generated/information-topology.png`

The `buildIdentity` hash covers `sourceFingerprint`, `configuration`, `referenceMode`, `assemblies`, `references`, `compilerInputs` and `distributionFiles`. The fingerprint additionally covers the source SVGs and generated package icon. Recording a candidate validates the frozen manifest, all PNG pixels/dimensions/modes, SVG/PNG hashes, and root/generated icon equality. Packaging verifies the final frozen distribution hashes against the candidate. A missing/partial distribution binding or an old schema-2 build/acceptance report is refused. Schema migration requires recording and testing the actual new candidate; copying old approval flags does not qualify it.

Before beginning acceptance, freeze the candidate and print its validated binding:

```sh
python scripts/package.py --tested-build --configuration Release
```

Record this entire object as `testedBuild` in a separate schema-v3 acceptance report inside the project. Also retain its `sourceFingerprint` in the report's top-level field. Preserve the tested DLLs, external PNGs, root icon, package/asset manifests, dependencies, sidecars and build report throughout acceptance. Never regenerate the binding from a newer build to make old evidence pass. Rebuilds, changed binaries, changed references, any changed distribution-file bytes (even valid manifest formatting changes), configuration/mode changes, and missing binding fields require new matching acceptance. `--fingerprint` alone is insufficient. Record actual installed package versions separately from assembly versions; BepInEx, CommonAPI and LDBTool's recorded assembly versions/hashes must match the compiler references. DSPModSave is a runtime-only transitive dependency and still needs its own observed version/hash and target-game evidence; compilation does not validate it.

The checked-in `testedBuild` is intentionally null and all release flags remain false. Neither this binding command nor a successful compile records any game-test pass or grants release approval. Release ZIPs include the supplied report as `RELEASE-ACCEPTANCE.json`.

For direct builds without PowerShell, run all pure tests first, then build the runtime project with the same explicit mode used when recording provenance:

```sh
dotnet clean src/DarkFogSynthesis/DarkFogSynthesis.csproj -c Release
dotnet build src/DarkFogSynthesis/DarkFogSynthesis.csproj -c Release -p:DarkFogReferenceMode=reference-assembly-smoke
# Run ResourceAudit against the captured output and both localization sources.
# Run PublicApiAudit against the captured Assembly-CSharp reference and resolved reference directories.
# Both successful JSON reports must be under artifacts/ before this command:
python scripts/package.py --record-build --configuration Release --reference-mode reference-assembly-smoke
```

Use `installed-local` only for actual installed references. Configured reference-path properties can also be passed to `dotnet build` with `-p:`. Do not build Core again after recording a candidate: another invocation changes its captured identity and invalidates that candidate. Run audits and packaging only after all builds stop; pre-snapshot changes inconsistent with the candidate are refused, while later edits cannot replace the already validated frozen package bytes.

## Declared dependency evidence (2026-10-04)

The official package pages list [BepInEx 5.4.17](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/BepInEx/), [LDBTool 3.0.3](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/LDBTool/) and [CommonAPI 1.6.7](https://thunderstore.io/c/dyson-sphere-program/p/CommonAPI/CommonAPI/). These are the declared manifest versions, not a game-tested combination. CommonAPI's [required packages](https://thunderstore.io/c/dyson-sphere-program/p/CommonAPI/CommonAPI/required) include DSPModSave 1.2.1; it is not bundled. Record actual loaded assembly versions and hashes separately from package versions during P0.

The package root requirements and 256 × 256 icon size follow [Thunderstore's package format](https://wiki.thunderstore.io/mods/creating-a-package). Local packaging does not publish or upload the mod.

## 中文摘要

核心与纯测试无需游戏；插件编译必须引用本机合法安装的游戏和依赖。构建不复制游戏 DLL，不安装插件，不改存档。源码包明确标为不可安装。实验安装包要求真实 DLL 和当前构建记录。正式包还要求全部 35 项游戏验收通过、明确批准及可核对证据；当前未执行的报告必定阻止正式包。编译成功不等于兼容性通过。

## Compiled resource gate

`Build.ps1` runs `ResourceAudit --self-test`, then checks the built main DLL against both approved localization JSON files. Culture inference is explicitly disabled; satellite `DarkFogSynthesis.resources.dll` files are rejected. The build resolves reference/output properties through MSBuild SDK 8 and runs PublicApiAudit before provenance. Direct `package.py --record-build` also requires current successful `artifacts/resource-audit.json` and `artifacts/public-api-audit.json`; stale or missing audit hashes are refused. Both reports are included in experimental packages. These checks inspect metadata/resources only and do not launch the game or certify runtime behavior.
