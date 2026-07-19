# P6-R7 Cross-profile Comparison Page

## Scope

The Comparison Page is an offline, read-only, user-directed view. The user must select two or more unique profile slots and explicitly press **Compare**. The application does not preselect, enumerate, or automatically load every profile for this view.

## Data boundary

For each explicitly selected profile, the repository reads only that profile's latest completed four-mode battery that already has persisted Performance Score and Grade data. The page displays these persisted values:

- eDPI, the normalized sensitivity basis; raw sensitivity is deliberately not displayed.
- Consistency utility.
- Reaction Time tier.
- Performance Score.
- Formula version, calibration-configuration version, and completion date as lineage.

The page does not calculate scores, alter a profile, write evidence, change a configuration, or choose a Winner. If a selected profile has no qualifying completed scored-and-graded battery, it remains visible with an explicit unavailable state.

## Interpretation boundary

The table is a descriptive comparison of persisted results. It does not make causal claims, infer relative player skill, or treat one profile's result as a change to another profile. All comparison values are shown through eDPI and persisted normalized metrics, never through raw sensitivity.

## Verification

Unity EditMode regression: **222/222 passed**. The P6-R7 tests verify exact forwarding of the explicit selected IDs, the persisted eDPI/Consistency/Reaction Tier/Performance Score surface, unavailable-result presentation, and rejection of fewer-than-two, duplicate, or non-positive profile selections. Calibration/analysis Python regression also passed **74/74**; the Windows production build passed (`SensCalibr8.exe`, 667648 bytes); and `git diff --check` passed.
