# P3-R2 — Deterministic input and timing

## Scope

P3-R2 implements the production raw-input and timing foundation. It does not implement the arena, target behavior, Test Mode metrics, filtering/Submovement detection, adaptation, scoring, or full session lifecycle.

## Capture contract

- Mouse deltas come from Unity Input System events for one selected mouse device.
- Redundant event merging is disabled before capture.
- Every event retains its Input System timestamp, `Stopwatch` timestamp/tick, device identity, and untouched X/Y delta.
- Cumulative azimuth/elevation is stored separately and uses the tested sensitivity with the centrally loaded Valorant yaw multiplier.
- No timing value is derived from render frames, configured polling rate, `Time.time`, or legacy `Input.GetAxis`.

## Timing and resampling

The runtime loads the accepted policy directly from `calibration_config_v1`: 1000 Hz processing grid, 0.5 ms cadence partition tolerance, `integrity-modal-cadence`, and the 20-sample minimum filterable segment.

The timing gate requires increasing event timestamps, a median interval mapping to one nominal cadence, the one-cadence class to be strictly modal, and the p99 one-cadence residual to remain within tolerance. Bursts and gaps are recorded as diagnostics. Resampling splits at gaps and interpolates cumulative angular position only inside each segment.

## Persistence and frame policy

Captured evidence maps to existing `mouse_samples` and `session_timing_diagnostics` records and commits through the existing atomic session repository. Input events are never written individually to SQLite. The frozen 144 Hz / VSync 0 frame policy is applied within a restorable scope, and acceptance-bearing use requires adaptive-sync-off confirmation.

## Verification

Ten new tests cover frozen contract loading, raw/angular evidence, stable timing parity, invalid timestamps, burst/gap handling, gap-safe resampling, modal cadence, persistence mapping and transaction compatibility, frame-policy restoration, and render-frame independence. The complete verification passed Unity EditMode 91/91, production Python 11/11, Phase 0 regression 72/72, and the Windows production build.
