# Pinned Harmony pipeline regression tests

This executable patches harmless `NoInlining` fixture methods with **real HarmonyX
2.7.0**, then routes finalizers and save prefixes through the production Core
`SessionBeginCompletion`, `SessionCompatibilityState`, and
`SessionPersistencePolicy` and `MaintenanceSessionGuard` implementations.

It loads no DSP, Unity, or BepInEx assembly, writes no save file, and contains no game
type stubs. The fixture's quarantine identity is an observable completion callback
marker. Its assertions do **not** establish that game-backed cleanup, actual saves,
or uninstall work correctly.

## Compatibility host

Build with the repository's .NET 8 SDK; execute on **.NET 6.0.36**. The project pins
this exact runtime with roll-forward disabled and exact dependency versions plus a
NuGet lock file. Use `--locked-mode` on CI restores.

```sh
dotnet restore tests/DarkFogSynthesis.Harmony.Tests --locked-mode
dotnet run --project tests/DarkFogSynthesis.Harmony.Tests -c Release --no-restore
```

Both the .NET 8 SDK and .NET 6.0.36 runtime must be installed in the selected host.
For a temporary isolated runtime, build using the .NET 8 SDK and launch the emitted
DLL with that runtime's `dotnet` executable instead. .NET SDK 6.0.428 includes runtime
6.0.36 if a CI setup tool installs SDKs rather than standalone runtimes.

.NET 6 is end-of-support. It is used only for this bounded legacy-dependency
characterization process, with no untrusted input, service, game, or credentials.
`CheckEolTargetFramework=false` is scoped to this test project; production targets
are unchanged. Do not migrate production software to .NET 6 because of this fixture.
The exact 2021 MonoMod dependencies caused a native segmentation fault (exit 139)
during Harmony initialization on the independently tested .NET 8.0.31 host, before
`Harmony.Patch` ran. A passing .NET 8 compilation is not a runnable pipeline test.

`characterization-host.json` records the measured package/assembly hashes and the
isolated Linux x64 runtime used for the first successful run. NuGet package hashes
for all transitive dependencies are additionally preserved in `packages.lock.json`.
No runtime or package binaries are checked in.

## What the tests exercise

- A void-only Begin prefix permits `__runOriginal=true` in the finalizer
- A foreign `false` prefix produces `__runOriginal=false` and a null incoming
  exception; production completion aborts instead of releasing the fixture latch
- Real save prefixes refuse named, autosave-like, error-autosave-like, and last-exit-like calls made in
  prefixes, the original body, and late postfixes until successful finalization
- Exact named/automatic save prefix and finalizer injection shapes preserve separate
  declaring-type state slots and balanced write counts, including native save failure
- Original, prefix, and late postfix exceptions retain their identity and block writes
- Late compatibility rejection prevents completion and retains fixture quarantine
- Repeated Begin cannot reuse stale validation during initialization callbacks
- Nested Begin in a prefix or postfix cannot acquire or release its caller's
  validation ticket, promote the blocked identity, or permit a write
- Completion callbacks remain unwritable until the owning finalizer releases its ticket
- Pending or abandoned replacements cannot make the old or new identity writable
- A blocked identity cannot reset itself; a different fully validated identity can
- Maintenance exceptions require an exact, ordinal named-save match and never
  bypass pending validation, compatibility failure, or incomplete startup
- A permitted backup callback cannot resume maintenance before quarantine exists
- Direct callback changes to pause, session/history/player, loading or validation
  refuse the next cleanup boundary; no actual native cleanup is simulated
- Failed-preflight restoration is scoped to the original valid session after the
  maintenance barrier clears; ordinary Begin still permits resume while saves wait
- A validated replacement during maintenance cannot clear quarantine; a later
  validated replacement outside maintenance can
- A higher-priority foreign Resume prefix cannot redirect automatic restoration
  to a replacement session; exceptions preserve identity and clear the temporary scope

## Pinned Harmony injection details

HarmonyX 2.7.0 does not implement the later `object[] __args` injection API. Real
patching rejects that parameter with `Parameter __args does not contain a valid
index`. Named-save prefixes use `string __0`; no-argument methods use a separate
prefix. Both route to the same production policy.

This version also cannot regenerate a `__runOriginal` finalizer when **zero**
prefixes remain. A void prefix is sufficient; it need not return a bool. Test
cleanup removes the finalizer before removing prefixes, preventing teardown from
masking test results with `Parameter __runOriginal does not contain a valid index`.
