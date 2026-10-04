# Lab mutation and abort failure containment

Reviewed against `9385acee9ab693e6b28f61aa1128db05397f0f69` on 2026-10-04. These are source-level defects and isolated failure-injection reproductions. No DSP game or native Harmony detour was executed.

## Uncertain lab mutations

The selector previously caught failures from `SetFunction`, `SyncLabFunctions`, `SyncLabForceAccMode` and the whole-stack postcondition only to log and display a popup. A root or partially synchronized stack could already have changed. Neither session readiness nor the existing resume/save guards saw a failure latch.

The selector now calls the production Core `SessionFailureBoundary` after its existing context and unlock checks. Read-only whole-stack preflight is still required. Once the first native mutator is attempted, any thrown exception or failed whole-stack postcondition blocks the actual session before optional pause, UI and logging callbacks. All later native stages stop. A blocked boundary refuses further mutation attempts. The existing reference-identity session latch keeps readiness, resume and saving closed through teardown and unsuccessful replacements; only a different fully validated session releases it.

There is no speculative rollback, invented buffer refund, native back-button call or direct buffer reset. Native reset/refund transaction semantics are not established by these sources. The failed in-memory stack is left untouched for diagnosis, and the player must abandon that session and load a different valid one. Pausing is an additional best-effort action; a native pause failure cannot clear the save/resume latch. Verified successful native selection returns before optional button cleanup, so a UI-only exception does not falsely claim that native mutation completion is uncertain.

The pinned [Nebula laboratory update processor](https://github.com/NebulaModTeam/nebula/blob/5ecd2b4168c8e4df72a301894d004aee5629961b/NebulaNetwork/PacketProcessors/Factory/Laboratory/LaboratoryUpdateEventProcessor.cs#L45-L56) uses these same three native operations. That establishes an existing call pattern, not atomicity, rollback guarantees or compatibility with this synthesis recipe. LabOpt remains rejected by the earlier source-backed compatibility guard.

## Blank errors and interrupted cleanup

`RejectSession` previously passed `Exception.Message` directly into a latch that rejects blank reasons. An empty/whitespace message could throw before blocking. `AbortSession` then skipped prerequisite restoration, and the import transition could retain its session/mode fields. A logger or pause failure could similarly interrupt later safety work.

`SessionFailureBoundary.FailureReason` now reads the virtual message once and supplies a nonblank type-labelled fallback for missing, blank or throwing getters. The compatibility latch is committed first. Pause, status presentation and diagnostics are isolated best-effort callbacks. Abort attempts restoration and then transition cleanup in nested `finally` paths, including when restoration itself fails. A diagnostic failure cannot replace the original import exception returned by the Harmony finalizer. Cleanup clears transition bookkeeping without treating destruction or a failed replacement as validation.

The low-level `SessionCompatibilityState.BlockSession` argument contract remains strict. Every runtime exception reaches it through the tested normalization boundary. Ordinary teardown also clears its bookkeeping in `finally`; a restoration error is latched and remains an error.

## Relative acceptance paths

Packaging accepted a safe relative acceptance file during release validation, then attempted to make that unchanged relative path relative to an absolute project root while assembling the ZIP. This caused a `ValueError` after earlier gates succeeded.

`project_path` now checks lexical symlink components and containment before returning one canonical absolute path. Snapshot/archive lookups use the same canonical root. Relative arguments remain relative to the invoking working directory. The stricter root/ancestor/file symlink and outside-root checks remain in force.

Subprocess CLI regressions cover root-relative, nested-relative, dot/parent segments, absolute paths and invocation from a nested working directory. They verify the exact acceptance bytes, ZIP hashes and recorded candidate identity. Negative CLI tests retain symlink, outside-root and checked-in unverified-acceptance refusal. Only ordinary text candidate fixtures substitute the PE-output discovery step; these tests do not create real gameplay evidence or qualify a release.

## Executed evidence and limits

- 75 pure tests / 17,380 assertions, including injected failure at every native stage, whole-stack verification, read-only preflight refusal, successful selection and UI-only failure
- Abort combinations include empty/whitespace/ordinary/throwing messages, every pause/UI/logger failure combination, restoration failure and cleanup failure; observations are asserted outside callbacks that intentionally swallow diagnostic exceptions
- 52 packaging regressions and 15 compiled-resource-audit self-tests
- Clean compile-only reference build and metadata audits; exact results and hashes are recorded in the compile baseline and package build report

These tests execute the actual shared production failure boundary with harmless callbacks. They do not run the native methods, UI, save engine or Harmony patches. Actual pause/resume/save denial, hidden-item acquisition, Unity framework assembly resolution, copied-save cleanup/vanilla reload and all other D01–V01 scenarios remain unexecuted. Generalized P4 refunds remain disabled. The frozen plan, IDs, recipes, costs and discovery rules are unchanged.
