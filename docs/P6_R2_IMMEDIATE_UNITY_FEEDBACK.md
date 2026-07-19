# P6-R2 — Immediate Unity Feedback

## Delivered scope

- Adds a Data/Service-only immediate-feedback path for the Dashboard.
- The Dashboard now displays the persisted Phase 3 Best Sensitivity when a unique Winner exists, the latest authoritative complete-battery Performance Score, existing Grade/activity, and a horizontal Accuracy bar chart.
- The chart uses the latest shot-mode protocol window for the active profile: latest available cycle, phase, and shot mode; all finalized post-adaptation shot evidence in that same context is grouped by tested sensitivity value.
- Accuracy remains inclusive of statistical outliers, matching the authoritative-score policy. No UI component recomputes Accuracy, eDPI, Performance Score, Best Sensitivity, or Grade.
- Incomplete batteries do not qualify for the latest authoritative Performance Score. A profile with any shot whose adaptation status is still null fails closed rather than displaying fabricated feedback.
- Tracking is not rendered as an Accuracy chart because its equivalent metric is Time-on-Target; it remains available for the later report/chart rounds.

## Verification

- Unity EditMode: **218/218 passed**.
- Tests cover two sensitivity bars at 100% and 50%, post-adaptation-only counting, profile/cycle/phase/mode context, persisted Best Sensitivity and Performance Score, Dashboard presentation, and unfinalized-evidence rejection.

P6-R2 does not add HTML reports, Python chart generation, file export, or the Comparison Page.
