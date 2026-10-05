# Effective UI mask selection

Baseline: `25988c65d5bc8ef9aa36b6c7441ce0a887307eff`.
This patch is restricted to opt-in UI observation, its checker, and tests.
Recipes, research, lab production/selection, save handling, multiplayer admission
and single-player behavior are unchanged. No target-game acceptance is granted.

## Correct mask inventory

`UiLayoutCapture.Record` delegates to the exact Unity-only `ScreenClips` helper
also copied into the optional EditMode project. The helper observes the active
`MaskableGraphic` on the recorded rectangle. It uses the installed uGUI's
`GetRectMaskForClippable`, then `GetRectMasksForClip`, rather than treating every
active transform ancestor as an applicable rectangular mask. An ordinary nested
canvas preserves outer masking; an `overrideSorting` boundary excludes masks above
it without discarding valid masks below it. Disabled masks, a mask on the graphic's
own object, and `maskable=false` follow their native selection rules.

The existing padded root-canvas-to-screen projection is unchanged. Excluded masks
are not projected, so an invalid padded region on an inapplicable outer mask
cannot cause a false capture failure. There is no mask-list cache, making live
sorting changes and reparenting visible to each explicit capture.

Stencil mask bounds follow `FindRootSortOverrideCanvas` and the same active-mask
predicate/stopping rule as `GetStencilDepth`. The native depth is checked. These
bounds remain approximations of stencil shapes and are not pixel/clickability
certification. No scene components, properties or rendering state are changed.

A layout-only rectangle without an active attached MaskableGraphic, or an ambiguous
graphic association, cannot establish effective masking. It refuses that diagnostic
record and produces an incomplete capture instead of inventing a proxy graphic,
choosing an arbitrary child, or claiming that no masks apply. This optional failure
never changes game/session readiness. Actual DSP panel association needs game testing.

## Capture compatibility

The outer runtime snapshot schema remains 3. The nested `uiLayout` schema becomes
**3** and requires `maskSelection = "native-graphic-sorting-boundaries"` alongside
`clipSemantics = "native-padded-rectmask2d"`. Old schema-1/2 observations cannot be
repaired from their rectangles; recapture them in game. Changing their version or
marker by hand does not create the missing native observation.

## Regression scope

The generated Unity project copies the production collector and fixture unchanged.
Its EditMode cases cover ordinary/override sorting, valid inner masks, non-unit
canvas scale with padding, disabled/self masks, unmaskable graphics, stencil
boundaries, a graphic on the sorting canvas itself, reparenting/toggling, and
unobservable rectangles. Rectangular cases also inspect actual CanvasRenderer
clipping state after native clipping. Existing padding/projection cases remain.

Python tests cover downstream effective-inventory observations, old capture refusal
and CLI exit behavior. They do not execute Unity mask selection. Reference compilation
of both the full plugin and the generated fixture checks C# and API signatures,
not editor execution, rendering or native masking behavior. Record a real Unity
XML/log before claiming those EditMode tests passed.

Source contract: Unity-Technologies/uGUI commit
`880f63719867187c41922c69ffaea83aca4211d2`,
`com.unity.ugui/Runtime/UGUI/UI/Core/MaskUtilities.cs`, and Unity's API documentation:
https://docs.unity.cn/Packages/com.unity.ugui%402.0/api/UnityEngine.UI.MaskUtilities.html
Runtime selection delegates to the installed implementation, not copied algorithm
assumptions about a newer uGUI version.
