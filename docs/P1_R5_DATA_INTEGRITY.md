# P1-R5: Data-Integrity Verification

## Scope

P1-R5 is the verification gate for the Phase 1 persistence boundary. It adds no product workflow, formula, scoring, input-capture, or Test Engine behavior. Existing migration and repository behavior is exercised against real temporary SQLite files and controlled failures.

## Verified contracts

- A battery cannot contain the same mode twice; the failed second session leaves the first intact.
- Candidate source provenance is unique and a duplicate source rolls back its parent candidate.
- Candidate and battery profile/cycle/phase/sensitivity relationships are checked before insertion.
- Profile deletion cascades through cycles, candidates, sources, batteries, sessions, timing diagnostics, shots, and raw mouse samples.
- Session capture transaction rollback leaves no session header, timing diagnostics, or child evidence after a deliberate uniqueness failure.
- Formula version and calibration configuration identity remain attached to a historical `sensitivity_tests` row.
- Foreign-key violations are rejected while existing valid rows remain unchanged.
- Invalid database locations are classified as unavailable and expose the retry recovery action.
- SQLite `integrity_check` reports `ok` and `pragma_foreign_key_check` reports zero violations after valid repository writes.

Together with P1-R3's schema parity, migration/checksum, required-field, per-connection foreign-key, cascade, and configuration-drift tests, this closes the Phase 1 Data Layer verification gate. Product-level input validation remains Phase 2 scope, where the documented DPI, eDPI, mousepad, duplicate-profile, and active-profile deletion rules will be tested with their calculation services.

## Verification result

Unity 6000.5.3f1 EditMode/NUnit passed **32/32**, with zero failures, skipped tests, or inconclusive tests. Production Python and Phase 0 regressions remain green at **11/11** and **72/72** respectively. The Windows build/preflight contract remains pinned and is rerun as the final release check for this round.

## Exit decision

Phase 1 Foundation and Data Layer is ready to exit after the final build/preflight run. The next authorized phase is Phase 2 — Profiles and Physical Setup; its first round owns PSA/eDPI calculations and all user-input validation.
