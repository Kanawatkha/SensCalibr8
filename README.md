# SensCalibr8

SensCalibr8 is an offline Unity/C#/SQLite/Python desktop application for structured Valorant mouse-sensitivity calibration. It never reads or modifies Valorant settings and never interacts with Vanguard.

## Current Status

- Phase 0 calibration is complete and frozen as `calibration_config_v1`.
- Phase 1 production foundation and Data Layer are complete. Phase 2 calculation/input-validation, profile lifecycle, slot/setup UI, persistent lifecycle guards, informational ergonomic warnings, and the Dashboard shell are established under the engine-independent Services assembly.
- Scoring and Test Engine behavior remain intentionally deferred to their planned rounds.

## Start Here

Read [`AGENTS.md`](AGENTS.md), [`CONTEXT.md`](CONTEXT.md), and [`PROGRESS.md`](PROGRESS.md) before changing code. Local environment setup and repeatable validation commands are documented in [`docs/LOCAL_SETUP.md`](docs/LOCAL_SETUP.md).
