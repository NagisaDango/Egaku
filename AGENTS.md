# Egaku Project Agent Guide

## Project intent

- Egaku is a two-player cooperative 2D platformer. One player is the Runner; the other is the Drawer.
- Treat the committed game design and the latest `origin/codex/egaku-development` history as the active development source of truth.
- Keep `main` frozen as the final historical pre-development baseline unless the user explicitly changes this policy.
- The active upgrade target is Unity `6000.5.1f1`.
- Networking uses Photon PUN 2. Preserve two-client behavior while upgrading or refactoring.

## Safety and Git

- Preserve user changes, `.meta` files, GUIDs, and serialized references. Never regenerate or replace them casually.
- Do not merge the old `version-change-test` branch into this branch. It is an incomplete historical baseline.
- Keep Unity migration, dependency upgrades, Photon compatibility fixes, gameplay fixes, and performance refactors in separate commits.
- Create feature branches from `codex/egaku-development` and target pull requests back to that branch, not `main`.
- Do not use destructive Git commands or discard Unity-generated changes without explicit approval.
- Do not push, merge, rebase, or rewrite shared history unless the user requests it.
- Ignore generated folders such as `Library`, `Temp`, `Logs`, `obj`, `Build`, and `Builds`; never commit them.

## Unity workflow

- Before operating on the Editor, confirm the active Unity instance and project path.
- Prefer Unity-aware tools for scenes, prefabs, components, and serialized assets. Avoid manual edits to large Unity YAML files unless no safe Editor operation exists.
- After changing scripts, wait for compilation to finish and inspect the full Console before continuing.
- Resolve compile errors in dependency order: packages, third-party plugins, project assemblies, serialized references, then runtime behavior.
- Do not enter Play Mode or save scenes while compilation is failing.
- Review every automatic change to `ProjectSettings`, `Packages`, scenes, prefabs, materials, and input assets before committing.
- Keep all enabled production scenes from `EditorBuildSettings.asset`; do not replace the complete scene list with a legacy one.

## Photon rules

- Treat `Assets/Plugins/Photon` as vendored code. Prefer an official compatible Photon update over local source edits.
- If a vendor patch is unavoidable, isolate it in its own commit and document the upstream version, reason, and replacement path.
- Preserve RPC names, PhotonView configuration, observed components, ownership settings, room properties, prefab names, and App Version compatibility.
- Validate network changes with two independent clients. A single Editor Play Mode session is insufficient.
- Before changing reconnect, leave-room, or scene-refresh code, trace the full event sequence on both clients: room identity, ActorNumber and Master changes, callbacks, refresh epoch and acknowledgements, network-object cleanup, scene load, respawn, and input resume. Compare both client logs when available; identify any unobserved step instead of treating a single symptom as a confirmed cause.
- Audit `RpcTarget.AllBuffered`, cached events, per-frame RPCs, ownership transfers, network instantiate/destroy calls, disconnects, and Master Client switches.
- Never expose or print Photon App IDs, credentials, tokens, or private connection settings.

## Drawn-object physics and synchronization

- Treat `Assets/Resources/DrawnMesh.prefab` and `Assets/Scripts/DrawMesh.cs` as one network contract. Inspect both before changing drawn-object physics, collision, or synchronization.
- The Drawer creates a drawn object. When a stroke finishes, ownership transfers to the Runner, which simulates its physics regardless of who is Master Client. Do not equate Master Client, PhotonView owner, and Runner role.
- The Drawer's local `Rigidbody2D` is intentionally not simulated. Its visible copy follows networked position and rotation; its `Collider2D` cannot be relied on for local physics queries. Preserve the geometry-based erase detection unless a replacement is validated on both clients.
- Keep one pose writer on `DrawnMesh`: `PhotonTransformView` is the sole observed component. `PhotonRigidbody2DView` is disabled and not observed. Do not enable both without proving that the replacement avoids conflicting position updates.
- Before changing simulation or ownership, trace object creation, stroke completion, ownership transfer, `Rigidbody2D.simulated` and `bodyType`, collision, and destruction on both clients. Do not leave a completed object with simulation disabled on both copies or infer physics authority from the local role alone.
- For Master-approved erase and destruction, the Master Client approves the result while the current legal PhotonView owner performs `PhotonNetwork.Destroy`. An ownership change alone must not destroy the object.
- For changes to drawn-object physics, collision, ownership, or observed components, test two standalone clients with Master as Runner and Master as Drawer. Check gravity, contact with the level and Runner, and interaction with other drawn objects. Confirm both screens show the same trajectory without snapping or attraction to a point.
- When diagnosing a mismatch, compare both Player logs by PhotonView ID, ActorNumber, owner, Master Client, role, `Rigidbody2D.simulated`, `bodyType`, and transform at creation, finish, ownership change, and collision. Mark unobserved steps as unknown instead of inferring their cause.

## Game invariants

- Runner and Drawer roles must remain unique and synchronized.
- Preserve Runner movement, jumping, grabbing, death, respawn, mouse indicator, and wire travel.
- Preserve Drawer drawing, erasing, ink accounting, forced stroke completion, and brush switching.
- Preserve wood, iron, cloud, and electric-wire behavior plus water, glass, batteries, gates, buttons, enemies, bullets, tutorials, checkpoints, and level transitions.
- Network authority changes must not allow one client to create conflicting gameplay state.

## Verification

- Minimum compile gate: zero Console compilation errors and all game assemblies loaded outside Safe Mode.
- Minimum scene gate: Launcher, room/role flow, every enabled level, and finish flow open without missing scripts or broken references.
- Minimum gameplay gate: smoke-test both roles and every drawing material.
- Minimum network gate: create/join/leave, scene sync, drawing and erasing, ownership transfer, reconnect/abandon behavior, and Master Client handoff with two clients.
- For reconnect, leave-room, and scene-refresh fixes, rerun the reported event sequence with same-version standalone clients, then test a new room and a second refresh. State clearly which client-side paths were not exercised.
- Minimum release gate: produce a Development Build and verify two built clients can complete the core flow through Photon.
- For performance work, capture profiler or network statistics before and after; do not claim improvement from code inspection alone.

## Change discipline

- Make the smallest change that resolves the current verified problem.
- For every code change, including scripts and shaders, add or update comments beside non-obvious logic to explain its purpose, assumptions, and important edge cases. Keep comments accurate when behavior changes; do not merely repeat what the code says.
- Comment newly added state, networking, authority, recovery, and compatibility code so future debugging can identify its purpose, authoritative owner, and fallback behavior.
- Do not mix speculative cleanup with compatibility repairs.
- Explain generated Unity changes separately from intentional code changes.
- Stop and report when a change would alter gameplay design, networking protocol, serialized GUIDs, or scene ownership semantics.
