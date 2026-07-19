# P7-R1 Golden-path end-to-end validation

## Journey covered

The acceptance fixture creates a clean SQLite database and uses the real application boundaries for:

1. Profile Setup with Hardware DPI `1600`, PSA starting sensitivity `0.175`, current sensitivity, polling-rate metadata, physical setup, and fixed crosshair palette.
2. Phase 1 seven-candidate generation and accepted confirmatory significance decision.
3. Phase 2 `-10% / 0% / +10%` candidate generation, five complete scored batteries per candidate, and unique Winner persistence.
4. Phase 3 `-5% / 0% / +5%` generation, five complete scored batteries per candidate, and unique preliminary Best/Winner persistence.
5. Continuous training checkpoint linked to the Phase 3 Winner, including the accepted five-session minimum.
6. Versioned Analysis dataset and HTML report-input reads from the same database.
7. Explicit cross-profile comparison between a completed primary profile and an isolated secondary profile.
8. Profile-separated JSON/CSV Data Export for the primary profile.

## Integrity assertions

- The worked example remains exact: DPI `1600` produces PSA sensitivity `0.175` and eDPI `280`.
- Phase 3 Winner eDPI `294` and its `Performance Score 81` remain present with formula/configuration lineage.
- `profiles.current_sensitivity` remains the user-entered value `0.175`; Winner persistence does not silently change the live-game setting.
- The secondary profile has no protocol candidates or sensitivity scores and is shown as unavailable by comparison.
- The training checkpoint, phase history, cycles, batteries, sessions, candidates, scores, and export tables retain profile/cycle/battery relationships.

## Results

- Unity EditMode: **224/224 passed**.
- Python report suite: **18/18 passed**.
- Calibration/analysis regression: **74/74 passed**.
- Windows production build: passed (`SensCalibr8.exe`, 667648 bytes).
- `git diff --check`: passed.

P7-R1 is complete. Edge/failure coverage is the next round; no Git operation was performed.
