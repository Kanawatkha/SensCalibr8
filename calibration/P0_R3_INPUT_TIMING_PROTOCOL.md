# P0-R3 Input and Timing Calibration

## Document Control

| Field | Value |
|---|---|
| Owning round | `P0-R3` |
| Protocol authority | `sc8-p0-r1-protocol-v1` |
| Analysis version | `p0-r3-timing-analysis-v3` |
| Current state | Completed by explicit project-owner calibration waiver after Confirmation v4; strict candidates remain rejected |
| Production authorization | Timing component accepted as `sc8-signal-pipeline-timing-v1`; P0-R5/P0-R7 gates still apply |

## 1. Objective

Measure the raw input-event cadence on the frozen target environment, define a reproducible uniform-resampling contract, and validate timestamp/gap/filter-edge behavior without altering raw evidence. This round owns `input_sampling_rate_hz`, `resampling_tolerance_ms`, and the timing/edge portion of `signal_pipeline_version`.

## 2. Non-Negotiable Rules

- Use Unity Input System event timestamps and independent high-resolution monotonic timestamps. Render-frame cadence is never an input-sampling-rate proxy.
- Disable Unity Input System redundant mouse-event merging before every acceptance capture, record the setting in the environment manifest, and reject any package where it is not explicitly `true`. Accumulated per-frame deltas are not physical-cadence evidence.
- Every raw trace remains append-only and hash-verified. Diagnostics and resampled signals are new artifacts and never replace source JSONL.
- Pilot observations may propose values but cannot accept themselves. Freeze a candidate contract, then test it using fresh independent runs.
- A backward or duplicate input-event timestamp fails confirmation. Receipt bursts and missing-cadence gaps are reported, bounded by the predeclared pilot envelope, and gaps are never bridged by interpolation.
- Filtering is not run on a segment unless its uniform-grid length satisfies the frozen edge contract.
- No acceptance-bearing capture may contain an operational field required by the active plan whose value is blank or `unknown`. Manufacturer, model, firmware, posture, and other audit-only descriptors may remain unavailable and must not block capture or affect scoring.

Microsoft documents Raw Input as direct HID input and notes buffered processing for high-frequency devices. `RAWMOUSE` data is not affected by Control Panel mouse-speed settings. These properties support the selected raw-event evidence path; the observed device cadence is still measured rather than inferred from a configured/advertised polling rate. Sources: [Microsoft Raw Input overview](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input), [Microsoft RAWMOUSE](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse).

## 3. Frozen Capture Plan

Create one immutable plan for pilot runs and a second immutable plan for fresh confirmation. The plan must be complete before its first run and must include:

- one environment ID, harness/build checksum, condition ID, input device/path, DPI and configured polling evidence;
- trace duration greater than zero, planned independent repeat count, repetition order, and a single controlled motion instruction;
- display/focus/power/background-load controls and all operator-only fields in `ENVIRONMENT_INVENTORY.md`;
- acceptance owner and explicit evidence state (`pilot` or `confirmation`).

The repeat count must be at least two because `P0_R1_CALIBRATION_PROTOCOL.md` explicitly states that one run cannot establish repeatability. The project owner approved the P0-R3-specific plan below on 2026-07-15; these values are calibration evidence-collection settings, not production aim-test sample contracts.

| Approved item | Frozen value |
|---|---|
| Pilot repeats | 5 independent runs |
| Fresh confirmation repeats | 5 independent runs |
| Trace duration | 30 seconds per run |
| Controlled motion | Continuous figure-eight motion at a comfortable steady pace, without lifting the mouse |
| Hardware DPI | 1600, manually confirmed from SIGNO software |
| Configured polling rate | 1000 Hz, manually confirmed; measured cadence remains authoritative |
| Connection evidence | User reports Bluetooth; Windows independently exposes `VID_1D57&PID_FA60` through a USB HID composite path. Preserve both observations without silently choosing one. |
| Menu/test display | 960 x 540 resizable window; native-resolution borderless fullscreen during capture |
| Controls | F11 optional fullscreen toggle outside capture; Escape interrupts active capture and returns windowed |
| Runtime/network | Standalone offline executable; no Unity Editor acceptance capture |

Acceptance artifacts use the protocol-defined full filename token order under the short local root `%LOCALAPPDATA%\SC8\P0R3`. The root is intentionally bounded so the complete protocol/environment/condition/run/artifact filename remains below the Windows legacy path limit on the target host; manifest relationships and integrity hashes remain unchanged.

The calibration device does not define universal product compatibility. Production measures actual event cadence per session and resamples accepted traces to the canonical processing grid under the active `signal_pipeline_version`. Mouse manufacturer/model/firmware are optional audit metadata; Hardware DPI remains the required manual user input.

During each run, keep the application focused and move the selected mouse continuously using the exact motion instruction recorded in the plan. A stopped mouse does not emit a complete polling-cadence trace and cannot be interpreted as dropped events. Any lift, focus change, device reconnect, plan deviation, or environmental change is retained and quarantined.

## 4. Pilot Diagnostics

For every integrity-verified raw trace:

1. require contiguous sequence numbers, one run/trace/device identity, and nondecreasing high-resolution timestamps;
2. compute positive input-event intervals;
3. report median interval and its reciprocal event rate, median absolute deviation (MAD), minimum, maximum, p95, p99, duplicate count, and reverse count;
4. against the predeclared configured nominal cadence, classify positive intervals by their nearest non-negative cadence count: zero is a receipt burst, one is a normal interval, and two or more are scheduling/missing-cadence gaps;
5. report burst/gap fractions and p95/p99 residuals for normal single-cadence intervals;
6. aggregate only independent run/trace IDs that share the same device, environment, capture plan, and condition.

Median and MAD are used as robust location/spread diagnostics; they do not silently remove observations. Source: [NIST Engineering Statistics Handbook, measures of scale](https://www.itl.nist.gov/div898/handbook/eda/section3/eda356.htm).

The aggregate reports the measured median event rate, but the canonical processing grid remains the predeclared configured device rate. QPC timestamps measure user-space `WM_INPUT` receipt, so short dispatch bursts and long scheduling gaps are not evidence that the physical device has a new non-integer polling rate.

Pilot v3 produced five independent native runs. Their median rates ranged from `1001.6024118863011` to `1003.1096158422927` Hz around the configured 1000 Hz rate. Candidate v1 used the raw pilot extrema and maxima as future-run limits. Confirmation v3 proved that method was too brittle and candidate v1 was rejected without changing any source evidence.

Candidate v2 is calculated exclusively from the original five Pilot v3 diagnostics. For each of five predeclared per-run metrics across five future confirmation runs, use the single-future-observation prediction formula:

```text
prediction = pilot_mean +/- t * pilot_sample_SD * sqrt(1 + 1 / pilot_n)
```

There are 25 simultaneous statements. With family-wise `alpha = 0.05`, Bonferroni gives cumulative probability `1 - 0.05 / (2 * 25) = 0.999`; with `df = 4`, the NIST Student-t table gives `t = 7.173`. The median-rate bound is two-sided; MAD, burst fraction, gap fraction, and p99 residual use the resulting conservative upper bounds. The method assumes approximately normal independent per-run summary metrics; the five-run pilot is a documented limitation, and the large corrected t value preserves that uncertainty rather than hiding it. Sources: [NIST prediction limits](https://www.itl.nist.gov/div898/software/dataplot/refman1/auxillar/predlimi.htm), [NIST Bonferroni method](https://itl.nist.gov/div898/handbook/prc/section4/prc473.htm), [NIST Student-t critical values](https://itl.nist.gov/div898/handbook/eda/section3/eda3672.htm).

The v2 candidate remains `1000 Hz`; its p99 single-cadence tolerance is `0.43251822850161387 ms`, with exact calculated prediction bounds and provenance stored in `calibration/evidence/p0-r3/pilot-v3/p0-r3-pilot-v3-prediction-contract-v2.json` and `calibration/plans/p0-r3-timing-contract-candidate-v2.json`. Failed Confirmation v3 values are excluded from every v2 calculation.

## 5. Fresh Confirmation

Confirmation uses a new capture-plan ID and run IDs that do not occur in the pilot contract or any rejected confirmation. The approved contract requires exactly five fresh independent runs with repetition ordinals 1-5. For nominal interval `T = 1 / input_sampling_rate_hz`, each positive observed interval `d` is classified by its nearest non-negative cadence count:

```text
k = floor(d / T + 0.5)
single_cadence_residual = abs(d - T), when k = 1
```

A confirmation run passes only when timestamps are strictly increasing, its median rate is within the frozen pilot range, timing MAD does not exceed the pilot maximum, burst/gap fractions do not exceed their pilot maxima, and its p99 single-cadence residual does not exceed `resampling_tolerance_ms`. Every predeclared confirmation run must pass. Failed or interrupted runs remain visible and are not silently replaced or used to retune the contract.

This distribution-based gate does not pretend that a Windows user-space message pump receives every HID report at perfectly uniform intervals. It requires the fresh runs to reproduce the timing behavior observed in the frozen pilot while preserving every anomaly for gap-safe downstream handling.

## 6. Uniform Resampling and Gap Handling

Raw deltas are converted to cumulative counts before interpolation. Each gap splits the trace into independent segments. Within each segment, cumulative X/Y are linearly interpolated onto the frozen uniform grid; no sample is generated outside the segment's observed time range and the uncovered tail is reported. This follows the defined one-dimensional linear interpolation behavior in [NumPy `interp`](https://numpy.org/doc/stable/reference/generated/numpy.interp.html), with stricter project rules that reject non-increasing input times and prohibit gap bridging.

The approved later filter uses forward-backward SOS filtering. For the actual SOS coefficients generated under the frozen sampling rate, edge length is calculated with SciPy's documented default:

```text
padlen = 3 * (2 * number_of_sos_sections + 1
              - min(count(sos[:, 2] == 0), count(sos[:, 5] == 0)))
minimum_filterable_samples = padlen + 2
```

The `+2` follows SciPy's strict requirement `padlen < sample_count - 1`. Segments below this length remain retained but are marked filter-edge-ineligible; padding, extrapolation, segment joining, or one-pass fallback is forbidden. Source: [SciPy `sosfiltfilt`](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.sosfiltfilt.html).

## 7. Immutable Analysis Workflow

Run commands from the repository root with the pinned Python/NumPy environment:

```powershell
python -m calibration.analysis.p0_r3_timing analyze-run --run-directory <run> --nominal-sampling-rate-hz 1000 --output <new-diagnostic.json>
python -m calibration.analysis.p0_r3_timing aggregate-pilot --diagnostic <diagnostic-1.json> --diagnostic <diagnostic-2.json> --output <new-pilot-report.json>
python -m calibration.analysis.p0_r3_timing derive-prediction-contract --diagnostic <diagnostic-1.json> --diagnostic <diagnostic-2.json> --future-run-count 5 --familywise-alpha 0.05 --student-t-critical-value 7.173 --critical-value-cumulative-probability 0.999 --output <new-prediction-contract.json>
python -m calibration.analysis.p0_r3_timing confirm --contract <frozen-contract.json> --run-directory <fresh-run-1> --run-directory <fresh-run-2> --run-directory <fresh-run-3> --run-directory <fresh-run-4> --run-directory <fresh-run-5> --output <new-confirmation.json>
python -m calibration.analysis.p0_r3_timing resample --contract <frozen-contract.json> --run-directory <run> --output <new-resampled.json>
```

Every output path must be new. The tool refuses overwrite. Confirmation also refuses pilot-run reuse and duplicate run/trace identities.

## 8. Acceptance Checklist

- [x] Timing diagnostics, repeat aggregation, frozen-contract confirmation, and gap-safe resampling implemented.
- [x] Duplicate/reverse/gap handling and filter-edge rule covered by automated fixtures.
- [x] Raw integrity is checked before run-directory analysis; derived output refuses overwrite.
- [x] Standalone offline build created and checksum-verified; operational preflight is implemented and must be confirmed for each run set; audit-only mouse/physical metadata does not block.
- [x] Pilot capture plan frozen, five repeated physical traces collected, and Pilot v3 aggregate generated.
- [x] Candidate v1 tested and rejected transparently; candidate v2 1000 Hz rate, family-wise prediction envelope, p99 single-cadence tolerance, and fifth-order SOS structural `filter_padlen_samples = 18` frozen from Pilot v3 only before Confirmation v4. P0-R5 subsequently verified pad length 18 against the generated accepted SOS coefficients.
- [x] Two fresh five-run confirmation sets retained. Neither strict candidate passed; the project-owner waiver explicitly ends further mouse calibration without rewriting those results.
- [x] Accepted operational values and timing version recorded in `RESEARCH.md`, `ARCHITECTURE.md`, the accepted contract, and `PROGRESS.md`.

P0-R3 is complete under the owner-approved operational policy. This closure accepts a 1000 Hz canonical grid and integrity/modal-cadence safeguards; it is not a statistical pass of candidate v1 or v2 and does not authorize the still-unfinished P0-R5 signal-response or P0-R7 complete calibration configuration.

## 9. Pilot v1 Instrumentation Rejection

The five `sc8-p0-r3-input-timing-pilot-v1` packages collected on 2026-07-15 passed file integrity but measured approximately 144 Hz, matching the active render rate. Unity Input System `1.19.0` package documentation confirms that `FastMouse` events are merged within an update by default unless `InputSystem.settings.disableRedundantEventsMerging = true`. Pilot v1 is therefore retained as `rejected-instrumentation-confound`; it cannot freeze a timing contract. Harness v4 and pilot plan v2 require unmerged events and fresh run IDs.

## 10. Pilot v2 Timestamp-Domain Non-Acceptance

Pilot v2 disabled Unity event merging and retained approximately 31,500 events per 30-second run, but `InputEventPtr.time` remained assigned in frame-sized batches with duplicate/microsecond-separated timestamps. The resulting hundreds-of-kHz median is not physical HID cadence. Pilot v2 is retained as `analytical-non-acceptance-timestamp-domain` and cannot freeze a timing contract.

Pilot v3 uses a dedicated Win32 `WM_INPUT` message pump and records QPC at message receipt, independent from the Unity render/update loop. Acceptance analysis requires `DedicatedRawInputMessagePump=true` and `TimestampSource=win32-wm-input-qpc`. This calibration-only native helper does not authorize a production Test Engine; production integration remains blocked until the P0-R3 contract is confirmed and P0-R7 freezes configuration v1.

## 11. Confirmation v3 Non-Acceptance

Five fresh runs under `sc8-p0-r3-input-timing-confirmation-v3` passed integrity, identity, repetition, timestamp-order, and evidence-state checks. Only one of five passed candidate v1. Four exceeded the frozen median-rate and burst-fraction extrema; two also exceeded the frozen p99 residual. The result is retained at `calibration/evidence/p0-r3/confirmation-v3/p0-r3-confirmation-v3-result.json`, with the decision at `p0-r3-confirmation-v3-non-acceptance.json`.

These runs are not operator failures and are not evidence of a different physical polling rate. They demonstrate that five pilot extrema are not an adequate future-observation envelope. They remain rejected for candidate v1 and may not be reused to accept candidate v2.

## 12. Confirmation v4 Instrumentation

Fresh Confirmation v4 uses `p0-r3-native-harness-v2`, environment `sc8-env-zulartan-wg903-native-v2`, plan `sc8-p0-r3-input-timing-confirmation-v4`, and the same approved physical condition. The only behavioral change from native harness v1 is immutable plan/environment/version identity for the new contract; raw input capture, QPC timestamping, preflight, duration, device selection, and motion instructions are unchanged.

All five Confirmation v4 packages passed integrity, timestamp order, device identity, and ordinal checks but failed candidate v2's strict distribution envelope, primarily because user-space receipt burst fractions exceeded the Pilot-derived bounds. The immutable strict result remains `accepted=false` at `calibration/evidence/p0-r3/confirmation-v4/p0-r3-confirmation-v4-result.json`.

## 13. Project-Owner Waiver Closure

On 2026-07-15 the project owner directed that no further mouse calibration runs be required and authorized progression to P0-R4. The accepted contract is `sc8-p0-r3-timing-contract-v1` under timing version `sc8-signal-pipeline-timing-v1`:

- canonical processing grid: `1000 Hz`;
- cadence interval: `1 ms`;
- partition/resampling tolerance: `0.5 ms`;
- fifth-order SOS structural pad length: `18 samples`, minimum filterable segment `20 samples`;
- hard gates: strictly increasing timestamps, median interval maps to one cadence, and one cadence is the modal interval class;
- receipt bursts/gaps: retained diagnostics, not automatic rejection; gaps always split resampling segments;
- setup/session policy: user enters DPI, current sensitivity, and configured polling rate; production measures actual cadence per session.

The closure decision at `calibration/evidence/p0-r3/p0-r3-owner-waiver-closure.json` explicitly states that neither strict candidate passed. Re-evaluation of Confirmation v4 under the owner-approved operational policy passes 5/5 runs and is retained separately as `p0-r3-owner-waiver-policy-evaluation.json`; it does not alter the strict result.
