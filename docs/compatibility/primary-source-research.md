# Runtime API research (2026-10-04)

## Evidence boundary

This is source research, not a P0 pass. No installed game was inspected or launched. No game DLL is included. Public reference assemblies can establish member signatures, but stripped methods cannot establish behavior. Mod authors' patches establish actual API usage and identify risks; they do not prove the user's current executable behaves identically.

`external-baseline-protos.json` contains factual subsets from an author-exported **0.10.34.28529** baseline, not a current local export. Do not rename it to a verified runtime baseline. It includes all main-page node coordinates as preliminary layout input, but no hover/expanded bounds or screenshots.

## Primary source versions

- DSPSeedCalc author export: [commit 142cc01517060be96be77e687d44512d756de851](https://github.com/soarqin/DSPSeedCalc/tree/142cc01517060be96be77e687d44512d756de851). Its AGENTS.md records game 0.10.34.28529, Unity 2022.3.62f3c1, Mono, Steam build 23109513, and Assembly-CSharp SHA-256 AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85. These are the source author's environment, not this project's measured runtime.
- [ItemProtoSet.json](https://github.com/soarqin/DSPSeedCalc/blob/142cc01517060be96be77e687d44512d756de851/Prototypes/ItemProtoSet.json), blob b71f190b1a5d6a1029e859b8d4d3e3cd54cb74a9
- [TechProtoSet.json](https://github.com/soarqin/DSPSeedCalc/blob/142cc01517060be96be77e687d44512d756de851/Prototypes/TechProtoSet.json), blob 01d87269caa0f2e523db3c21cc210f40267684e8
- [RecipeProtoSet.json](https://github.com/soarqin/DSPSeedCalc/blob/142cc01517060be96be77e687d44512d756de851/Prototypes/RecipeProtoSet.json), blob 2f8af8a204801102feff6f25d2ed68858c6a67d1
- CommonAPI source tree 4a5d7c21c0e2a5f91b639e4d710fa5bb5dcb928a; [ProtoRegistry](https://github.com/limoka/CommonAPI/blob/4a5d7c21c0e2a5f91b639e4d710fa5bb5dcb928a/CommonAPI/Systems/ProtoRegistrySystem/ProtoRegistry.cs), blob 81b4f7d53e4691a109800c132b852852fc6398eb
- LDBTool source tree 6b84a5539d4f4bb06d0e840cc8203655e313da4e; [VFPreload patch](https://github.com/xiaoye97/DSP_LDBTool/blob/6b84a5539d4f4bb06d0e840cc8203655e313da4e/LDBTool/Patches/VFPreload_Patch.cs), blob d002bd7d24ecb6599edbbb35734bb7001549b040
- soarqin/DSP_Mods source tree [9fc2723bcaa3b4b0a13e47477f70f2bf697edf98](https://github.com/soarqin/DSP_Mods/tree/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98)
- Nebula source tree [5ecd2b4168c8e4df72a301894d004aee5629961b](https://github.com/NebulaModTeam/nebula/tree/5ecd2b4168c8e4df72a301894d004aee5629961b)

## Verified IDs and naming traps

Outputs: Energy Shard **5206**, Dark Fog Matrix **5201**, Silicon-based Neuron **5202**, Matter Recombinator **5203**, Negentropy Singularity **5204**, Core Element **5205**. Raw prototype names for 5201/5205 are `存储单元`/`虚粒子`; localized display names must not drive ID lookup. All six are Type 10 with UnlockKey -2 in the baseline.

Inputs: Combustible Unit 1128; Energetic Graphite 1109; Glass 1110; Crystal Silicon 1113; Photon Combiner 1404; Plasma Exciter 1401; Titanium Glass 1119; Microcrystalline Component 1302; Particle Broadband 1402; Plane Filter 1304; Super-magnetic Ring 1205; Hydrogen 1120; Strange Matter 1127; Casimir Crystal 1126; Deuteron Fuel Rod 1802; Antimatter 1122; Frame Material 1125. Research matrices: blue 6001, red 6002, yellow 6003.

Techs: Battlefield Analysis Base 1826; Signal Tower 1808; Structure Matrix 1124; Information Matrix 1312; Quantum Printing 1203; Plane Metallurgy 1417; Controlled Annihilation Reaction 1145. Hidden techs are respectively 1901, 1902, 1903, 1904, with existing explicit prerequisites [1312], [1203], [1417], [1145] and PreItem [5202], [5203], [5204], [5205]. Their extra combat techs are Precision Drone 1820 (raw name T地面战斗机-A型), Magnetized Plasma Cannon 1811, Planetary Defense System 1809, Antimatter Capsule 1818.

**1801 and 1802 are occupied vanilla Tech IDs** (Weapon System and Combustible Unit). Do not use them for the mod. 1911, 1912, 1951, and 1952 are absent in this external baseline only. CommonAPI documents Tech IDs above 2000 as entering the upgrade page. No global ID reservation was established; live pre-registration collision rejection is still required. Other mods can occupy otherwise-free IDs.

The matrix-synthesis unlock does not independently supply its industrial ingredients: native Crystal Silicon recipe37 is unlocked by Crystal Smelting1403 (blue/red research), Photon Combiner68 by Photon Frequency Conversion1502 (blue/red), Plasma Exciter12 by High-efficiency Plasma Control1101 (blue), and Titanium Glass30 by High-strength Glass1126 (red/yellow). These technologies still need normal research even after custom information-topology completion. No frozen ingredient or machine needs a Dark Fog item along these direct recipe links; this is a source-graph observation, not a full current-save availability test.

## Registration and caches

CommonAPI `RegisterRecipe` queues via LDBTool.PreAddProto. NewRecipeProto sets `Handcraft=true`, arrays, type, ticks, grid/icon/name, and sets `preTech` only if the supplied tech ID already exists in LDB.techs. Registration order alone does not bind a not-yet-added custom tech. RegisterTech initializes PreItem and implicit prerequisites empty and computes IsLabTech by membership in LabComponent.matrixIds. It documents total research quantity as HashNeeded × ItemPoints / 3600. Rates [10] and [6,6,6] encode the frozen costs.

CommonAPI's post-add callback preloads recipes, then calls `TechProto.Preload()` and `Preload2()` for its new techs. It appends custom successor nodes to old prerequisite `postTechArray`, then invokes `ProtoRegistry.onLoadingFinished`. The verified cache member names include `preTechArray` and `postTechArray`; no separate `preTechArrayImplicit` member was established by this research. Do not invent one.

LDBTool runs its pre-add before VFPreload.InvokeOnLoadWorkEnded and post-add after it. Within the post-add callback: callbacks run, post-added protos are added, EditDataAction runs, item caches are rebuilt, then `RecipeProto.recipeExecuteData` is reconstructed. Each RecipeExecuteData uses recipe input/output arrays, TimeSpend × 10000 and × 100000, and `recipe.productive`. `ProtoRegistry.onLoadingFinished` is inside CommonAPI's post-add callback, **before** LDBTool's final recipe-execution cache reconstruction. Bind/validate before the final cache; mutating raw recipe fields afterwards can leave stale execute data. CommonAPI catches post-add exceptions, so a validator exception alone may log and allow later processing: maintain an explicit failed-registration state.

[LDBTool history patch](https://github.com/xiaoye97/DSP_LDBTool/blob/6b84a5539d4f4bb06d0e840cc8203655e313da4e/LDBTool/Patches/GameHistoryData_Patch.cs) runs after GameHistoryData.Import and calls `UnlockRecipe(recipe.ID)` only when recipe.preTech is non-null, that TechState is unlocked, and the recipe is not already unlocked. It does not initialize or unlock new techs wholesale. New-game behavior and custom tech partial-progress import still require runtime validation.

## Lab production: supported capacity, real integration risks

The baseline's native Universe Matrix recipe 75 is Type **15 (Research)** and already has **six inputs**. Four inputs therefore fit native lab recipe data capacity; do not expand global scientific matrix arrays to accommodate them. Energy/Structure/Information Matrix native recipes are also Type15. The production type and scientific research mode are separate concerns.

[LabOpt Functions.cs](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/LabOpt/Functions.cs) provides author-maintained replacement lab logic. SetFunctionInternal accepts `ERecipeType.Research`, uses a six-entry `needs` array, sizes input/output buffers from the recipe, copies `recipe.productive`, and sets timeSpend=TimeSpend×10000 and extraTimeSpend=TimeSpend×100000. Its production loop consumes `served` and `incServed` through split_inc_level, chooses extra products when productive and not forceAccMode, otherwise acceleration, and uses native Cargo tables for power/speed. It is evidence about API/algorithm shape, not this project's current-game validation.

Two concrete risks require narrow compatibility work:

1. **Recipe selection is not an arbitrary research-recipe picker.** [Nebula's UILabWindow patch](https://github.com/NebulaModTeam/nebula/blob/5ecd2b4168c8e4df72a301894d004aee5629961b/NebulaPatcher/Patches/Dynamic/UILabWindow_Patch.cs) intercepts native OnItemButtonClick(index) in unconfigured mode as a cube choice. Its [event processor](https://github.com/NebulaModTeam/nebula/blob/5ecd2b4168c8e4df72a301894d004aee5629961b/NebulaNetwork/PacketProcessors/Factory/Laboratory/LaboratoryUpdateEventProcessor.cs) maps matrixIds[index] → ItemProto.maincraft → SetFunction. Merely registering a Type15 recipe does not establish a selectable seventh output. Add only a recipe-specific entry; leave global matrixIds, matrixPoints and research slot interpretation untouched.
2. **Production-state encoding assumes normal matrix IDs.** LabOpt returns uint(products[0] - matrixIds[0] + 1). For 5201 against 6001 this underflows. [Genesis ResearchLabPatches](https://github.com/Awbugl/ProjectGenesis/blob/8d6b8abb786323ca0e362f9132da230b968227a9/src/Patches/Hooks/AddMatrix/ResearchLabPatches.cs) independently documents and patches the same native InternalUpdateAssemble return expression. A recipe-scoped rendering/state adaptation is a candidate, but the returned value's current consumers must be verified before declaring safety.

Native usage also establishes `FactorySystem.SyncLabFunctions(Player,int)` and `SyncLabForceAccMode(Player,int)` after a selection. A direct SetFunction without stack synchronization is insufficient. UI, production-mode stack transfer, saving, upgraded lab speed, and switch-to-research must all be tested.

## Mode timing and discovery

[UXAssist TechPatch](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/UXAssist/Patches/TechPatch.cs) reads `DSPGame.GameDesc.isPeaceMode`; its custom `OnGameBegin` is actually a GameMain.Begin postfix ([GameLogic](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/UXAssist/Common/GameLogic.cs)). This does not establish early-enough timing for pre-import queue validation. End is similarly GameMain.End postfix.

[UXAssist GamePatch](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/UXAssist/Patches/GamePatch.cs) demonstrates GameDesc.Import postfix can read `__instance.isPeaceMode` before its GameHistoryData.Import callback. A scoped actual GameData.Import lifecycle is a candidate early integration point. **Do not mutate shared tech protos on every GameDesc.Import**: save-list/header previews also import descriptions. Current game call order, fresh-game path, failure/finalizer restoration, and profile/mod ordering remain unverified. The source's peace conversion and signature changes are unrelated features and must not be copied into this mod.

The source-authored [PlanetaryAnomalies audit](https://github.com/pwelty/planetary-anomalies/blob/05d588597ce3512db79a3f7d0cb8b3c57412b635/ROADMAP.md) identifies native visibility as UITechNode.DetermineTechVisible → GameHistoryData.ItemUnlocked for PreItem, backed for Dark Fog items by enemyDropItemUnlocked. [FractionateEverything's developer specification](https://github.com/MengLeiFudge/MLJ_DSPmods/blob/c5637d1f48ae4fe3291542e95b7d48997acaf9c4/FractionateEverything/GAME_DESIGN_SPEC.md) independently distinguishes EItemType.DarkFog / UnlockKey=-2 discovery from ordinary recipe unlock. This supports preserving PreItem but **does not prove synthetic manufacture/pickup will trigger discovery**.

Native `GameHistoryData.UnlockSpecialItem(int)` exists: [DSP_Battle Rank.cs](https://github.com/ckcz123/DSP_Battle/blob/432b308028ba30547e6ef3e663a4dca36b5621c5/src/Rank.cs) calls it separately after TryAddItemToPackage for a custom UnlockKey=-2 reward. Its existence is not authorization to fabricate discovery flags. The exact native acquisition call sites, and whether adding explicit prerequisites influences visibility, still require inspection/testing of the user's genuine target game. Do not assert T02–T04 pass, patch PreItem, or directly populate enemyDropItemUnlocked on recipe unlock.

## Safe-removal machinery and limits

Verified APIs used by maintained mod sources:

- `FactorySystem.TakeBackItems_Assembler(Player, int assemblerId)` followed by assemblerPool[id].SetRecipe(0, factory.entitySignPool). [AssemblerVerticalConstruction](https://github.com/dccif/DSP_AssemblerVerticalConstruction/blob/23bfc3889ac89981417301104dc0fa1507ae7c1e/AssemblerVerticalConstruction.cs)
- `FactorySystem.TakeBackItems_Lab(Player, int labId)` followed by labPool[id].SetFunction(false,0,0,factory.entitySignPool), with native stack synchronization as appropriate. [AutomaticDSP command executor](https://github.com/yeliex/AutomaticDSP/blob/5b242889fc1a9524ca5a2aa83d0b2e725f234ca1/src/AutomaticDSP/Tasks/TaskCommandExecutor.Commands.cs)
- `GameHistoryData.RemoveTechInQueue(int index)`, index rather than Tech ID. [Nebula queue-removal processor](https://github.com/NebulaModTeam/nebula/blob/5ecd2b4168c8e4df72a301894d004aee5629961b/NebulaNetwork/PacketProcessors/GameHistory/GameHistoryRemoveTechProcessor.cs)
- `GameHistoryData.VerifyTechQueue()`. [UXAssist TechFunctions](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/UXAssist/Functions/TechFunctions.cs)
- MechaForge.CancelTask refunds reserved served items according to [AutoQueueBuild's first-hand decompilation notes](https://github.com/Living-Instinkt/DSP-AutoQueueBuild/blob/747cee11c7c2913bdfdc522763ec8a5ed02d3120/DESIGN.md). Exact overload and dependent-task handling still need target-reference verification.

Names alone do not prove lossless refunds: inventory capacity, overflow behavior, proliferator point preservation, partially consumed batches, shared lab-stack buffers, and uninstantiated factories need tests. Never clear buffers first or equate SetRecipe(0) alone to returning items. Preflight before mutation; no unsafe fallback if refund contract fails. An active custom research tech may also be referenced by labs and mecha research state beyond queue/state dictionaries.

[CommonAPI ModProtoHistory](https://github.com/limoka/CommonAPI/blob/4a5d7c21c0e2a5f91b639e4d710fa5bb5dcb928a/CommonAPI/Systems/ProtoRegistrySystem/ModProtoHistory.cs) enumerates GameMain.data.factories up to factoryCount and removes entities whose **item proto** belongs to a missing custom machine. It is not a cleanup implementation for custom Recipe/Tech IDs in vanilla buildings. Do not delegate this mod's safe removal to it.

## Reference assemblies

Exact reference package name: **DysonSphereProgram.GameLibs**, without `.Modding`. Published feed: https://nuget.bepinex.dev/v3/index.json. [GalacticScale's project file](https://github.com/Touhma/DSP_Galactic_Scale/blob/13a90c66954ee5f5b1b23796fe9da9bba00adb8e/GalacticScale3.csproj.bak) references 0.10.35.29088-r.0 with IncludeAssets=compile; [Nebula Directory.Build.props](https://github.com/NebulaModTeam/nebula/blob/5ecd2b4168c8e4df72a301894d004aee5629961b/Directory.Build.props) uses that package compile-only with PrivateAssets=all. Registry access in this research environment returned HTTP403, so exact feed availability was not independently fetched here.

[arxxyr/dsp-mods developer guidance](https://github.com/arxxyr/dsp-mods/blob/d67a2e02add929cbb5fd6ecacf8c513cfea0ddcd/AGENTS.md) explicitly notes GameLibs has stripped method bodies and real behavior requires the user's original installed Assembly-CSharp. CommonAPI's own development project references local Assembly-CSharp and UnityEngine.UI with Private=false. Neither scheme permits shipping game DLLs. Pin the compile target explicitly; compile success against a reference does not certify the runtime build or P0 scenarios.

## Remaining blocking evidence

1. Genuine target DLL/version/hash and exact load/new-game order
2. Recipe-specific lab selection/rendering integration; native production and stacked transfer QA
3. Synthetic acquisition vs native special-item discovery, with explicit/implicit prerequisite visibility controls
4. Refund capacity/increment accounting and complete active-reference scan
5. Actual exception/achievement/Metadata/Milky Way baseline comparisons and clean vanilla reload

Until these pass, label the build experimental and list unexecuted scenarios as unexecuted, never PASS.

## Implementation addendum: current reference check

The build coordinator subsequently obtained the actual **DysonSphereProgram.GameLibs 0.10.35.29088-r.0** package and Unity reference modules. Local ILSpy inspection of that stripped reference verifies `UILabWindow` serialized button/icon arrays, `labId`, and private factory/factorySystem/player/history fields; `LabComponent.InternalUpdateAssemble(float,int[],int[])` returns uint; `SetFunction(bool,int,int,SignData[])`, SyncLabFunctions(Player,int), and SyncLabForceAccMode(Player,int) exist. The methods have no usable game bodies. Some reference fields/methods are publicized and retain OriginalAttributes markers, so source must not directly depend on that artificial visibility.

The current LabComponent layout differs materially from LabOpt's copied older body: it has `RecipeExecuteData recipeExecuteData`, served/incServed/needs/produced buffers, and cycle counters. Old requires/products/timeSpend/productive fields must not be transplanted into target code. The compatibility implementation uses native methods and current fields only.

`NativeRecipeCompatibility.cs` implements a separate cloned native UI choice without appending to any native arrays. It clears cloned UIButton event delegates and Unity click listeners, injects private window context through Harmony, checks the recipe's unlock state, and requires the entire connected lab stack to be unconfigured and have no retained buffers or progress before calling native SetFunction/SyncLabFunctions/SyncLabForceAccMode. A populated stack must be reset through the native UI first. This intentionally avoids an unverified automatic refund path in the selector.

For recipe 48102 only, a postfix maps a nonzero InternalUpdateAssemble result to the native white-matrix visual index 6. It leaves zero/idle, all other recipes, all inventory/progress/power/proliferation state, and native production untouched. **The current target consumer behavior and visual index meaning still require in-game P0 validation**; the source evidence supports a candidate adaptation, not a runtime PASS.

The extra choice's position is a candidate to the right of the existing choice grid, using native art/font. No game screenshot was available, so window clipping, hover bounds, localization, input interaction, top/middle/bottom stack selection, switching modes, and visual-state consumers are explicitly untested.

The isolated selector source compilation passed with **0 warnings and 0 errors** against those real current package references. The temporary harness supplied only this mod's not-yet-integrated Plugin.Instance/Ready surface, not simulated game APIs. This is a focused compile check, not the final integrated plugin build or runtime testing. A registration-ready guard also prevents the visual adapter from modifying another mod's colliding recipe if registration was rejected.
