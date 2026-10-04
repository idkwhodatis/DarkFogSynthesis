# Source-side verification evidence

Recorded 2026-10-04 in the implementation workspace. These checks validate source artifacts and packaging behavior only; they do not execute Dyson Sphere Program or establish the D01–V01 game acceptance results.

| Check | Result | Evidence/command |
|---|---|---|
| Localization and fixed-content metadata | PASS | `python scripts/validate-content.py`: 30 matching EN/ZH keys; both technologies' names, descriptions and conclusions exactly match the frozen plan; fixed ID manifest matches Core constants; all 35 acceptance IDs present; manifest syntax valid |
| PNG dimensions and alpha | PASS | `python scripts/generate-assets.py --check`: 3 RGBA PNGs at 256 × 256, transparent corners, opaque and antialiased pixels; visible technology pixels are white |
| Renderer reproducibility | PASS | Regenerated with installed Inkscape 1.4 (`e7c3feb100`, 2024-10-09); all PNGs, root package icon and asset manifest matched their earlier SHA-256 byte for byte |
| Artwork inspection | PASS, source preview only | Visually inspected original vector rendering on a dark background at 256 and 64 pixels. This is not Unity tint or game UI evidence |
| Packaging guards | PASS | `python scripts/test-packaging.py`: 64 tests covering manifest/source allowlist, root/ancestor/file/runtime symlink refusal, complete production input inventory, stale or missing compiler/binary/reference evidence, exact tested-build release binding, mandatory audits, reference-smoke refusal, deterministic source-only ZIP and per-file SHA-256 inventory |
| Python syntax | PASS | `python -m py_compile scripts/generate-assets.py scripts/validate-content.py scripts/package.py scripts/test-packaging.py` |
| Whitespace errors | PASS at this check | `git diff --check`; rerun after integration because this check does not validate untracked-file contents |
| PowerShell wrappers | NOT EXECUTED | PowerShell was not installed in the verification environment. The wrappers call the tested Python engine; this does not count as executing the wrappers |
| Runtime integration, production, research, saves, removal and integrity | NOT EXECUTED | Target game is unavailable. See `acceptance.md` and machine-readable status |

The compiler and pure-test evidence is recorded separately in the compile baseline. Source and packaging changes require rerunning affected checks; a static document is not self-updating CI evidence.

## Integrated source and compiler checks

- Core: **76/76 tests, 17,471 assertions passed**. Covers frozen recipes/costs, immutable typed IDs, all four configuration combinations, 4,096 research/unlock combinations, repeated sessions, foreign-edge ownership/restoration, conservative removal predicates and fixed manifest/localization coherence
- Runtime plugin: **build passed, zero warnings/errors**, against the exact compile-only reference combination in [compile-baseline.json](compatibility/compile-baseline.json). This is not the user's installation or a game execution
- [PublicApiAudit](../scripts/PublicApiAudit/README.md): metadata-only scan passed with no unresolved or originally nonpublic direct game accesses. A compile-only negative fixture correctly rejected the private RecipeProto productivity setter; runtime access uses explicit reflection instead
- Both owned DLLs can be packaged only with matching hashes/fingerprint and the `reference-assembly-smoke` classification. Formal release remains refused

The technology position check is page-aware: main-tree candidate (29, -43) must not conflict with upgrade-page technology 3506 at the same coordinates. Candidate geometry is still not a measured UI-layout pass. Exact prototype/cached values are exported by the runtime diagnostic action for the future P0 comparison.

## Source-comparison follow-up

The [three-mod comparison](compatibility/mod-source-comparison.md) records pinned sources, licenses, adopted patterns and exclusions. The compiled-resource startup defect from the first experimental build is fixed: both exact 30-key locale dictionaries are embedded in the main DLL. ResourceAudit passed 15 metadata-only regression tests and the actual DLL audit. Packaging passed 57 tests and now requires both resource and API audit evidence bound to the current DLL hash. The final API audit examined 838 direct game member sites, 195 unique members and 46 game types with no originally nonpublic or unresolved accesses. No target-game behavior was executed.

## Review regression and bounded optimization pass

All six source findings against `e4b9c7d` are described in [review regressions](compatibility/review-regressions.md). New actual Core tests cover exact LabOpt identity, original mutable-buffer alias refusal, nonfatal export exceptions and session-latch transitions. The clean plugin reference build passed with zero warnings/errors. Diagnostic schema 2 reports prototype registration separately from aggregate session readiness.

The [performance review](compatibility/performance-review.md) records optional reproducible .NET 8 helper measurements. Execution checks retain exact live values while avoiding iterator/temporary-array allocations; import lookups reuse immutable definitions; unrelated/idle lab hooks return early; the GUI callback is reused. No Unity frame-rate claim or relaxation of save validation is inferred from those helper results.

The successful build capture records 24 actual plugin compiler references and 113 Core references, including the exact game, framework and dependency hashes, with source/configuration/SDK and emitted-DLL identity. The artifact report contains no private dependency paths. Release acceptance must identify that exact tested build; the checked-in acceptance remains unexecuted and release packaging is refused.

## Startup, progression and immutable-package follow-up

The [four subsequent source findings](compatibility/startup-progress-packaging-review.md) are addressed. Startup tests execute the exact production Core prefixes under configuration/localization, partial initialization, binding and disable failures: 118 assertions. The [critical target metadata check](compatibility/startup-safety.md) confirms ten exact signatures and original public access against the recorded game reference; native Harmony detours were not executed. Live progression tests add 907 assertions for mode-aware required edges, current native cache identities, synthesis availability and preservation of valid foreign prerequisites.

Schema-v3 candidate identity includes five distribution asset/manifest hashes. Frozen bytes are validated and reused; archive output is privately verified before atomic no-overwrite publication. The 57 packaging tests include PNG/manifest mutation windows, destination collisions and failure cleanup. Final combined checks: 76 tests / 17,471 assertions, clean reference build, both metadata audits and 15 resource-audit self-tests. All 35 actual game scenarios remain unexecuted.

## Lab mutation, error normalization and relative-path follow-up

The [three latest review findings](compatibility/session-failure-review.md) are addressed with the actual production Core failure boundary and subprocess packaging tests. The 75-test suite (17,380 assertions) injects failures at each native-call stage and the whole-stack postcondition, blank/throwing exception messages, every pause/UI/logger failure combination, restoration/cleanup failures, preflight refusal and UI-only cleanup failure. Assertions are evaluated outside intentionally swallowed callbacks. All 57 packaging tests passed, including five CLI acceptance-path forms and retained symlink/outside-root/unverified-release refusal. A clean reference build, exact bilingual resource audit, 15 resource-audit self-tests and public API audit (837 sites, 195 members, 46 types) passed. No native DSP call, Harmony detour or game acceptance case was executed.

## Native Begin completion and frozen evidence follow-up

The [session-completion and evidence review](compatibility/session-completion-evidence-review.md) records the canceled-original fix, pending/current-identity persistence guard, nested-Begin completion token, supported legacy save-prefix signatures and final frozen evidence inventory gate. The actual HarmonyX 2.7.0 pipeline passed **16 cases / 264 assertions** on isolated .NET 6.0.36 using harmless managed methods and production Core helpers. It found and regressed the unsupported `__args` injection missed by reference compilation. A metadata-only check tied the six production Begin/save adapter signatures and helper call sites to the final runtime DLL. This is not DSP or disk-save execution.

Final aggregate results: **76 pure tests / 17,471 assertions**, **57 packaging tests**, **15 resource-audit self-tests**, clean reference compile with zero warnings/errors, exact main-assembly localization and public API audit (**838 sites / 195 members / 46 types**, zero nonpublic/unresolved). The prior three-fix test totals above describe that earlier pass. All 35 actual game scenarios remain unexecuted, the checked-in release acceptance is still refused, and generalized P4 refunds remain disabled.

## Checkout bytes and compiled-source inventory follow-up

The [build-input review](compatibility/build-input-review.md) records independent reproductions and fixes for clean CRLF checkout hash failures and stale source-to-DLL recording. Final local checks passed: **76 pure tests / 17,471 assertions**, **16 real-Harmony cases / 264 assertions**, **64 packaging tests**, **3 fresh-checkout cases**, **30 genuine Core-build assertions**, and **15 resource-audit self-tests**. The Core fixture covers preserved timestamps, compiler failures, files added before capture/after compilation, explicit compiler skipping, successful recovery and inventory membership changes.

A separate fresh plugin/Core reference build and both real metadata audits confirmed that adding an uncompiled default C# source refuses recording, tested-build inspection and experimental packaging while leaving the prior report/captures intact; retaining a repaired source and rebuilding restores qualification. The final main reference compile passed with zero warnings/errors, exact bilingual resources and **838 direct API sites / 195 members / 46 types**, zero nonpublic/unresolved accesses. The source/package tools and new scripts passed Python syntax and whitespace checks. Local checkout tests ran on Linux; native Windows checkout/Core results are separate CI evidence. All 35 actual game scenarios remain unexecuted.
