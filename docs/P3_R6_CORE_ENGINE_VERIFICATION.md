# P3-R6 Core-Engine Verification

## Scope

P3-R6 verifies the shared Phase 3 engine boundary after deterministic input/timing capture, calibrated arena setup, target sequencing, and session/battery persistence were implemented. It does not implement scoring or the four production mode behaviors.

## Verification matrix

| Area | Evidence | Result |
|---|---|---|
| Raw-input replay | Replayed the same timestamped raw deltas twice and compared raw, timestamp, and cumulative angular evidence | Passed |
| Target sequencing | Recreated the same frozen sequence and verified stable audit/signature; mode changes produce a different seed | Passed |
| Geometry/frame invariance | Loaded accepted frozen geometry and compared letterboxed viewport results at scaled equivalent resolutions | Passed |
| Lifecycle safety | Existing P3-R1 state-machine tests cover invalid transitions, callback faults, cancellation, recovery, and terminal states | Passed |
| Incomplete battery | Existing P3-R5 persistence tests verify fewer than four completed modes does not complete a battery | Passed |
| Adaptation finalization | Existing P3-R5 tests verify shot/tracking adaptation flags are assigned only after capture ends and reject preflagged data | Passed |
| Interrupted write recovery | Injected an invalid mouse-to-shot reference after session/shot insertion began; the transaction rolled back session, shots, and mouse samples | Passed |
| Raw-data preservation | Existing P3-R2 persistence tests verify raw deltas and derived angular values remain separately stored | Passed |
| Frame dependency guard | Static verification rejects render-frame timing/input dependencies in the core TestLogic layer | Passed |

## Automated results

- Unity EditMode: **115/115 passed**.
- Windows production build: **passed**; `app/Builds/Windows/SensCalibr8.exe` generated.
- Calibration analysis regression: **72/72 passed**.
- General analysis regression: not fully executable in the bundled runtime because the environment lacks locked `contourpy` package metadata; this is an environment setup issue, not a P3-R6 assertion failure.
- `git diff --check`: passed.

## Exit decision

P3-R6 is complete. The shared core engine is ready for the next planned acceptance round. Four mode implementations and protocol-level scoring remain outside this round.
