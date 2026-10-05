# Native-mutation persistence barrier and startup error containment

Baseline: `bd51ce71da4356ba4d040e939c53c8c0d9ca16a7`.

## Mutual save/mutation admission

`SessionCompatibilityState` owns atomic admission for both ordinary persistence
and the bounded native lab mutation. The same lock checks/grants these leases;
it is never held while invoking native methods, user callbacks or diagnostics.
Every admitted save retains its lease until its paired Harmony finalizer. Nested
native save delegation remains allowed, and disposal is idempotent and releases
the original owner rather than consulting a possibly replaced current session.

`SessionFailureBoundary.TryMutate` performs read-only preflight, then requests a
mutation lease for the exact validated identity. An already admitted save or
nested mutation refuses selection without waiting or latching a healthy session.
While native mutation and its whole-stack verification run, `CanPersist` is false
and new saves cannot acquire a lease. A native/verification exception records the
existing session failure before the temporary lease is released in `finally`.
Successful completion restores ordinary persistence. A silently invalidated
session cannot report a successful mutation. Session initialization during the
mutation cannot re-qualify the world or release the caller's lease.

This gate does not simulate Begin/End session transitions, patch anti-cheat,
modify Metadata, invent rollback/refunds, or make native calls transactional.
Existing startup, maintenance-name/quarantine, and live Nebula checks remain
mandatory before save admission. The existing save-entry signatures are preserved;
an additional late-priority save prefix/finalizer owns an IDisposable lease. The
original maintenance prefix/finalizer and its write accounting are unchanged.
There is no game-save format change. Ordinary single-player and inactive Nebula
continue to use their existing successful lab-selection/save paths.

## Startup failure recording

`StartupSafetyState.Fail` atomically records a nonblank first-failure fallback
BEFORE reading the virtual Exception.Message getter. The winner may enrich that
fallback with a successfully observed message. Blank/throwing/reentrant getters
cannot reopen the gate or replace an earlier failure; later Fail calls do not
read another diagnostic getter. This also protects already-ready content, not
only initialization failures whose readiness flags have not yet been set.

## Regression boundaries

Core regressions use the production classes to exercise mutation/verification
save refusal, nested mutation refusal, failure ordering, successful reopening,
nested/idempotent write leases, pending/stale session refusal, interrupted-session
completion, and synchronized concurrent save/mutation admission races. Startup
cases include ready/unready gates and blank/throwing/reentrant Message getters.
The additional `DarkFogSynthesis.Persistence.Tests` executable uses actual
Harmony detours and the same lease API, all four save shapes,
callback saves, native save exceptions, nested save delegation, active-write
mutation refusal and absent/installed-but-inactive Nebula controls.

These tests do not run DSP, Unity, actual native lab methods or game saves.
Reference compilation is only type/signature/accessibility evidence. Target-game
acceptance and anti-cheat/Metadata eligibility cannot be inferred from green CI.
