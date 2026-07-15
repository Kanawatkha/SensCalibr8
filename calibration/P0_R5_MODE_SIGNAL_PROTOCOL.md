# P0-R5 Mode and Signal-Response Calibration

## Document Control

| Field | Value |
|---|---|
| Owning round | `P0-R5` |
| Protocol authority | `sc8-p0-r1-protocol-v1` |
| Timing dependency | `sc8-p0-r3-timing-contract-v1` |
| Geometry dependency | `sc8-test-geometry-v1` |
| Candidate | `sc8-p0-r5-signal-mode-candidate-v1` |
| Accepted contract | `sc8-p0-r5-signal-mode-contract-v1` |
| Current state | Completed and accepted on 2026-07-15 |
| Operator testing | None required; use deterministic angular fixtures and existing accepted timing/geometry contracts |
| Production authorization | Signal/mode component authorized; P0-R6/P0-R7 dependencies remain blocked |

## 1. Objective and Scope

P0-R5 validates the already approved fifth-order 7 Hz Butterworth signal pipeline at the accepted 1000 Hz processing grid and freezes reproducible behavior for Close Flick, Far Flick, Tracking, and Micro-Correction. It owns filter coefficients/edge behavior, angular-velocity event semantics, spawn/activation timing, Tracking paths and sample contract, mode completion, deterministic condition balancing, and mode-specific trace acceptance.

It does not calibrate normalization bounds, Submovement penalty bounds, Consistency Grade cutpoints, score-zero tolerance, or confirmatory statistical sample size. Those remain P0-R6.

## 2. Authority and Reconciliation

The filter order, cutoff, start/end thresholds, refractory period, and 50% adaptation rule are fixed requirements, not candidate values to optimize. The underlying study specifies a fifth-order 7 Hz low-pass filter, 8 degrees/second movement onset, 4 degrees/second movement end, and merging detections within an 80 ms refractory period. It also used a 1.5-second maximum completion time for failed stationary-target trials. Source: [Boudaoud, Spjut, and Kim, IEEE CoG 2022](https://ieee-cog.org/2022/assets/papers/paper_64.pdf), Sections III-C and IV-D.

SciPy documents `output='sos'` as the numerically preferred representation for general-purpose filtering and defines `sosfiltfilt` as forward-backward SOS filtering with odd extension by default. Its default pad-length formula yields 18 samples for this odd fifth-order design. Sources: [SciPy `butter`](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.butter.html) and [SciPy `sosfiltfilt`](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.sosfiltfilt.html).

`FEATURES.md` previously said every mode varies Small/Medium/Large, while the specific Micro-Correction definition and accepted `sc8-test-geometry-v1` require a Small target. P0-R5 preserves the later immutable geometry contract: Micro-Correction is the explicit Small-only exception; Close Flick, Far Flick, and Tracking retain size variation. This reconciliation must be reflected in `FEATURES.md` before P0-R5 closes.

## 3. Signal Contract

1. Validate the P0-R3 timing contract before signal processing.
2. Split detected receipt gaps; never interpolate or filter across a gap.
3. Convert each valid segment to uniformly sampled cumulative angular azimuth/elevation at 1000 Hz.
4. Reject signal derivation for segments shorter than 20 samples; retain their raw evidence and do not use a one-pass fallback.
5. Filter azimuth and elevation independently with the frozen SOS coefficients, odd padding, 18-sample pad length, and forward-backward application.
6. Compute interval angular-velocity magnitude as `hypot(delta_azimuth, delta_elevation) x 1000`.
7. Start an event when velocity changes from below 8 deg/s to greater than or equal to 8 deg/s. End it at the first later sample strictly below 4 deg/s.
8. If the next onset occurs less than 80 ms after the preceding end, merge it into the same Submovement. Exactly 80 ms begins a separate Submovement.
9. Do not introduce the paper's unspecified “minimum duration” as a hidden threshold. The approved project contract contains no such value; filtering, hysteresis, and refractory merging are the only event-shaping rules.

## 4. Mode Contracts

### 4.1 Shared Shot-Based Contract

- Close Flick, Far Flick, and Micro-Correction complete exactly 30 resolved target opportunities for the minimum Phase 1 session.
- Each opportunity permits one click. A hit, miss-click, or 1.5-second timeout resolves the opportunity; timeout is a miss.
- Adaptation finalization occurs only after the session and marks the first 50% (15 of 30) as adaptation trials.
- Every post-adaptation hit must have a filter-eligible signal segment. A miss stores null Submovement Count with an explicit outcome reason; it must never be converted to a favorable zero penalty.
- Close/Far each use three complete 3 x 3 size-by-distance blocks (27 trials) plus three rotating conditions. Therefore every condition occurs three or four times in one session, and the extra conditions rotate by battery repetition.
- The deterministic sequence seed excludes sensitivity and blind candidate label, ensuring compared sensitivity values receive the same task conditions.

### 4.2 Close Flick

The target is hidden until a deterministic randomized foreperiod from 500-1000 ms completes. Reaction Time starts when the target becomes visible and ends at click/timeout. This preserves a high-throughput sequence while preventing a fixed spawn rhythm. Positions use the P0-R4 Close offsets and all three target sizes.

### 4.3 Far Flick

A center reference and inactive preview establish the start state. Clicking the reference activates the Far target, following the source study's reference-target method. Reaction Time is activation to the first 8 deg/s movement onset; Travel Time is that onset to click. This keeps Travel Time separate from visual reaction/initiation. Missing onset produces null Travel Time and a retained miss/data-quality review record rather than a fabricated zero.

### 4.4 Micro-Correction

A center reference activates one Small target at a deterministic uniformly sampled radial/directional offset within the source-authorized 5-20 px range. The Small-only rule is an explicit exception to generic size variation because this mode measures fine correction and `sc8-test-geometry-v1` already freezes that geometry.

### 4.5 Tracking

- Two balanced blocks are required. Each block contains the complete 3 patterns x 3 target sizes cross-product (9 trials); block one is adaptation and block two is authoritative.
- Every trial lasts 6 seconds and is divided into six exact, non-overlapping 1-second metric windows. The contract therefore yields 18 trials total, 9 post-adaptation trials, and 54 post-adaptation metric windows.
- Linear: horizontal triangle-wave motion over +/-15 degrees at 15 deg/s, giving a derived 4-second round trip.
- Curved: ellipse with 15-degree horizontal and 10-degree vertical radii, one analytic lap in 6 seconds.
- Variable speed: horizontal sinusoid with 15-degree amplitude and 4-second period.
- Target state is evaluated analytically from high-resolution elapsed time; never integrate position per render frame.
- Time-on-Target is interval-weighted duration with radial center error less than or equal to target angular radius.
- Tracking Deviation is interval-weighted RMS radial center error in degrees for each 1-second window. Tracking Consistency later uses the sample SD of these post-adaptation window values.
- Frame samples must have strictly increasing timestamps and cover the exact trial boundaries. Metric intervals are clipped to `[0,1)`, `[1,2)`, ..., `[5,6]` so frame-count variation does not change duration weighting.

## 5. Session Acceptance

A session is accepted only when timing, geometry, signal, and mode version identities are immutable; native borderless fullscreen/focus/display state remains unchanged; high-resolution timestamps are strictly increasing; P0-R3 timing gates pass; raw and derived data remain separate; the condition sequence reproduces from its recorded seed; mode completion passes; and adaptation flags are finalized transactionally after capture.

Receipt burst/gap fractions and render-frame interval distributions remain diagnostics. Gaps still split signal segments and may make a hit signal-ineligible, but they are not silently bridged or erased.

## 6. Automated Acceptance Matrix

| Gate | Required result |
|---|---|
| Dependency identity | Candidate references the accepted P0-R3 timing and P0-R4 geometry versions |
| SOS design | Three stable sections, unity DC gain, fifth order, 7 Hz critical point at 1000 Hz |
| Edge handling | Generated odd-order SOS yields pad length 18 and minimum segment 20 |
| Zero phase | Forward-backward impulse/transition fixture has no measurable peak displacement beyond one 1000 Hz sample |
| Threshold boundaries | `>= 8` starts; `< 4` ends; below-threshold fixture produces no event |
| Refractory boundary | Separation below 80 ms merges; exactly 80 ms remains separate |
| Gap/short segment | No filtering across gaps; <20 samples is retained but ineligible |
| Shot completion | 30 resolved trials; exactly 15 adaptation and 15 authoritative trials |
| Close/Far balance | Nine conditions each occur 3-4 times; rotating extras are deterministic |
| Sensitivity blindness | Sequence identity is unchanged when only sensitivity/candidate label changes |
| Tracking balance | Each block contains every pattern-by-size pair exactly once |
| Tracking duration/windows | 18 x 6-second trials; 54 authoritative 1-second windows |
| Tracking paths | Analytic bounds remain within P0-R4 safe geometry and repeat exactly |
| Tracking metrics | Interval weighting is invariant to equivalent irregular frame partitions |
| Unity parity | Plain C# path/event/metric fixtures match Python evidence |
| Reproducibility | Same immutable candidate produces byte-identical evidence |

## 7. Evidence Workflow

1. Validate the candidate and both immutable dependency contracts.
2. Generate canonical SOS coefficients and deterministic signal fixtures.
3. Derive every path, balance count, timing count, and acceptance gate into a new evidence JSON.
4. Run Python unit/reproducibility tests.
5. Add plain C# parity implementations and Unity EditMode tests; no production Test Engine behavior is authorized.
6. If every gate passes, write a new accepted immutable contract, update the authoritative Markdown files, and advance to P0-R6.

## 8. Acceptance Result

P0-R5 is accepted under signal version `sc8-signal-pipeline-v1`, mode version `sc8-mode-contract-v1`, and combined contract `sc8-p0-r5-signal-mode-contract-v1`.

| Verification | Result |
|---|---|
| Dependency identity | Passed against accepted P0-R3 timing and P0-R4 geometry hashes |
| Signal derivation | Passed; three stable SOS sections, DC gain 1.0, single-pass 7 Hz gain 0.7071067811866065, forward-backward 7 Hz gain 0.5000000000000835 |
| Edge/phase response | Passed; pad length 18, 20-sample minimum, zero impulse-peak displacement |
| Threshold/refractory semantics | Passed for below/start/end, less-than-80-ms merge, and exact-80-ms separation fixtures |
| Shot completion/balance | Passed; 30 trials, 15 adaptation, nine conditions represented 3-4 times |
| Tracking contract | Passed; 18 trials, balanced pattern-size blocks, analytic paths inside geometry, 54 authoritative windows |
| Metric invariance | Passed for equivalent regular and irregular frame partitions |
| Python tests | Passed 44/44 total; 15 P0-R5-specific tests |
| Unity EditMode parity | Passed 27/27 total on Unity 6000.5.3f1; 8 P0-R5-specific tests |
| Operator polling/aim rerun | Not required |

Authoritative artifacts:

- Accepted contract: `calibration/plans/p0-r5-signal-mode-accepted-v1.json`
- Candidate: `calibration/plans/p0-r5-signal-mode-candidate-v1.json`
- Derived evidence: `calibration/evidence/p0-r5/p0-r5-signal-mode-derived-v1.json`
- Unity result: `calibration/evidence/p0-r5/p0-r5-unity-editmode-results-v1.xml`

The installed pinned Python runtime has NumPy but not SciPy. P0-R5 therefore derives the canonical Butterworth poles/SOS in-project from the analog prototype and bilinear transform, freezes the accepted coefficients, and validates their documented response and pad-length behavior independently in Python and plain C#. Production has no SciPy runtime dependency and must load the frozen coefficients from configuration.
