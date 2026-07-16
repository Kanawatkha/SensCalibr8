# P2-R5 — Ergonomic Warnings and Dashboard Shell

## Scope

P2-R5 adds a profile-scoped, informational warning surface and the initial Dashboard shell. It does not implement a diagnosis, medical recommendation, score adjustment, test blocking rule, Test Engine behavior, or score calculation.

## Warning contract

Dashboard refresh evaluates the active profile using the accepted research-backed conditions:

- `low_edpi_wrist_strain`: the calculated eDPI is strictly below 200 and the selected movement strategy is `wrist`.
- `mousepad_constraint_violation`: calculated `cm/360` is strictly greater than the configured mousepad width in centimetres.

The messages label both conditions as informational and non-diagnostic. A warning cannot alter a test result, score, candidate, or mode availability. It is never a reason to prevent a session from running.

Warnings persist in `injury_risk_flags`. For a single profile, a repeated evaluation does not add another row while an unacknowledged row with the same type and eDPI exists. Acknowledgement is limited to that profile. If the condition continues and the user has acknowledged the existing row, the next dashboard evaluation records a new occurrence so it can be surfaced again.

## Dashboard shell

The active-profile Dashboard supplies:

- Best Sensitivity placeholder;
- Grade placeholder;
- completed-session count and latest recorded session date;
- non-blocking ergonomic warning rows with acknowledgement;
- edit-setup navigation; and
- disabled Flick, Micro-Correction, Tracking, and Reflex mode entry points until their planned Test Engine rounds.

All activity and warnings are read using the active profile identifier. The UI receives presentation data through Services and performs no SQL or calculations.

## Verification

The P2-R5 EditMode tests cover the strict eDPI boundary, warning persistence and recurrence, acknowledgement isolation, and profile-scoped Dashboard activity. The full project verification at completion passed Unity EditMode 67/67, production Python 11/11, Phase 0 regression 72/72, and the Windows production build.
