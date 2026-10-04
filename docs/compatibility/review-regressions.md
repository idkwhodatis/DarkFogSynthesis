# Review findings and regression boundaries

Review baseline: `e4b9c7df90096fdc1a8f14239ea0021086aae061`, 2026-10-04. The six reported defects were checked against source. None requires changing the frozen recipes, technology costs, IDs, native discovery fields or integrity flags.

## 1. LabOpt bypasses the native production postfix

Confirmed against pinned upstream source. Both factory lab-production overloads use a replacement helper, so normalizing only the native `LabComponent.InternalUpdateAssemble` return cannot cover it. The peer guard rejects the exact BepInEx identity and the actual declaring type of its Harmony patches, including a patch left behind without the registry entry. All versions remain unqualified; no guessed Harmony owner or untested adapter is used. Missing/uninspectable call-site metadata fails closed. See [source proof and buffer aliases](labopt-buffer-review.md).

## 2. Optional export I/O invalidates valid registration

The automatic export now runs after the fatal registration boundary. A throwing diagnostic writes a warning without marking registered content failed. Regression tests exercise successful registration with export exceptions, genuine registration failure, ordering and preservation of state. Manual export failure likewise does not clear a compatibility block. This is not a claim that the installed Unity runtime resolves every serializer dependency.

## 3. Pausing does not prevent resuming a conflict

A distinct compatibility latch now blocks readiness, resume and save entrypoints, and its reason remains visible even when another diagnostic action changes the status message. Teardown, a failed replacement, repeated Begin and re-entering the blocked object do not clear it. Only a different session that completes native Begin, postfix work and late compatibility checks can release it. Registration remains available for validating that replacement. The guard does not publish, unlock or re-enable another mod's technology.

Pure tests use reference-distinct session objects, including objects with equal values, to exercise the lifecycle. They do not prove actual Harmony/native load order; the runtime integration remains part of copied-save acceptance.

## 4. Defensive copies erase shared-buffer identity

The read-only refund preflight checks all original mutable machine arrays before making any defensive snapshot. It rejects positive-length reference aliases involving a refund target, regardless of current values. Independent identical arrays are accepted; shared zero-length singleton arrays have no refundable contents and are ignored. Cross-field, cross-machine and owned/unowned sharing are covered. No generalized refund operation is enabled by this diagnostic.

## 5. Source-only acceptance can approve a different binary build

Release evidence now has to match the candidate build's exact plugin/Core hashes, resolved reference assembly identities and hashes, source fingerprint, configuration/reference mode and content-addressed build identity. Compiler-input sidecars come from MSBuild's actual resolved references, not guessed HintPaths. Missing, altered or stale evidence is refused. Local override files remain out of distributed source packages, while their effective build inputs are bound to the tested build.

The checked-in acceptance record remains unverified and cannot authorize a release. A schema upgrade is not itself acceptance, and the reference-assembly smoke build remains ineligible.

## 6. A symlinked allowlist root escapes source packaging

Package/build inputs now reject symlinked roots, ancestors and files before reading bytes, and project inputs must resolve beneath the project root. Runtime inputs receive equivalent checks. Temporary-fixture tests cover symlinked source roots, directory ancestors, files and runtime paths, along with ordinary allowed files. No external symlink target needs to be read to reject it.

## What this does not establish

The actual game's crafted-item acquisition and hidden-technology discovery, Unity resolution of `System.Web.Extensions`, lab production/stacking/proliferation, and cleanup followed by vanilla reload remain unexecuted. No discovery grants, serializer replacement or integrity bypass was added to conceal those gaps. General P4 refund/cancellation still needs native receiver/cancellation side-effect and conservation evidence; restoring copied buffers after native credits without that evidence could duplicate or lose inventory.

The aggregate pure tests, package guards, clean reference compilation and metadata audits are recorded in [source verification](../source-verification.md). The full game matrix stays at [acceptance](../acceptance.md).
