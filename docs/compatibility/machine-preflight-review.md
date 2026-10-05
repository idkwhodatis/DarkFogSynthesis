# Per-machine removal and laboratory preflight fixes

Baseline: `846e7e784704f32a719bdd5b8890a466547bda74`.
No recipe, fixed identifier, research cost, discovery rule, save format, networking
policy, refund behavior or target-game acceptance entry changes.

## Per-target cleanup admission

Scans retain the factory index, factory/system references, component/entity IDs,
recipe/research configuration and entity prototype/model. The post-backup scan
must match that identity, not just the component-pool index.

Before each reset, the native adapter rechecks factory/pool membership, entity
backlinks, the exact captured owned configuration and every production/research
buffer or progress field. `RemovalTargetGuard.Reset` enforces this order:

1. Validate the current target and paused, validated session.
2. Export the rollback snapshot without enrolling it yet.
3. Revalidate the same target and session after export callbacks.
4. Enroll the rollback action, then invoke the native reset.

A callback from an earlier machine or the current export cannot turn a changed,
occupied or replaced later target into a reset request. No stale snapshot for that
refused target is enrolled. Earlier mod edits still use the existing limited
rollback and quarantine path; peer writes are not repaired, refunded or erased to
make admission succeed. Native reset exceptions retain their enrolled snapshot.
Entity checks are identity/configuration checks, not a native generation counter
or a transaction against arbitrary reentrant changes inside the reset itself.

## Laboratory selector

The selector uses the production `LabStackPreflight` traversal and empty-state
predicate. The native adapter passes both `hashBytes` and `extraHashBytes` through
`RemovalSafetyPolicy.HasResearchState`. Positive and negative residual values on
any member of the connected stack refuse selection without changing them. Null,
empty and all-zero buffers continue to count as empty. Predecessor discovery still
reads only IDs/links; buffer inspection is limited to the selected connected stack.
The existing multiplayer guard, native selection calls and failure boundary remain
unchanged. No ordinary single-player session is blocked merely by this preflight.

## Regression scope

Core tests execute the actual target admission/export boundary and actual selector
predicate/traversal, covering changes before export, during export and from an
earlier reset; identity/configuration drift; resource/progress state; positive and
negative hash fields on selected and nonselected labs; invalid stack topology; and
unchanged single/stacked-lab controls. Inputs are checked for unintended mutation.
Harmony tests run actual detours on harmless export/reset functions around the same
production boundary, with changed-target refusal and unchanged-target controls.
They do not emulate or execute DSP serialization or its reset/rollback semantics.

Reference compilation can check native field/signature accessibility; it is not
Unity/DSP execution. Installed-game research, production, UI and copied-save
cleanup/reload must still be tested. No green source test grants release approval.
