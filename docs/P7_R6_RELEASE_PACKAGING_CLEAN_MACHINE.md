# P7-R6 — Release Packaging and Clean-Machine Smoke

## Scope

This round creates an offline Windows release candidate from the verified Unity production build and exercises it from a package-local runtime boundary. The package contains no editor project, Unity cache, Python environment, or source-only calibration harness.

## Release candidate

- Package: `artifacts/release-candidate/SensCalibr8-Windows-x86_64-v6`
- Platform: Windows x86_64
- Entrypoint: `SensCalibr8.exe`
- Build size: 667,648 bytes
- Package file count: 182
- Manifest: `RELEASE_MANIFEST.json`
- Runtime mode: offline; Import/Restore remains out of scope

The package includes the Unity executable/Data directory, `config/`, the eight frozen calibration contract/evidence files required at runtime, the operator guide, dependency/license inventory, smoke runner, and SHA-256 manifest.

## Clean-machine smoke boundary

`Smoke-Test-Release.ps1` validates required package paths and manifest scope, verifies every manifest SHA-256 checksum, creates an isolated temporary `userDataPath`, launches the packaged executable in batch/no-graphics mode, waits up to 15 seconds for a player log, rejects known startup exceptions, and stops the process. The temporary user-data directory is removed afterward. No network service is required.

Result: **passed**. Startup was observed, no startup exception was reported, and no `SensCalibr8` or `UnityCrashHandler64` process remained afterward.

## Verification results

- Unity EditMode: **234/234 passed**
- Python reporting suite: **18/18 passed**
- Calibration/analysis regression: **74/74 passed**
- Windows production build: **passed**
- Release packaging: **passed; 182 files**
- Manifest checksum verification: **passed**
- Clean-machine smoke: **passed**

The initial smoke attempt exposed a real UI runtime issue: the menu used the legacy `UnityEngine.Input` API while the project was configured for the Input System package. The UI assembly now declares `Unity.InputSystem` and uses `Keyboard.current`; the corrected build and package passed the final smoke run.

## Operator boundary

The package is a release candidate, not a claim that pixel-level human visual review has been completed automatically. The operator should launch the executable on the target machine, confirm the documented 960×540 resizable window behavior and F11 fullscreen toggle, then perform the protocol-level acceptance described in the operator guide.
