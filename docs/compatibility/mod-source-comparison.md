# Existing content-mod comparison and adaptations

Reviewed 2026-10-04 against the frozen DarkFogSynthesis plan. These are pinned source comparisons, not game compatibility tests. No external code or artwork was copied. The plan, recipes, costs, IDs and native hidden-discovery fields remain unchanged.

## Three primary content-mod examples

| Mod | Source revision | Reuse boundary |
|---|---|---|
| [ProjectGenesis](https://github.com/Awbugl/ProjectGenesis/tree/8d6b8abb786323ca0e362f9132da230b968227a9) | `8d6b8abb786323ca0e362f9132da230b968227a9`, 2026-09-30; source adapts game 0.10.35.29088 | GPL-3.0-only code/data; separately restricted artwork. API patterns studied, no code/data/art transplanted |
| [MoreMegaStructure](https://github.com/jinxOAO/DSPmod_MoreMegaStructures/tree/12459077171c30162dece8108e93406df2dce097) | `12459077171c30162dece8108e93406df2dce097`, 2026-09-27; plugin 1.9.3 | No reuse license found in repository tree or GitHub metadata; reference-only |
| [FractionateEverything](https://github.com/MengLeiFudge/MLJ_DSPmods/tree/c5637d1f48ae4fe3291542e95b7d48997acaf9c4) | `c5637d1f48ae4fe3291542e95b7d48997acaf9c4`, 2026-07-23; source 3.0.0 | MIT; patterns independently implemented. Source version is not asserted equivalent to an indexed older package |

## Adopted changes

### 1. Explicit localization resources and compiled-artifact checks

Genesis loads explicitly named, culture-neutral JSON and registers translations before prototype callbacks ([resource naming](https://github.com/Awbugl/ProjectGenesis/blob/8d6b8abb786323ca0e362f9132da230b968227a9/src/Utils/JsonHelper.cs#L19-L23), [translation loading](https://github.com/Awbugl/ProjectGenesis/blob/8d6b8abb786323ca0e362f9132da230b968227a9/src/Utils/TranslateUtils.cs#L15-L43)).

The comparison exposed a deterministic bug in our first experimental build: MSBuild inferred cultures from `Strings.en-US.json` and `Strings.zh-CN.json`, emitted satellite DLLs, and left zero resources in the main DLL that the loader searches. Compilation alone did not detect it.

The project now sets `WithCulture=false` and explicit resource names. The metadata-only ResourceAudit rejects empty, missing, duplicate, linked or altered resources and unexpected satellites; both dictionaries must match the approved source bytes and key sets. Build/package provenance requires matching successful resource and original-API accessibility audits. The original faulty DLL was rejected by this check; rebuilt resources passed. No game/plugin code is executed by the audit.

### 2. Registration, cache binding and saved-machine validation

Genesis separates initial prototype import from explicit cache reconstruction ([post-add stages](https://github.com/Awbugl/ProjectGenesis/blob/8d6b8abb786323ca0e362f9132da230b968227a9/src/ProjectGenesis.cs#L325-L350)). FractionateEverything also separates registration/finalization and distinguishes a new global recipe cache from existing machine references ([lifecycle](https://github.com/MengLeiFudge/MLJ_DSPmods/blob/c5637d1f48ae4fe3291542e95b7d48997acaf9c4/FractionateEverything/src/Lifecycle/FeatureBootstrap.cs#L36-L108), [machine execution caches](https://github.com/MengLeiFudge/MLJ_DSPmods/blob/c5637d1f48ae4fe3291542e95b7d48997acaf9c4/FractionateEverything/src/Logic/VanillaRecipes/VanillaRecipeManager.cs#L130-L184)).

We retain staged registration, then strengthen validation of unlockRecipeArray references, item recipe/handcraft membership, fallback recipe counts, and loaded owned-recipe machine caches/buffer shapes. We do not reload or overwrite all vanilla prototypes. Another mod's valid existing maincraft/handcraft remains intact. Owned assembler/lab imports are checked before the known LDBTool sanitizer through explicit Harmony ordering, because a check only at GameMain.Begin would be too late after that sanitizer replaced incompatible buffers. Final Begin-time checks remain. These checks throw without resetting arrays; exact target-game import behavior still needs P0 testing.

### 3. Pending and final technology anchors

MoreMegaStructure registers through pre-/post-add callbacks and additionally changes dynamic technology layout ([callbacks](https://github.com/jinxOAO/DSPmod_MoreMegaStructures/blob/12459077171c30162dece8108e93406df2dce097/MoreMegaStructure/MoreMegaStructure/MoreMegaStructure.cs#L335-L340), [layout patch](https://github.com/jinxOAO/DSPmod_MoreMegaStructures/blob/12459077171c30162dece8108e93406df2dce097/MoreMegaStructure/MoreMegaStructure/MoreMegaStructure.cs#L2068-L2095)).

Our anchor checks now include queued TechProto entries before insertion and repeat against the final prototype set. Upgrade-page coordinates are a separate canvas: upgrade 3506 at (29,-43) does not conflict with our main-page information node. Anchor uniqueness is still not a measured normal/hover/expanded layout pass; no vanilla node is moved.

### 4. Peer-owned saved recipe references

MoreMegaStructure can select arbitrary recipes, persist their IDs in StarAssembly, and later dereference the recipe without a missing-ID guard ([selection](https://github.com/jinxOAO/DSPmod_MoreMegaStructures/blob/12459077171c30162dece8108e93406df2dce097/MoreMegaStructure/MoreMegaStructure/StarAssembly.cs#L1947-L2025), [serialization](https://github.com/jinxOAO/DSPmod_MoreMegaStructures/blob/12459077171c30162dece8108e93406df2dce097/MoreMegaStructure/MoreMegaStructure/StarAssembly.cs#L2499-L2523), [lookup](https://github.com/jinxOAO/DSPmod_MoreMegaStructures/blob/12459077171c30162dece8108e93406df2dce097/MoreMegaStructure/MoreMegaStructure/StarAssembly.cs#L694-L712)).

A vanilla-factory sweep cannot certify removal of those sidecar references. Candidate cleanup therefore conservatively refuses while the exact verified MoreMegaStructure GUID is loaded, regardless of version. It names the uncovered state, does not edit that mod's slots or save, and does not advise simply deleting its DLL. Absence of this known-peer blocker does not certify every arbitrary mod combination.

### 5. Narrow lab adaptation and registry guard

Genesis documents why native lab production encodes a visual state from the output matrix ID ([production visual arithmetic](https://github.com/Awbugl/ProjectGenesis/blob/8d6b8abb786323ca0e362f9132da230b968227a9/src/Patches/Hooks/AddMatrix/ResearchLabPatches.cs#L136-L153)). It solves its overhaul's needs by expanding research arrays and UI controls.

We keep our separate recipe choice and recipe-only visual correction, and now require the original six matrix IDs/order before applying that supported configuration. No matrix registry or research slot is changed. This rejects altered-matrix overhauls rather than claiming an untested combination works; it does not prove animation or stacking correctness in-game.

The subsequent [LabOpt and shared-buffer review](labopt-buffer-review.md) identifies an additional incompatible production replacement that leaves those six IDs unchanged. Both `FactorySystem.GameTickLabProduceMode` overloads are transpiled to LabOpt's own helper, bypassing our native `LabComponent` postfix. All loaded/unqualified LabOpt versions are therefore blocked at session entry and late validation until a tested adapter exists. The six-ID check alone is not a compatibility certificate.

## P4: useful refund foundations, with execution still blocked

Additional maintained automation source was inspected specifically for native inventory handling:

- [AutomaticDSP](https://github.com/yeliex/AutomaticDSP/blob/5b242889fc1a9524ca5a2aa83d0b2e725f234ca1/src/AutomaticDSP/Tasks/TaskCommandExecutor.Commands.cs#L1843-L1886) demonstrates native TakeBackItems before recipe replacement; [queue code](https://github.com/yeliex/AutomaticDSP/blob/5b242889fc1a9524ca5a2aa83d0b2e725f234ca1/src/AutomaticDSP/Tasks/TaskCommandExecutor.QueueControl.cs#L24-L46) uses CancelTask by index. Apache-2.0
- [Spherewright](https://github.com/AvaloNero/Spherewright/blob/4ac502ca4c4349d028e49c49c45aac83c0fba1b0/src/Spherewright.Plugin/Game/NormalGameActionCoordinator.StructuredActions.cs#L3027-L3055) checks capacity on a detached native storage copy; [copy construction](https://github.com/AvaloNero/Spherewright/blob/4ac502ca4c4349d028e49c49c45aac83c0fba1b0/src/Spherewright.Plugin/Game/NormalGameActionCoordinator.StructuredActions.cs#L3402-L3416) preserves grids and storage flags. MIT
- [UXAssist](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/UXAssist/Functions/PlanetFunctions.cs#L207-L231) accounts for current RecipeExecuteData buffers and proliferation points. MIT

Independent adaptations now provide an immutable idle-buffer ledger and a read-only capacity diagnostic. All packets use one detached package copy, preventing several items from incorrectly reserving the same empty slot. Exact mixed proliferation points are retained rather than averaging away remainders. Malformed, overflowing, active-cycle or research state is rejected; the original package is checked for unchanged identity, contents and cache counters.

Before copying those snapshots, the diagnostic now checks the original mutable arrays by reference identity across all instantiated factories. A positive-length array shared with a refundable machine is refused, including cross-field and owned/unowned aliases; distinct arrays with identical values remain independent. This closes double-counting in a shared-buffer stack without inventing ownership or refunding anything.

This is deliberately not a refund action. Public callers establish API usage, but not exact native receiver routing, in-flight production, forge parent/child cancellation, research fractional-point handling, or every side effect that rollback must reverse. A copied machine buffer must never be restored while already-credited player/delivery/ground items remain, which would duplicate inventory. Those target-game semantics remain to be inspected/tested before enabling general P4 mutation. The current candidate cleanup still requires drained/idle state.

Direct DLL removal is especially unsafe: [LDBTool's missing-recipe import patch](https://github.com/xiaoye97/DSP_LDBTool/blob/6b84a5539d4f4bb06d0e840cc8203655e313da4e/LDBTool/Patches/AssemblerComponent_Patch.cs#L10-L47) clears recipe/buffer state when execute data is missing. A save loading successfully is not evidence that inventory survived.

## Deliberately not adopted

No global prototype rebuild, extra scientific matrices, altered hidden-tech discovery, automatic technology completion, configuration-dependent IDs, clearing other mods' configuration files, or progression compensation gifts were imported. FractionateEverything's tagged/versioned sidecar is a useful future format pattern, but our current native Tech/Recipe persistence does not justify adding mandatory custom save state.

All 35 game acceptance scenarios remain unexecuted. To complete P0/P4/P6, use the exact installed DSP and dependencies, a separate test profile and copied saves; inspect native refund/cancel/serialization behavior and run inventory/proliferation ledgers plus A/B/C/D integrity comparisons. No game installation, launch, save mutation, or external upload was performed during this source review.

The final independent review also checked the framework-serialization dependency boundary. `System.Web.Extensions` is a compiler reference; its actual resolution in the installed Unity profile has not been established here. This is a P0 runtime dependency check, not evidence that the assembly is missing. No speculative serializer replacement or additional runtime dependency was introduced.
