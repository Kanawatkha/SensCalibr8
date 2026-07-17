# P5-R1 — Metric normalization and score services

## Scope

P5-R1 turns finalized post-adaptation mode evidence into versioned mode and battery scores. It does not generate protocol candidates, rank Winners, assign Grades, or run significance tests.

## Configuration boundary

The SHA-verified `calibration_config_v1` top-level `formula_contract` is projected into immutable `ScoringFormulaContract` values. Fixed normalization and Submovement bounds continue to come from the accepted database-record JSON fields. Scoring code contains no independent copy of formula weights, sample counts, or metric bounds.

## Aggregation contract

- Shot modes require exactly 15 authoritative observations. Accuracy uses all resolved opportunities; mean primary time and mean Final Precision Error use all 15; Consistency is sample SD of all 15 precision errors; Submovement Count averages authoritative hits only.
- Far missing-onset observations retain null raw Travel Time and use the configured ceiling only in the scoring aggregate.
- A zero-hit shot mode retains null raw Submovement Count and uses penalty 1.0.
- Tracking requires exactly 54 equal one-second windows. Time-on-Target and deviation are arithmetic means; Consistency is sample SD of the same 54 deviations.
- All utilities use fixed min-max normalization with direction inversion where lower is better and clamp only at normalization. The shot formula receives no final clamp.
- A battery requires one result from each of the four modes and uses their unweighted arithmetic mean.

## Persistence

`SensitivityScorePersistenceService` rejects a score whose formula or normalization identity differs from the active accepted configuration. `SensitivityTestRepository` stores the aggregate, each mode score, configuration FK, formula version, phase, and sample size in `sensitivity_tests`.

As of P5-R3/migration 6, every newly persisted score must also reference its unique completed four-mode battery. The repository verifies battery profile, cycle, phase, eDPI, and calibration configuration before accepting the aggregate.

## Verification

- Unity EditMode: 161/161 passed.
- Accepted worked examples: Shot 77.0; Tracking 81.875.
- Boundary fixtures: normalization inversion/clamping, Submovement 1/6, theoretical shot floor -10, exact 15/54 sample requirements, missing-value rejection, zero-hit and Far fallback behavior.
- SQLite fixture: formula version and four-mode JSON persist with accepted configuration lineage.
- Calibration analysis regression: 72/72 passed.
- Production Python: 10/11 checks completed; the dependency-lock check is environment-blocked because the bundled runtime has no `contourpy` distribution metadata. No scoring calculation failed.
- Windows production build and `git diff --check`: passed.
