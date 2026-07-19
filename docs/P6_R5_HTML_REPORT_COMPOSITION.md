# P6-R5: HTML report composition

P6-R5 turns the existing offline chart report into a standalone evidence report. The report input is versioned as sc8-html-report-input-v3 and is assembled only by the C# Data/Service boundary before Python renders it.

## Composed sections

- **Winner and tie context** shows the latest persisted Phase 3 Winner when one exists. It also lists persisted confirmatory significance decisions. When no Phase 3 Winner exists, the report states only that no Winner is persisted; it does not guess whether a protocol is incomplete or tied.
- **Inclusive authoritative results** lists persisted complete-battery Performance Scores, Grades, eDPI, formula versions, and calibration configuration versions.
- **Outlier sensitivity analysis** displays persisted inclusive and flagged-row-excluded metric aggregates side by side. The excluded aggregate remains diagnostic and never replaces the inclusive Winner result.
- **Fatigue status** displays persisted informative fatigue observations. Fatigue does not exclude a session from Winner selection.
- **Ergonomic warnings** displays recorded informational, non-diagnostic warnings and acknowledgement state. Warnings do not alter test execution or scoring.
- **Scientific notes and scope** records post-adaptation-only evidence, C#/Python authority boundaries, offline-only operation, descriptive cross-profile comparison, and the explicit non-backup nature of the report.

## Missing evidence

Every composed section produces an explicit insufficient-data statement when its source records are absent. The renderer does not infer a Winner, a tie, an outlier conclusion, fatigue state, warning, score, Grade, or value not stored in its versioned input.

## Scope controls

- Python reads only the versioned report JSON. It never opens the production SQLite database and never recalculates Performance Score, Grade, eDPI, or Winner.
- The C# report-input read rejects local raw evidence with unfinalized adaptation status.
- The final HTML is self-contained and offline; it embeds chart images and has no network dependency.
