# Removal-history and tooling review fixes

Baseline: `8b43a454e41bb17b6101b6381fbea8770e145f37`.
Recipes (including Titanium Alloy x2), stable IDs, research costs, discovery rules,
save formats and the existing game acceptance statuses are unchanged.

## Generated Unity project

The assembly definition now references `UnityEngine.UI`; the UPM dependency remains
`com.unity.ugui`. These are different identifiers. `test-unity-ui-project.py`
executes the generator and checks the actual asmdef, package manifest, unchanged
source bytes, no-overwrite behavior and outside-checkout destination requirement.
It also runs the CLI from another working directory. The suite runs on both Windows
and Linux in ordinary CI and in Build.ps1. It does not execute the Unity editor.

Native assembly-definition naming is documented by Unity:
https://docs.unity3d.com/2022.3/Documentation/Manual/AssemblyDefinitionFileFormat.html

## Preserve unrelated history across candidate-save callbacks

After the durable backup and second preflight, cleanup captures the expected
remaining recipe unlock set, complete technology-state dictionary, ordered research
queue and current technology. `RemovalHistoryGuard.EnsurePreserved` is a shared
read-only check; it allows only set ordering and unused zero queue slots to differ.
`VerifyAcrossSave` invokes that same validation before and after the candidate's
native save, and the runtime checks again before emitting the preservation report.
Identity, pause and custom-reference checks remain separate and still run.

A callback-induced deletion, addition or value change refuses success. The existing
catch path rolls back only this mod's edits, retains quarantine and names the backup
for recovery; it never rewrites unrelated peer state to force preservation. The
failure message now describes that limited rollback explicitly. A file that may
already have been written is not declared accepted or overwritten on failure.

Core tests execute the production comparison and save-boundary helpers. Harmony
fixtures execute the same helpers around an actual detoured harmless save method,
with a callback changing history contents without replacing session identities.
They check rejection, no success report, balanced write counters, expired permits,
retained quarantine and an unchanged-history control. These are NOT DSP disk-save
or actual cleanup tests; copied-save reload remains required to verify on-disk data.

## Explain the owned empty reverse-cache round trip

The restoration comparator permits only a baseline `postCache=null` that acquired
exactly this mod's child and was restored to `[]` while the mode policy was active.
The baseline must not contain an explicit or implicit edge still requiring that
child. It records the difference in `normalizations` instead of hiding it.
Mode/equality checks, other fields and unrelated caches are not normalized.
Missing surviving children, missing applied links and unavailable classifications
remain differences/refusals. Caller snapshots are never modified.

Run these read-only tooling checks:

```sh
python scripts/test-unity-ui-project.py
python scripts/test-runtime-snapshots.py
python scripts/test-ui-layout.py
```

Source and reference compilation are not runtime acceptance. Unity/DSP execution,
real research/production, inventory-preserving cleanup and actual save reload were
not established by this patch; no release approval is granted by a green CI run.
