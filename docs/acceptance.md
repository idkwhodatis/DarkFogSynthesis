# Acceptance record / 验收记录

Date: 2026-10-04. Scope: frozen implementation plan v1.0. **Game acceptance: NOT EXECUTED; release blocked.**

No legally installed target game is present in this implementation environment. Public source research, compiler checks, pure tests and generated-asset validation are separate evidence categories; none proves production UI, save safety or integrity eligibility. Empty baseline fields are intentional, not missing evidence disguised as a pass. All 35 plan scenarios remain unexecuted until real target-game evidence is attached.

当前无目标游戏安装，全部 35 项游戏验收仍为“未执行”。静态检查、纯逻辑测试及资源验证不能替代游戏、卸载或完整性验收。

## Execution protocol

1. Freeze and hash actual DSP, Unity, BepInEx, LDBTool, CommonAPI and DSPModSave versions; replace `compatibility/runtime-baseline.json` with observed values
2. Export the involved vanilla protos and full main technology-page layout, including hidden/dependency nodes; verify fixed IDs, recipe grid capacity, chain accessibility and 60-tick time units before enabling an experimental profile
3. Create A (vanilla), B (dependencies), C (dependencies + mod) and later D (cleaned vanilla) from the same copied save; record pre-existing flags and never erase them
4. Record each case as `passed`, `failed`, `not_executed` or `environment_limited`, with setup, actions, observed results, logs/screenshots and inventory/flag deltas. Use a unique evidence path per run
5. Do not auto-upload save data. I03 needs explicit tester authorization. Offline/online-service failures are `environment_limited`, never a pass
6. Before testing, use `python scripts/package.py --tested-build` to freeze the candidate's validated exact-build binding, then record the entire object as `testedBuild` in the schema-v2 acceptance report. Retain its source fingerprint at the top level too. Repeat affected checks after code, ID, binary, dependency, configuration or game-version changes; a source fingerprint alone cannot qualify a different build. See [the binding contract](build.md#exact-build-acceptance-binding)

## Focused P0 evidence still required

- P0-A: exact IDs, semantic fields, resolved caches, recipe grid occupancy, complete hidden-node layout, upstream ingredient/device reachability; verify actual framework assembly resolution, including System.Web.Extensions, in the installed Unity profile
- P0-B: three-input smelter and four-input matrix-lab production, automation, storage, upgrades, stacking, mode switches, proliferation and handcrafting; no changes to global ordinary matrix arrays
- P0-C: four hidden technologies across recipe locked / recipe unlocked / machine output uncollected / acquired / inventory emptied, before and after combat prerequisite; compare original discovery and queue behavior
- P0-D: minimal registration, research, manufacture, save/reload and direct-removal experiments; expand only with explained integrity results

## Full frozen matrix

The requirements below are copied from the authoritative plan. `NOT EXECUTED` applies to the actual game scenario, even where a corresponding pure-logic assertion exists.

| ID | Scenario / 场景 | Required result / 必须满足 | Target-game result |
|---|---|---|---|
| D01 | 六个配方定义 | 与冻结数量一致；核心素反物质为 2；物质重组器氢为 2；负熵奇点奇异物质为 1 | NOT EXECUTED |
| D02 | 新增内容计数 | 2 Tech、6 Recipe、0 新 Item、0 新 Building | NOT EXECUTED |
| D03 | 配置四种组合 | 与第 3.3 节完全一致 | NOT EXECUTED |
| D04 | 配方时长与产量 | 基础速度下符合 60/15/15/10/7.5/6 个每分钟 | NOT EXECUTED |
| R01 | 能量碎片三输入熔炉 | 不漏原料、不丢缓存，三类输入均可自动供料 | NOT EXECUTED |
| R02 | 黑雾矩阵四输入研究站 | 正确生产、输出，未变成普通科研槽位 | NOT EXECUTED |
| R03 | 研究站堆叠/切模式 | 上下层传输、生产转科研再转回均正常 | NOT EXECUTED |
| R04 | 其余四配方制造台 | 所有兼容级别保持原版速度/功耗 | NOT EXECUTED |
| R05 | 六种手搓 | 均可制造；上游原版不可手搓限制不被全局解除 | NOT EXECUTED |
| R06 | 六配方 × 增产状态 | 加速与额外产物互斥、点数与能耗遵守原版 | NOT EXECUTED |
| T01 | 两项新科技首次研究 | 前置、Hash、总矩阵消耗和解锁内容正确 | NOT EXECUTED |
| T02 | 仅解锁高级材料配方 | 不因 Mod 自动赠物或伪造发现而提前显示隐藏科技 | NOT EXECUTED |
| T03 | 得到材料、缺战斗前置 | 发现行为与原版一致；启用额外前置时不能绕过研究条件 | NOT EXECUTED |
| T04 | 有战斗前置、无材料 | 不单靠新前置提前发现隐藏科技 | NOT EXECUTED |
| T05 | 已完成原版隐藏科技 | 装 Mod/切换配置不重新锁定 | NOT EXECUTED |
| T06 | 原版队列/自动补前置 | 不绕过未完成的研究条件，不损坏正常排队 | NOT EXECUTED |
| S01 | 早期和平旧档 | 正常载入；两项新科技按正常条件可研究 | NOT EXECUTED |
| S02 | 中后期旧档 | 只补齐已有前置对应的新配方，不免费完成自定义科技 | NOT EXECUTED |
| S03 | 同档连续十次加载 | 不重复奖励、追加前置或修改库存 | NOT EXECUTED |
| S04 | 和平→战斗→和平 | 前置策略无跨档泄漏 | NOT EXECUTED |
| S05 | 新科技研究到一半重载 | 进度保持，不重复扣费或免费完成 | NOT EXECUTED |
| S06 | 机器半批次/喷涂缓存重载 | 输入、输出、进度和增产点数一致 | NOT EXECUTED |
| S07 | 多工厂与非当前星球 | 配方补齐和清理覆盖全部相关工厂 | NOT EXECUTED |
| U01 | 直接删除插件 | 记录真实支持边界；失败时有受支持的清理流程 | NOT EXECUTED |
| U02 | 清理另存→纯原版加载 | 无 Mod ID 活动引用导致的报错，保留物品和原版科技 | NOT EXECUTED |
| U03 | 退料空间不足/清理失败 | 不报告成功、不覆盖原档、不丢物品 | NOT EXECUTED |
| U04 | 含自定义配方的蓝图 | 明确风险；不自动破坏用户外部文件 | NOT EXECUTED |
| U05 | 清理后重新安装 | 原版状态不倒退，本 Mod 新内容可重新正常建立 | NOT EXECUTED |
| I01 | A/B/C 完整性对照 | 本 Mod 不新增异常标记、Metadata 消费或成就资格变化 | NOT EXECUTED |
| I02 | 安全卸载后的 D 组 | 纯原版完整性和正常功能保持 | NOT EXECUTED |
| I03 | 银河联网验证 | 授权测试并记录结果；无法验证不得标通过 | NOT EXECUTED |
| C01 | UXAssist 等禁用战斗科技选项 | 冲突可诊断，不偷偷打开、解锁或更改其他 Mod 配置 | NOT EXECUTED |
| C02 | Proto ID/配方槽位冲突 | 清楚报错，不覆盖其他内容、不运行时随机改 ID | NOT EXECUTED |
| C03 | 未支持游戏版本 | 可见提示和诊断，不在正式存档上默默试错 | NOT EXECUTED |
| V01 | 中英科技树 UI | 两节点不重叠、展开后不遮挡、前置可理解 | NOT EXECUTED |

## Measurements, not impressions

For D04 and R01–R06, verify input/output/power are unconstrained and identify machine speed, spray level and mode. Expected unsprayed 1× rates are 60 / 15 / 15 / 10 / 7.5 / 6 outputs per minute. Capture both vanilla matrix-lab production and research baselines before and after adding the custom matrix recipe.

For T01, verify actual 100 blue / 36,000 Hash and 300 each blue/red/yellow / 180,000 Hash totals; candidate ItemPoints `[10]` and `[6,6,6]` are not accepted without consumption evidence. New technologies must not depend on Dark Fog drops, and Information Topology must not implicitly require Energy Analysis.

For S03/S04, record repeated same-save loads and Peace → combat → Peace in one process, including completed and half-completed research. For U02/U03/S07, inventory ledgers must cover all instantiated factories and preserve unsprayed/sprayed inputs without loss or duplication. Partial cleanup or unknown references must fail closed.

For I01–I03, inspect flags and user-visible warnings after registration, load, each technology, each handcraft/industrial recipe, spray mode, vanilla hidden unlock, save/reload and cleanup. Check a reproducible ordinary achievement condition; inspect Metadata use records and existing Milky Way eligibility rather than inferring from absence of an exception. Never modify these flags or auto-complete milestones.

## Acceptance data

`compatibility/acceptance-status.json` is the machine-readable report. No release flag is true. `compatibility/tech-layout.json` contains candidate coordinates only. `compatibility/proto-ids.json` fixes project IDs but does not claim global reservation or a live collision scan. `compatibility/vanilla-proto-snapshot.json` is an intentionally empty target-export template.

`release` packaging requires every matrix ID exactly once with target-game execution, `passed` status, real evidence files, an actual environment record, exact plugin/Core DLL hashes, resolved compiler-reference identities/hashes, configuration, reference mode, source fingerprint, matching build identity and explicit release approval. The candidate's local files and build/audit reports must still match the tested binding; missing, stale or different-binary evidence is refused. Conservative guards are not themselves proof of compatibility.
