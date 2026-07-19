# SensCalibr8 Release Operator Guide

## Package contents

Run `SensCalibr8.exe` from the package root. Keep these items together:

- `SensCalibr8.exe` and its `SensCalibr8_Data` runtime directory;
- `calibration/` and `config/`, which contain the accepted immutable contracts required at startup;
- `docs/`, which contains this guide and the dependency/license inventory; and
- `RELEASE_MANIFEST.json`, which records package files and SHA-256 checksums.

Do not remove or edit the calibration/configuration files. A contract mutation is expected to fail closed.

## First launch

1. Extract the package to a local Windows x86-64 directory.
2. Start `SensCalibr8.exe` offline.
3. The menu opens in the documented 960×540 resizable window. F11 toggles menu fullscreen.
4. Create a profile and enter hardware DPI, current in-game sensitivity, polling-rate metadata, physical setup, and an approved crosshair color.
5. The application stores its SQLite database in the normal per-user application-data location. Data Export is profile-scoped; it is not Import/Restore.

## Clean-machine smoke

From the extracted package root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Smoke-Test-Release.ps1
```

The smoke script uses an isolated temporary user-data path, starts the actual executable in offline batch mode, verifies a clean exit, and writes a local smoke log. It does not create or modify a production profile.

## Final release acceptance

After the automated smoke test passes, perform the target-machine visual, setup, session, persistence, and Data Export checks in [FINAL_OPERATOR_RELEASE_HANDOFF.md](FINAL_OPERATOR_RELEASE_HANDOFF.md). Those checks require a human operator because automated tests cannot judge a specific monitor, GPU, or interactive visual flow.

## Troubleshooting

- A startup contract error means `calibration/`, `config/`, or a file recorded in `RELEASE_MANIFEST.json` is missing or edited. Re-extract an unmodified package.
- A database error should be surfaced by the UI without claiming that an export can restore a corrupt database.
- Do not launch Valorant or interact with Vanguard as part of SensCalibr8 operation.
