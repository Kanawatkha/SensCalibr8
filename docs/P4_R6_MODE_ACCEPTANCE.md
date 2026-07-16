# P4-R6 — Mode acceptance and reproducibility

## Scope

P4-R6 is the Phase 4 exit gate. It verifies that all four production modes conform to the frozen contracts when created through the production battery factory, that deterministic sequences reproduce exactly, and that lifecycle/visual-confound boundaries remain enforceable. It does not add scoring or protocol selection.

## Acceptance matrix

| Area | Acceptance evidence |
|---|---|
| Production composition | `ProductionBatteryTestModeFactory` creates Close Flick, Far Flick, Tracking, and Micro-Correction implementations from the shared sequencer. |
| Reproducibility | Same profile/cycle/phase/mode/repetition context produces identical audit seed, seed material, and every target condition. |
| Geometry safety | All stationary target conditions satisfy the frozen target radius, 32-pixel edge margin, and 64-pixel top HUD reserve. Existing arena tests verify the fixed 16:9 letterbox, camera/FOV, unlit room, cyan spheres, and four-pixel crosshair. |
| Cancellation | Cancellation is terminal for every production mode; capture and recovery are rejected afterward. |
| Restart | An incomplete mode transitions to Faulted; recovery returns to Prepared and requires a fresh Start before capture. |
| UI confound control | Only the approved crosshair color palette is accepted; style and size remain fixed. The engine exposes no score or Winner result. |
| Release gate | Full Unity EditMode suite, calibration regression, production build, diff check, and legacy-input/frame-timer scan pass. |

## Non-goals

P4-R6 does not implement Performance Score, normalization, Consistency aggregation, outlier handling, fatigue, Grade, plateau detection, candidate selection, or confirmatory significance decisions. Those belong to Phase 5 and must continue to consume the frozen configuration and persisted raw/derived evidence.

## Verification result

- Unity EditMode: 151/151 passed.
- Calibration analysis: 72/72 passed.
- Windows production build: passed; `SensCalibr8.exe`, 667648 bytes.
- `git diff --check`: passed.
- Production source contains no legacy `Time.time` or `Input.GetAxis` usage.
