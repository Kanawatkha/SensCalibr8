# Phase 0 Calibration Protocol and Acceptance Matrix

## Document Control

| Field | Value |
|---|---|
| Protocol ID | `sc8-p0-r1-protocol-v1` |
| Project | SensCalibr8 |
| Owning round | `P0-R1` |
| Status | Accepted for calibration-harness implementation |
| Scope | Phase 0 calibration evidence only |
| Production authorization | None; production Test Engine and scoring remain blocked |

## 1. Purpose

This protocol defines how SensCalibr8 calibration evidence must be identified, collected, controlled, retained, reviewed, and accepted. It is the input contract for `P0-R2` and the acceptance framework for measured outputs produced in `P0-R3` through `P0-R7`.

This document deliberately does not invent sampling rates, tolerances, geometry dimensions, Tracking duration/speed, normalization bounds, tier cutpoints, scoring-zero tolerance, repeat counts, or confirmatory sample sizes. Those values must be established by the owning Phase 0 round from recorded evidence and then frozen in an immutable calibration configuration.

## 2. Authority and Non-Negotiable Constraints

The following constraints come from the authoritative root documents and apply to every calibration run:

- Use Unity's Input System raw mouse delta; legacy `Input.GetAxis` is forbidden.
- Use a high-resolution timestamp source; frame-count-derived timing and `Time.time` are forbidden for latency measurements.
- Raw mouse events are timestamped independently from render frames.
- The production frame policy is fixed Target Frame Rate with adaptive VSync disabled; calibration must record and verify the tested frame/display state.
- Raw samples are append-only evidence. Filtering, resampling, angular velocity, and all other derived data must be stored separately and must never overwrite raw data.
- Approved signal constants are validated, not re-selected: fifth-order Butterworth SOS, 7 Hz cutoff, 8/4 degrees-per-second start/end thresholds, and 80 ms refractory period.
- Target size and distance are calibrated jointly under the Fitts's Law requirement.
- World geometry, camera, FOV, frame rate, crosshair size, target geometry, Tracking contract, normalization bounds, and confirmatory contract remain production-blocking until accepted and versioned.
- Grip style and movement strategy may be recorded as environmental descriptors but must not influence scoring or calibration acceptance.

## 3. Calibration Evidence States

Every configuration value and evidence package uses one of these states:

| State | Meaning |
|---|---|
| `draft` | Procedure, capture plan, or artifact is incomplete and cannot support a decision. |
| `candidate` | Structurally valid evidence exists, but repeatability or field-specific acceptance has not passed. |
| `accepted` | All predeclared evidence and acceptance rules passed; the value may enter the next configuration candidate. |
| `rejected` | Evidence failed a structural, environmental, integrity, or analytical rule. It is retained with the reason. |
| `superseded` | Previously accepted evidence was replaced by a newer approved version. Historical files remain immutable. |

Only `accepted` values may be assembled into `calibration_config_v1`. A complete configuration remains a candidate until every required field has accepted evidence.

## 4. Environment Inventory Contract

One immutable environment manifest identifies the conditions under which traces were captured. A new manifest and `environment_id` are required whenever a required field changes.

### 4.1 Runtime and Build

- Operating-system platform, version, build, architecture, and timezone.
- Computer identifier suitable for local audit; do not include a person's name.
- Unity Editor version, Unity Player/build identifier, scripting backend, build type, and executable checksum.
- Unity Input System package version and input-update mode.
- Calibration harness version and checksum.
- Analysis runtime and dependency versions used to derive candidate values.

### 4.2 Display and Rendering

- Display identifier/model, active resolution, native resolution when known, refresh rate, scaling, orientation, and primary-display status.
- Window/display mode, render resolution, Target Frame Rate candidate, VSync/adaptive-sync state, and measured frame-timing source.
- GPU and driver version when the operating environment exposes them.

### 4.3 Input Device

- Mouse manufacturer/model, connection type, firmware version, USB path or hub use, and power state where applicable.
- DPI value and its evidence source: manufacturer software/manual setting or Physical Ruler Test.
- Configured polling rate and its evidence source; do not treat an advertised setting as a measured event rate.
- Operating-system pointer settings and acceleration state for audit, even though the harness must consume raw delta.
- Mousepad dimensions/surface and any other physical condition intentionally held constant.

### 4.4 Operating Conditions

- Active power plan, foreground/focus state, relevant background load, thermal/power anomalies, and network-independent/offline state.
- Operator identifier, dominant hand, grip/movement descriptor, posture/setup notes, and any warm-up procedure used.
- Local date/time and monotonic run timestamps.

Unknown mandatory fields are permitted while preparing `P0-R2`, but they block production-quality trace collection in `P0-R3` and later. The environment manifest must say `unknown` explicitly; blank values are not acceptable.

## 5. Identifier and Artifact Naming Contract

Identifiers must be stable, machine-readable, and must not contain profile names or other personal data.

| Identifier | Required form and role |
|---|---|
| `protocol_id` | Immutable protocol version, such as `sc8-p0-r1-protocol-v1`. |
| `environment_id` | Identifies one complete environment manifest. |
| `capture_plan_id` | Identifies the frozen condition grid and planned repeats for one calibration experiment. |
| `condition_id` | Identifies one exact parameter combination in the capture plan. |
| `run_id` | Identifies one independent execution of one condition. |
| `trace_id` | Identifies one append-only raw trace belonging to one run. |
| `analysis_id` | Identifies one versioned derivation from immutable source traces. |
| `acceptance_record_id` | Identifies the review decision for a candidate field/configuration. |

Artifact filenames use this token order:

```text
sc8_<protocol-id>_<environment-id>_<condition-id>_<run-id>_<artifact-type>.<extension>
```

Rules:

- Use lowercase ASCII letters, digits, hyphens inside IDs, and underscores only between filename tokens.
- A run ID includes a UTC timestamp and repetition ordinal generated by the harness.
- Raw and derived files never share the same artifact type.
- Renaming an evidence file after hashing is forbidden. A changed artifact receives a new ID and hash.
- Every artifact appears in the integrity manifest with byte size, cryptographic hash, creation timestamp, producer version, and source IDs.

## 6. Evidence Package Contract

Each run must produce or reference the following artifacts. Formats are selected in `P0-R2`, but their semantic content is fixed here.

| Artifact | Minimum required content |
|---|---|
| Environment manifest | All Section 4 fields, explicit unknowns, environment ID, and capture timestamp. |
| Capture plan | Condition grid, parameter candidates, planned repeat count, execution order, controlled variables, and predeclared acceptance owner. |
| Run manifest | Run/condition/environment/harness IDs, operator notes, start/end timestamps, completion state, and deviations. |
| Raw mouse events | Trace ID, run ID, monotonically ordered sample index, high-resolution event timestamp, raw delta X/Y, and input-device identity. |
| Frame timing | Frame index, high-resolution timestamp, frame duration, focus state, and frame/display configuration identity. |
| Target/camera events | Target lifecycle, target condition, camera/angular state, test event timestamps, and links to the run/trace. |
| Derived signal artifact | Source trace hashes, resampling parameters, filtered angular trace, angular velocity, event detections, and analysis version. |
| Analysis report | Diagnostics, plots/tables, candidate value, uncertainty/stability evidence, rejected-run sensitivity, and source trace list. |
| Acceptance record | Field reviewed, decision state, evidence IDs, acceptance/rejection rationale, reviewer, and version destination. |
| Integrity manifest | Every artifact ID, relative path, byte size, hash, producer version, and source relationship. |

## 7. Controlled Calibration Procedure

### 7.1 Prepare the Capture Plan

1. Select exactly one owning calibration question, such as input timing, geometry, Tracking contract, or normalization bounds.
2. Define the candidate conditions and controlled variables before capture.
3. Set and freeze the planned repeat count and execution order in the capture plan. P0-R1 does not assign an unsupported universal repeat count.
4. Declare the evidence and acceptance rows from Section 10 that the experiment will satisfy.
5. Mark the plan immutable once capture begins. A change creates a new capture-plan ID.

### 7.2 Preflight the Environment

1. Load the environment manifest and reject blanks in mandatory fields.
2. Verify mouse identity, DPI evidence, polling setting, display state, focus, power state, harness version, and capture-plan identity.
3. Verify use of the approved raw-input and high-resolution timing paths.
4. Verify adaptive VSync is disabled for a run intended to validate production timing.
5. Record all deviations before the first event. Do not correct metadata after seeing results without creating an amended manifest and audit record.

### 7.3 Execute One Run

1. Start a new run with clean in-memory capture state and unique IDs.
2. Execute only the selected condition script; do not mix unrecorded parameter changes in one run.
3. Capture raw input, frame timing, target/camera events, focus transitions, and operator annotations concurrently.
4. If an interruption occurs, finalize the partial artifact as rejected or quarantined; never silently resume under the same run ID.
5. Close all artifacts, calculate integrity hashes, and finalize the run manifest before reviewing performance.

### 7.4 Repeatability

- A repeat is an independent run with a new run ID and reset harness state, not a copy or slice of an existing trace.
- Every candidate condition requires repeated valid runs according to the frozen capture plan; one run cannot demonstrate repeatability.
- The execution order must be recorded and counterbalanced or randomized when order can affect the measured result.
- Failed runs remain in the evidence package and do not silently reduce the planned repeat count. Replacement runs receive new IDs and explicit links to the failed run.
- Acceptance requires all planned valid evidence and agreement under the field-specific tolerance defined by the owning Phase 0 round. Until that tolerance exists, results remain `candidate`.

### 7.5 Derive and Review

1. Verify integrity hashes before analysis.
2. Derive data into new versioned artifacts without modifying raw files.
3. Apply only predeclared calculations and approved constants from `RESEARCH.md`.
4. Report every included, rejected, and quarantined run and rerun the summary with rejected-run disposition visible.
5. Create an acceptance record. Do not promote a value solely because a plot appears plausible.

## 8. Trace Rejection and Quarantine Rules

Raw evidence is never deleted. The disposition determines whether it may support an accepted value.

### 8.1 Structural Rejection

Reject a run when any of the following is true:

- Required manifest, raw-event, timing, or integrity artifact is missing.
- IDs cannot establish an unambiguous environment -> plan -> condition -> run -> trace relationship.
- Raw timestamps are missing or not monotonically ordered, or sample ordering cannot be reconstructed without altering evidence.
- Artifact hash/size does not match the integrity manifest.
- Capture ended without a finalized completion or interruption state.
- Raw data was overwritten, smoothed, aggregated in place, or derived from a forbidden input/timer API.

### 8.2 Environmental Quarantine

Quarantine a run pending review when:

- DPI, polling setting, mouse identity/path, resolution, refresh/display mode, frame policy, focus, or harness version differs from the capture plan.
- The application loses focus, changes display mode, changes power state unexpectedly, or reports a material background/thermal anomaly.
- Operator intervention or an undocumented setup change occurs during capture.
- The environment contains an explicit `unknown` for a field required by the owning acceptance row.

### 8.3 Analytical Non-Acceptance

Retain the run as structurally valid but do not accept its candidate value when:

- Event timing does not meet the tolerance later frozen by `P0-R3`.
- The trace is too short for the approved forward/backward filter edge contract.
- Repeated runs do not satisfy the predeclared stability/reproducibility rule.
- Geometry, Tracking, normalization, tier, or confirmatory evidence fails its owning acceptance row.

Statistical extremeness alone is not a structural/data-quality rejection. It must be reported and investigated separately.

## 9. Change and Version Control

- Protocol, environment, capture plan, harness, analysis, and acceptance versions are independent.
- Any change that can alter recorded or derived values requires a new relevant version and new evidence.
- Cosmetic documentation corrections that cannot alter interpretation may retain the protocol version but require a dated change note.
- Accepted evidence is never silently reinterpreted under a new analysis/configuration version.
- `calibration_config_v1` is immutable after first production use and cannot reference draft, rejected, quarantined, or incomplete evidence.

## 10. Acceptance Matrix

| Calibration item | Owning round | Required evidence | Acceptance rule | Reject / remain candidate when | Versioned destination |
|---|---|---|---|---|---|
| Protocol and evidence contract | P0-R1 | This protocol, initialized environment inventory, coverage review | Scope, IDs, procedure, repeat policy, invalidation rules, evidence states, and downstream field ownership are complete without unsupported constants | Any required evidence path or production blocker is undefined | `protocol_id` |
| Runtime/environment identity | P0-R1 / P0-R2 preflight | Environment manifest and integrity record | Every required field is populated or explicitly unknown; all fields needed by the active capture row are known before capture | Blank field, untracked environment change, or ambiguous device/display identity | `environment_id` |
| Harness evidence integrity | P0-R2 | Harness build/version, raw/frame/target fixtures, interruption fixture, hashes | Harness produces append-only linked artifacts, preserves raw data, and exposes failures without scoring | Raw overwrite, missing linkage, forbidden API, unreported interruption, or unverifiable artifact | `harness_version` |
| Input sampling rate and stability | P0-R3 | Repeated raw-event traces and interval diagnostics | One measured event-rate policy and stability/tolerance contract is supported across the frozen capture plan | Rate is inferred from render frames, timing is unstable, or repeat evidence does not agree | `input_sampling_rate_hz`, `resampling_tolerance_ms`, `signal_pipeline_version` |
| Resampling and edge handling | P0-R3 | Raw/derived trace pairs, boundary diagnostics, short/invalid traces | Uniform resampling and edge policy preserve event timing and identify traces unsuitable for filtering | Silent interpolation across invalid gaps, phase/timing distortion, or undefined short-trace behavior | `signal_pipeline_version` |
| Approved signal response | P0-R5 | Representative raw/filtered traces and detected-event overlays | Approved fifth-order/7 Hz/8-4 degrees-per-second/80 ms behavior identifies movements reproducibly without changing the authorized constants | Trace behavior is not reproducible or implementation deviates from approved constants | `butterworth_order`, `cutoff_frequency_hz`, `submovement_start_deg_per_sec`, `submovement_end_deg_per_sec`, `refractory_period_ms`, `signal_pipeline_version` |
| Arena/camera/display geometry | P0-R4 | Render captures, geometry manifest, frame diagnostics, repeated condition runs | Fixed arena, camera, FOV, render/display policy, and frame setting reproduce the same condition identity | Unversioned geometry/display change or repeat mismatch | `target_geometry_json`, `test_geometry_version` |
| Target/Fitts condition matrix | P0-R4 | Small/Medium/Large size and distance matrix, visual-angle/ID derivation, spawn diagnostics | Size and distance vary jointly, are distinguishable, fit the valid arena, and reproduce from the same condition ID | Distance-only variation, clipping/overlap, ambiguous units, or nondeterministic condition reconstruction | `target_geometry_json`, `test_geometry_version` |
| Crosshair and Center-Hit geometry | P0-R4 | Crosshair/target render captures and geometric derivation | Dot style/size and Center-Hit zone are fixed/versioned; user choice is color only and remains high contrast | Style/size is user-editable, center zone is undefined, or contrast condition is not controlled | `target_geometry_json`, `test_geometry_version` |
| Tracking contract | P0-R5 | All required pattern traces, speed/duration/trial candidates, completion diagnostics | Linear, curved, and variable-speed conditions have reproducible speed/duration/trial and valid aggregation windows | Missing pattern, unstable target motion, undefined completion, or invalid windowing | `tracking_contract_json`, `config_version` |
| Normalization bounds | P0-R6 | Mode/metric distributions, clipping/coverage diagnostics, sensitivity analysis | Fixed lower/upper bounds are justified, `U > L`, directions are correct, and current production data cannot redefine them | Dynamic/current-set bounds, invalid order, excessive unexplained clipping, or missing mode/metric coverage | `normalization_bounds_json`, `normalization_version` |
| Submovement bounds | P0-R6 | Valid post-adaptation count distributions by applicable mode and mapping diagnostics | Mode-specific lower/upper bounds are justified, ordered, capped to the approved output range, and versioned | Missing applicable mode, invalid ordering, or post-hoc dynamic remapping | `submovement_bounds_by_mode_json`, `normalization_version` |
| Consistency tiers | P0-R6 | Normalized Consistency distributions and boundary diagnostics | Fixed ordered cutpoints cover the normalized score without gaps/overlap and remain independent of the current comparison set | Dynamic ranks, gaps/overlap, or missing mode contract | `consistency_tier_cutpoints_json`, `normalization_version` |
| Scoring-zero tolerance | P0-R6 | Numeric precision and near-zero CV diagnostics | One tolerance is justified for the approved score precision and forces undefined/non-passing CV near zero | Silent zero substitution, raw-SD fallback, or platform-dependent behavior | `scoring_zero_tolerance`, `normalization_version` |
| Confirmatory contract | P0-R6 | Matched-block design, fresh-data proof, pairing audit, power/sample rationale, CI method validation | Pairing/sequence is controlled, exploratory data is not reused, and sample/CI procedure is frozen before production testing | Unpaired/reused evidence, post-result sample choice, or incomplete audit fields | `confirmatory_contract_json`, `config_version` |
| Complete Calibration Configuration v1 | P0-R7 | All accepted rows, immutable package, regression fixtures, research update | Every `calibration_configs` field is populated from accepted evidence and fixtures reproduce the package | Any draft/unknown/unversioned field, failed fixture, or missing evidence link | `config_version = calibration_config_v1`, `created_date` |

## 11. P0-R1 Exit Checklist

- [x] Calibration scope and production prohibition are explicit.
- [x] Environment inventory fields and unknown-field behavior are defined.
- [x] Stable identifiers, trace naming, integrity metadata, and evidence relationships are defined.
- [x] Controlled preparation, preflight, capture, repeat, derivation, and review procedures are defined.
- [x] Structural rejection, environmental quarantine, and analytical non-acceptance are separated.
- [x] Every required `calibration_configs` output has an owning round and acceptance evidence.
- [x] Raw evidence is append-only and derived evidence is versioned separately.
- [x] No unsupported measured constant, threshold, sample size, or repeat count was invented.
- [x] The `P0-R2` harness contract is sufficiently defined to begin implementation.

## 12. P0-R2 Handoff

`P0-R2 — Minimal calibration harness` must implement the evidence package and failure behavior defined here. It may create instrumentation, manifests, fixtures, and append-only export only. It must not implement production scoring, Winner selection, production protocol progression, or calibrated production target behavior.

Before the first production-quality trace in `P0-R3`, the operator must complete all mandatory manual environment fields, and the harness must freeze a capture plan containing its condition grid and planned repeat count.
