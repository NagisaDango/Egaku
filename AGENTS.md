# Egaku Project Agent Guide

## Project intent

- Egaku is a two-player cooperative 2D platformer. One player is the Runner; the other is the Drawer.
- Treat the committed game design and the latest `origin/main` history as the functional source of truth.
- The active upgrade target is Unity `6000.5.1f1`. The complete `main` baseline was last serialized by Unity `6000.2.0b2`.
- Networking uses Photon PUN 2. Preserve two-client behavior while upgrading or refactoring.

## Safety and Git

- Preserve user changes, `.meta` files, GUIDs, and serialized references. Never regenerate or replace them casually.
- Do not merge the old `version-change-test` branch into this branch. It is an incomplete historical baseline.
- Keep Unity migration, dependency upgrades, Photon compatibility fixes, gameplay fixes, and performance refactors in separate commits.
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
- Audit `RpcTarget.AllBuffered`, cached events, per-frame RPCs, ownership transfers, network instantiate/destroy calls, disconnects, and Master Client switches.
- Never expose or print Photon App IDs, credentials, tokens, or private connection settings.

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
- Minimum release gate: produce a Development Build and verify two built clients can complete the core flow through Photon.
- For performance work, capture profiler or network statistics before and after; do not claim improvement from code inspection alone.

## Change discipline

- Make the smallest change that resolves the current verified problem.
- Do not mix speculative cleanup with compatibility repairs.
- Explain generated Unity changes separately from intentional code changes.
- Stop and report when a change would alter gameplay design, networking protocol, serialized GUIDs, or scene ownership semantics.
