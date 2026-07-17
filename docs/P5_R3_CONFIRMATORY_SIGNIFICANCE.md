# P5-R3 — Confirmatory significance gate

## Scope

P5-R3 converts the two highest completed exploratory candidates into a fresh, matched confirmatory experiment and persists either a statistically supported candidate or a statistical tie. It does not generate Phase 2 candidates or evaluate stabilization.

## Frozen contract

Production projects `sc8-confirmatory-v1` directly from the accepted calibration configuration. The contract requires exactly 10 fresh complete-battery pairs, five A-first and five B-first, no exploratory reuse, no early stopping, matched mode/condition seeds within each pair, exhaustive 1024-assignment two-sided paired sign-flip inference at strict alpha 0.05, and a reported 95% Student-t interval over paired differences.

## Workflow

1. Rank only completed, battery-linked Phase 1 exploratory scores and retain the top two as opaque Candidate A/B.
2. Create two purpose-`confirmatory` batteries atomically for each frozen pair index.
3. Use the pair index as the deterministic repetition context and preserve the pairing seed and matched-condition key.
4. Count a pair only after both batteries contain exactly four distinct completed sessions under the accepted configuration and each battery has a persisted formula-versioned score.
5. Require all 10 unique indices, recompute exact statistics, and transactionally persist one `significance_tests` row plus all 10 `significance_test_pairs` rows.

Interrupted batteries remain ordinary raw evidence but do not count. Re-running the same pair index creates fresh batteries. A battery already present in any significance pair cannot be reused.

## Score lineage migration

Migration 6 adds `sensitivity_tests.battery_id` and a unique partial index. The physical column is nullable only for compatibility with pre-P5-R3 development rows; all repository-created scores require a positive parent battery and complete lineage. Confirmatory persistence additionally requires the supplied pair score to equal that battery's stored aggregate exactly.

## Verification

- Unity EditMode: 176/176 passed.
- Accepted positive fixture: 10 differences of +5, 1024 assignments, p = 0.001953125, effect = 5, 95% CI [5,5], Candidate A.
- Decision fixtures: symmetric statistical tie and significant Candidate B.
- Integrity fixtures: exact 10-pair gate, unique indices, five/five order, deterministic pair keys, completed four-mode batteries, purpose isolation, no evidence reuse, score-tampering rejection, and atomic no-partial persistence.
- Calibration analysis regression: 72/72 passed.
- Windows production build: passed (`SensCalibr8.exe`, 667648 bytes).
- `git diff --check`: passed.
