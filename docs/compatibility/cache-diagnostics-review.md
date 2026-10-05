# Prerequisite-cache and diagnostic review fixes

Baseline: `cb24eed2147b107b50fc654f7c596891a3860194`. These changes do not alter
recipe IDs, quantities (including Titanium Alloy x2), research costs, discovery
rules, save formats, release eligibility or cleanup/refund policy.

## 1. Preserve prerequisite-cache representation across owned edits

`RuntimeProgression` retains each edited technology's observed explicit-only or
combined cache representation for the lifetime of its owned forward edge. The
immutable production `PrerequisiteCacheModes` snapshot survives repeated
application with no list edit; the next snapshot is prepared before mutation and
published only after successful edits. Retained contracts are validated even on
a no-op application, and incompatible rewrites are refused rather than repaired.
Restoration uses the current surviving explicit/implicit IDs, never a wholesale
old array, then releases the corresponding representation ownership.

The Core regression combines the actual forward planner, cache representation
policy and ownership snapshots. It exercises ten apply/repeat/restore cycles for
combined and explicit-only caches, including a combat prerequisite that already
exists implicitly and later foreign explicit/implicit additions. Invalid caches,
missing ownership evidence and input-array mutation are checked separately. These
are production pure-helper tests, not execution of DSP's native graph consumers.

## 2. Validate newly registered technology classification and page

The registration comparator independently requires both added technologies to
have `isLabTech=true`. `false` is drift; `null` is an unavailable observation and
refuses the comparison (CLI exit 2), not a pass. Pages must be integers and must
match the pre-registration page observed on the two known vanilla prerequisite
anchors (1826 and 1808). Disagreeing anchors refuse qualification. No guessed
numeric page constant or value from the new technology is used as its own oracle.
Tests cover both technologies, invalid types, alternate consistent native page
labels and actual CLI exit codes for correct, incorrect and unavailable fields.

## 3. Capture native padded mask regions

The Unity-only `RectMaskClipCapture` delegates to the installed uGUI
`Clipping.FindCullAndClipWorldRect` for each active RectMask2D. Its padded rectangle
is in root-canvas coordinates; the adapter transforms its corners through that
root canvas and the relevant camera before producing screen-pixel bounds. Nested
masks remain independent constraints. Empty/nonfinite projections refuse capture.
Stencil-mask rectangles remain bounds approximations, not pixel-shape guarantees.

The nested `uiLayout` schema is now **2**, with explicit
`clipSemantics=native-padded-rectmask2d`. The outer report remains schema 3. Older
UI-schema-1 captures must be recaptured: the checker cannot recover padding absent
from old exports. Python tests exercise effective-region clipping, nested masks,
scaled fixtures and refusal of legacy or unqualified observations.

Optional real Unity EditMode fixtures compile the exact production adapter; see
[`tests/DarkFogSynthesis.Unity.Tests`](../../tests/DarkFogSynthesis.Unity.Tests/README.md).
They require a licensed Unity editor and are **not** run by ordinary CI. They must
not be counted as passing merely because the helper or plugin compiles.

Unity's native clipping operation is the source of truth, rather than a copied
local-space/pixel-space padding implementation:
https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/Culling/Clipping.cs

## Verification boundary

Core/Harmony tests, Python checks and reference compilation do not execute DSP.
No actual save, UI measurement, native discovery/production or uninstall test is
claimed here. Historical compile/adaptation reports remain tied to their original
builds; the 35 target-game acceptance entries are unchanged.
