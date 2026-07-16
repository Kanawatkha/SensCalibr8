# P4-R2 Far Flick

## Scope

P4-R2 implements Far Flick on the shared Phase 3 lifecycle. Unlike Close Flick, it separates the target preview and center-reference activation from the movement-onset and Travel-Time measurements.

## Behavior

- `FarFlickMode` implements `ITestMode` for `FlickFar` only.
- A deterministic Far target first enters the visible-preview state.
- `center_reference_activated` starts the measured interval.
- `movement_onset` is accepted only when its supplied angular velocity meets the frozen P0-R5 start threshold of 8 deg/s.
- Travel Time is raw `movement_onset_timestamp -> resolution_timestamp`. It remains null when onset is absent; no zero or fabricated onset timestamp is written.
- Click/timeout evidence retains Final Precision Error, Center-Hit, hit/miss disposition, and signed horizontal aim error without computing a Performance Score.

## Persistence

Migration 5 adds nullable `shots.preview_timestamp`.

| Stored field | Far Flick meaning |
|---|---|
| `preview_timestamp` | Time the preview target became visible |
| `spawn_timestamp` | Center-reference activation; authoritative start of the Travel-Time contract |
| `first_mouse_movement_timestamp` | First verified movement-onset timestamp, or `NULL` |
| `resolution_timestamp` | Click or timeout resolution time |

The later Phase 5 scoring fallback may use the accepted timeout ceiling when raw movement onset is null, but P4-R2 deliberately preserves that raw null.

## Verification

- Unity EditMode: **129/129 passed**.
- Windows production build: passed.
- Calibration regression: **72/72 passed**.
- `git diff --check`: passed.

## Deferred work

Live arena/input binding and combined four-mode battery flow remain P4-R5. Scoring, fallback aggregation, Grade, fatigue, outliers, and Winner selection remain Phase 5 scope.
