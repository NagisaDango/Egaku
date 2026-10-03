# Scene flow and setup

This change uses single-scene loading. It does not add a Bootstrap scene or Additive loading.

## Authored data

- `Assets/Resources/LevelCatalog.asset` owns ordered level IDs, scene names, thumbnails and pen budgets. Its initial values are migrated from `LevelSetup.csv`; the CSV is retained as historical reference and is no longer read at runtime.
- `LevelSetup` keeps scene geometry references, spawn/checkpoint position, initial pen and authored unlock flags. Its scene-scoped lookup no longer depends on the GameObject being named `LevelSetup`.
- `RoleSelection/Canvas/SelectionSceneBindings` owns selection UI references, button binding, device/role presentation, navigation focus, and forwards room-code updates to the authored OnlineSelection Prefab. The persistent manager forwards state updates; the scene removes its listeners on destruction.
- Each selectable level has an authored `GameplaySceneContext` on its LevelSetup object, referencing setup, gameplay camera, ink slider, fog and transition. Runner/Drawer initialization and transition targeting use these scene-owned references. Level_0 remains unchanged with its compatibility fallback.
- Existing scene/prefab GUIDs, role RPC names, observed components and scene list are retained.

## Progress and navigation

`levelUnlocked` is a count into catalog order. The current RoleSelection override remains 21 for fully unlocked testing. To test progression beginning at Level_1, set that override to 2 (the hidden Level_0 still occupies catalog index zero); level zero remains excluded from the existing selection screen. No disk-backed progression/save system is added.

Completing a level unlocks its immediate next entry. Completing the final entry loads FinishGame. Unlocking the last level alone does not finish the game.

The existing gameplay Back button (`GeneralUI.LeaveRoom`, retained for serialized compatibility) returns both clients to level selection. It preserves room identity, actor numbers, roles and readiness; the Master destroys runtime objects/cached RPCs before loading RoleSelection. The separate room/role-selection Leave action still leaves the session. FinishGame Home explicitly leaves before loading AllanLauncher.

A non-Master requests shared return using the room property `sceneReturnSelectionRequest`; only the Master loads the scene. `sceneUnlockedLevels` mirrors the session's unlock count so a new Master retains it. These additions require same-version clients; mixed old/new behavior is not validated.

## Recovery readiness

The existing refresh epoch, cleanup acknowledgements, reliable DestroyAll ordering and per-client same-scene reload remain. After both Runner and Drawer copies finish Start, including the local UI/camera and local/remote physics configuration, an actor publishes the existing target acknowledgement. Both stay input-paused until the Master publishes epoch zero after both acknowledgements. Master handoff resumes the pending epoch.

A recovery timeout is now a failure, not successful epoch completion: it uses the existing session-expiry and leave callbacks to return both actors to the room entry. It never writes epoch zero to simulate two Ready acknowledgements. The failing gameplay copy remains input-blocked while LeaveRoom completes. Transport-disconnect recovery remains distinct from this readiness barrier.

Normal initialization has a 30-second `sceneReadyTimeout` Inspector setting and reports which role failed readiness. Invalid required context references fail before spawning. Missing setup/camera/ink UI is guarded in role initialization, so these failures do not silently acknowledge partial setup.

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

## Previous implementation validation record

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


## Flow and responsibility follow-up (2026-10-02)

- `GameManager.SceneFlow.cs` owns navigation, initialization deadlines and departure timing. `GameManager.Recovery.cs` contains the existing recovery handshake and session-expiry helpers. Both are partial files of the original MonoBehaviour: its original script GUID, serialized fields and Photon callback owner stay intact. This is a bounded separation by responsibility, not a replacement networking component or protocol.
- A departure uses the existing `LoadLevelEnd` RPC. The persistent flow starts its unscaled deadline independently of the mask. At the deadline it writes the exact closing endpoint, then only the current Master commits progression. A non-Master keeps the accepted departure pending until the scene changes, allowing a Master handoff during the wipe.
- A missing material/camera/target or disabled visual does not strand departure. Missing camera/target uses a centered wipe. Missing material skips presentation. Opening/closing retain exact endpoints and aspect-correct full-screen coverage. Each scene releases its cloned mask material.
- Scene validation now checks unique complete gameplay contexts and the authored OnlineSelection room-code/copy controls. References from another loaded scene are rejected.
- Unity-aware authoring changed Level_1 through Level_20 plus RoleSelection. Saving also upgraded SpriteRenderer/SpriteShapeRenderer, Light, Canvas and TMP serialization: added default fields, moved color/mask fields, and renamed cookie/radius fields preserving values. These generated changes were reviewed and preserved. Level_0, production Build Settings, packages, Project Settings and the drawn-object network contract are unchanged.

Additional Development-only smoke options (same flag on both clients unless noted):

- `-egaku-smoke-no-mask`: stop the Master's presentation and exercise independent departure.
- `-egaku-smoke-transition-handoff`: change Master during both closing transitions, restoring actor one before the ordinary handoff stage.
- `-egaku-smoke-failure=ready`: after the ordinary flow, start a third refresh and inject a short initialization timeout on the client with `-egaku-smoke-inject`.
- `-egaku-smoke-failure=refresh`: the injecting client keeps its owning role unready, with a 45-second local initialization timeout, so the real 30-second shared refresh timeout handles the failure.

Fault injection arms in sceneLoaded but changes readiness in Update, before the manager's yield-null check; it does not assume a particular sceneLoaded subscriber order. Returning after the probe does not rejoin its closed test room. Test failures from earlier tool iterations are kept in the ignored Builds folder and are not counted as successful verification.

Current follow-up verification:

- Compilation: zero errors. SceneFlowTests: 11/11 passed, including cross-scene UI rejection and missing-mask fallback. All 24 enabled scenes passed validation.
- Two separately packaged Development clients completed both initial Master-role permutations, two refreshes, retained-room returns, last-level/finish flow, and Master handoff during the second refresh. The disabled-mask variant also completed.
- Local two-player lifecycle: Launcher to RoleSelection, Level_3 readiness, actual destination event to Level_4, retained-room return with old roles destroyed, re-entry to Level_3 and offline refresh all completed; Console had no runtime errors. Physical controller assignment and movement were not exercised by this automation.
- Visual appearance, all drawing materials/collision interactions and actual transport loss/rejoin still require manual gameplay verification; headless scene-flow logs do not establish these.

- Final standalone fault runs passed on both independent clients: `guards-pass-ready-host.log` / `guards-pass-ready-peer.log` (Master Runner initialization deadline, plus both transition handoffs), and `guards-pass-refresh-host.log` / `guards-pass-refresh-peer.log` (initial Master Drawer, non-Master Runner kept unready, real third refresh timeout). All four are in `Builds/SceneFlowSmoke`. Their expected `[SceneFlow]` failure messages are deliberate injections; no NullReference/MissingReference/RPC/tween exception signatures occurred.
- Earlier successful normal and disabled-mask records: `guards-runner-host.log` / `guards-drawer-peer.log` and `guards-drawer-host.log` / `guards-runner-peer.log`. Later final fault runs repeated the complete ordinary sequence before injecting the failure.
- Both final Development build packages succeeded. Editor was stopped and restored to its initial Level_1 scene after local checks. No commits or pushes were made by this follow-up.

## Online room-code UI

The `UI/OnlineSelection` resource Prefab owns `RoomCodeBar/PrivateRoomCodeLabel` and `CopyRoomCodeButton`. `SelectionSceneBindings.ShowCurrentRoomCode` forwards to the view; no separate scene room-code label is required. The persistent button event calls `OnlineRoleSelectionView.CopyRoomCode`, copies the raw current Photon room name, and shows `Copied!` for 1.5 unscaled seconds. Offline/out-of-room calls do not copy anything. No room properties, RPCs, or ownership settings change.

The authored `RoleColumns` group scales down when the width-scaled scene Canvas has less height. The header reserves space for room code, Copy, role instructions, Settings, and Leave. RoleSelection's redundant Canvas-level local `BackToRoles` button is inactive; the local-device Prefab and online Prefab retain their own buttons. SceneContextAuthoring only migrates gameplay contexts and no longer creates a room-code scene label.

Verification: compilation and all 24 enabled scene-reference checks passed. In an isolated real Photon room in Editor, the persistent Copy event produced the exact raw room name, showed feedback, preserved room properties, and was the top UI raycast hit. Visually checked 4:3, 16:9, and 21:9, maximum-length room-code text, and a temporary second card/Ready panel. This UI change has not been tested in two rebuilt standalone clients. Starting RoleSelection directly before joining a room exposed existing RolesManager.Start errors (network Instantiate at line 42 and null access at line 51); these are outside this UI change.
