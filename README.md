# Dark Fog Synthesis / 黑雾材料合成

**Experimental implementation, not a validated release. No target-game tests have been executed.** This repository implements the frozen [V1 plan](DarkFogSynthesis_Implementation_Plan_ZH.md). It does not yet establish safe save migration/removal, machine compatibility, achievements, Metadata, or Milky Way eligibility. Use an isolated mod profile and disposable copies of saves. Keep the untouched original backup.

**实验实现，尚未通过游戏验收，不是正式可用版本。** 本仓库遵循已冻结的 V1 计划。未在目标游戏中验证生产设备、存档迁移/安全卸载、成就、Metadata 或银河资格。只用于独立 Mod 配置档和可丢弃的存档副本，保留未修改的原始备份。

## Scope / 范围

The general P4 cleanup/refund workflow is **not implemented**: the included maintenance action accepts only already-drained, idle state and produces an unverified candidate. Automatic return of buffered items/proliferator points and reserved crafting/research resources still requires target-game implementation and testing. 完整 P4 自动退料尚未实现；当前仅支持检查已清空状态并另存实验候选，不代表已完成计划全部验收。

Two technologies and six recipes produce existing vanilla Dark Fog materials. No new items or buildings, enemies, save-mode conversion, Metadata redemption, free technology completion, or integrity bypasses are intended.

- Energy Shard: 1 combustible unit + 1 energetic graphite + 1 glass → 2 shards / 2 s, smelter
- Dark Fog Matrix: 2 crystalline silicon + 1 photon combiner + 1 plasma exciter + 1 titanium glass → 1 matrix / 4 s, matrix lab
- Silicon-based Neuron: 2 microcrystalline components + 1 particle broadband + 2 crystalline silicon → 1 / 4 s, assembler
- Matter Recombinator: 1 plane filter + 2 super-magnetic rings + 2 hydrogen + 2 crystalline silicon → 1 / 6 s, assembler
- Negentropy Singularity: 1 strange matter + 2 Casimir crystals + 1 deuteron fuel rod + 2 crystalline silicon → 1 / 8 s, assembler
- Core Element: 2 antimatter + 2 frame materials + 2 super-magnetic rings + 4 crystalline silicon → 1 / 10 s, assembler

All six recipes permit handcrafting. Industrial acceleration and extra-products modes must follow vanilla mutually exclusive rules. Times are base recipe times; ordinary machine upgrades still apply. 黑雾矩阵不消耗普通科研矩阵；上游原料仍按原版取得。

## Build / 构建

Prerequisites: .NET 8 SDK, Python 3.9+ with Pillow, and PowerShell 7 for the `.ps1` wrappers. Inkscape is needed only to regenerate the included original SVG assets. Core targets `netstandard2.0`; the plugin targets `net472`. The executable pure-test harness has no third-party test-framework dependency.

```powershell
# No game installation required for these checks / 无需游戏程序集
./scripts/Build.ps1 -CoreOnly
# Equivalent portable commands
python scripts/generate-assets.py --check
python scripts/validate-content.py
dotnet run --project tests/DarkFogSynthesis.Core.Tests -c Release
```

For a runtime build, copy `Local.Build.props.example` to `Local.Build.props` and edit paths to your **own lawful installation**. `DSPGameDir` is the game installation root; `DSPManagedDir` points to its `DSPGAME_Data/Managed` directory. `BepInExDir` points to the directory containing `BepInEx.dll` and `0Harmony.dll` (normally the isolated profile's `BepInEx/core`); `CommonApiDir` and `LdbToolDir` point to the directories containing those installed plugin DLLs. The template's paths are examples, not detected locations.

```powershell
Copy-Item Local.Build.props.example Local.Build.props
# Edit Local.Build.props before building / 先填写本机合法安装路径
./scripts/Build.ps1
./scripts/Package.ps1 -Channel experimental
```

The build fails visibly when game/dependency references are absent. It never downloads game DLLs, copies dependencies into the package, or installs into your game. `Release` compiler optimization is not release acceptance. See [build and package details](docs/build.md).

若缺少合法游戏引用，只能生成源码包，不能伪造可安装版本：

```powershell
./scripts/Package.ps1 -Channel source-only
# Cross-platform equivalent
python scripts/package.py --channel source-only
```

## Experimental installation / 实验安装

Only a successfully compiled **experimental runtime ZIP** is installable; the source-only ZIP is not. Use a separate mod-manager profile, install the declared [BepInEx](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/BepInEx/), [LDBTool](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/LDBTool/) and [CommonAPI](https://thunderstore.io/c/dyson-sphere-program/p/CommonAPI/CommonAPI/) dependencies including CommonAPI's DSPModSave dependency, then place the runtime package's `BepInEx/plugins/DarkFogSynthesis` folder into that profile. Dependency declarations are not a tested-runtime compatibility claim.

No supported game-version combination is currently recorded. Stop on prototype conflicts, unknown-layout warnings, or exceptions; do not change fixed IDs to force a save to load. Never edit integrity flags to make a test pass. 升级前备份存档、旧插件及配置；不要改变已保存的原型 ID。

**LabOpt is currently blocked in every detected version.** Its replacement production path bypasses this mod's native lab adapter and shares lab buffers; an unchanged ordinary matrix list does not make that combination compatible. Use an independent compatible test profile and an untouched copied save. See the [pinned source review](docs/compatibility/labopt-buffer-review.md). Compatibility conflicts keep the session paused and block resume/save until a different session completes validation; no foreign technologies or mod settings are changed.

Critical load/save/session guards are installed and verified before configuration or localization. A fatal startup error requires restarting after correction; a new save cannot clear it. If critical hook coverage itself is incomplete, do not load, resume or save: quit manually. Successfully installed guards are retained, but a missing native hook cannot be claimed to protect its target. See [startup safety and limits](docs/compatibility/startup-safety.md).

## Configuration / 配置

```ini
[Progression]
ApplyCombatPrerequisitesInNonPeaceMode = false
```

Peace Mode always adds the four agreed combat prerequisites to the four vanilla hidden technologies. `true` extends those requirements to non-Peace saves; `false` preserves the ordinary non-Peace prerequisites. Recipes and the two new technologies exist in both modes. Restart after changes. 原版隐藏科技的发现条件、已有研究进度及普通科技的材料配方解锁必须保留；此项不是 Mod 总开关。

## Verification and removal / 验收与卸载

[Existing-mod source comparison](docs/compatibility/mod-source-comparison.md) · [Review regressions](docs/compatibility/review-regressions.md) · [Startup/progression/package follow-up](docs/compatibility/startup-progress-packaging-review.md) · [Bounded performance review](docs/compatibility/performance-review.md) · [Acceptance matrix](docs/acceptance.md) · [current machine-readable status](docs/compatibility/acceptance-status.json) · [uninstall limits](docs/uninstall.md) · [changelog](docs/CHANGELOG.md)

**Do not assume deleting the DLL is safe.** Active machines, research/crafting queues and blueprints may retain custom IDs. A fully validated “Prepare a Vanilla-Compatible Save” operation is not currently established. Retain or restore the untouched pre-Mod backup for ordinary play; do not overwrite it with experimental saves. 不能将“产物全是原版物品”视为安全卸载证明。

Original editable art and generated PNGs are covered by the [MIT license](LICENSE); see [asset provenance](assets/README.md). No game or framework binaries are distributed.
