# Abnormality detection and in-game Metadata: source assessment

Date: 2026-10-05. Code inspected: `71fb365936090e8d5cb0cd2db4807356ed16ca2d`.

**Verdict: not yet qualified for abnormality, achievement, Metadata or online
eligibility. No trigger was reproduced, and no no-trigger result was established.**
The two safety fixes and passing CI do not change this verdict. No detector or
eligibility bypass is implemented or recommended by this assessment.

## What is established from this mod's source

The production source and actual registration/progression/reconciliation paths
were inspected, not just the presence of a mod loader. No direct writes to native
abnormality flags, detector registries, game Metadata balances/consumption records,
platform achievements or Milky Way upload state were found. There are no patches
that disable the game's abnormality checks or erase a previous abnormality.
This conclusion covers DarkFogSynthesis's inspected source; it is not a blanket
claim about every dependency, peer, native side effect or future game version.

| Area | Inspected behavior | What remains unknown |
|---|---|---|
| New content | `FrozenContent` and `ContentRegistry` register two technologies and six recipes using native prototypes. | Whether the installed detector accepts the added IDs and their production/research histories. |
| Research costs | New research uses ordinary blue/red/yellow matrices and the frozen native cost/Hash fields. It is not implemented as a Metadata buyout. | Actual native consumption and detector accounting after partial progress, completion and reload. |
| Dark Fog Matrix | The product is the existing Dark Fog item 5201. The native scientific matrix list remains 6001 through 6006. | Any native assumptions about acquisition of Dark Fog drops through the new recipes. This does not add a seventh scientific-matrix Metadata type. |
| Hidden prerequisites | `RuntimeProgression` adds/restores the four owned prerequisite edges, without gifting research progress or removing native discovery requirements. | A pre-existing completed hidden technology whose newly required combat parent is not yet unlocked deserves explicit testing. No specific native rejection rule is asserted here. |
| Old-save reconciliation | `SaveReconciler` calls `UnlockRecipe` only for missing mod recipes whose supporting technology is already completed; it does not complete technologies. | How that new recipe availability interacts with the installed native checks at the next event/save. |
| Metadata | No direct native property-currency credit, debit, buyout, reset or used-Metadata-flag write was found. | Native production-derived changes, user-initiated buyouts, and consequences of any later abnormality flag. No direct spending is not proof of continued earning/eligibility. |
| Removal | Separate cleanup candidates remove owned references, not abnormality records. | Native reload and integrity after cleanup. Removing the plugin is not evidence that an already-flagged save becomes clean. |

Relevant source: `src/DarkFogSynthesis.Core/Definitions/FrozenContent.cs`,
`Definitions/VanillaIds.cs`, `src/DarkFogSynthesis/Registration/ContentRegistry.cs`,
`Progression/RuntimeProgression.cs`, `Compatibility/SaveReconciler.cs`,
`Compatibility/NativeRecipeCompatibility.cs`, and `Compatibility/SafeRemovalService.cs`.

## The two native systems are different

The developer's September 29, 2021 abnormal-data announcement associates detected
abnormal items/technology/game values with achievement and Milky Way restrictions.
It does not establish today's complete detector rules or a Metadata-specific
consequence. [1]

The developer's April 21, 2022 FAQ describes Metadata as matrix-production-derived
currency for cross-cluster technology buyouts. Using it can affect specified
achievements without itself being an abnormality. The FAQ separately identifies
`Documents/Dyson Sphere Program/Property` for its data. These are historical
mechanism/storage references, not a current mod-compatibility whitelist. [2]

The repository's .NET assembly-metadata/PublicApiAudit reports are unrelated to
that in-game currency. Public API accessibility and compilation do not establish
native abnormality acceptance. The reference build used GameLibs
`0.10.35.29088-r.0`; equivalence to the user's installed game was not established.

## Event surfaces and why comparison mods are not proof

The inspected CheatEnabler source identifies native notifications for game start,
assembler recipe selection, handcraft completion, technology unlock, console use
and before-save processing. That provides evidence of event surfaces, NOT the
implementation or thresholds of every determinator. [3]

GenesisBook explicitly patches abnormality initialization/ticking and separately
suppresses platform/online reporting. Its ability to add research and recipes is
therefore not evidence that the same changes pass untouched native detection. [4]
DarkFogSynthesis does not adopt those patches. Absence of an immediate warning at
startup is insufficient: exercise all relevant events and a save/reload round trip.
No installed native detector bodies or native game execution were available in
this assessment; the actual trigger/no-trigger question remains unresolved.

## Qualification protocol: existing I01, I02 and I03

Do not mark these cases passed from this document or CI. Keep all existing
acceptance statuses unchanged until actual evidence is recorded.

Before running, close the game and back up the full game user-data directory,
including both Saves and Property. Do not assume that a copied .dsv or separate
mod-manager profile isolates account-wide property data. Keep the backup outside
the test installation and identify the actual user-data path on that machine.
Do not use a save belonging to another player as a clean control.

Use separate launches and matching copied baselines:

- A: vanilla only; establish its existing integrity and property/achievement state.
- B: only BepInEx, LDBTool and CommonAPI; distinguish dependency effects from ours.
- C: B plus the exact tested DarkFogSynthesis binaries/configuration. Test both
  fresh research and old saves with already-completed prerequisite/hidden techs.
- D: a separately produced cleanup candidate, loaded under vanilla after its
  normal copied-save safety checks. This is I02, not automatic certification.

For C, inspect native warnings/flags, achievement eligibility, and Metadata
balances/contribution/consumption records at each boundary: game start; partial
and completed new research; each handcraft route; industrial recipe selection
and production; both proliferation modes; hidden-item acquisition and technology
unlock; and save/reload. Include single and stacked labs and both progression-mode
policies. Record ordinary matrix production separately, so legitimate Metadata
contribution changes are not mistaken for a direct currency edit by this mod.
Use no console gifts, research cheats or abnormality-disabling plugins in these
qualification runs. Any intentional Metadata buyout is a separate controlled
case, not part of the no-spending baseline.

Retain the exact game/Unity/dependency versions and hashes, initial status,
event order, first changed warning/flag, report/log/screenshots, and post-reload
status. If C diverges from B, preserve that evidence and stop using that candidate
for important achievement/Metadata progress; do not clear the native flag.

I03 online submission requires separate explicit authorization. This work did not
upload a score or Milky Way record. Offline/local checks must not be reported as
successful server-side eligibility. Account sanctions, if any, are also not
established by the evidence reviewed here.

## Sources and provenance

[1] Developer patch notes, 0.8.22.8915, September 29, 2021. Text inspected through
the SteamDB mirror of the developer announcement:
https://steamdb.info/patchnotes/7445180/
Upstream announcement (linked by the mirror):
https://steamcommunity.com/games/1366540/announcements/detail/2909879326025819169

[2] Developer patch notes and Metadata FAQ, 0.9.25.11985, April 21, 2022. Full text
inspected in the official Steam announcement feed:
https://store.steampowered.com/news/posts/?enddate=1650551701&feed=steam_community_announcements
Canonical announcement:
https://steamcommunity.com/games/1366540/announcements/detail/3225149691053638629

[3] Author source, pinned revision (read as evidence, not adopted):
https://github.com/soarqin/DSP_Mods/blob/9fc2723bcaa3b4b0a13e47477f70f2bf697edf98/CheatEnabler/Patches/GamePatch.cs

[4] Author source, pinned revision (read as evidence, not adopted):
https://github.com/Awbugl/ProjectGenesis/blob/8d6b8abb786323ca0e362f9132da230b968227a9/src/Patches/Hooks/AbnormalityLogicPatches.cs

Result: source audit and native-system research completed; native game tests
not executed, game acceptance remains 0/35, and release approval remains false.
