# Egaku two-client release check

Record the commit, Application.version, Unity version, Windows version, and each client log path with the result. Use two separately built Players; do not use Editor Play Mode as either client.

## Automated gate

1. Run all EditMode tests. Confirm zero failures and zero compilation errors.
2. Build Client A and Client B with Unity 6000.5.1f1 and -buildTarget StandaloneWindows64:
   - -executeMethod Egaku.Editor.ReleaseBuildValidation.BuildClientA
   - -executeMethod Egaku.Editor.ReleaseBuildValidation.BuildClientB
3. Confirm both build logs report success and zero errors. Keep Builds/ out of Git.

## Manual gate

Start the two built Players from Builds/EgakuReleaseValidation/ClientA and ClientB. Give each a separate -logFile path. Repeat the role flow once with A as Runner and once with B as Runner.

- [ ] Create a private room, join by code, choose unique Runner and Drawer roles, and leave back to the launcher.
- [ ] Verify scene sync through all enabled levels and the finish scene.
- [ ] Draw and erase wood, iron, cloud, and electric wire on both screens; check ink use and collision.
- [ ] Verify Runner movement, jump, grabbing, wire travel, death, respawn, checkpoints, and level transition.
- [ ] Disconnect each role for less than 30 seconds; confirm recovery, interactions, and state on both screens.
- [ ] Disconnect the Master Client; confirm the new Master takes over and play continues after recovery.
- [ ] Let recovery exceed 30 seconds, then try explicit leave; confirm the correct launcher UI and no stale player or room state.
- [ ] Search both Player logs for Error, Missing Script, serialization layout, Xenia errors, and Photon ownership warnings. Record any findings.

## Result

| Item | Client A | Client B |
| --- | --- | --- |
| Build path |  |  |
| Player log path |  |  |
| Role in each run |  |  |
| Core flow |  |  |
| Disconnect and recovery |  |  |
| Console or Player errors |  |  |
