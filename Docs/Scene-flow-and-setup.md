# Scene flow and setup

This change uses single-scene loading. It does not add a Bootstrap scene or Additive loading.

## Authored data

- `Assets/Resources/LevelCatalog.asset` owns ordered level IDs, scene names, thumbnails and pen budgets. Its initial values are migrated from `LevelSetup.csv`; the CSV is retained as historical reference and is no longer read at runtime.
- `LevelSetup` keeps scene geometry references, spawn/checkpoint position, initial pen and authored unlock flags. Its scene-scoped lookup no longer depends on the GameObject being named `LevelSetup`.
- `RoleSelection/Canvas/SelectionSceneBindings` owns selection UI references. The persistent manager rebinds these on return and removes its own listener before adding it again.
- Existing scene/prefab GUIDs, role RPC names, observed components and scene list are retained.

## Progress and navigation

`levelUnlocked` is a count into catalog order. The current RoleSelection override remains 21 for fully unlocked testing. To test progression beginning at Level_1, set that override to 2 (the hidden Level_0 still occupies catalog index zero); level zero remains excluded from the existing selection screen. No disk-backed progression/save system is added.

Completing a level unlocks its immediate next entry. Completing the final entry loads FinishGame. Unlocking the last level alone does not finish the game.

The existing gameplay Back button (`GeneralUI.LeaveRoom`, retained for serialized compatibility) returns both clients to level selection. It preserves room identity, actor numbers, roles and readiness; the Master destroys runtime objects/cached RPCs before loading RoleSelection. The separate room/role-selection Leave action still leaves the session. FinishGame Home explicitly leaves before loading AllanLauncher.

A non-Master requests shared return using the room property `sceneReturnSelectionRequest`; only the Master loads the scene. `sceneUnlockedLevels` mirrors the session's unlock count so a new Master retains it. These additions require same-version clients; mixed old/new behavior is not validated.

## Recovery readiness

The existing refresh epoch, cleanup acknowledgements, reliable DestroyAll ordering and per-client same-scene reload remain. After both Runner and Drawer copies finish Start, including the local UI/camera and local/remote physics configuration, an actor publishes the existing target acknowledgement. Both stay input-paused until the Master publishes epoch zero after both acknowledgements. Master handoff resumes the pending epoch.

The existing 30-second recovery timeout still releases local controls as an escape from a stalled handshake. This exceptional timeout is logged and is not proof that both clients became ready. Transport-disconnect recovery remains distinct from the scene readiness barrier.

## Validation tools

Use **Egaku > Validation > Validate Scene Setup** to check catalog entries, enabled scenes, thumbnails, missing scripts, LevelSetup camera references, transition components and selection bindings without running or saving scenes. The Development build command also runs this check.

`SceneFlowTests` covers frontier unlock, final-level boundaries, all-unlocked preservation, unknown IDs and production scene bindings.

`SceneFlowSmokeClient` exists only in Editor/Development compilation and is inert without explicit command-line arguments. It creates a private two-client test session and skips role-selection UI by seeding the existing role properties. Launch a host first, then the peer with the same unique room code:

```
ClientA/Egaku.exe -batchmode -nographics -egaku-scene-smoke=UNIQUECODE -egaku-smoke-host -egaku-smoke-runner -logFile host.log
ClientB/Egaku.exe -batchmode -nographics -egaku-scene-smoke=UNIQUECODE -logFile peer.log
```

For Master initially as Drawer, omit `-egaku-smoke-runner` on the host and add it on the peer. Use a new room code for each run.

Both logs must contain `[SceneFlowSmoke] PASS`. The tool waits for both clients at each stage: load Level_3, refresh, peer-requested return, load Level_19, transition to Level_20, finish, return, Master handoff, load Level_3 and refresh again with another Master handoff while the epoch is pending. It checks that refreshed Runner instances are new, refresh epochs are retired and input pause is released. A stage timeout exits with failure.

This automated flow does not replace manual checks of input devices, collision, all drawing materials, erase, latency, unexpected transport loss/rejoin, or visual trajectories on both screens. Do not claim these from headless results.

## Implementation validation record

- Unity 6000.5.1f1: compilation completed with zero compilation errors.
- All 24 enabled scenes passed catalog/setup/reference validation.
- Full EditMode run: 76 of 77 passed. The sole failure is the obsolete `RoleSelectionHasBothButtonsAndTheNetworkDisplay` test against removed legacy role fields; the user explicitly excluded role-selection tests from this task. No test was suppressed or rewritten to conceal it.
- New SceneFlowTests: 4/4 passed.
- Local runtime: Launcher to selection, Level_3 readiness, retained-room return with no old Runner/Drawer instances, re-entry, Level_19 to Level_20 to FinishGame, and home. The local FinishGame remote-actor assumption was reproduced and repaired; replay had zero Console errors.
- Two separately built Development Players: initial Master as Drawer passed the complete automatic scene flow, including two refreshes and Master change during the second refresh. Logs: `Builds/SceneFlowSmoke/final-drawer-host.log` and `final-runner-peer.log`.
- Initial Master as Runner also passed in a second new room: `Builds/SceneFlowSmoke/final-runner-host.log` and `final-drawer-peer.log`. All four final logs contain PASS and no exception/tween-error signatures. Both runs changed Master during the second pending refresh epoch.
- Runner physics remained Dynamic/simulated on the Runner client and Kinematic/non-simulated on the Drawer client, independent of Master role.
- Headless tests do not establish visual quality or unexpected disconnect/rejoin behavior; those manual checks remain outstanding.
- Unity authored the catalog asset and the Canvas binding component. Build/import operations also touched newline formatting of Runner/Drawer/RunnerMouse prefabs; Git reports no normalized serialized-content differences for those prefabs. No Project Settings, packages or scene-list changes are intentional.
