# LabOpt production bypass and native-buffer ownership review

Inspection date: 2026-10-04. This is a source review, pure-test result and compile-only check. No game or LabOpt plugin was executed, and no upstream LabOpt source build was run; no game compatibility or P0 acceptance is claimed.

## Pinned primary evidence

All LabOpt links below use the actual upstream `soarqin/DSP_Mods` tree at commit `9fc2723bcaa3b4b0a13e47477f70f2bf697edf98`, read directly through the GitHub connector. The existing local `Functions.cs` excerpt was not treated as sufficient evidence for the plugin identity or call-site patches. No LabOpt implementation was copied into DarkFogSynthesis.

- [LabOpt.csproj, lines 4–7](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/LabOpt/LabOpt.csproj#L4-L7): assembly `LabOpt`, BepInEx GUID `org.soardev.labopt`, version `0.3.6`. Source blob `3c9a5d7e281c0a43e6b7bf966665033848afb2be`
- [LabOpt.cs, lines 8–19](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/LabOpt/LabOpt.cs#L8-L19): plugin attributes use generated PluginInfo; `Awake` calls `Harmony.CreateAndPatchAll(typeof(LabOptPatch))` without an explicit Harmony owner. Source blob `2f836070389dd5d063b5e55c57ec4c58cf3b62c5`
- [LabOpt.cs, lines 160–173](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/LabOpt/LabOpt.cs#L160-L173): a transpiler replaces the `LabComponent.InternalUpdateAssemble` call with `LabOptPatchFunctions.InternalUpdateAssembleNew` in **both** `FactorySystem.GameTickLabProduceMode(long,bool)` and `FactorySystem.GameTickLabProduceMode(long,bool,int,int,int)`
- [Functions.cs, lines 215–365](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/LabOpt/Functions.cs#L215-L365): the replacement has its own production body and returns a matrix-index calculation derived from the output item ID. Source blob `eb69cb5f248f94bd315212a3c54b34de26fd68c0`
- [Functions.cs, lines 86–141](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/LabOpt/Functions.cs#L86-L141): `AssignRootLabValues` aggregates child contents, then assigns child `produced`, `served`, `incServed`, `matrixServed` and `matrixIncServed` to root arrays; [lines 507–517](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/LabOpt/Functions.cs#L507-L517) also assign shared production buffers when configuring a stack
- [Upstream license](https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/LICENSE): MIT, copyright Soar Qin. This review independently implements a small detector and an ownership check; it does not transplant the replacement production body

For the compile baseline's HarmonyX 2.7.0, [CreateAndPatchAll source](https://github.com/BepInEx/HarmonyX/blob/v2.7.0/Harmony/Public/Harmony.cs#L361-L370) constructs a `harmony-auto-` owner with a generated GUID when no owner is supplied. The BepInEx GUID must therefore **not** be assumed to be its Harmony owner. [GetPatchInfo source](https://github.com/BepInEx/HarmonyX/blob/v2.7.0/Harmony/Public/PatchProcessor.cs#L223-L228) returns null for absent patch metadata and exposes prefixes, postfixes, transpilers, finalizers and IL manipulators otherwise.

## Confirmed source-level failure paths

1. The DarkFogSynthesis animation postfix on `LabComponent.InternalUpdateAssemble` cannot adapt a caller that has been rewritten to call another method. Both single-worker and multi-worker factory overloads are affected by the pinned LabOpt transpiler
2. An unchanged six-ID native matrix registry does not prevent that bypass. For output item 5201 and first native matrix ID 6001, the replacement's expression yields -799 before unsigned conversion, or 4294966497 in an unchecked 32-bit conversion. That lies outside the expected normal matrix visual-index range. The exact target-game consumers and visible consequences still require runtime validation
3. Copying shared live buffers into per-machine `RefundBufferSnapshot` values erases the alias. A root and child observing one five-item output array can become two separate five-item snapshots, and ordinary value aggregation would report ten items. Reference identity must be checked before copying; comparing element values would also wrongly reject two genuinely independent machines with equal quantities
4. The pinned replacement body still refers to older LabComponent fields. The project's current stripped compile reference uses `RecipeExecuteData`. This review does not establish that pinned LabOpt runs on that current reference target, or that another LabOpt version is compatible

## Implemented conservative boundary

`RuntimeCompatibilityGuard.ValidateSupportedPeers()` throws when `GetBlockingReason()` returns a reason. It is intended for lifecycle/session-validation boundaries, not the per-lab production hook.

- Read the BepInEx registry and match only the exact, ordinal `org.soardev.labopt` key/metadata GUID
- If that GUID is absent, inspect both exact production overloads with Harmony's read-only metadata API and match the actual declaring type `LabOpt.LabOptPatch`. Generated Harmony owner IDs, display names, suffixes and partial matches are not used as identity guesses
- Inspect all five supported HarmonyX patch categories. Known unrelated declaring types do not trigger the LabOpt diagnosis
- If the registry, overload lookup or patch identities cannot be inspected, return a separate inspection-failure blocker instead of claiming the inventory is clean
- **Every detected LabOpt version is blocked**. There is no allowlist, version-number exemption or implicit permission to continue. A future adapter needs target-game testing of both call paths, stack buffer ownership and save lifecycle before this rule can be relaxed
- The detector reads metadata only. It does not unpatch LabOpt, modify peer configuration, rewrite the global matrix registry, touch buffers, or alter saves
- A null result means no known LabOpt blocker was found, not universal certification of other mods or hot-loaded patches

`NativeBufferRefunds.Plan` now performs a complete original-array ownership preflight before constructing **any** defensive snapshot:

- Use one `RefundBufferAliasGuard` across every loaded factory and active assembler/lab
- Track `served`, `incServed`, `produced` and lab research buffers with a comparer using `ReferenceEquals` and `RuntimeHelpers.GetHashCode`
- Reject a positive-length shared buffer whenever either participant is included in the refund plan, including owned/foreign sharing, cross-factory sharing and cross-field sharing within one machine
- Ignore unrelated foreign-only sharing; if an owned machine later references that array, still reject
- Ignore null and zero-length arrays, including repeated `Array.Empty<int>()` singletons. Buffer-shape/content validation remains separate. A shared **positive-length zero-filled** array is conservatively unsupported because its ownership is still shared
- Do not register immutable recipe metadata arrays, which legitimately may be shared
- Never drain, reset, normalize or deduplicate a live buffer; ownership refusal occurs before a refund plan is returned

## Regression evidence and remaining qualification

New pure test classes:

- `LabRuntimeCompatibilityPolicyTests`: exact plugin GUID, exact patch type, unrelated patches, display-name/prefix/case near-misses, no version exemption, and missing/invalid inventories
- `RefundBufferAliasGuardTests`: independent equal arrays accepted; all five mutable field types rejected when shared; cross-field/cross-factory and owned/foreign aliases rejected in both orders; multiple foreign aliases then owned rejected; foreign-only sharing accepted; repeated empty singletons and null buffers accepted; positive-length zero-filled sharing rejected; buffers unchanged on refusal

An isolated .NET 8 runner passed **38 assertions** on 2026-10-04. An isolated `net472` compilation of the actual `RuntimeCompatibilityGuard.cs` and `NativeBufferRefunds.cs`, with the Core project and existing real stripped game/BepInEx/Harmony references, passed with **0 warnings and 0 errors**. The harness contained no game API substitutes, and did not invoke game or Harmony runtime patching. Integration checks are recorded by the main build coordinator separately.

Still not executed in the target game: real plugin-registry/patch metadata discovery; rejection before import/new-game progression; late-Begin pause/resume latching; both single-/multi-worker lab production paths; shared-buffer inventory behavior; load/save behavior with LabOpt; native refund execution. Safe native refunds and a LabOpt adapter remain unqualified and disabled.
