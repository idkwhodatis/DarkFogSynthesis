# Source-side verification evidence

Recorded 2026-10-04 in the implementation workspace. These checks validate source artifacts and packaging behavior only; they do not execute Dyson Sphere Program or establish the D01–V01 game acceptance results.

| Check | Result | Evidence/command |
|---|---|---|
| Localization and fixed-content metadata | PASS | `python scripts/validate-content.py`: 30 matching EN/ZH keys; both technologies' names, descriptions and conclusions exactly match the frozen plan; fixed ID manifest matches Core constants; all 35 acceptance IDs present; manifest syntax valid |
| PNG dimensions and alpha | PASS | `python scripts/generate-assets.py --check`: 3 RGBA PNGs at 256 × 256, transparent corners, opaque and antialiased pixels; visible technology pixels are white |
| Renderer reproducibility | PASS | Regenerated with installed Inkscape 1.4 (`e7c3feb100`, 2024-10-09); all PNGs, root package icon and asset manifest matched their earlier SHA-256 byte for byte |
| Artwork inspection | PASS, source preview only | Visually inspected original vector rendering on a dark background at 256 and 64 pixels. This is not Unity tint or game UI evidence |
| Packaging guards | PASS | `python scripts/test-packaging.py`: 35 tests covering manifest/source allowlist, root/ancestor/file/runtime symlink refusal, stale or missing compiler/binary/reference evidence, exact tested-build release binding, mandatory audits, reference-smoke refusal, deterministic source-only ZIP and per-file SHA-256 inventory |
| Python syntax | PASS | `python -m py_compile scripts/generate-assets.py scripts/validate-content.py scripts/package.py scripts/test-packaging.py` |
| Whitespace errors | PASS at this check | `git diff --check`; rerun after integration because this check does not validate untracked-file contents |
| PowerShell wrappers | NOT EXECUTED | PowerShell was not installed in the verification environment. The wrappers call the tested Python engine; this does not count as executing the wrappers |
| Runtime integration, production, research, saves, removal and integrity | NOT EXECUTED | Target game is unavailable. See `acceptance.md` and machine-readable status |

The compiler and pure-test evidence is recorded separately in the compile baseline. Source and packaging changes require rerunning affected checks; a static document is not self-updating CI evidence.

## Integrated source and compiler checks

- Core: **71/71 tests, 15,282 assertions passed**. Covers frozen recipes/costs, immutable typed IDs, all four configuration combinations, 4,096 research/unlock combinations, repeated sessions, foreign-edge ownership/restoration, conservative removal predicates and fixed manifest/localization coherence
- Runtime plugin: **build passed, zero warnings/errors**, against the exact compile-only reference combination in [compile-baseline.json](compatibility/compile-baseline.json). This is not the user's installation or a game execution
- [PublicApiAudit](../scripts/PublicApiAudit/README.md): metadata-only scan passed with no unresolved or originally nonpublic direct game accesses. A compile-only negative fixture correctly rejected the private RecipeProto productivity setter; runtime access uses explicit reflection instead
- Both owned DLLs can be packaged only with matching hashes/fingerprint and the `reference-assembly-smoke` classification. Formal release remains refused

The technology position check is page-aware: main-tree candidate (29, -43) must not conflict with upgrade-page technology 3506 at the same coordinates. Candidate geometry is still not a measured UI-layout pass. Exact prototype/cached values are exported by the runtime diagnostic action for the future P0 comparison.

## Source-comparison follow-up

The [three-mod comparison](compatibility/mod-source-comparison.md) records pinned sources, licenses, adopted patterns and exclusions. The compiled-resource startup defect from the first experimental build is fixed: both exact 30-key locale dictionaries are embedded in the main DLL. ResourceAudit passed 15 metadata-only regression tests and the actual DLL audit. Packaging passed 35 tests and now requires both resource and API audit evidence bound to the current DLL hash. The final API audit examined 820 direct game member sites, 195 unique members and 46 game types with no originally nonpublic or unresolved accesses. No target-game behavior was executed.

## Review regression and bounded optimization pass

All six source findings against `e4b9c7d` are described in [review regressions](compatibility/review-regressions.md). New actual Core tests cover exact LabOpt identity, original mutable-buffer alias refusal, nonfatal export exceptions and session-latch transitions. The clean plugin reference build passed with zero warnings/errors. Diagnostic schema 2 reports prototype registration separately from aggregate session readiness.

The [performance review](compatibility/performance-review.md) records optional reproducible .NET 8 helper measurements. Execution checks retain exact live values while avoiding iterator/temporary-array allocations; import lookups reuse immutable definitions; unrelated/idle lab hooks return early; the GUI callback is reused. No Unity frame-rate claim or relaxation of save validation is inferred from those helper results.

The successful build capture records 24 actual plugin compiler references and 113 Core references, including the exact game, framework and dependency hashes, with source/configuration/SDK and emitted-DLL identity. The artifact report contains no private dependency paths. Release acceptance must identify that exact tested build; the checked-in acceptance remains unexecuted and release packaging is refused.
