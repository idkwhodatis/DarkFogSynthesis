# Build and package contract

## Local references

The plugin compiles against `net472`; Core is `netstandard2.0`; the pure-test executable is `net8.0`. Install the official .NET SDK appropriate for those projects. The .NET Framework reference-assembly package is a compile-time NuGet dependency, not a substitute for a lawful game installation.

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

`-CoreOnly` runs asset/content checks and the pure-test executable, without claiming runtime compilation. A full build runs the same checks, compiles the plugin against configured local references, runs metadata-only resource and original-API accessibility audits, then records assembly hashes and a build-input fingerprint in `artifacts/build-report.json`. No install/copy-to-game target runs. A failed build does not produce success provenance.

The default `-ReferenceMode installed-local` means references were supplied from the local installation; it still records `installedGameValidated=false` and `runtimeExecution=not_executed`. For an explicitly identified compile-only reference-assembly experiment, pass `-ReferenceMode reference-assembly-smoke`. That classification is preserved in `BUILD-STATUS.json` and the package notice and can produce an experimental package only. Reference-assembly compilation is not an installed-game test and cannot qualify for `release`. Never relabel a reference-assembly compile as a local game build.

On platforms without PowerShell, the content/test commands in the README and `python scripts/package.py` are usable directly. The packaging engine is Python standard library; texture validation additionally uses Pillow. `scripts/generate-assets.py` uses installed Inkscape only when regeneration is requested, and records its version. No font or extracted game resource is required.

## Package channels and safeguards

- `source-only` (default): source tree beneath a clearly named source directory, plus `PACKAGE-STATUS.json` with `installable=false`; no DLLs or installation claim
- `experimental`: requires both actual project DLLs plus a matching successful build report; includes own PNGs, source SVGs, documentation, license, icon and dependency manifest. No external binaries
- `release`: additionally requires an explicitly supplied acceptance report whose 35 IDs all passed in the recorded target game, whose evidence files exist, whose build fingerprint matches, and whose `approvedForRelease`, `runtimeValidated` and `releaseEligible` flags are true

```powershell
./scripts/Package.ps1 -Channel release -AcceptancePath docs/compatibility/acceptance-status.json
```

This command **must fail with the checked-in unverified report**. A compiler configuration called `Release` does not satisfy acceptance. The package guard checks data and evidence existence; it cannot determine whether a human's claimed gameplay evidence is truthful. Record real results, never flip flags merely to make packaging succeed.

All required checks, including online I03, must have real evidence. I03 must explicitly record authorization for the online test. Do not upload experimental save data merely to satisfy a checklist. Evidence belongs under `docs/compatibility/evidence/`; remove personal information before including it in a package. Never put save binaries, credentials or game DLLs there.

A deterministic ZIP includes only allowlisted file types and ignores `bin`, `obj`, symlinks, private build settings and VCS metadata. Runtime DLLs are explicitly limited to `DarkFogSynthesis.dll` and `DarkFogSynthesis.Core.dll`; their hashes must match current build provenance. ZIP entries receive a fixed timestamp and a SHA-256 inventory. Existing output ZIPs are not silently overwritten; use a fresh `-OutputDirectory` / `--output-dir`.

## Declared dependency evidence (2026-10-04)

The official package pages list [BepInEx 5.4.17](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/BepInEx/), [LDBTool 3.0.3](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/LDBTool/) and [CommonAPI 1.6.7](https://thunderstore.io/c/dyson-sphere-program/p/CommonAPI/CommonAPI/). These are the declared manifest versions, not a game-tested combination. CommonAPI's [required packages](https://thunderstore.io/c/dyson-sphere-program/p/CommonAPI/CommonAPI/required) include DSPModSave 1.2.1; it is not bundled. Record actual loaded assembly versions and hashes separately from package versions during P0.

The package root requirements and 256 × 256 icon size follow [Thunderstore's package format](https://wiki.thunderstore.io/mods/creating-a-package). Local packaging does not publish or upload the mod.

## 中文摘要

核心与纯测试无需游戏；插件编译必须引用本机合法安装的游戏和依赖。构建不复制游戏 DLL，不安装插件，不改存档。源码包明确标为不可安装。实验安装包要求真实 DLL 和当前构建记录。正式包还要求全部 35 项游戏验收通过、明确批准及可核对证据；当前未执行的报告必定阻止正式包。编译成功不等于兼容性通过。

## Compiled resource gate

`Build.ps1` runs `ResourceAudit --self-test`, then checks the built main DLL against both approved localization JSON files. Culture inference is explicitly disabled; satellite `DarkFogSynthesis.resources.dll` files are rejected. The build resolves reference/output properties through MSBuild SDK 8 and runs PublicApiAudit before provenance. Direct `package.py --record-build` also requires current successful `artifacts/resource-audit.json` and `artifacts/public-api-audit.json`; stale or missing audit hashes are refused. Both reports are included in experimental packages. These checks inspect metadata/resources only and do not launch the game or certify runtime behavior.
