# P1-R4: Repositories and Transaction Boundary

## Scope

P1-R4 turns the P1-R3 schema into the only production persistence API. It does not add product setup flows, calculations, scoring, adaptation finalization, UI, target behavior, or input capture.

## Data-only API

`ProfileRepository` persists and maps profile rows. `ProtocolRepository` persists cycles, canonical protocol candidates with their source provenance, and protocol batteries after verifying that profile, cycle, phase, and sensitivity references agree. `CalibrationConfigurationRepository` resolves only an accepted configuration row already created by the bootstrapper.

`SessionCaptureRepository` persists one completed capture within an immediate SQLite transaction. The aggregate contains the session header, one timing-diagnostics row, zero or more shot rows, zero or more Tracking trials/windows, and zero or more raw mouse samples. It validates parent references and request-local child links, commits only after every child insert succeeds, and rolls back all rows after any failure. Mode-specific completeness and adaptation rules remain owned by future Test Engine rounds.

Raw SQL execution is internal to `SensCalibr8.Data`; tests receive friend visibility only. Services, UI, Test Logic, and Python receive typed repository methods rather than an executable SQL connection.

## Error and recovery contract

Repositories wrap failures in `DataAccessException`, which carries the failed operation, a failure classification, and a recovery action. An injected `IDataFailureReporter` is the logging/surfacing handoff point; it is intentionally UI-agnostic. Constraint failures recommend preserving the in-memory session so it can be retried after the caller resolves the problem. Integrity failures recommend reinitializing only from the accepted immutable configuration. This application provides Data Export rather than import/restore, so no recovery path claims that exports restore a corrupt database.

## Verification

- Unity EditMode: 32/32 passed after the P1-R5 integrity extension; P1-R4 itself contributed 6 repository integration tests.
- Verified profile mapping, candidate/source provenance, and battery-parent agreement.
- Verified atomic commit for shot and Tracking captures.
- Forced a duplicate raw-sample constraint failure and verified rollback of session, diagnostics, and shots plus the structured preserve-in-memory recovery action.
- Forced duplicate candidate-source provenance and verified rollback of the candidate parent.
- Verified no raw SQL pattern outside Data production source and that connection SQL methods are non-public.

## Deferred work

P1-R5 expands data-integrity and controlled-failure coverage across the remaining schema contracts. Phase 2 owns profile lifecycle semantics and user input validation. Phases 3-5 own session completion rules, adaptation finalization, scoring, protocol orchestration, and UI-facing recovery presentation.
