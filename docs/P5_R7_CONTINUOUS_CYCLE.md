# P5-R7 — Continuous Cycle and Plateau

## Delivered scope

- Adds immutable `sc8-continuous-cycle-v1`: 5-10 Database Sessions at the Phase 3 Best Sensitivity, three consecutive cycle checkpoints, and strict relative score change below 5%.
- Creates deterministic/counterbalanced training-session plans at the persisted Phase 3 Winner without changing the user's `current_sensitivity` automatically.
- Records one auditable cycle checkpoint from the latest complete scored/graded training battery after the training-session count is met.
- Detects plateau only when the three latest checkpoint Grades are identical and the first-to-third score change is strictly below 5%; near-zero baseline score fails closed.
- Adds migration 8: unique profile cycle numbers, cycle checkpoints, and one-to-one recalibration events.
- Automatically creates the next cycle and fresh seven-value Phase 1 candidates around the source Phase 3 Winner, retaining full candidate-source provenance.

## Verification

- Contract values and 5-session minimum guard.
- Three equal Grade checkpoints with 4% score change trigger recalibration; exact 5% does not.
- New cycle number, Phase 1 candidate count, Phase 3 baseline provenance, and duplicate recalibration guard.
- Full Unity, calibration, build, and diff verification recorded in `PROGRESS.md`.
