# SensCalibr8

SensCalibr8 is an offline Unity/C#/SQLite/Python desktop application for structured Valorant mouse-sensitivity calibration. It never reads or modifies Valorant settings and never interacts with Vanguard.

## Current Status

- Phase 0 calibration is complete and frozen as `calibration_config_v1`.
- Phase 1 production foundation and Data Layer are complete. Phase 2 calculation/input-validation, profile lifecycle, slot/setup UI, persistent lifecycle guards, informational ergonomic warnings, and the Dashboard shell are established under the engine-independent Services assembly.
- Phase 3 Test Engine and all four Phase 4 modes are implemented, integrated, reproducible, and accepted. Phase 5 protocol/scoring is complete through the full P5-R8 regression sweep. Phase 6 is complete: it provides a versioned, profile-isolated analysis read model, immediate Dashboard feedback, a self-contained offline evidence report with all ten charts, profile-separated JSON/CSV Data Export, and an explicit opt-in Cross-profile Comparison Page based only on persisted normalized metrics. Phase 7 integration and release QA is next.

## Start Here

Read [`AGENTS.md`](AGENTS.md), [`CONTEXT.md`](CONTEXT.md), and [`PROGRESS.md`](PROGRESS.md) before changing code. Local environment setup and repeatable validation commands are documented in [`docs/LOCAL_SETUP.md`](docs/LOCAL_SETUP.md).
