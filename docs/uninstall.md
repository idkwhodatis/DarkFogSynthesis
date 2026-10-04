# Removal status / 卸载状态

**No safe-removal workflow is currently validated for any game build. Do not remove this experimental mod from your only copy of a save.** The safest currently established fallback is to keep or restore the untouched pre-Mod backup in its original environment. That restores the backup's earlier progress; it does not migrate new progress from an experimental save.

**当前没有任何已通过游戏验证的安全卸载流程。不要在唯一存档上直接删除插件。** 请保留或恢复安装前未修改的备份。恢复旧备份不会保留实验期间新增的进度。

## Why vanilla outputs are insufficient

Inputs, products and buildings are vanilla, but custom recipe IDs 48101–48106 and technology IDs 1951/1952 can remain in machines, queues, technology history, blueprints and runtime caches. CommonAPI's cleanup for missing custom machines does not establish removal of these references from vanilla machines. Direct deletion is **NOT EXECUTED** (U01), not supported merely by inspection of the item list.

## Current conservative cleanup scope

The runtime implementation offers an explicitly requested, confirmed **candidate** cleanup only when all custom-recipe machines are already drained, custom crafting and research queues are empty, no custom research is active, and active build/blueprint references are clear. It checks before mutation, creates a uniquely named backup first, and writes a separate uniquely named candidate save. Active buffers, nonzero cycle/progress counters, unknown references or unsafe states block the operation. It does not guess inventory refunds.

Open the experimental diagnostics panel, choose **Preview removal blockers**, then **Prepare removal candidate…** and read its confirmation before proceeding. The backup and candidate use unique `DFS-backup-…` / `DFS-removal-candidate-…` names. A successful candidate operation, or an operation that began mutation and then failed, keeps that session paused and blocks ordinary saves/resume until a different session finishes validation or the game is restarted. Starting or failing a replacement load does not release this guard. Failure messages identify the backup; any partially created candidate remains unverified. The operation does not overwrite the original save. These guards are source-implemented and still require target-game testing.

This drained-only path is narrower than frozen P4: generalized cancellation/refunds with inventory conservation are not implemented. It cannot yet be called a validated vanilla-compatible save operation, even if its preflight and candidate write succeed. The target-game behavior has not been executed.

当前仅准备保守的“已清空设备后生成候选副本”路径：需要明确触发并确认，先备份，另存新文件；设备缓存、手搓队列、活动蓝图或未知引用不安全时停止，不猜测退料。完整 P4 退料和库存守恒尚未验证，不能把候选副本宣称为已证明安全的纯原版存档。

## Read-only buffer diagnostics and known peers

While the current game is paused, **Preview removal blockers** can also show an exact idle-buffer count/proliferation ledger and a whole-batch capacity check on one detached player-package copy. It checks that the actual package is unchanged. This is a diagnostic only: a positive capacity result does not enable automatic refunds or allow occupied machines through cleanup. Active production/research and malformed state are rejected.

Original mutable buffers are checked by reference identity before snapshots are copied. Sharing a positive-length array with an owned-recipe machine blocks this diagnostic instead of counting it twice. Equal values in independent arrays do not imply sharing. LabOpt's replacement production/shared-buffer combination is separately blocked at session entry for all detected versions; see the [source review](compatibility/labopt-buffer-review.md).

Candidate cleanup is blocked whenever MoreMegaStructure is loaded: its StarAssembly may persist these recipe IDs outside native factory/save structures. No peer slots, sidecar files or refunds are changed. Keep the mod and backup until a tested peer-specific removal path exists; simply removing DLLs is not a supported workaround. Other arbitrary combinations are not certified by the absence of this known-peer blocker.

## Required future supported operation

The frozen plan requires an explicit “Prepare a Vanilla-Compatible Save / 清理本 Mod 数据并另存” maintenance action. It must, in this order:

1. Refuse during save writes; create and verify a uniquely named backup before mutation
2. Stop relevant simulation and inspect every instantiated factory, including other planets
3. Cancel owned crafting jobs and return unconsumed materials, machine buffers and proliferator points using verified vanilla rules, with an inventory ledger
4. Clear only the six owned recipe assignments; retain vanilla machines and valid produced items
5. Remove only the two owned technologies from queues and custom state, preserving vanilla research and consumed-cost rules
6. Inspect copy parameters, current blueprints and active build plans for residual IDs; report external blueprint-file risks without scanning or rewriting the user's library
7. Abort if refunds cannot fit, references are unknown, or inventories cannot be conserved; never claim success or overwrite the original
8. Save a new file, publish cleanup counts and a zero-residual-reference check, then load and save that copy in genuinely vanilla group D with the mod and solely-needed frameworks disabled

The implementation may conservatively refuse unsupported cleanup states. A successful preflight or a “no known references” message is not evidence that U02 or I02 passed. Until those game-backed tests pass, no clean-save result should be advertised as proven vanilla-compatible.

## Upgrade and blueprints

Keep an untouched save backup and the prior plugin/configuration before upgrading. Fixed IDs must not change based on load order. Stop on ID conflicts; never edit IDs in LDBTool or a save to silence a conflict. Blueprints that capture custom recipes can outlive the plugin; keep them marked as requiring the mod. Any future blueprint cleanup must operate on explicitly chosen copies.

U01–U05 and integrity group D remain separately tracked in [acceptance.md](acceptance.md). Reinstallation must not regress vanilla progress or silently complete removed custom technologies.
