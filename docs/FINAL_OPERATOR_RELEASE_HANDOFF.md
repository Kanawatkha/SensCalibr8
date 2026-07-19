# Final Operator Release Handoff

## Status

All automated release gates passed in P7-R6. This checklist is the remaining human-operated acceptance boundary for the Windows release candidate. Record **Pass**, **Fail**, or **Not Run** beside each item; a failure should include a screenshot and the relevant player log.

## Package integrity

1. Extract the supplied final release-candidate directory to a local Windows x86-64 folder, keeping `SensCalibr8.exe`, `SensCalibr8_Data`, `calibration`, `config`, `docs`, and `RELEASE_MANIFEST.json` together.
2. Before normal use, run `Smoke-Test-Release.ps1` from the extracted package root. Expected result: `status: passed`.
3. Confirm that the test is performed offline and that Valorant/Vanguard is not launched or accessed by SensCalibr8.

## Menu and setup acceptance

1. Start `SensCalibr8.exe`. Confirm there is no configuration/startup error.
2. Confirm the initial menu is a resizable 960 x 540 window and is usable without visual clipping.
3. Press F11 from the menu. Confirm fullscreen behavior; return to the windowed menu with the documented escape/pause route.
4. Create a profile using the actual hardware values. DPI must be manually entered; do not expect automatic mouse-firmware detection.
5. Confirm invalid DPI, in-game sensitivity, polling rate, and mousepad dimensions are rejected with clear guidance.
6. Confirm the crosshair chooser exposes only the approved colors; dot style and size must not be editable.

## Test-session acceptance

1. Begin one acceptance-bearing test session. Confirm it enters the frozen test display state automatically and returns to the prior menu display state after completion or escape.
2. Confirm the arena is an enclosed, unlit, shadow-free checkerboard room; targets are cyan spheres; HUD is minimal; and the crosshair is the fixed dot in the selected color.
3. Confirm no in-game Valorant setting is read or changed, no network sign-in/request occurs, and no ADS/scoped mode appears.
4. Complete or intentionally stop the session. Confirm the application presents a clear result/state without crashing and preserves the correct session boundary.

## Persistence and export acceptance

1. Relaunch the application and confirm the created profile and completed/recorded session state remain available.
2. Export data for the explicitly selected profile. Confirm the UI calls it **Data Export**, not Backup/Restore.
3. Confirm the exported result is profile-scoped and that the application exposes no Import or Restore action.

## Release decision

- **Accept release candidate:** every required item passes, or any not-run item is explicitly approved as a deferred post-release check.
- **Block release:** startup/configuration error, visual breakage, data loss, scope violation, unexpected network behavior, or any failed input/session/export boundary.

When reporting the result, provide the package directory name, Windows version, display resolution/refresh rate, and any screenshot or player log path for a failed item.
