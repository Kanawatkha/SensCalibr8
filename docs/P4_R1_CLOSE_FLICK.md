# P4-R1 Close Flick

## Scope

P4-R1 implements the first real production mode on the shared Phase 3 lifecycle. It remains score-independent and does not choose a sensitivity or alter the user's game settings.

## Behavior

- `CloseFlickMode` implements `ITestMode` for `FlickClose` only.
- `Prepare` derives the deterministic 30-condition sequence from the accepted `sc8-mode-contract-v1` seed contract.
- Every condition retains its frozen hidden foreperiod. The runtime supplies the authoritative high-resolution `target_visible` timestamp after that foreperiod; Reaction Time remains target-visible to click or timeout.
- The mode accepts an optional `first_mouse_movement`, a `click` with hit and final aim evidence, or a `timeout` with final aim evidence. It rejects events outside the allowed lifecycle or a click after the frozen timeout.
- Final Precision Error is the radial angular difference between final aim and target center. Center-Hit uses the frozen half-target-radius rule.

## Raw evidence and persistence

`CloseFlickCaptureEvidence` lives in Core so Test Logic can produce it and Services can persist it without reversing assembly dependencies. `CloseFlickEvidencePersistenceMapper` produces `ShotCaptureRecord` values with:

- `distance_zone = close`
- visibility, movement, resolution, hit, and final-aim evidence
- Final Precision Error and Center-Hit diagnostic
- `signed_overflick_underflick_deg = final_aim_azimuth_deg - target_center_azimuth_deg`

Migration 4 adds this nullable raw signed-error field to `shots`. It is intentionally nullable for historical/non-flick rows. Adaptation remains null while capture is active and is finalized only by the existing P3-R5 transaction boundary.

## Verification

- Unity EditMode: **124/124 passed**.
- Windows production build: passed.
- Calibration regression: **72/72 passed**.
- `git diff --check`: passed.

## Deferred work

Rendering/scheduling glue that displays the prepared target through the arena and binds live input capture into a full battery workflow is shared cross-mode integration work. It remains P4-R5 scope. Score aggregation, Grade, outliers, fatigue, and sensitivity selection remain Phase 5 scope.
