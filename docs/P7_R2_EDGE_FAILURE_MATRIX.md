# P7-R2 Edge and failure matrix

This matrix is the audit index for the existing automated behavior. It records the expected fail-closed, rejection, rollback, flagging, or isolation outcome for each class. No production shortcut or new threshold was introduced in P7-R2.

| Failure class | Expected behavior | Automated coverage |
|---|---|---|
| Invalid DPI, sensitivity, polling rate, or mousepad dimensions | Reject before persistence/calculation | `P2R6ProfileSetupAcceptanceTests.SetupValidationRejectsInvalidInputsAndDuplicateNamesBeforePersistence` |
| Duplicate profile name | Reject with `profile_name_duplicate` | `P2R6ProfileSetupAcceptanceTests.SetupValidationRejectsInvalidInputsAndDuplicateNamesBeforePersistence` |
| Delete active profile | Reject; inactive confirmed delete cascades | `P2R6ProfileSetupAcceptanceTests.ActiveDeletionIsBlockedAndConfirmedInactiveDeletionCascadesChildData` |
| Physical-ruler fractional/confirmation path | Require preview and explicit positive integer confirmation | `P2R6ProfileSetupAcceptanceTests.PhysicalRulerAcceptanceRequiresExplicitPositiveIntegerConfirmation` |
| Unsupported crosshair color or mutable style/size | Reject unsupported palette; preserve fixed geometry | `P4R6ModeAcceptanceTests.AcceptedCrosshairPaletteRejectsUnsupportedColorsAndKeepsFixedGeometry` |
| eDPI below floor and converging candidates | Notify, apply floor, deduplicate canonical value, preserve sources | `P2R6ProfileSetupAcceptanceTests.FormulaWorkedExampleAndEdpiFloorRemainAvailableAtAcceptanceBoundary`; `P5R2PhaseOneExploratoryProtocolTests.EdpiFloorDeduplicatesEffectiveCandidatesAndPreservesEverySource`; `P5R4PhaseTwoNarrowingTests.StatisticalTieAppliesFloorBeforeDeduplicationAndRetainsAllSixSources` |
| Candidate batch insertion failure | Roll back all candidates and source rows | `P1R4RepositoryTransactionTests`; `P5R2PhaseOneExploratoryProtocolTests.CandidateBatchFailureRollsBackEveryCandidateAndSource` |
| Duplicate launch, invalid repetition, incomplete battery, or unstable candidate | Reject launch/finalization; never count partial evidence | `P4R5CrossModeBatteryWorkflowTests`; `P5R2PhaseOneExploratoryProtocolTests.LaunchCreatesExploratoryBatteryAndCounterbalancesAllFourModes`; `P5R4PhaseTwoNarrowingTests`; `P5R5PhaseThreeFinalNarrowingTests.WinnerSelectionRejectsUnstableOrIncompletePhaseEvidence` |
| Exact stabilized-score tie | Return explicit tie and persist no Winner/history | `P5R5PhaseThreeFinalNarrowingTests.ExactEqualHighestStableMeansReturnTieWithoutPersistingPhaseWinner`; `P5R3ConfirmatorySignificanceTests.SymmetricAndNegativeFixturesProduceTieAndCandidateB` |
| Near-zero CV or maximum stabilization boundary | Treat undefined CV as non-stabilized and stop at accepted maximum | `P5R4PhaseTwoNarrowingTests.NearZeroMeanIsUndefinedAndIncompleteBatteryDoesNotCount` |
| Bad timing, mode mismatch, incomplete capture, or cancellation | Reject capture, preserve active state when recoverable, or make cancellation terminal | `P4R5CrossModeBatteryWorkflowTests`; `P4R6ModeAcceptanceTests.CancellationIsTerminalForEveryProductionModeAndCannotResumeCapture` |
| Unfinalized adaptation evidence | Reject Analysis, HTML report, and Data Export reads | `P3R5SessionBatteryPersistenceTests`; `P6R1AnalysisDataContractTests.UnfinalizedAdaptationEvidenceFailsClosedUntilTheCaptureIsFinalized`; `P6R2ImmediateFeedbackTests`; `P6R8AnalysisExportVerificationTests` |
| Outlier scope/value/adaptation misuse | Reject invalid audit; flag/include by default; require documented reason for exclusion | `P5R6ScientificRigorTests` outlier persistence and exclusion cases |
| Fatigue threshold and Grade boundaries | Persist informational fatigue; apply exact Reaction/Consistency tiers and worse-tier Grade | `P5R6ScientificRigorTests` fatigue and Grade boundary cases |
| Plateau insufficient history or exact threshold | Do not plateau; require accepted three-cycle/5-session evidence; prevent duplicate recalibration | `P5R7ContinuousCycleTests.CheckpointRequiresFiveToTenSessionsAndLatestCompleteGrade`; `P5R7ContinuousCycleTests.ExactFivePercentOrGradeChangeDoesNotPlateau`; duplicate recalibration case |
| Profile-scoped Analysis/Comparison/Export access | Read only selected profile; unavailable results remain unavailable | `P6R1AnalysisDataContractTests`; `P6R7CrossProfileComparisonTests`; `P6R8AnalysisExportVerificationTests`; `P7R1GoldenPathTests` |
| Export overwrite, non-UTC timestamp, malformed CSV/manifest surface | Refuse overwrite/non-UTC; export remains data-only with no Import/Restore API | `P6R6DataExportTests`; `P6R8AnalysisExportVerificationTests` |
| Empty/partial report, missing Winner, tie, outlier, or fatigue records | Render explicit insufficient-data/diagnostic/informational output; never estimate | `analysis/tests/test_reporting.py` (18 tests) |
| Configuration mutation, missing fields, or version/hash drift | Fail closed before production use | `P1R2ConfigurationTests`; `P1R5DataIntegrityTests`; `P5R1MetricScoringTests` |

## Results

- Unity EditMode: **224/224 passed**, 0 skipped, 0 inconclusive.
- Python report suite: **18/18 passed**.
- Calibration/analysis regression: **74/74 passed**.
- No production code changed in P7-R2; the outcome is an auditable matrix over the accepted implementation and tests.
