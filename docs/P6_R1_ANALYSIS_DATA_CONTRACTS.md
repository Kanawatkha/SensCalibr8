# P6-R1 — Analysis Data Contracts

## Delivered scope

- Adds the immutable sc8-analysis-dataset-v1 read model for one profile at a time.
- Analysis reads through the C# Data Layer only. Python receives future JSON data from this model and never opens the production SQLite database or recalculates authoritative scores.
- Every authoritative score retains its persisted eDPI, cm/360, Performance Score, per-mode score JSON, Grade, Formula Version, Calibration Configuration Version, and completed-battery date.
- Incomplete batteries remain visible as session evidence with isCompleteBattery = false, but their score rows are excluded from authoritativeScores.
- Analysis fails closed when a profile contains raw shot or Tracking evidence whose adaptation status has not been finalized.
- Outlier run aggregates expose both inclusive and flagged-row-excluded means side by side. The contract does not replace the authoritative score with the sensitivity-analysis value.
- Defines explicit units for eDPI, cm/360, Performance Score, and Reaction Time and validates dataset/version/lineage fields in the Python reader.

## Verification

- Unity EditMode: **215/215 passed**.
- Python calibration/analysis suite: **74/74 passed**.
- The test fixture verifies profile isolation, source-score parity at **77.0**, formula/config lineage, complete versus incomplete batteries, inclusive/excluded outlier aggregates, unfinalized-adaptation rejection, and JSON serialization.

P6-R1 does not implement Unity charts, HTML report generation, file export, or the Comparison Page. Those remain in P6-R2 through P6-R7.
