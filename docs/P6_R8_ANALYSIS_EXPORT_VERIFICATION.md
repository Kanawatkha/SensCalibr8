# P6-R8 Analysis/export verification

## Verified boundaries

- Python report rendering consumes persisted C# projection values. It does not calculate a Performance Score, eDPI, Grade, Winner, or statistical decision.
- Empty or partial evidence renders explicit insufficient-data panels. Missing Phase 3 Winner evidence is not interpreted as a tie or any other protocol outcome.
- A persisted statistical tie remains descriptive evidence; it does not fabricate a Winner.
- Outlier flagged-row-excluded values remain a parallel diagnostic. Fatigue remains informational and never removes a session from Winner selection.
- Data Export is parsed back only as JSON and CSV data. It preserves profile scope, version, text quoting, null fields, and invariant numeric values. The public export surface has no Import or Restore method.

## Fixtures and checks

The C# and Python acceptance fixtures each use persisted `eDPI = 280` and `Performance Score = 77` values, which are presented unchanged in the offline HTML report and JSON/CSV export checks. The report fixture verifies all ten chart IDs, embedded PNG data URIs, report composition sections, and absence of external HTTPS references.

## Visual-review boundary

The offline fixture report was generated successfully. This agent environment blocks `file://` navigation in the browser, so an interactive visual inspection could not be performed here. The remaining human visual screen pass is intentionally recorded for P7-R5 rather than being inferred from automated tests.

## Results

- Unity EditMode: **223/223 passed**.
- Python report suite: **18/18 passed**.
- Calibration/analysis regression: **74/74 passed**.
- Windows production build: passed (`SensCalibr8.exe`, 667648 bytes).
- `git diff --check`: passed.

No Import/Restore capability was added.
