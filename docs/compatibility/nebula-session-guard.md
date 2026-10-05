# Active Nebula session guard (single-player remains supported)

Baseline: `df7ea277c62ea3cd0c00634cccc461c1050e3698`.
This is a compatibility refusal, NOT a multiplayer synchronization implementation.
No recipe, identifier, research cost, discovery rule, save format or acceptance status changes.

## Detection and single-player behavior

- No Nebula implementation plugin: allowed. This includes an API-only installation.
  The probe does not resolve optional types or load assemblies on that path.
- Nebula installed, `NebulaWorld.Multiplayer.IsActive == false`: allowed. Native
  single-player creation, loading, research, lab selection, Resume and save policy
  stay in place. Presence alone is not a BepInEx incompatibility.
- `IsActive == true`: unsupported session. This also covers host/client setup where
  a session exists before its game finishes loading; lobby/player count is not used.
- Installed peer with an unreadable/unsupported state getter: session qualification
  is refused as unknown, not declared active and not treated as a fatal registration
  failure. Retry a supported peer or use an isolated single-player profile.

The BepInEx dependency is **soft**, solely for load order. There is no compile-time
Nebula dependency, assembly load, invocation of networking APIs or patch to Nebula.
The adapter binds the public static bool getter in the already loaded `NebulaWorld`
assembly. Only successful getter resolution is cached; its live value is read for
all decisions. Missing type resolution and transient getter errors are retryable.

Source contract pinned to NebulaModTeam/nebula
`5ecd2b4168c8e4df72a301894d004aee5629961b`:
- `NebulaAPI/NebulaModAPI.cs`: implementation GUID `dsp.nebula-multiplayer` is
  distinct from `dsp.nebula-multiplayer-api` and `NebulaIsInstalled`.
- `NebulaWorld/Multiplayer.cs`: `IsActive` is `Session != null`; HostGame/JoinGame
  assign Session, and LeaveGame disposes and clears it.
- `NebulaPatcher/Patches/Dynamic/UILabWindow_Patch.cs`: normal selection sends an
  outgoing packet from the original click handler which DFS's button bypasses.
- `NebulaNetwork/PacketProcessors/Factory/Laboratory/LaboratoryUpdateEventProcessor.cs`:
  the existing packet carries a scientific-matrix slot, not an arbitrary recipe ID.

## Enforcement and recovery

Session admission checks run before prototype-mode application/import and again
at Begin/late validation through EnsureRegistryReady. Save and Resume admission
read live activity too, independently of the per-session latch. A loaded-session
frame check catches activation after single-player Begin and records the existing
session failure, with best-effort pause/presentation; no networking is stopped or
claimed synchronized. Native lab selection rechecks at preflight, immediately
before its first mutation, and at its postcondition. Other existing mutation and
maintenance paths inherit the session-readiness check.

Multiplayer is NOT a startup/content failure. Leaving MP does not make an already
affected live world safe: leave it and load a **fresh** single-player session.
Only successful normal validation of that replacement clears the compatibility
latch. Nebula can remain installed; unaffected SP sessions are never latched.

## Regression scope

Core tests execute the production reflection/delegate probe using harmless types:
absent/inactive/active, API-only absence, repeated toggles, no negative cache,
missing/wrong/non-public/instance getters, read failures, guarded native-action
boundaries, and fresh SP recovery. Existing mode/research definitions remain tested.

Harmony fixtures execute real detours over harmless managed methods using the same
production probe in admission predicates: absent and inactive SP Begin/Resume and
all four save entrypoints, MP entry before the native body, activation in Begin's
postfix, immediate late-activity save refusal, custom mutation refusal and recovery
into a different SP identity. No BepInEx/Unity/Nebula/DSP runtime is executed by these
fixtures. Reference compilation is not game execution. Installed-game tests must
still verify an ordinary SP profile and a Nebula-installed inactive SP profile,
then rejection of host/join and loading fresh SP after leaving multiplayer.

Do not count those target-game observations as passed without actually running them.
