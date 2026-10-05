# Target-game adaptation runbook / 目标游戏验收操作手册

**Execution status: NOT EXECUTED.** This is Patch D's test procedure, not evidence that DSP was launched. Use the existing [35-case acceptance matrix](../acceptance.md) and [machine-readable acceptance](acceptance-status.json); do not create a competing release gate. Offline fixture results, successful reference compilation and `structural_match`/`observed_geometry_match` outputs are not target-game passes.

The code adaptations and their limits are tracked in [content-adaptation.md](content-adaptation.md). Their upstream rationale remains in [mod-source-comparison.md](mod-source-comparison.md). Source/reference-only verification is recorded separately in [adaptation-ci-verification.json](adaptation-ci-verification.json).

## 0. Freeze the candidate and protect the original

Use a legally installed game with a separate mod-manager profile and disposable copies of saves. Retain an untouched, external backup. Start with only the declared dependencies and DFS; test optional mods in separate profiles afterward. Do not change shared LDBTool IDs, unlock research with a console, erase integrity flags, or enable general refund code to make a test pass.

**硅基神经元配方已修改：微晶元件 ×2、钛合金 ×2、晶格硅 ×2。配方 ID 48103、产物数量 1、240 tick、解锁科技 1312 不变。旧版在制材料不能直接解释为钛合金。** Before upgrading an old experimental save, use the old DLL to drain/reset affected machines and finish/cancel related handcraft jobs using native controls. Save the drained copy and retain its original backup. No migration of occupied old-recipe buffers is supported or claimed. Exercise S06 with the new recipe's buffers, not by loading unconverted old buffers.

Record exact game, Unity, framework and dependency versions/hashes. Build against the installed references using the existing [build procedure](../build.md), audit the resulting DLLs, then run:

```sh
python scripts/package.py --tested-build
```

Retain the entire validated object for this test run; it is not an approval. A source-only package or the CI's reference-assembly smoke output cannot qualify an installed-game release. Rebuild and repeat affected tests whenever a bound source, binary, dependency, configuration or asset changes. Do not re-record an old approval against a new candidate.

Keep evidence in a fresh `docs/compatibility/evidence/<run-id>/` directory. Use sanitized `.txt`/`.json` observations and `.png` screenshots supported by the package allowlist. Do not commit game DLLs, save files, tokens, account identifiers or private installation paths. The adjacent [observation template](templates/adaptation-observation.json) is a blank, non-acceptance worksheet; populate it from actual observations rather than copying fixture output.

## 1. Registration and mode isolation — D01–D03, C01–C03

Enable `Diagnostics.TraceSnapshots=true` in the isolated profile before starting the process. It defaults to false. The existing automatic post-binding export remains warning-only. The new lifecycle captures are optional; their failure is not a reason to suppress compatibility guards.

Collect one same-process pair of `pre-register` and `post-bind` exports. The latter observes bound prototypes, **not** a claim that final execution caches already exist. Check the expected additions (2 technologies, 6 recipes, no new items/buildings), localization, exact IDs and the independent recipe oracle:

```sh
python scripts/compare-runtime-snapshots.py pre-register.json post-bind.json --phase registration --output registration-result.json
```

The recipe report must include Silicon Neuron's input IDs `[1302,1107,1113]`, counts `[2,2,2]`, output 5202 ×1, time 240 and owner 1312. Check all other recipe rows as well. A framework or peer's unrelated changes between checkpoints are reported as drift, not automatically attributed to DFS. Inspect them; do not edit evidence to force equality.

Prepare Peace Mode and non-Peace test saves. For each startup configuration (`ApplyCombatPrerequisitesInNonPeaceMode=false/true`), restart, capture the matching `pre-mode`/`post-mode` pair and compare:

```sh
python scripts/compare-runtime-snapshots.py pre-mode.json post-mode.json --phase mode
```

Peace Mode always adds the four specified combat prerequisites; non-Peace adds them only with the option enabled. Technology/recipe registration must remain constant. The save identity in the two exports must match. Confirm native prerequisite caches, availability and queue behavior, not just array values in the report.

On leaving that session, compare restoration to its **already registered, pre-mode baseline**:

```sh
python scripts/compare-runtime-snapshots.py post-mode.json post-restore.json --phase restore --baseline pre-mode.json
```

Do not compare restoration to pre-registration, which would incorrectly expect the custom content to disappear. S03/S04 require ten same-save loads and Peace → non-Peace → Peace in one process, with no duplicate edges, gifts or progress leakage. Preserve foreign prerequisites and crafting fallbacks. Test exact-ID/slot collisions only in a disposable developer profile; confirm clear refusal, never reassignment. LabOpt and altered scientific-matrix registries remain unsupported; do not remove their guards to pass C01.

## 2. Actual research accounting — T01, T05–T06, S01–S02, S05

Use a new or appropriately early copied save. Record the native technology display before research, the supplied matrix inventory, progress and consumed totals. Do not infer a cost pass from `ItemPoints` alone.

| New technology | Required observed research totals | Expected effect |
|---|---|---|
| Energy Analysis, 1951 | 100 electromagnetic matrices; 36,000 hash | Unlock Energy Shard recipe 48101 only |
| Information Topology, 1952 | 300 each electromagnetic/energy/structure matrix; 180,000 hash | Unlock Dark Fog Matrix recipe 48102 only |

The runtime parameters remain `[10]` and `[6,6,6]` item points, respectively; these encode totals in conjunction with hash, rather than being the total number of matrices. Verify explicit and implicit prerequisites, independent branches and no required Dark Fog drops for either new technology.

Save at partial research, reload, and finish. Compare progress and cumulative material consumption so duplicated costs or free completion cannot hide behind a correct final unlocked state. In a completed mid/late-game copied save, verify only the four advanced recipes whose native owner research is already complete are reconciled; neither new technology is granted for free. Exercise native queue ordering and automatic prerequisite insertion. Completed hidden research must remain completed when mode policy changes.

## 3. Acquisition versus hidden discovery — T02–T04

For each relevant Dark Fog material, preserve a baseline and observe these states separately: recipe locked; recipe unlocked; output produced but uncollected; actual player acquisition; inventory emptied again. Pause and use **Export runtime diagnostics** at each observation point so the manual report captures native `ItemUnlocked` and `enemyDropItemUnlocked` observations from the current validated session.

Repeat through handcraft completion, manual machine pickup and supported automated acquisition routes. Also test machine output delivered to another storage without player pickup; do not assume these are equivalent acquisition events. Record what actually happens in the native controls and compare with an equivalent vanilla discovery baseline.

Test the four hidden technologies both before and after the added combat prerequisite is completed. Merely registering or unlocking a recipe must not fabricate discovery. Having the combat prerequisite without acquiring the special item must not fabricate discovery either. On the enabled mode policy, discovered research must still honor the added prerequisite.

**No acquisition patch was added speculatively.** If a route fails the desired contract, retain its observations and inspect the exact native event/API before designing a recipe-scoped adapter. Never substitute direct discovery-set writes, forced research completion or removal of native hidden-tech discovery requirements.

## 4. Production and persistence — D04, R01–R06, S06–S07

Use unconstrained power and input/output capacity, and record the actual machine tier and base speed. At unsprayed 1× recipe speed, the expected output rates in definition order are 60 / 15 / 15 / 10 / 7.5 / 6 items per minute. Distinguish the ideal recipe rate from a machine-tier multiplier.

Check Energy Shard in smelters with all three inputs; Dark Fog Matrix in matrix labs with all four inputs; Silicon Neuron, Matter Recombinator, Negentropy Singularity and Core Element in assemblers. Verify exact material deltas and no vanished input/output buffers. All six recipes must support handcrafting without globally changing which upstream vanilla materials can be handcrafted.

For Dark Fog Matrix, test a single lab, stacks, each supported lab tier, a selected non-root layer, output accumulation, native reset/reselection, production → research → production and save/reload. Confirm the extra choice does not append a seventh scientific research slot or call the native index-six handler. Check animation separately from actual output accounting.

For each recipe, test no spray, acceleration, and extra-product mode as separate cases. Record input quantities and total proliferation points, output, progress, energy and mode; do not average away mixed-point remainders. Capture new-recipe half-batches and sprayed buffers before and after a normal save/reload. Include factories on noncurrent planets and repeated planet/UI changes. Current runtime export can observe owned machine state only in a paused, current, validated manual session; it deliberately does not scan active simulation buffers from import workers.

## 5. Rendered UI, not just coordinates — V01 and R02

Open the technology tree, then request **Capture UI in 3 seconds / 3 秒后采集 UI 边界**. The diagnostics panel hides; return the pointer to the technology to be measured before the one-shot capture. It records actual screen-space rectangles, page/canvas grouping, focus amount, viewport, masks and language. It neither moves nodes nor certifies layout.

For each language and supported UI scale/resolution, collect normal, hovered and expanded states for both technology IDs. Target one node for hover/expansion because the other node may correctly remain normal:

```sh
python scripts/check-ui-layout.py normal.json --kind technology --state normal
python scripts/check-ui-layout.py hover-energy.json --kind technology --tech-id 1951 --state hover
python scripts/check-ui-layout.py expanded-information.json --kind technology --tech-id 1952 --state expanded
python scripts/check-ui-layout.py lab-locked.json --kind lab-choice --state locked
python scripts/check-ui-layout.py lab-unlocked.json --kind lab-choice --state unlocked
```

Repeat for an empty lab with the recipe locked and unlocked. A locked tooltip must name localized Information Topology, remain disabled, and become usable after native research completes. Include open/close, window destruction/recreation, planet changes and save switching; verify one functional custom listener rather than duplicated native callbacks.

A geometry result of exit 0 covers only one captured frame's rectangles. Inspect screenshots and interaction for text clipping, rendered glyphs, icon legibility, arrows/connector crossings, stencil shape, overlay occlusion, tooltip visibility and click routing. Unknown/transition focus or partial capture must not qualify the requested stable state. Record all missing environments as unexecuted/environment-limited, not passed. Keep the existing candidate coordinates until real evidence justifies moving **only** DFS-owned controls.

## 6. Removal and integrity remain separate — U01–U05, I01–I03

Only proceed on disposable copies after normal gameplay/persistence checks. Compare A (vanilla), B (dependencies), C (dependencies + DFS) and D (cleaned vanilla) without erasing pre-existing flags. General occupied-buffer refunds remain disabled. The cleanup candidate requires drained idle machines, cleared native custom queues and relevant blueprints/clipboards; a successful serializer or preflight is not proof of inventory preservation or vanilla compatibility.

Verify unique backup/candidate naming, no original overwrite, pause through save callbacks, failure containment, full factory coverage, item/proliferation conservation, vanilla research preservation and actual candidate reload in an isolated vanilla profile. Never advise deleting MoreMegaStructure to evade its sidecar-reference blocker. External blueprint files remain untouched. Direct-DLL-removal testing belongs only to disposable research copies, never an ordinary save.

Record achievements, Metadata and integrity eligibility at the lifecycle points listed in the existing acceptance matrix. **I03 online testing requires separate explicit authorization**; no script in this adaptation launches or uploads to the game service. A missing service/environment is not a pass.

## 7. Close only evidence-backed cases

Use exit 1 from diagnostic scripts to investigate drift/geometry problems, and exit 2 to correct invalid evidence inputs. Neither tool edits its inputs or writes a save. Reports use create-only output paths. Disabled lifecycle diagnostics must not call an exporter; a broken output directory/logger must not alter compatibility or persistence state.

Populate the existing per-case acceptance only after executing its target-game procedure, include sanitized evidence under the packaged allowlist, and bind approval to the exact tested candidate. All 35 cases and all release approval flags remain unchanged by this runbook. The CI fixture suites are independently useful, but cannot complete Patch D's target-game execution or Patch C's actual visual qualification.
