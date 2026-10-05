# Design-time builds and maintenance callback boundaries

Review baseline: `3849f7ca80fcfd5b2aca714259fcb12dcf6bf361`. The previous
checkout/source-inventory fixes remain in place. This follow-up addresses two
new, independently checked control-flow issues without changing frozen content
or enabling general refunds.

## Design-time compilation

An isolated actual Core build reproduced the reported sequence: the ordinary
Release build passed, then an SDK `Compile` operation with `DesignTimeBuild=true`,
`SkipCompilerExecution=true` and `ProvideCommandLineArgs=true` failed. The DLL
remained unchanged, but the completed `.build-inputs.txt` sidecar had already
been overwritten with an incomplete invocation.

Microsoft's [project-system guidance](https://github.com/dotnet/project-system/blob/main/docs/design-time-builds.md)
explains that design-time builds supply editor information without compiling,
and that unrelated targets should avoid running in them. The local .NET SDK
8.0.425 compiler target also documents its skipped-compiler command-line path.
The reproduction invokes that SDK path; Visual Studio itself was not launched.

Capture-only evaluation, compiler-entry and completion work now exclude
design-time builds before starting an invocation or touching the sidecar. They
return compiler arguments normally and leave an existing candidate intact,
including when `DarkFogCaptureBuild=true` was supplied explicitly. An ordinary
build that requests `SkipCompilerExecution=true` still refuses to issue new
provenance and invalidates its incomplete attempt.

The genuine Core fixture checks an initial design-time invocation without prior
outputs, preservation after a real build, actual returned Csc arguments, unchanged
output bytes/timestamps and file membership, and the retained ordinary-build
refusal. The same fixture runs in Linux and Windows CI. This establishes SDK
build behavior, not an observed IntelliSense session in a user's IDE.

## Cleanup pause and session ownership

The earlier resume prefix checked startup/compatibility and quarantine, while
`PrepareCandidate` set quarantine only after its backup save. During that gap,
the maintenance flag was already active but a native save callback could call
`GameMain.Resume`. The next scan compared blockers and machine targets without
confirming that the current session was still the captured, paused session.
Failed preflight could also resume whichever session happened to be current.

Maintenance now blocks the ordinary resume path for its whole interval. The
captured session and its dependent state must still match, remain valid and
remain paused across native callbacks before cleanup proceeds or claims success.
Failed-preflight restoration occurs after maintenance is cleared and only for
the captured session when it remains eligible. Its temporary thread-local scope
rechecks that identity at the native resume prefix and is restored in `finally`,
including if an earlier foreign prefix changes the session or throws. Native
callbacks run outside the maintenance lock. The usual resume behavior during
native `Begin` remains distinct from the stricter persistence qualification.

Quarantine cannot be cleared by a replacement session while maintenance is
active. A later validated replacement outside that interval can still clear it.
Existing rollback actions require the same validated paused context; an invalid
context refuses restoration and reports incomplete rollback instead of importing
state into a replacement session or claiming that pause was confirmed.

Focused pure tests and real Harmony detours over harmless managed methods cover
the pre-quarantine backup callback requesting resume, changed pause/identity
refusal and preflight restoration. Those fixtures exercise shared production
decisions; they do not execute DSP's simulation, disk saves or native cleanup.
The final reference build and metadata audit check the actual adapter separately.

## Verification boundary

See [source-side verification](../source-verification.md) and
[`compile-baseline.json`](compile-baseline.json) for final counts and build
identity. No particular installed peer mod was identified as triggering this
callback, and no concurrent DSP simulation, item loss or corrupted save was
demonstrated. All 35 game acceptance cases remain unexecuted. General P4 refunds
remain disabled, and removal candidates still require isolated copied-save and
vanilla-reload acceptance before ordinary use.
