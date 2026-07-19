# P7-R4 Migration and Historical Compatibility

## Migration compatibility

`P7R4MigrationHistoricalCompatibilityTests` constructs a database at every supported catalog state, including an empty state and schema versions 1 through 8. It applies the exact immutable migration SQL and metadata that a prior release would have recorded, then initializes through the production bootstrapper.

For every starting state, the test verifies that the database reaches the latest catalog version, retains one metadata entry per known migration, receives the accepted `calibration_config_v1` projection, and passes both SQLite integrity and foreign-key checks.

## Controlled rejection

The migration runner now validates the existing state before it applies any pending migration. It rejects:

- an applied migration version not represented by the production catalog;
- a `PRAGMA user_version` that disagrees with the applied migration history; and
- a non-contiguous history.

The rejection tests seed a normal profile before corrupting metadata, then confirm that the profile remains present after bootstrap fails. The runner neither rewrites metadata nor deletes user data to make a corrupted or newer database appear compatible.

## Historical interpretation

A completed historical sensitivity-test row is read through all three consumer boundaries:

- the versioned Analysis dataset;
- the versioned HTML-report input; and
- the profile-separated Data Export dataset.

The stored formula version and `calibration_config_id` / configuration version are preserved in every read. Nothing recalculates a historical score using a newer formula or configuration.

## Cascade regression

No cascade definition changed in this round. The complete schema audit (`P1R3SqliteSchemaTests.EveryDeclaredForeignKeyCascadesOnDelete`) and persisted aggregate deletion test (`P1R5DataIntegrityTests.ProfileDeletionCascadesTheCompletePersistedAggregate`) remain part of the P7-R4 Unity regression suite and passed unchanged.

## Automated evidence

- `P7R4MigrationHistoricalCompatibilityTests.EverySupportedSchemaStateUpgradesToTheCurrentCatalogWithoutReplacingMigrationHistory`
- `P7R4MigrationHistoricalCompatibilityTests.HistoricalScoreKeepsFormulaAndCalibrationLineageInAnalysisReportAndExportReads`
- `P7R4MigrationHistoricalCompatibilityTests.UnsupportedAndInconsistentMigrationMetadataIsRejectedWithoutRemovingExistingRows`
- `P7R4MigrationHistoricalCompatibilityTests.GappedMigrationHistoryIsRejectedWithoutRemovingExistingRows`
