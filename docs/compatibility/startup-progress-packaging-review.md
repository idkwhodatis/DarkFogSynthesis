# Startup, progression and package snapshot follow-up

Review baseline: `e15161c9b0d3a6983cf1963ef94875536aefec0e`, 2026-10-04. These four source findings were reproduced or verified without executing DSP. The frozen gameplay definition set is unchanged.

## Startup protection precedes optional initialization

Previously, configuration and localization could fail before Harmony installed any of the session/save guards. A fatal status alone could not protect those unpatched entrypoints.

A small independent startup-safety owner now installs and verifies critical load, session, resume and save prefixes before configuration, localization, gameplay patches or content callback subscriptions. The exact prefix method, Harmony owner and early priority are checked. The gate starts closed and opens only after initialization and prototype binding succeed. Fatal callback failures close it permanently for the process; neither a later callback nor a new session can clear a startup failure. Optional diagnostics remain warning-only.

The actual production prefix decisions are exercised by source-side tests under localization failure. Installation tests also cover partial failures: remaining independent barriers are attempted, installed barriers are retained, and content initialization never starts without complete verified coverage.

There is an unavoidable boundary: if Harmony cannot install a critical native hook, that missing hook cannot honestly be claimed to protect its target. The plugin reports incomplete coverage, retains successful guards, and refuses initialization. The user must quit manually and correct the installation; there is no automatic process termination or save attempt. Actual native detour behavior and shutdown order still require target-game testing.

## Late validation checks relationships, not just parent availability

A published combat technology does not prove that its required edge still exists. The final mode-aware validator now observes the live prerequisite IDs and the corresponding current-LDB cache references. Removing an explicit required edge, its forward cache reference or its reverse cache reference is a conflict even when every technology remains published. Same-ID stale objects do not count as current references.

The two synthesis technologies must remain available, non-obsolete and connected to their frozen prerequisites. The hidden technologies' original industrial prerequisites remain required. Additional valid foreign prerequisites are preserved; validation does not overwrite, reorder or repair another mod's arrays, publish technology, unlock research or grant discovery.

Implicit prerequisites are handled according to the observed supported cache shape. CommonAPI's explicit reverse-link behavior is not generalized into an invented implicit reverse-link requirement. If binding observed a combined forward cache, that required implicit reference cannot silently disappear later. The extra combat edges are required in Peace mode, or when the sole configuration option also enables them in non-Peace mode; ordinary non-Peace/default behavior is retained.

## Runtime distribution bytes belong to the tested build

DLL-only snapshot checks did not cover external runtime PNGs and package metadata. Schema-v3 build/acceptance binding includes distribution-critical asset and manifest hashes. The package validates frozen asset bytes and compares the bytes actually written to the candidate identity. A file changed between earlier validation and snapshot capture is refused instead of being shipped under older evidence.

Targeted fixtures mutate PNG and manifest contents in that window. They are isolated packaging probes, not game behavior tests. The checked-in acceptance report remains unverified and cannot authorize a release.

## Output creation cannot overwrite a race winner

A preliminary existence check followed by overwrite-mode ZIP creation was racy. Packaging now creates and verifies a private staged archive, then publishes it through an exclusive no-overwrite operation. A competing target created in the gap is preserved. Failed private staging is cleaned up without treating somebody else's target as an owned partial file.

Collision and failure-path regressions exercise this boundary deterministically. Repeated packaging of identical stable input remains deterministic, and existing ZIPs still require a fresh output location rather than silent replacement.

## Evidence boundary

Final aggregate checks are recorded in [source verification](../source-verification.md) and the [compile baseline](compile-baseline.json). Pure tests, metadata audits and reference compilation do not establish actual Harmony coverage, hidden discovery, Unity serializer availability, lab behavior, refund conservation, vanilla reload or integrity eligibility. All 35 target-game acceptance cases remain unexecuted, and generalized P4 refunds remain disabled.
