# P1-R3: SQLite Schema and Migrations

## Scope

P1-R3 implements the complete SQLite structure documented in `ARCHITECTURE.md`. It supplies the native connection boundary, versioned migrations, integrity metadata, schema constraints/indexes, and transactional insertion of the accepted frozen calibration configuration. Repositories and product workflows remain deferred to P1-R4 and later rounds.

## Runtime provisioning

The Data assembly calls SQLite through the native C API. Production scripts copy `sqlite3.dll` from the pinned Unity 6000.5.3f1 editor runtime only after its SHA-256 matches `config/production-environment-v1.json`. The generated plugin is ignored by Git and recreated during prepare, preflight, test, or build. Windows builds package it under `SensCalibr8_Data/Plugins/x86_64/`.

The pinned SQLite build omits automatic initialization, so the connection boundary explicitly calls `sqlite3_initialize()` before opening a database. `SqliteConnectionFactory` then enables and verifies `PRAGMA foreign_keys = ON` for every connection.

## Migration contract

- Migration `1 / initial_schema` creates all 18 product tables, 23 explicit query indexes, uniqueness/check constraints, and all 33 documented foreign keys with `ON DELETE CASCADE`.
- The additional `schema_migrations` metadata table records version, name, SHA-256 checksum, and UTC application timestamp.
- Every migration runs transactionally and updates `PRAGMA user_version`.
- A changed name or checksum for an applied version is rejected as migration drift.
- The bootstrapper inserts the exact 20 non-ID `calibration_configs` fields from the accepted P0-R7 configuration in one transaction. Repeated bootstrap is idempotent; stored-value drift fails closed.

## Verification

- Unity EditMode: 20/20 passed, including 8 P1-R3 SQLite integration tests against temporary database files.
- Schema parity audit: all 18 authoritative table definitions matched columns, SQLite type mapping, `NOT NULL`, foreign-key target, and cascade behavior; 0 mismatches.
- Production Python: 11/11 passed.
- Phase 0 Python regression: 72/72 passed.
- Environment preflight: passed, including the native SQLite hash.
- Windows x86-64 build: passed; packaged `sqlite3.dll` hash matched the pinned source.

## Deferred work

P1-R4 owns repositories, row mapping, aggregate transaction boundaries, database error logging/user surfacing, and recovery behavior. P1-R5 owns broader data-integrity and failure-path coverage. No profile UI, scoring, target, input-capture, or Test Engine behavior is introduced here.
