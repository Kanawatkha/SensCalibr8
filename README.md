# SensCalibr8

SensCalibr8 is an offline Unity/C#/SQLite/Python desktop application for structured Valorant mouse-sensitivity calibration. It never reads or modifies Valorant settings and never interacts with Vanguard.

## Current Status

- Phase 0 calibration is complete and frozen as `calibration_config_v1`.
- Phase 1 production foundation and Data Layer are complete. Phase 2 calculation/input-validation, profile lifecycle, slot/setup UI, persistent lifecycle guards, informational ergonomic warnings, and the Dashboard shell are established under the engine-independent Services assembly.
- Phase 3 Test Engine and all four Phase 4 modes are implemented, integrated, reproducible, and accepted. Phase 5 protocol/scoring is complete through the full P5-R8 regression sweep, including versioned scoring, the complete Phase 1-3 narrowing flow, scientific-rigor controls, continuous 5-10-session Best-Sensitivity training, plateau detection, and automatic fresh Phase 1 initialization with immutable cycle lineage.

## Start Here

Read [`AGENTS.md`](AGENTS.md), [`CONTEXT.md`](CONTEXT.md), and [`PROGRESS.md`](PROGRESS.md) before changing code. Local environment setup and repeatable validation commands are documented in [`docs/LOCAL_SETUP.md`](docs/LOCAL_SETUP.md).
