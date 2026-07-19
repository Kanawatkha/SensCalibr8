# P6-R6: Profile-separated Data Export

P6-R6 provides an explicit Data Export boundary for one selected profile at a time. It is portable data for inspection and external analysis, not an Import/Restore backup or a recovery guarantee.

## Output

Each export creates a new, safe-name directory containing:

- manifest.json: export-contract version, UTC export timestamp, selected profile identity, all exported tables, and the export-only disclaimer.
- One UTF-8 CSV file for every exported table. CSV headers follow the SQLite schema column order, rows are ordered by database ID, and all fields use invariant formatting and RFC-style quoted escaping.

The export covers profile metadata; referenced calibration configurations; protocol/cycle/candidate/battery/session lineage; raw shots, Tracking trials/windows, mouse samples, timing diagnostics; persisted scores/Grades; outlier runs/flags; confirmatory significance/pairs; Winner history; fatigue; ergonomic warnings; continuous-cycle checkpoints; and recalibration events.

## Safety and scope

- The repository queries only rows owned by the selected profile, including children constrained through that profile’s sessions, cycles, candidates, and significance records.
- It rejects a selected profile whose shot or Tracking adaptation status is still unfinalized.
- The writer requires a UTC timestamp, uses a filesystem-safe profile slug, refuses to overwrite an existing export directory, writes UTF-8 without a BOM, and removes only its newly-created incomplete directory if a write fails.
- JSON/CSV Import and Restore are not implemented and are explicitly Out of Scope.
