# Incremental content adaptation

Approved against b30dce929ac6b2872cd49b527b06692c786a3231.

The explicit recipe revision supersedes only Silicon Neuron's Particle Broadband ×1 input: use Titanium Alloy (1107) ×2. Other quantities, research costs, identifiers, machines and progression policy remain unchanged.

## Ordered work

1. Recipe definition, independent oracle and upgrade warning.
2. Patch A: resolve both icons and all recipe slots, and create detached mutable argument arrays before the first custom prototype is queued; retain existing lifecycle/failure guards.
3. Patch B: extend the existing report with opt-in, phase-labelled snapshots and deterministic structural comparison; diagnostics must never affect readiness or write saves.
4. Patch C: add native-unlock-based locked feedback and opt-in rendered rectangle capture/comparison; do not claim measured layout or move vanilla nodes without target-game evidence.
5. Patch D: provide the exact-build target-game runbook using existing acceptance IDs; research consumption, acquisition/discovery, production, stacking and save/reload are unexecuted until actually observed. Add no speculative discovery hook.

## Preserved boundaries

Use the existing CommonAPI/LDBTool registration and native research engine. No new scientific matrix, save sidecar, blanket cache reset, automatic unlock, full refund operation or release approval. Prior compile-baseline reports describe their historical build, not the new runtime.

The source-comparison rationale and pinned upstream revisions remain in [mod-source-comparison.md](mod-source-comparison.md). This document tracks implementation, not target-game acceptance.

## Patch A implemented

`ContentPreparation` is the shared production preparation routine, not a lifecycle controller. It resolves both technology icons before allocating a tab, checks all six slots after allocation, and supplies detached per-attempt mutable arrays. `ContentRegistry` validates the icon accessor before entering that routine and queues prototypes only after it returns. Existing binding, ownership, collision and startup failure policies remain in force. Failed image decoding disposes the newly allocated texture.

Core regression coverage injects a second-icon failure, final-slot collision, overflow and invalid tabs, and verifies array isolation across definitions and repeated attempts. These tests exercise production Core preparation; they do not execute LDBTool or Unity registration.


## Patch B: phase-aware observations and comparison

`Diagnostics.TraceSnapshots=false` invokes no exporter at the new lifecycle checkpoints. Enable it before startup for `pre-register`, `pre-mode`, `post-mode`, `execution-cache-ready`, `session-ready`, and `post-restore`. The existing warning-only automatic export is labelled `post-bind`, not execution-cache readiness. `session-ready` follows release of the native Begin token. Schema 3 includes random process IDs, weakly held per-session IDs, startup policy, copied arrays, item crafting lists/fallbacks and technology caches. Save names and installation paths are not exported. Manual export while paused in a validated current session additionally observes owned machines and special-item discovery; other stages explicitly omit those live observations.

Commands:

```sh
python scripts/compare-runtime-snapshots.py before.json after.json --phase registration
python scripts/compare-runtime-snapshots.py pre-mode.json post-mode.json --phase mode
python scripts/compare-runtime-snapshots.py post-mode.json post-restore.json --phase restore --baseline pre-mode.json
python scripts/compare-runtime-snapshots.py a.json b.json --phase equal --output new-report.json
```

The independent oracle includes Titanium Alloy (1107) ×2. Restoration compares with the same session's pre-mode, already-registered baseline, not pre-registration. Existing foreign fallbacks and prerequisites are preserved. Duplicate IDs, missing baselines, malformed caches and differing process/build environments are refused. Exit 0 is only a structural match of exported fields; exit 1 reports drift; exit 2 refuses input. No acceptance flag or save is written. Other mods/framework changes between checkpoints deliberately appear as drift and are not automatically attributed to DFS. Native object-identity checks remain authoritative. Output reports are create-only. No background or production-tick scan was added.
