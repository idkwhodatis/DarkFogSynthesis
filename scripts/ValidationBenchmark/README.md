# Optional execution-validation benchmark

```sh
DOTNET_TieredCompilation=0 dotnet run --project scripts/ValidationBenchmark -c Release
```

On PowerShell set `$env:DOTNET_TieredCompilation = '0'` before the same command.
This optional, threshold-free .NET 8 tool uses the real immutable Core definitions
and optimized contract. It has no game references or game-type stubs. It first
runs the exact contract regression file and independent original-LINQ differential
cases, then compares capturing-int `Single` versus a six-entry dictionary and
original LINQ execution-array checks versus the real indexed contract.

Inputs and delegates are created before measurement. Each case warms up for
20,000 calls and takes seven 200,000-call samples, recording current-thread
allocated bytes, median duration and a stable checksum. Valid first/last recipe
and 25%-valid mixed cases are included. No validation result is cached.

Measurements describe this pure .NET 8 model, not Unity Mono, actual game loading,
frame time, or FPS. The lookup baseline captures an integer, not the larger game
component struct. The eager successful-load diagnostic-string allocation and GUI
callback allocation are separate source/IL observations, not included here.
