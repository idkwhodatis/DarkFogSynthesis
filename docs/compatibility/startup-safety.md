# Startup failure containment

This is a source/pure-code/compile-reference review, **not target-game acceptance**. No game was launched, native load/save was executed, or Harmony detour was exercised by these checks. The frozen technology/recipe definitions and IDs are unchanged.

## Why ordering matters

The previous `Awake` bound configuration outside its catch and registered localization before `Harmony.PatchAll`. A configuration or localization exception could therefore leave every load/session/save guard absent. Setting the plugin's `Ready` property to false did not stop native entry points. The ordinary save/resume guards also consulted only the session compatibility latch, which did not contain fatal initialization errors.

The reviewed dependency sources establish why subscribing content callbacks is a consequential step:

- [LDBTool `VFPreload_Patch`](https://github.com/xiaoye97/DSP_LDBTool/blob/6b84a5539d4f4bb06d0e840cc8203655e313da4e/LDBTool/Patches/VFPreload_Patch.cs#L10-L23) invokes `PreAddDataAction` before adding the queued prototypes. Registration can enqueue content and must not be subscribed before safety installation succeeds
- [CommonAPI `ProtoRegistry`](https://github.com/limoka/CommonAPI/blob/4a5d7c21c0e2a5f91b639e4d710fa5bb5dcb928a/CommonAPI/Systems/ProtoRegistrySystem/ProtoRegistry.cs#L180-L222) invokes `onLoadingFinished` after native cache initialization inside a catch that logs errors. A thrown binding error alone cannot be assumed to stop subsequent game activity; the independent failure latch must remain closed
- [HarmonyX 2.7.0 patch metadata](https://github.com/BepInEx/HarmonyX/blob/v2.7.0/Harmony/Public/PatchProcessor.cs) exposes registered patch ownership and prefix methods. The installer checks this metadata instead of treating a nonthrowing `Patch` call as coverage evidence

These pinned source snapshots inform sequencing. They do not prove the target game's lifecycle, installed dependency combination, or interactions with other native patches.

## Runtime sequence

1. Set the plugin instance, then enter the pure, fail-closed startup state
2. Install ten independent critical prefixes under `idkwhodatis.darkfogsynthesis.startup-safety`. Attempt the remaining barriers after any individual failure, then verify every exact target/prefix/owner/priority before continuing
3. Only after verified coverage, construct runtime content/progression services, bind configuration, register localization and install ordinary gameplay/session patches
4. Reverify critical coverage, then subscribe content callbacks last
5. The registration and binding callbacks each recheck completed initialization and critical coverage before any content work. Only successful binding opens the process gate
6. Existing import/session checks still perform prototype, execution-cache, native matrix, peer and progression validation. The minimal gate does not replace them. A failed ordinary save/resume guard now also checks fatal/incomplete startup

Critical prefixes use maximum Harmony priority and an explicit ordering constraint before the ordinary DarkFogSynthesis owner. Existing session prefixes remain at `Priority.First`; known nested import sanitizers are downstream of the guarded outer `GameData.Import`. Unknown competing patches and actual detour behavior still require game verification.

## Exact critical coverage

| Native entry point | Startup decision before completed binding or after a fatal error |
| --- | --- |
| `GameSave.LoadCurrentGame(string)` | Skip original; return `false` |
| `GameSave.LoadCurrentGameInResource(int)` | Skip original; return `false` |
| `GameData.Import(BinaryReader)` | Throw before native import |
| `GameData.NewGame(GameDesc)` | Skip original; return `false` |
| `GameMain.Begin()` | Throw before native session entry |
| `GameMain.Resume()` | Skip original |
| `GameSave.SaveCurrentGame(string)` | Skip original; return `false` |
| `GameSave.AutoSave()` | Skip original; return `false` |
| `GameSave.AutoSaveAfterErrored()` | Skip original; return `false` |
| `GameSave.SaveAsLastExit()` | Skip original; return `false` |

Each target is resolved with an exact parameter list and checked for declaring type, return type and instance/static shape. An independent Mono.Cecil metadata inspection confirmed all ten signatures, original public member visibility and original public declaring-type visibility, honoring `BepInEx.AssemblyPublicizer.OriginalAttributesAttribute`, against the compile-only reference:

- `Assembly-CSharp.dll`, GameLibs `0.10.35.29088-r.0`
- SHA-256 `3c6d3211a948a528af1d4f813bffa5cc2d3bdddfacbc1a49dd7a29bb3b9deec9`

This establishes the reflected targets' reference metadata only. Publicized/stripped reference bodies are not runtime evidence.

## Fatal errors and incomplete coverage

The process gate is closed by default, and remains closed after configuration, localization, ordinary patch installation, callback setup, registration, binding or critical-coverage verification failures. A stale callback or a second initialization attempt cannot erase a recorded fatal error. Diagnostic rendering tolerates a configuration entry or content registry that was never created.

Critical installation does not roll back successful safety prefixes. An incomplete-coverage error identifies the failed/unverified entry points, prevents the optional initializer and content subscription, and instructs the user not to load, resume or save. Existing running gameplay is paused only as a best-effort additional measure. There is no automatic quit, process kill, save-on-error, settings change or attempt to repair other mods.

**Unavoidable limit:** if Harmony cannot install a native hook, a boolean state flag cannot protect that unhooked entry point. Retaining other independent barriers reduces exposure but does not establish universal protection. Failures before this plugin's `Awake` can run are likewise outside these hooks. The visible fatal warning requires the user to quit manually and fix the installation; loading or saving is not certified safe.

On plugin destruction/disable, the gate is permanently closed before teardown. Ordinary gameplay patches may be removed, but the separate startup-safety owner is deliberately not unpatched. Restart is required. This conservative behavior does not certify native shutdown or save behavior.

## Executed tests and remaining boundaries

`StartupGuardTests.Run` contains 118 assertions. Each case loads a fresh copy of the real Core assembly to isolate its process-lifetime static state, then invokes the **exact static methods installed as production prefixes**. Tests assert thrown session-entry failures, skipped originals, false load/save results and denied resume, not merely a `Ready == false` value.

Cases cover pending startup, the successful path waiting for content, configuration/localization failure, partial ordinary patching, callback setup failure, partial critical installation with later survivors attempted and retained, missing ownership despite a nonthrowing installation, late binding failure, disabled plugin, lost coverage and rejected stale callbacks/reinitialization. The shared install-and-verify algorithm is exercised with harmless callbacks. It does not execute Harmony or pretend those callbacks are game types.

An isolated `net472` compile of the full runtime and Core source against the declared references passed with zero warnings/errors. Aggregate repository tests and provenance/package audits remain separate checks. Actual Harmony installation/ordering, native import failure handling, pause/resume, autosave/last-exit behavior, callback timing and the complete save lifecycle still need isolated copied-save target-game acceptance.
