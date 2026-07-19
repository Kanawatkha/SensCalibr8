# Release Dependency and License Inventory

This inventory identifies runtime components shipped or required by the release candidate. The Unity editor, Unity packages, and native SQLite runtime are build/runtime dependencies; the editor and package manager are not required on the clean target machine.

| Component | Pinned version/source | Release role |
|---|---|---|
| Unity Windows x86-64 player runtime | Unity `6000.5.3f1` | Executable/player runtime bundled in `SensCalibr8_Data` and native player files |
| Unity Input System | `1.19.0`, package lock source `registry` | Runtime input handling; resolved into the player build |
| Unity Test Framework | `1.7.0`, package lock source `builtin` | Development/acceptance dependency; not required to operate the release player |
| Native SQLite C API | `3.49.1` pinned by `config/production-environment-v1.json` | Bundled as `SensCalibr8_Data/Plugins/x86_64/sqlite3.dll` |
| .NET/Mono managed runtime | Bundled by Unity player | Required by the standalone executable; no separate installation is required |
| Python analysis environment | Python `3.12.13`, pandas `3.0.1`, NumPy `2.3.5`, matplotlib `3.11.0` | Development/offline report generation environment; not required for the Unity player smoke launch |

The authoritative package versions, source metadata, and native SQLite SHA-256 are in `config/production-environment-v1.json` and `app/Packages/packages-lock.json` in the repository. The release package does not include the Unity editor, package cache, Python virtual environment, Unity `Library`, test results, or development source tree.

License notices for redistributed third-party binaries must be retained according to the Unity editor/package terms used to produce the build. This project does not claim ownership of Unity, Mono/.NET, Input System, or SQLite licensing.
