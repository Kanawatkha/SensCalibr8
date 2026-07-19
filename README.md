# SensCalibr8

SensCalibr8 is an offline Unity/C#/SQLite/Python desktop application for structured Valorant mouse-sensitivity calibration. It never reads or modifies Valorant settings and never interacts with Vanguard.

## Current Status

- Phase 0 calibration is complete and frozen as `calibration_config_v1`.
- Phase 1 production foundation and Data Layer are complete. Phase 2 calculation/input-validation, profile lifecycle, slot/setup UI, persistent lifecycle guards, informational ergonomic warnings, and the Dashboard shell are established under the engine-independent Services assembly.
- Phase 3 Test Engine and all four Phase 4 modes are implemented, integrated, reproducible, and accepted. Phase 5 protocol/scoring and Phase 6 analysis/reporting/export/comparison are complete. Phase 7 automated release QA is complete: P7-R1 through P7-R6 passed, including a checksum-verified offline release candidate and clean-machine smoke. Target-machine visual/protocol acceptance remains an operator handoff activity.

## Start Here

Read [`AGENTS.md`](AGENTS.md), [`CONTEXT.md`](CONTEXT.md), and [`PROGRESS.md`](PROGRESS.md) before changing code. Local environment setup and repeatable validation commands are documented in [`docs/LOCAL_SETUP.md`](docs/LOCAL_SETUP.md).
