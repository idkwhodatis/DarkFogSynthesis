# Pure Core tests

Run with a .NET 8 SDK:

```sh
dotnet run --project tests/DarkFogSynthesis.Core.Tests/DarkFogSynthesis.Core.Tests.csproj --configuration Release
```

The executable returns a nonzero exit code if any test fails. There is no third-party test runner dependency. The Core project targets `netstandard2.0`, compatible with the plugin's .NET Framework target; its standard framework reference may need NuGet access on the first restore. The test harness targets `net8.0` and contains **no substitute game types or game API stubs**.

## Coverage

- All six recipe input/output quantities, vanilla item IDs, fixed custom IDs, machines, 60-tick time units and ideal base rates
- Two independent technology branches, explicit/implicit prerequisites, total matrix costs, Hash and candidate CommonAPI rate encoding
- Immutable, strongly typed definitions and edit plans; complete four-row mode/configuration truth table
- Prerequisite ownership, peace → combat → peace transitions, ten repeat loads, preservation of preexisting/late-appended third-party edges, and atomic refusal when ownership becomes ambiguous
- Independent reverse-cache ownership: remove only links actually appended by this mod; preserve preexisting reverse links and links needed by surviving foreign forward edges
- Two hundred deterministic randomized graph-preservation cases
- All 4,096 completed-technology/unlocked-recipe combinations; recipe-only old-save repair; no automatic technology completion
- Every fixed ID collision rejected without reassignment
- Candidate-only layout status and caller-supplied measured-rectangle collision detection
- Checked-in ID manifest matches the executable constants; both localization files contain every required content key and the frozen technology names

These tests validate **pure definitions and plans**, not gameplay. The references to acceptance scenario IDs identify the related requirement, not a passed in-game acceptance scenario. Research consumption, station production/stacking, hidden discovery, native queue behavior, save lifecycle timing, inventory preservation, safe removal and Achievement/Metadata/Milky Way status still require target-game verification.

## Runtime application contract

`ProgressionPolicy.PlanSession(current, previousChanges, isPeaceMode, setting)` first plans restoration of previously owned changes, then the requested next-session state. `PlanRestore` removes only changes owned by this mod. Neither function mutates its input.

For a successful plan:

1. Check `CanApply` before writing anything
2. Compare **all** `Edits.Before` lists against the live graph immediately before committing, to reject stale plans
3. Apply all `Edits.After` arrays and rebuild target-version native caches as one transaction; roll back on failure
4. Adopt `plan.OwnedChanges` only after all writes and cache work succeed
5. Complete the transition before the next save's native research queue/UI can read shared technology prototypes

Never write `OwnedPrerequisiteChange.Baseline` back wholesale. It is evidence used to recognize the appended slot, not a replacement array. If another actor changes the prefix around an owned insertion, the planner returns conflicts and no edits. The runtime must block the affected transition and report the incompatibility instead of carrying stale peace-mode prerequisites into another save. Identical graph rewrites cannot establish another mod's semantic ownership; native integration must serialize graph mutation and treat unsupported concurrent behavior as unverified.

`SaveReconciler.PlanMissingRecipes` takes only **completed** technology IDs, and returns only this mod's missing recipe IDs. Execute those through a verified recipe-only unlock path. Do not use a full technology-completion API, replay rewards or touch discovery flags.

`TechLayoutResolver.Resolve` deliberately ships no verified target-version profiles. `IsVerified` is always false until actual layout evidence and a versioned profile are implemented. An empty measured-collision result is not a substitute for checking hidden nodes, connectors, preview panels, both languages and expanded UI states in the game.
