# P4-R3 — Tracking

## Scope

P4-R3 implements the production Tracking mode on the shared `ITestMode` lifecycle. It consumes the accepted `sc8-mode-contract-v1` and `sc8-test-geometry-v1` values only; it introduces no score, candidate selection, or mode-local numeric tuning.

## Capture contract

- The deterministic sequence supplies 18 trials: two balanced nine-condition blocks spanning linear, curved, and variable-speed paths crossed with Small, Medium, and Large targets.
- A trial accepts `trial_started`, strictly increasing `aim_sample` events, then `trial_completed` at the frozen six-second boundary.
- Every trace must begin at elapsed time zero and end at the six-second boundary. Incomplete, reversed, duplicate, or out-of-contract timestamps fail closed.
- The target position is analytic from high-resolution elapsed time, never render-frame integration.
- Samples retain radial target-center error. Each sample interval is clipped into six exact half-open one-second windows. A window stores interval-weighted Time-on-Target and RMS Tracking Deviation.

## Persistence boundary

`TrackingEvidencePersistenceMapper` maps raw trial evidence and the six derived windows per trial into the existing `tracking_data` and `tracking_windows` repository records. It deliberately leaves `is_adaptation_trial` null: the existing transactional session/battery finalizer assigns adaptation after session completion.

## Verification

- Frozen contract identity, duration, window, and block values.
- Linear, curved, and variable-speed analytic position fixtures.
- Complete 18-trial lifecycle, 108 derived windows, 100% Time-on-Target / zero deviation fixture.
- Boundary-coverage failure behavior.
- Trial/window mapper behavior without an adaptation guess.

No Performance Score, Consistency score, protocol progression, or Git operation is included in this round.
