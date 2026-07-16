# SensCalibr8

SensCalibr8 is an offline Unity/C#/SQLite/Python desktop application for structured Valorant mouse-sensitivity calibration. It never reads or modifies Valorant settings and never interacts with Vanguard.

## Current Status

- Phase 0 calibration is complete and frozen as `calibration_config_v1`.
- Phase 1 production foundation and Data Layer are complete. Phase 2 calculation/input-validation, profile lifecycle, slot/setup UI, persistent lifecycle guards, informational ergonomic warnings, and the Dashboard shell are established under the engine-independent Services assembly.
- Phase 3 Test Engine lifecycle contracts, mode-independent state machine, deterministic raw-input capture, high-resolution timing, calibrated arena, confound-controlled sequencing, and atomic session/battery persistence are established and accepted. Four production modes and scoring remain in their planned later phases.

## Start Here

Read [`AGENTS.md`](AGENTS.md), [`CONTEXT.md`](CONTEXT.md), and [`PROGRESS.md`](PROGRESS.md) before changing code. Local environment setup and repeatable validation commands are documented in [`docs/LOCAL_SETUP.md`](docs/LOCAL_SETUP.md).
