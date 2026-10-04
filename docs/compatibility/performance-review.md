# Bounded allocation and hook review

Recorded 2026-10-04. These are source, compiled-IL and isolated .NET 8 measurements, **not Unity Mono, game frame-time or FPS results**. The frozen definitions and all live compatibility checks remain in force.

## Changes retained

- Cache the six immutable recipe definitions by ID instead of allocating a dictionary for each loaded-factory sweep or a capturing predicate for each imported owned machine. No mutable prototype or validation result is cached.
- Validate live execution arrays with exact lengths and indexed comparisons. This removes LINQ iterators and temporary output arrays while retaining productivity, timing, input/output ID, count and order checks.
- Construct the saved-machine error message only on a failing branch. Successful validation no longer eagerly formats a planet/recipe diagnostic.
- Reject unrelated or idle production-lab calls before looking up plugin readiness. The owned active recipe still checks readiness and all six current matrix values.
- Reuse the diagnostic window callback rather than constructing its delegate on every GUI event.

Import-time checks, all registry/session checkpoints, the loaded-machine sweep and in-place array mutation detection remain. The native matrix array is not cached by identity or assumed immutable. No per-frame world/research scan was introduced, and the review found no justification for a global prototype rebuild or extra UI refresh loop.

## Reproducible helper measurements

Run the optional [execution benchmark](../../scripts/ValidationBenchmark/README.md) and [matrix benchmark](../../scripts/LabHookBenchmark/README.md). Both use the actual Core project and independent expected-value checks, with preallocated inputs, warmup, seven samples and checksums. Neither uses game-type stubs or imposes a CI timing threshold.

Observed on .NET 8.0.31, Linux, with tiered compilation disabled:

| Helper / dataset | Previous implementation | Revised implementation |
|---|---|---|
| Definition lookup, first/last of six | 120 bytes/call | 0 bytes/call |
| Valid execution-array comparison | 304 bytes/call | 0 bytes/call |
| Mixed execution-array comparison, 25% valid | 166 bytes/call | 0 bytes/call |
| Valid execution comparison, median per 200,000 calls | 53.55–62.20 ms | 5.39–6.27 ms |
| Mixed execution comparison, median per 200,000 calls | 32.09 ms | 4.04 ms |
| Six-matrix helper, valid dataset, median | 35.251 ns/call | 4.509 ns/call |
| Six-matrix helper, mixed dataset, median | 22.161 ns/call | 4.836 ns/call |

Both matrix implementations allocated zero measured bytes. Those timings isolate interface dispatch versus direct array access; they do not measure a complete Harmony hook. The lookup baseline captures an integer, not a game component struct. The removed eager diagnostic string and reused GUI delegate are separate source/IL findings and are not included in these allocation totals.

Focused regressions cover valid/malformed execution data, exact lengths and order, native-array/interface parity, null/short/long matrices, in-place changes, and preservation of caller arrays. They run in the ordinary pure test suite; timing comparisons remain optional. Final aggregate counts and reference-build evidence are in [source verification](../source-verification.md).

## Acceptance boundary

An isolated benchmark cannot justify weakening live validation or claim a measured gameplay improvement. Real target-game startup, lab stacking/animation, save transitions, hidden acquisition, serialization dependencies, refund conservation and cleanup/reload remain gated by the full acceptance matrix. Unsupported production replacements such as the reviewed LabOpt path must be rejected until a compatible adapter is verified; a faster local helper does not make that replacement compatible.
