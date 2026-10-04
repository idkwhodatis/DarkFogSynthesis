# Session completion, pending writes and release evidence

Reviewed from `4fde0d64612394e371b025961b8cd33f07915646` on 2026-10-04. The preceding three source findings remain fixed. This follow-up adds actual HarmonyX tests over harmless managed fixture methods; no DSP assembly, game save, Unity object or installed-game profile is used.

## Canceled native Begin is not successful initialization

HarmonyX 2.7.0 can run finalizers after a prefix returns `false`, with the original body skipped and no exception. Its pinned [prefix control flow](https://github.com/BepInEx/HarmonyX/blob/253725768e59b0e1ea90105cdbcc4a0a477422c7/Harmony/Public/Patching/HarmonyManipulator.cs#L293-L347) and [finalizer generation](https://github.com/BepInEx/HarmonyX/blob/253725768e59b0e1ea90105cdbcc4a0a477422c7/Harmony/Public/Patching/HarmonyManipulator.cs#L376-L442) establish the path. The real detour fixture reproduced `__runOriginal == false` and `__exception == null` without calling the original body.

The runtime finalizer now consumes `__runOriginal` and routes completion through `SessionBeginCompletion.Finish`. A skipped original becomes an explicit compatibility failure before late validation or either completion callback. An existing exception is preserved. A canceled/failed replacement cannot clear the compatibility latch or removal quarantine. Unknown replacement initializers are unsupported rather than assumed equivalent to native Begin.

Each Begin prefix also acquires an opaque validation token, passed to its finalizer through Harmony's `__state`. Nested/reentrant Begin is refused; a finalizer without an entry token cannot promote a session. Only the owning invocation releases its in-flight barrier in `finally`. This prevents an inner completion from reopening writes while an outer Begin/postfix pipeline is still active.

## Pending sessions cannot write

An absence of compatibility errors did not previously prove that `GameMain.data` had completed native initialization and final checks. `SessionCompatibilityState.CanPersist` now requires the exact validated identity, no pending replacement, no active Begin token and no compatibility failure. Startup must also be ready. The save prefixes consult this contract, and ordinary mod actions/maintenance entry require the same completed validation.

First load, replacement load and repeated Begin all keep persistence closed until final validation. Ending an abandoned replacement invalidates the prior qualification: it cannot revive an old session after shared progression was changed for the attempted load. Actual native exit/load ordering still needs target-game acceptance.

`SessionPersistencePolicy` retains the deliberate maintenance exception: when cleaning or quarantined, only the explicitly permitted named save is allowed, using ordinal name equality. Autosave, error autosave and last-exit saves cannot borrow that permit. The permit never overrides failed startup, a compatibility block, the wrong identity or pending validation. It authorizes neither automatic refunds nor general save cleanup.

Resume retains its existing separate startup/compatibility/quarantine checks. It is not blindly tied to the stricter persistence qualification, because native Begin may have its own resume-related lifecycle. This change qualifies writes, not the game's entire startup order.

## Actual legacy Harmony testing exposed unsupported injection

The new pipeline test also found that HarmonyX 2.7.0 rejects the old save prefix's `object[] __args` injection at patch installation. Successful C# compilation could not detect that runtime argument binding defect. Runtime saves now use supported, separate exact signatures: `string __0` for the named save and no native arguments for the three automatic paths. Each prefix/finalizer pair keeps its own `__state` slot for save-counter cleanup and shares the same permission policy.

The actual legacy wrapper accepts `__runOriginal` in a finalizer with a void prefix, as used by this plugin. The isolated fixture removes its finalizer before its last prefix during teardown because HarmonyX 2.7.0 cannot regenerate this injection with zero prefixes. This test-harness detail is not a claim that arbitrary foreign patch changes are compatible.

The pinned HarmonyX 2.7.0, MonoMod.RuntimeDetour/Utils 21.12.13.1 and Mono.Cecil 0.11.4 packages execute successfully in the isolated .NET 6.0.36 test process. Our Linux .NET 8.0.31 probe crashed during legacy Harmony initialization before any target patch. CI therefore installs SDK 6.0.428 alongside the existing .NET 8 build SDK and runs the fixture on its exact .NET 6.0.36 host. This end-of-support runtime is used only for bounded compatibility tests, not production execution, a service or a distributed dependency. The plugin remains `net472` and Core remains `netstandard2.0`.

## Evidence references must resolve inside the frozen ZIP

Release validation previously accepted a safe nonempty local evidence file even if source selection omitted its extension or ancestor directory. Full temporary candidate-fixture CLI tests reproduced missing `.log`, `.jpg`, `.jpeg` and `evidence/bin/*.txt` references in otherwise internally valid ZIPs.

The final release gate now checks every accepted evidence reference against the exact frozen `contents` dictionary before output publication. Each entry must exist and have nonempty bytes. Those same bytes and paths produce the ZIP and SHA-256 inventory. A live-file repair cannot make an empty frozen snapshot acceptable.

The source allowlist is unchanged. Unsupported/excluded evidence refuses packaging with a clear error; arbitrary evidence files are not automatically included. Sanitized logs can be recorded as `.txt` and screenshots as `.png` under non-excluded evidence directories. Existing containment, symlink, binary, exact-build, acceptance-ID and online-authorization checks remain required. The checked-in acceptance report still refuses release.

## Verification boundary

The real-Harmony project exercises actual prefix/original/postfix/finalizer order using production Core completion, identity and save-permission helpers. Its counters establish whether harmless method bodies executed; its quarantine marker observes the completion callback and does not simulate or certify game cleanup. It covers cancellation, successful and failed Begin, nested/repeated entry, pending/abandoned replacement, callback save attempts and named maintenance permissions. Pure tests and the complete packaging suite remain separate checks.

Final counts and compile metadata are recorded in [source verification](../source-verification.md), [compile baseline](compile-baseline.json) and the test output. Actual DSP Harmony targets, native game initialization/pause/exit, save serialization, hidden discovery, Unity framework resolution, cleanup/vanilla reload and integrity acceptance remain unexecuted. All 35 D01–V01 game scenarios are still pending; generalized P4 refunds remain disabled.
