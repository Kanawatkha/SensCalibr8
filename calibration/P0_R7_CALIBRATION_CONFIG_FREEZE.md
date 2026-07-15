# P0-R7 Freeze Calibration Configuration v1

## Document Control

| Field | Value |
|---|---|
| Owning round | `P0-R7` |
| Protocol authority | `sc8-p0-r1-protocol-v1` |
| Freeze plan | `sc8-p0-r7-calibration-config-freeze-plan-v1` |
| Frozen contract | `sc8-calibration-config-v1` |
| Configuration version | `calibration_config_v1` |
| Acceptance envelope | `sc8-p0-r7-calibration-config-acceptance-v1` |
| Operator testing | None; deterministic consolidation and parity verification only |

## 1. Objective and Boundary

P0-R7 consolidates the accepted P0-R3 timing, P0-R4 geometry, P0-R5 signal/mode, and P0-R6 scoring/statistical contracts into one complete immutable configuration. This round verifies the Phase 0 exit gate; it does not build the production database, profile system, Test Engine, modes, Winner selection, or UI.

The exact bytes of `calibration/plans/calibration-config-v1.json` are authoritative only when their SHA-256 matches the acceptance envelope. Editing the file under the same version is prohibited. A changed value requires a new configuration version, a complete dependency audit, new evidence, and a new acceptance envelope.

## 2. Source Chain

The freeze plan pins six inputs by path, role, and SHA-256:

1. Accepted P0-R3 timing contract.
2. P0-R3 project-owner waiver closure.
3. Accepted P0-R4 geometry contract.
4. Accepted P0-R5 signal/mode contract.
5. Accepted P0-R6 envelope.
6. Exact P0-R6 payload adopted by that envelope.

The P0-R3 operational contract remains accepted by project-owner calibration waiver. Both strict distribution candidates remain rejected, `strict_confirmation_passed` remains false, and no universal physical 1000 Hz claim is introduced. Production must continue to store configured polling rate as metadata and measure actual cadence per session.

## 3. Database Projection

The frozen `calibration_configs_record` contains exactly all 20 schema fields other than the database-generated `id`:

| Group | Fields |
|---|---|
| Identity | `config_version`, `normalization_version`, `signal_pipeline_version`, `test_geometry_version`, `created_date` |
| Timing/signal scalars | `input_sampling_rate_hz`, `resampling_tolerance_ms`, `timing_acceptance_policy`, `butterworth_order`, `cutoff_frequency_hz`, `submovement_start_deg_per_sec`, `submovement_end_deg_per_sec`, `refractory_period_ms` |
| Scoring scalar | `scoring_zero_tolerance` |
| Canonical JSON contracts | `normalization_bounds_json`, `submovement_bounds_by_mode_json`, `consistency_tier_cutpoints_json`, `target_geometry_json`, `tracking_contract_json`, `confirmatory_contract_json` |

Every value is non-null. JSON columns use sorted-key compact UTF-8 JSON with no NaN and must round-trip exactly. Geometry and signal/mode JSON equal their accepted source contracts; the scoring fields equal the SHA-pinned P0-R6 payload.

`formula_version`, `mode_contract_version`, `consistency_tier_version`, and `confirmatory_contract_version` are also present at the frozen document level. `formula_version` is later copied to each `sensitivity_tests` result as required by the production schema.

## 4. Rejection and Immutability Rules

Production loading must fail closed when any of these conditions occurs:

- config hash does not match the accepted envelope;
- source path, role, or SHA-256 differs;
- status is not `accepted` or `immutable` is not true;
- a required database field is missing, null, non-finite, or invalid;
- embedded JSON is malformed or no longer canonical;
- a scalar differs from its embedded accepted contract;
- a version/dependency identity differs;
- the owner-waiver limitation is removed or relabeled as a strict pass.

The Python builder refuses to overwrite existing frozen outputs. The plain-C# loader verifies the config hash, source manifest, identities, required values, embedded contracts, and scalar bindings before returning a read-only snapshot.

## 5. Regression Matrix

| Gate | Accepted result |
|---|---|
| Required schema projection | 20/20 fields populated |
| Source verification | All six paths and SHA-256 values match |
| Deterministic build | Two independent in-memory builds are byte-identical |
| Embedded JSON | All six JSON fields canonical and exact round-trip |
| PSA worked example | `280 / 1600 = 0.175` exactly |
| Shot score fixture | 77.0 |
| Tracking score fixture | 81.875 |
| Shot formula floor | -10 retained |
| CV fixture | 5.0% |
| Reaction boundaries | S/A/B/C/D edges remain exhaustive |
| Confirmatory fixture | 1024 assignments; minimum p = 0.001953125 |
| Mutation policy | Draft, incomplete, changed scalar, missing field, bad hash, and overwrite rejected |
| Timing limitation | Owner waiver explicit; strict pass remains false |

## 6. Acceptance Result

P0-R7 and the Phase 0 exit gate are accepted.

| Verification | Result |
|---|---|
| Deterministic P0-R7 gates | Passed 20/20 |
| Database fields | Populated 20/20 |
| Python calibration suite | Passed 72/72; 15 P0-R7-specific tests |
| Unity EditMode suite | Passed 41/41; 6 P0-R7-specific tests |
| Unity failed/skipped/inconclusive | 0/0/0 |
| Frozen config SHA-256 | `c618a3e50473b072b107d2e2926f4d05e7bbafa33bc04af8beb5eb5f775b3b2e` |
| Phase 0 exit | Passed |

Authoritative artifacts:

- `calibration/plans/calibration-config-v1.json`
- `calibration/plans/p0-r7-calibration-config-accepted-v1.json`
- `calibration/evidence/p0-r7/p0-r7-calibration-config-derived-v1.json`
- `calibration/evidence/p0-r7/p0-r7-unity-editmode-results-v1.xml`

Phase 1 may now begin. This unblocks the configuration dependency only; it does not mean the production Test Engine already exists.
