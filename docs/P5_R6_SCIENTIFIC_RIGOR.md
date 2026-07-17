# P5-R6 — Adaptation, Outlier, Fatigue, and Grade

## Delivered scope

- Adds immutable `sc8-scientific-rigor-v1`, cross-checked against frozen adaptation values and the research 3-SD multiplier.
- Keeps adaptation labeling post-session: shot rows use the first `floor(n × 0.5)` observations and Tracking uses the frozen first block.
- Adds one-pass sample-SD outlier analysis in canonical profile/cycle/phase/mode/sensitivity/metric scopes. Statistical flags remain in the authoritative score.
- Produces a parallel flagged-row-excluded Performance Score for sensitivity analysis without replacing Winner evidence.
- Requires a separate documented data-quality confirmation before a statistical flag may be excluded from the authoritative score.
- Adds chronological first/second-half fatigue scoring. A decline strictly greater than 15% flags the session but never removes it from Winner selection.
- Assigns and persists Reaction, Consistency, and final worse-tier Grade with exact boundary semantics and version lineage.
- Adds migration 7 for outlier-run audit records and fatigue/Grade version fields.

## Verification targets

- Adaptation contract parity and post-adaptation-only analysis.
- Strict `> 3 × sample SD` and one-pass/idempotent scope handling.
- Inclusive Winner score plus flagged-excluded sensitivity analysis.
- Documented data-quality exclusion guard.
- Fatigue threshold boundary and near-zero denominator handling.
- Reaction boundaries at 200/250/350/500 ms and Consistency boundaries at 0.8/0.6/0.4/0.2.
- Database lineage, one-time Grade/fatigue assignment, migration integrity, and full regression/build checks.

## Verification results

- Unity EditMode: 209 passed, 0 failed/skipped/inconclusive.
- Windows production build: passed; `app/Builds/Windows/SensCalibr8.exe` is 667648 bytes.
- Calibration analysis regression: 72 passed.
- No-hardcoded-threshold scan and `git diff --check`: passed.

## Explicit boundary

P5-R6 does not implement the 5-10-session continuous training cycle, plateau detection, automatic recalibration, reporting UI, export, or Git operations. Those remain later rounds.
