# Checkout bytes and compiled-source inventory review

Review baseline: `6b769280d1d4c4e9eb857cb84efabea95f7fc0cc`. These are build-tooling
findings, not target-game failures. The frozen recipes and runtime behavior are
unchanged by this follow-up.

## 1. Clean CRLF checkouts failed the asset contract

A fresh checkout with `core.autocrlf=true` converted all three source SVGs from
LF to CRLF while `git status --porcelain` remained empty. The actual validator
is `scripts/generate-assets.py --check`: it hashes raw source bytes and correctly
refused their mismatch with the committed asset manifest. Both build-wrapper
modes and source-only packaging encountered that refusal. A fresh
`core.autocrlf=false` control passed.

The root `.gitattributes` now pins `/assets/source/*.svg` to `text eol=lf`.
The source package carries this file. The validator still checks raw bytes;
neither manifest hashes nor original art were regenerated to conceal a content
change. The policy applies on checkout, without changing global Git settings.

`python scripts/test-checkout-assets.py` creates isolated repositories from the
actual committed-policy/asset fixtures, including when run from an extracted
source archive. It checks genuinely fresh `false`, `input` and `true` checkouts,
Git cleanliness, exact SVG bytes/hashes and rejection of a real SVG content
mutation. An unprotected text control must become CRLF in the `true` case,
proving that checkout conversion was exercised. Git and Pillow are needed only
for this explicit regression command. CI runs it on Linux and Windows; a local
Linux run alone is not native Windows evidence.

## 2. Re-recording could misassociate new source with old DLLs

The independent reproduction used a fresh, isolated build of the actual Core
and plugin against the documented compile-only references, followed by both
genuine metadata audits. A new default-included Core `.cs` file containing
`#error` was then added without recompiling. The existing report was correctly
rejected, but `--record-build` produced a new source fingerprint/build identity
around the unchanged DLLs, compiler captures/invocations and references.
`--tested-build` and experimental packaging subsequently succeeded. A real
rebuild then failed with CS1029 at the new file, confirming it belonged to the
compiler's default input set.

The old validation checked every previously captured path's existence and
hash, but did not check whether the input inventory had grown. Creating a fresh
fingerprint at report time therefore was not proof those sources were compiled.
This was a provenance gap. It did not establish that the delivered baseline was
stale: its 14 plugin and 25 Core production C# files were all present in its
captures, and its existing candidate validation passed. Old release acceptance
does not automatically qualify a new build identity; the checked-in acceptance
remains unexecuted and ineligible.

Local compiler captures now include a compilation-time production inventory,
and recording/packaging must match that inventory rather than manufacture a new
source association. Additions, changes, deletions and renames require another
successful build. The inventory is independently checked at successful build
completion, before a completed capture may be recorded. Old captures require a
rebuild. Existing exact dependency hashes, generated compiler input hashes,
configuration, SDK and output-DLL checks remain necessary.

Additional real-build probes found two adjacent cases during review: an existing
source changed to `#error` with its old timestamp restored could skip incremental
compilation, and a target could create a `.cs` file after MSBuild evaluated
default source items but before capture. Both are refused now. Each capture
forces a genuine `CoreCompile` invocation using the SDK-supported custom-output
contract; evaluation-time membership must also match compiler-entry membership.
Source additions after compilation fail the completion check. Failed,
design-time or explicitly skipped compilation cannot receive a completed capture.

`python scripts/test-build-capture.py` builds the real Core project in an
isolated copy and uses the production capture validator. It checks refusal of
an uncompiled default-source addition while both the DLL and capture remain
unchanged, refusal after an actual compiler error, and recovery only after a
successful rebuild that contains the repaired type in PE metadata. Added
resource/import members and renamed/deleted sources are also checked. It does
not create game-shaped substitutes or execute the plugin. CI runs this real
Core-build check on Linux and Windows.

The full plugin/Core reference-build reproduction was also repeated separately:
adding an uncompilable default Core source refused recording, tested-build
inspection and experimental packaging while preserving the prior report and
captures. Keeping a repaired source and actually rebuilding both projects,
then rerunning both metadata audits, restored qualification and produced the new
Core type in PE metadata. This remained a compile-only reference experiment.

Focused packaging tests additionally exercise the complete recording path and
ensure refused recording preserves the prior report. The final source-side
counts and exact reference build identity are in
[`compile-baseline.json`](compile-baseline.json) and
[`source-verification.md`](../source-verification.md).

## Boundary

All game acceptance cases remain unexecuted. These tests establish checkout,
compiler-capture and package-consistency behavior, not Unity loading, native
production/discovery, saves, safe removal or integrity eligibility. General P4
refund execution is still disabled. No game install, save mutation or public
package release was performed.
