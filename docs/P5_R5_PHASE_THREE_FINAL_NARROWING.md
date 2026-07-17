# P5-R5 — Phase 3 Final Narrowing

## Delivered scope

- Adds immutable `sc8-phase-three-protocol-v1` with the approved `-5/0/+5%` offsets around the persisted Phase 2 Winner.
- Adds a transactional Phase 2/3 Winner-selection boundary. Every candidate must first satisfy the existing five-to-ten complete-battery, strict-CV-below-10 stabilization contract.
- Selects the unique highest mean complete-battery Performance Score. Exactly equal highest means are explicit ties: no arbitrary eDPI tie-break and no `phase_history` Winner is written.
- Generates Phase 3 candidates only from a persisted unique Phase 2 Winner. The eDPI floor is applied before canonical deduplication and every source path is retained.
- Provides deterministic blind labels, four-mode counterbalancing, and repetition bounds for Phase 3 narrowing batteries.
- Persists a unique Phase 3 Winner as the preliminary Best Sensitivity without changing `profiles.current_sensitivity`; the user must explicitly apply any live-game setting.

## Verification

- Unity EditMode: 190 passed, 0 failed.
- Windows production build: passed; `app/Builds/Windows/SensCalibr8.exe` is 667648 bytes.
- Calibration analysis regression: 72 passed.
- `git diff --check`: passed.

## Explicit boundary

P5-R5 does not implement adaptation reporting, outlier processing, fatigue flags, Grade assignment, plateau detection, analysis/export, or Git operations. Those remain in later approved rounds.
