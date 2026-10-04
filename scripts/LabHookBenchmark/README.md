# Optional native-matrix helper benchmark

```sh
DOTNET_TieredCompilation=0 dotnet run --project scripts/LabHookBenchmark/LabHookBenchmark.csproj --configuration Release
```

On PowerShell, set `$env:DOTNET_TieredCompilation = '0'` before the same `dotnet run` command. Disabling tiered compilation avoids measuring transitions between JIT tiers. The printed runtime/configuration identifies what was actually measured.

This optional tool invokes both real `NativeMatrixContract.IsSupported` overloads from the Core project. The `IReadOnlyList<int>` overload retains the previous implementation; the `int[]` overload is the native lab path. It has no game references or game-type stubs. It does not run in CI and has no timing threshold.

The accepted dataset and a seeded, shuffled 4,096-array mixed dataset each receive five million-call warmups per overload, then seven 10-million-call samples in alternating order. The mixed dataset includes equal numbers of valid registries, first-slot mismatches, last-slot mismatches and short arrays. Inputs are allocated before timing. Checksums are checked against an independent expected-value oracle; per-sample current-thread allocation deltas and median timings are reported. An instrumented read-only list also exposes the previous accepted-path work count: eight `Count` accesses and six indexed accesses. The array overload has no interface dispatch and still rereads every live value.

Results concern only this helper under the printed .NET 8 runtime, CPU and JIT conditions. They are not Unity Mono timings, whole patched-call timings, game frame times, or FPS gains. The postfix's owned-recipe/active-result early return and the diagnostic-window callback reuse are separate source/compiled-IL work reductions; this tool does not pretend to execute either game hook.
