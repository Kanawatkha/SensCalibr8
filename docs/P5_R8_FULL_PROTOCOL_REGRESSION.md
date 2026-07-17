# P5-R8 — Full Protocol Regression

## Scope

P5-R8 is the release-style regression sweep for the complete Phase 1-3 protocol and the scientific-rigor/continuous-cycle controls delivered in P5-R1 through P5-R7. It does not introduce new protocol behavior or new numeric requirements.

## Regression matrix

| Area | Regression evidence |
|---|---|
| Normal Winner and historical lineage | P5R3ConfirmatorySignificanceTests, P5R5PhaseThreeFinalNarrowingTests, P5R7ContinuousCycleTests |
| Statistical tie | P5R3ConfirmatorySignificanceTests |
| eDPI floor collision and candidate deduplication | P5R2PhaseOneExploratoryProtocolTests, P5R4PhaseTwoNarrowingTests |
| Incomplete batteries and stabilization minimum/maximum | P5R4PhaseTwoNarrowingTests |
| Undefined/near-zero CV | P5R4PhaseTwoNarrowingTests |
| Outlier versus documented data-quality exclusion | P5R6ScientificRigorTests |
| Fatigue flag without Winner exclusion | P5R6ScientificRigorTests |
| Reaction/Consistency Grade boundaries | P5R6ScientificRigorTests |
| Plateau and non-plateau boundaries | P5R7ContinuousCycleTests |
| Cancellation and restart recovery | P3R1EngineStateMachineTests, P3R5SessionBatteryPersistenceTests |
| Schema, transaction, and version preservation | P1R3SqliteSchemaTests, P1R4RepositoryTransactionTests, P1R5DataIntegrityTests, P5R1MetricScoringTests |

## Verification result

- Unity EditMode: **213/213 passed**, 0 failed, 0 skipped, 0 inconclusive.
- Windows production build: passed; app/Builds/Windows/SensCalibr8.exe, 667648 bytes.
- Calibration analysis suite: **72/72 passed**.
- git diff --check: passed.

All regression checks passed without changing the frozen protocol configuration or adding undocumented constants. No Git operation was performed in this round.
