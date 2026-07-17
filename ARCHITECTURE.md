# ARCHITECTURE.md

## Purpose

This file defines the technology stack, system design, timer precision requirements, and the full database schema for SensCalibr8. Treat the schema in this file as authoritative — do not add, remove, or rename fields without updating this file first.

---

## 1. Technology Stack

| Layer | Technology | Purpose |
|---|---|---|
| Aim-Test Engine | Unity (C#) | Raw mouse input capture, 3D test scenes, in-app quick charting |
| Data Storage | SQLite | Local relational database; single-file, no server required |
| Deep Analysis | Python (pandas, matplotlib) | Statistical computation, chart generation, HTML report export |
| Documentation | Markdown (.md) | Research reference and rule files consumed by the coding agent during development |

### 1.1 Rationale for Technology Choices

**Unity (C#) over Python for the Test Engine.** Unity's Input System provides direct access to raw mouse delta values, bypassing OS-level cursor acceleration that commonly interferes with Python-based input libraries (e.g. pynput). This is essential for replicating the Raw Input Buffer behavior referenced in the source research.

**SQLite for Storage.** SQLite requires no server setup, supports complex analytical queries, and scales sufficiently for a single-user, long-term dataset spanning months to years of testing history.

**Python for Analysis.** Separating the analysis layer from the Unity engine allows independent iteration on statistical formulas (e.g. Performance Score weighting) without requiring changes to the test engine itself.

### 1.2 Production Repository and Assembly Boundaries

The production Unity project lives under `app/`; the Phase 0 calibration harness remains isolated under `calibration/harness/` and must never be referenced by a production assembly. The offline Python package lives under `analysis/`. The machine-readable toolchain contract is `config/production-environment-v1.json`, and repeatable local commands live under `scripts/production/`.

Production assembly dependencies are directional:

```
SensCalibr8.Core
    -> SensCalibr8.Data
        -> SensCalibr8.Services
            -> SensCalibr8.UI
            -> SensCalibr8.TestLogic (+ Unity.InputSystem)
                -> SensCalibr8.Integration
```

- `Core` contains engine-independent shared/domain contracts. It references neither Unity nor persistence.
- `Data` is the only assembly permitted to implement SQLite access and references only `Core`.
- `Services` owns application orchestration and calculations and may call `Data`; it remains engine-independent.
- `UI` renders/forwards interaction and may call `Services`; it must not reference `Data` or calculate results.
- `TestLogic` owns future test-runtime behavior and raw Input System integration; it may call `Services` but must not reference `Data` directly.
- `Integration` owns workflow composition that must coordinate Test Logic, Services, and Data contracts. It may reference those layers, but it must not implement raw input, direct SQLite access, scoring, or UI rendering.
- Python Analysis consumes future Data Layer exports only. It must not import Unity code or bypass the Data Layer with an independently managed production database connection.

The supported baseline is Unity `6000.5.3f1` revision `c2eb47b3a2a9`, Input System `1.19.0`, Test Framework `1.7.0`, and Python `3.12.13`. Exact Python dependencies are pinned in `analysis/requirements.lock`. Changes require a new environment manifest version and a clean preflight/test/build run.

### 1.3 Central Configuration Contracts

Production configuration enters the application only through immutable Core contracts and Service-Layer loaders. `FrozenCalibrationConfigurationLoader` reads the P0-R7 acceptance envelope before the configuration bytes, verifies the pinned SHA-256 and accepted/immutable identity, and fails closed unless it can validate the complete 20-field `calibration_configs` projection, canonical embedded JSON, all scalar/embedded-contract version agreements, and all six frozen source hashes. `ResearchConstantsLoader` is the equivalent typed source for non-Phase-0 `RESEARCH.md` constants; Phase 0-owned scoring, geometry, signal, and protocol values remain in the SHA-pinned calibration configuration and are never duplicated. The Core contracts contain no Unity or database reference; the loaders perform no writes. Python exposes equivalent frozen dataclass readers, and both runtimes validate the shared `config/production-config-parity-v1.json` parity manifest. No feature may bypass this boundary by reading a calibration plan directly.

### 1.4 SQLite Runtime and Migration Contract

The production Data assembly uses SQLite's native C API through the project-owned `SqliteDatabaseConnection`; no ORM or SQL provider may be referenced outside `SensCalibr8.Data`. The supported Windows x86-64 runtime is provisioned from the pinned Unity 6000.5.3f1 editor installation by the production scripts. Its source path and SHA-256 are fixed in `config/production-environment-v1.json`. The generated `app/Assets/Plugins/sqlite3.dll` is a verified local dependency and is not stored in Git; setup, preflight, test, and build commands recreate it deterministically and fail closed on a hash mismatch.

`SqliteConnectionFactory` is the only production connection entry point. It explicitly initializes the pinned SQLite build, opens the database, executes `PRAGMA foreign_keys = ON`, and verifies that enforcement is active before returning the connection. Versioned migrations are ordered by integer version, wrapped in transactions, and recorded in `schema_migrations` with migration name, SHA-256 checksum, and UTC application time. Migration 1 creates the complete schema below, its uniqueness/check constraints and query indexes, then sets `PRAGMA user_version = 1`. An already-applied migration whose name or checksum differs is an integrity failure, never an implicit rewrite.

Database bootstrap stores the exact accepted 20-field frozen calibration projection transactionally. Reopening is idempotent; an existing row whose value differs from the accepted configuration is rejected rather than updated.

P1-R4 exposes typed Data-layer repositories for profiles, cycles, protocol candidates with source provenance, protocol batteries, accepted calibration-configuration identity, and completed session captures. A `SessionCaptureRepository` transaction persists the session header, mandatory timing diagnostics, shot evidence, Tracking trial/window evidence, and raw mouse samples as one aggregate; any failed child write rolls back the entire aggregate so the caller can preserve its in-memory capture. SQL execution methods are internal to the Data assembly (with test-only friend visibility), preventing Services, UI, Test Logic, and Python from issuing raw production SQL. `DataAccessException` communicates operation, classified failure kind, and a recovery action to an injected `IDataFailureReporter`; it does not render UI or erase in-memory capture state. Product validation, adaptation finalization, scoring, and UI recovery presentation remain later-phase responsibilities.

### 1.5 Production Test Engine Lifecycle Contract

All production modes implement the shared `ITestMode` contract in `SensCalibr8.TestLogic`: `Prepare`, `Start`, `Capture`, `End`, `Report`, `Cancel`, and `Recover`. The mode-independent `TestEngineStateMachine` owns lifecycle transitions and invokes these callbacks; individual modes must not invent parallel session state machines.

The normal transition path is `Created -> Prepared -> Capturing -> Ending -> Completed`. `Capture` may repeat only while `Capturing`. Cancellation may terminate any non-completed/non-cancelled state. A callback exception or incomplete mode completion moves the session to `Faulted`. Recovery is legal only from `Faulted` and returns to `Prepared`, requiring a fresh `Start` before any further capture so potentially partial evidence is never silently continued. `Completed` and `Cancelled` are terminal.

Before a mode can prepare, the engine validates immutable profile/cycle/candidate/battery/session lineage, exact candidate-to-battery sensitivity and phase agreement, mode identity, and the complete accepted calibration-configuration identity. Missing source contracts, an internally inconsistent configuration projection, or a version mismatch fails closed. These contracts perform no SQL, input capture, timing, arena behavior, scoring, or persistence; those responsibilities remain in their planned Phase 3 rounds.

### 1.6 Deterministic Input and Timing Runtime

Production raw input is received through `UnityEngine.InputSystem.InputSystem.onEvent` for the selected `Mouse` device. Only `StateEvent` and `DeltaStateEvent` mouse deltas are accepted, and redundant Input System event merging is disabled before capture. Each event records the Input System event timestamp, a `System.Diagnostics.Stopwatch` timestamp/tick, device identity, and untouched raw X/Y delta. No render-frame count, `Time.time`, legacy `Input.GetAxis`, smoothing, or configured polling-rate assumption participates in capture or timing.

`DeterministicInputCapture` retains the complete in-memory raw sequence and separately accumulates azimuth/elevation as `cumulative raw count x tested sensitivity x ResearchConstants.ValorantYawMultiplier`. Raw deltas are never overwritten by this angular representation. Persistence mapping copies both representations into `mouse_samples` and timing diagnostics into `session_timing_diagnostics`; the existing atomic `SessionCaptureRepository` remains the only write boundary, so production does not write one partial database row per input event.

`InputTimingAnalyzer` reproduces the accepted `integrity-modal-cadence` policy from the frozen configuration. It requires one stable device and contiguous sample indices, measures positive event-time intervals, counts duplicate/reversed timestamps, classifies intervals at the frozen half-cadence partition, requires the median to map to one cadence, requires the one-cadence class to be strictly modal, and enforces the frozen p99 residual tolerance. Bursts and gaps remain diagnostics. Gap-safe uniform resampling splits before every interval greater than `nominal interval + frozen tolerance`, interpolates cumulative angular position only within each segment, and marks filter eligibility using the frozen minimum segment length. It never bridges a gap or modifies source samples.

The runtime frame policy is parsed from `target_geometry_json`, applies its frozen Target Frame Rate and VSync count for the test scope, requires explicit confirmation when adaptive sync must be off, and restores the previous menu settings afterward.

### 1.7 Versioned Scoring Boundary

`FrozenCalibrationConfigurationLoader` validates and projects the top-level `formula_contract` into immutable `ScoringFormulaContract` values. Formula weights, multiplier, and authoritative observation counts may not be duplicated in scoring classes. `PerformanceScoringService` is plain C# under Services: it parses fixed mode bounds and Submovement bounds from the accepted configuration, performs high/low min-max utility normalization, aggregates post-adaptation observations, and returns version-tagged mode and battery results. Test Logic and UI do not calculate scores.

Shot modes require exactly 15 authoritative resolved opportunities and Tracking requires exactly 54 authoritative one-second windows. Required-data omissions fail closed except for the two explicit accepted fallbacks: Far missing onset contributes its configured scoring ceiling without changing the raw null, and a zero-hit shot mode contributes Submovement Penalty 1.0 without fabricating a raw count. The shot formula is not final-clamped. A battery score requires four unique mode results and is their unweighted arithmetic mean.

`SensitivityScorePersistenceService` verifies formula/normalization identity against the active accepted configuration before calling `SensitivityTestRepository`. The stored `sensitivity_tests.formula_version` is mandatory, `calibration_config_id` preserves normalization/configuration lineage, and `performance_score_by_mode` retains all four individual scores as JSON. Historical rows are never silently recalculated.

---

## 2. Timer Precision Requirement

This is a hard technical constraint. Reaction Time and Travel Time are measured at millisecond resolution, and standard frame-bound timing is not accurate enough for this purpose.

Rules that must be followed without exception:

- Do NOT use `Time.time` for measuring Reaction Time, Travel Time, or any other latency-sensitive timestamp, because it is tied to frame rate and will drift when frame rate is unstable (e.g. dropping from 60fps to 45fps mid-test introduces measurement error).
- Use a high-resolution timer instead: `Time.realtimeSinceStartupAsDouble`, or a `System.Diagnostics.Stopwatch` instance, for all timestamp capture in shot-level and tracking-level data.
- Do NOT use `Input.GetAxis("Mouse X")` / `Input.GetAxis("Mouse Y")`, because these have built-in smoothing applied by Unity's legacy input system, which distorts the true raw mouse movement.
- Use Unity's Input System package to read raw mouse delta directly, with no smoothing or acceleration curve applied.
- Set a fixed Target Frame Rate and disable adaptive VSync during test sessions, so that frame time variance does not leak into recorded data as noise.

Any violation of these rules invalidates the scientific rigor mechanisms described in `RULES.md`, since timestamp noise cannot be distinguished from genuine performance variance.

---

## 3. System Architecture Diagram

```
+----------------------+           +--------------------------+
|   Unity (C#)          |  write    |    SQLite Database        |
|  - Aim Test Scenes x4  | -------->  |   - profiles                |
|  - Raw mouse capture   |           |   - calibration_configs      |
|                       |           |   - protocol_candidates       |
|                       |           |   - protocol_batteries        |
|  - In-app quick chart  | <--------  |   - sessions                  |
|                       |           |   - shots (raw log)             |
+----------------------+   read     |   - tracking_data                 |
                                     |   - mouse_samples                  |
                                     |   - outlier_flags                   |
                                     |   - significance_tests               |
                                     |   - sensitivity_tests               |
                                     |   - phase_history                     |
                                     |   - cycles                              |
                                     |   - injury_risk_flags                     |
                                     +--------------------------+
                                               ^
                                               | read
                                     +--------------------------+
                                     |   Python Analysis Layer     |
                                     |  - pandas statistics          |
                                     |  - matplotlib charts             |
                                     |  - HTML / JSON / CSV export        |
                                     +--------------------------+
```

---

## 4. Database Schema

All foreign keys use `ON DELETE CASCADE`. Deleting a profile therefore removes all associated data, and deleting a parent session or cycle removes its dependent rows. This cascading behavior must be enforced at the database level, not application level. SQLite foreign-key enforcement is disabled by default on each new connection, so every Data Layer connection must execute `PRAGMA foreign_keys = ON` immediately after opening and verify that it is enabled before any schema mutation or write.

```sql
PRAGMA foreign_keys = ON;

profiles (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    created_date TEXT NOT NULL,
    mouse_dpi INTEGER NOT NULL,
    current_sensitivity REAL NOT NULL,
    configured_polling_rate_hz REAL NOT NULL,
    dominant_hand TEXT NOT NULL,
    crosshair_config TEXT NOT NULL, -- user-selected color only; dot style and size are application-fixed
    grip_style TEXT NOT NULL,       -- Fingertip / Palm / Claw / Hybrid (descriptive only, see CONTEXT.md)
    movement_strategy TEXT NOT NULL,-- Wrist / Arm / Hybrid (descriptive only, see CONTEXT.md)
    mousepad_width_cm REAL NOT NULL,
    mousepad_height_cm REAL NOT NULL,
    ads_multiplier REAL NOT NULL,   -- reference field only, no dedicated test mode (see CONTEXT.md)
    last_active_date TEXT NOT NULL
)
   |
   | (ON DELETE CASCADE — applies to all tables below via profile_id)
   v

calibration_configs (
    id INTEGER PRIMARY KEY,
    config_version TEXT NOT NULL UNIQUE,
    normalization_version TEXT NOT NULL UNIQUE,
    signal_pipeline_version TEXT NOT NULL UNIQUE,
    test_geometry_version TEXT NOT NULL UNIQUE,
    created_date TEXT NOT NULL,
    input_sampling_rate_hz REAL NOT NULL, -- canonical uniform processing-grid rate; never assumed to equal every physical mouse's event rate
    resampling_tolerance_ms REAL NOT NULL,
    timing_acceptance_policy TEXT NOT NULL,
    butterworth_order INTEGER NOT NULL,
    cutoff_frequency_hz REAL NOT NULL,
    submovement_start_deg_per_sec REAL NOT NULL,
    submovement_end_deg_per_sec REAL NOT NULL,
    refractory_period_ms REAL NOT NULL,
    normalization_bounds_json TEXT NOT NULL,
    submovement_bounds_by_mode_json TEXT NOT NULL,
    consistency_tier_cutpoints_json TEXT NOT NULL,
    scoring_zero_tolerance REAL NOT NULL,
    target_geometry_json TEXT NOT NULL,
    tracking_contract_json TEXT NOT NULL,
    confirmatory_contract_json TEXT NOT NULL
)

cycles (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_number INTEGER NOT NULL,
    start_date TEXT NOT NULL,
    end_date TEXT,
    outcome TEXT,
    UNIQUE (profile_id, cycle_number)
)

protocol_candidates (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    phase INTEGER NOT NULL,
    edpi REAL NOT NULL,
    sensitivity_value REAL NOT NULL,
    generation_rule TEXT NOT NULL,   -- phase1_offsets / single_anchor / tie_union
    created_date TEXT NOT NULL,
    UNIQUE (cycle_id, phase, edpi)
)

protocol_candidate_sources (
    id INTEGER PRIMARY KEY,
    candidate_id INTEGER NOT NULL REFERENCES protocol_candidates(id) ON DELETE CASCADE,
    anchor_edpi REAL NOT NULL,
    offset_percent REAL NOT NULL,
    pre_floor_edpi REAL NOT NULL,
    floor_applied BOOLEAN NOT NULL,
    UNIQUE (candidate_id, anchor_edpi, offset_percent)
)

protocol_batteries (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    candidate_id INTEGER NOT NULL REFERENCES protocol_candidates(id) ON DELETE CASCADE,
    sensitivity_value REAL NOT NULL,
    phase INTEGER NOT NULL,          -- 1, 2, or 3
    purpose TEXT NOT NULL,           -- exploratory / confirmatory / narrowing / training
    started_date TEXT NOT NULL,
    completed_date TEXT
)

sessions (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    battery_id INTEGER NOT NULL REFERENCES protocol_batteries(id) ON DELETE CASCADE,
    calibration_config_id INTEGER NOT NULL REFERENCES calibration_configs(id) ON DELETE CASCADE,
    date TEXT NOT NULL,
    mode TEXT NOT NULL,             -- one of the 4 Test Modes defined in FEATURES.md
    duration_sec INTEGER NOT NULL,
    fatigue_score_change_percentage REAL,
    fatigue_flag BOOLEAN NOT NULL DEFAULT 0,
    fatigue_algorithm_version TEXT,
    UNIQUE (battery_id, mode)
)

session_timing_diagnostics (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL UNIQUE REFERENCES sessions(id) ON DELETE CASCADE,
    signal_pipeline_version TEXT NOT NULL,
    input_device_identity TEXT NOT NULL,
    configured_polling_rate_hz REAL NOT NULL,
    measured_event_rate_hz REAL NOT NULL,
    median_interval_ms REAL NOT NULL,
    interval_mad_ms REAL NOT NULL,
    p95_interval_ms REAL NOT NULL,
    p99_interval_ms REAL NOT NULL,
    duplicate_timestamp_count INTEGER NOT NULL,
    reverse_timestamp_count INTEGER NOT NULL,
    burst_interval_count INTEGER NOT NULL,
    single_cadence_interval_count INTEGER NOT NULL,
    gap_interval_count INTEGER NOT NULL,
    p99_single_cadence_residual_ms REAL NOT NULL,
    timing_contract_passed BOOLEAN NOT NULL,
    disposition_reason TEXT NOT NULL
)

shots (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    target_id INTEGER NOT NULL,
    distance_zone TEXT NOT NULL,
    target_size TEXT NOT NULL,
    spawn_position TEXT NOT NULL,
    spawn_timestamp REAL NOT NULL,
    preview_timestamp REAL,          -- Far preview visibility; NULL for modes without a preview stage
    first_mouse_movement_timestamp REAL,
    resolution_timestamp REAL NOT NULL,
    hit_timestamp REAL,
    hit_position TEXT,
    is_hit BOOLEAN NOT NULL,
    outcome_reason TEXT NOT NULL CHECK (outcome_reason IN ('hit', 'miss_click', 'timeout')),
    final_aim_position TEXT NOT NULL,
    is_outlier BOOLEAN,
    is_adaptation_shot BOOLEAN,     -- NULL while active; finalized after the session ends (see Schema Notes)
    sensitivity_value REAL NOT NULL,
    initial_offset_distance REAL NOT NULL,
    micro_adjustment_count INTEGER,
    submovement_count INTEGER,       -- computed per algorithm in RESEARCH.md, Section 5
    final_precision_error REAL NOT NULL,
    is_center_hit BOOLEAN NOT NULL,   -- diagnostic only; never enters Performance Score directly
    signed_overflick_underflick_deg REAL -- raw horizontal aim error: final azimuth minus target-center azimuth
)

tracking_data (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    sensitivity_value REAL NOT NULL,
    trial_index INTEGER NOT NULL,
    block_index INTEGER NOT NULL,
    is_adaptation_trial BOOLEAN,    -- NULL while active; finalized after session completion
    pattern_type TEXT NOT NULL,      -- linear / curved / variable-speed
    target_size TEXT NOT NULL,       -- small / medium / large; balanced once per pattern per block
    path_contract_id TEXT NOT NULL,
    path_parameters_json TEXT NOT NULL,
    duration_ms INTEGER NOT NULL,
    deviation_samples TEXT NOT NULL,
    time_on_target_ms REAL NOT NULL,
    time_on_target_percentage REAL NOT NULL,
    UNIQUE (session_id, trial_index)
)

tracking_windows (
    id INTEGER PRIMARY KEY,
    tracking_trial_id INTEGER NOT NULL REFERENCES tracking_data(id) ON DELETE CASCADE,
    window_index INTEGER NOT NULL,
    window_start_ms INTEGER NOT NULL,
    window_end_ms INTEGER NOT NULL,
    time_on_target_ms REAL NOT NULL,
    time_on_target_percentage REAL NOT NULL,
    tracking_deviation_rms_deg REAL NOT NULL,
    UNIQUE (tracking_trial_id, window_index)
)

mouse_samples (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    shot_id INTEGER REFERENCES shots(id) ON DELETE CASCADE,
    sample_index INTEGER NOT NULL,
    timestamp_sec REAL NOT NULL,
    raw_delta_x REAL NOT NULL,
    raw_delta_y REAL NOT NULL,
    azimuth_deg REAL NOT NULL,
    elevation_deg REAL NOT NULL,
    UNIQUE (session_id, sample_index)
)

outlier_analysis_runs (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    calibration_config_id INTEGER NOT NULL REFERENCES calibration_configs(id) ON DELETE CASCADE,
    phase INTEGER NOT NULL,
    mode TEXT NOT NULL,
    sensitivity_value REAL NOT NULL,
    metric_name TEXT NOT NULL,
    scope_key TEXT NOT NULL UNIQUE,
    group_mean REAL NOT NULL,
    sample_sd REAL NOT NULL,
    threshold_value REAL NOT NULL,
    inclusive_mean REAL NOT NULL,
    flagged_excluded_mean REAL NOT NULL,
    observation_count INTEGER NOT NULL,
    flagged_count INTEGER NOT NULL,
    algorithm_version TEXT NOT NULL
)

outlier_flags (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    shot_id INTEGER REFERENCES shots(id) ON DELETE CASCADE,
    tracking_data_id INTEGER REFERENCES tracking_data(id) ON DELETE CASCADE,
    metric_name TEXT NOT NULL,
    scope_key TEXT NOT NULL,
    observed_value REAL NOT NULL,
    group_mean REAL NOT NULL,
    sample_sd REAL NOT NULL,
    threshold_value REAL NOT NULL,
    algorithm_version TEXT NOT NULL,
    is_statistical_outlier BOOLEAN NOT NULL,
    is_data_quality_error BOOLEAN NOT NULL DEFAULT 0,
    excluded_from_authoritative_score BOOLEAN NOT NULL DEFAULT 0,
    disposition_reason TEXT,
    analysis_run_id INTEGER REFERENCES outlier_analysis_runs(id) ON DELETE CASCADE,
    CHECK ((shot_id IS NOT NULL) <> (tracking_data_id IS NOT NULL))
)

sensitivity_tests (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    calibration_config_id INTEGER NOT NULL REFERENCES calibration_configs(id) ON DELETE CASCADE,
    battery_id INTEGER NOT NULL REFERENCES protocol_batteries(id) ON DELETE CASCADE,
    edpi REAL NOT NULL,
    cm_360 REAL NOT NULL,
    avg_performance_score REAL NOT NULL,
    performance_score_by_mode TEXT NOT NULL, -- JSON: {mode: score}, one entry per Test Mode
    grade TEXT,                      -- S / A / B / C / D
    grade_contract_version TEXT,
    reaction_tier TEXT,
    consistency_tier TEXT,
    close_reaction_time_ms REAL,
    battery_consistency_utility REAL,
    formula_version TEXT NOT NULL,   -- must be recorded on every row, see RESEARCH.md Section 4
    phase INTEGER NOT NULL,          -- 1, 2, or 3 (Testing Protocol phase, see FEATURES.md)
    sample_size INTEGER NOT NULL
)

significance_tests (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    calibration_config_id INTEGER NOT NULL REFERENCES calibration_configs(id) ON DELETE CASCADE,
    phase INTEGER NOT NULL,
    candidate_a_edpi REAL NOT NULL,
    candidate_b_edpi REAL NOT NULL,
    test_method TEXT NOT NULL,
    alternative TEXT NOT NULL,
    alpha REAL NOT NULL,
    p_value REAL NOT NULL,
    effect_estimate REAL NOT NULL,
    confidence_level REAL NOT NULL,
    confidence_interval_lower REAL NOT NULL,
    confidence_interval_upper REAL NOT NULL,
    paired_sample_size INTEGER NOT NULL,
    is_significant BOOLEAN NOT NULL,
    formula_version TEXT NOT NULL,
    result TEXT NOT NULL             -- winner candidate or statistical_tie
)

significance_test_pairs (
    id INTEGER PRIMARY KEY,
    significance_test_id INTEGER NOT NULL REFERENCES significance_tests(id) ON DELETE CASCADE,
    pair_index INTEGER NOT NULL,
    first_candidate TEXT NOT NULL CHECK (first_candidate IN ('A', 'B')),
    pairing_seed TEXT NOT NULL,
    matched_condition_key TEXT NOT NULL,
    candidate_a_battery_id INTEGER NOT NULL REFERENCES protocol_batteries(id) ON DELETE CASCADE,
    candidate_b_battery_id INTEGER NOT NULL REFERENCES protocol_batteries(id) ON DELETE CASCADE,
    candidate_a_score REAL NOT NULL,
    candidate_b_score REAL NOT NULL,
    UNIQUE (significance_test_id, pair_index)
)

phase_history (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    phase_number INTEGER NOT NULL,
    winner_edpi REAL NOT NULL,
    timestamp TEXT NOT NULL
)

injury_risk_flags (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    flag_type TEXT NOT NULL,         -- e.g. 'low_edpi_wrist_strain', 'mousepad_constraint_violation'
    triggered_date TEXT NOT NULL,
    edpi_at_trigger REAL NOT NULL,
    acknowledged BOOLEAN NOT NULL DEFAULT 0
)

application_state (
    state_key TEXT PRIMARY KEY,      -- currently the singleton 'active_profile'
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    updated_date TEXT NOT NULL
)

cycle_checkpoints (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL UNIQUE REFERENCES cycles(id) ON DELETE CASCADE,
    source_phase_history_id INTEGER NOT NULL REFERENCES phase_history(id) ON DELETE CASCADE,
    best_edpi REAL NOT NULL,
    training_session_count INTEGER NOT NULL,
    completed_training_battery_count INTEGER NOT NULL,
    performance_score REAL NOT NULL,
    final_grade TEXT NOT NULL,
    contract_version TEXT NOT NULL,
    checkpoint_date TEXT NOT NULL
)

recalibration_events (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    source_cycle_id INTEGER NOT NULL UNIQUE REFERENCES cycles(id) ON DELETE CASCADE,
    destination_cycle_id INTEGER NOT NULL UNIQUE REFERENCES cycles(id) ON DELETE CASCADE,
    source_phase_history_id INTEGER NOT NULL REFERENCES phase_history(id) ON DELETE CASCADE,
    source_checkpoint_id INTEGER NOT NULL UNIQUE REFERENCES cycle_checkpoints(id) ON DELETE CASCADE,
    baseline_edpi REAL NOT NULL,
    contract_version TEXT NOT NULL,
    triggered_date TEXT NOT NULL
)
```

### 4.1 Schema Notes

- `grip_style`, `movement_strategy`, `ads_multiplier` are descriptive/reference fields only. They must never be read by any Performance Score calculation or used to bias results (see `CONTEXT.md`, Out of Scope).
- `current_sensitivity` stores the user's current Valorant sensitivity entered during profile setup so that Step 0 can compare it with the PSA baseline.
- `calibration_configs` is immutable after first use. Phase 0 must populate every field from validated measurements; production sessions cannot reference an incomplete or draft configuration. The fixed signal constants must match `RESEARCH.md`, Section 5, while measured sampling, tolerance, bounds, tier cutpoints, geometry, Tracking, and confirmatory contracts are versioned outputs of Phase 0. `test_geometry_version = 'sc8-test-geometry-v1'` and `target_geometry_json` must serialize the accepted P0-R4 contract exactly, including the fixed 16:9/letterbox policy, camera/FOV, arena, target dimensions/placements, frame policy, safe viewport, crosshair, and Center-Hit zone; application code must not duplicate these as unrelated constants. P0-R6 fields must adopt the SHA-pinned payload from `sc8-p0-r6-scoring-statistics-contract-v1`: `normalization_bounds_json`, `submovement_bounds_by_mode_json`, `consistency_tier_cutpoints_json`, `scoring_zero_tolerance`, and `confirmatory_contract_json` may not be independently edited or partially copied.
- Phase 0 accepted the complete projection as `calibration_config_v1` / `sc8-calibration-config-v1`. Its exact source is `calibration/plans/calibration-config-v1.json`, pinned by `p0-r7-calibration-config-accepted-v1.json`. Database creation must insert all 20 non-ID fields from `calibration_configs_record` as one transaction and reject any hash mismatch, missing field, draft status, non-immutable status, non-canonical embedded JSON, source dependency drift, or scalar/embedded-contract mismatch. No application layer may reconstruct or partially override this row.
- Every `session` and `sensitivity_test` references the exact calibration configuration used. Together with `formula_version`, this preserves the meaning of historical raw data and scores.
- Every newly persisted `sensitivity_test` also references exactly one complete four-mode `protocol_battery`. Migration 6 adds the nullable physical column for compatibility with pre-P5-R3 development rows, while the repository contract requires a positive unique `battery_id` for every new score and verifies profile/cycle/phase/eDPI/configuration lineage plus four distinct completed sessions before insertion.
- A database `session` is exactly one Test Mode at one sensitivity value. A `protocol_battery` groups exactly four sessions (one per mode) at one sensitivity value. `UNIQUE (battery_id, mode)` prevents duplicate mode runs inside a battery; battery completion requires all four modes.
- `protocol_candidates` stores the canonical final eDPI after floor application and deduplication. `UNIQUE (cycle_id, phase, edpi)` enforces one candidate per final eDPI in a phase. Candidate generation must not use intermediate rounding.
- `protocol_candidate_sources` preserves every anchor/offset path that generated a candidate. A deduplicated candidate may therefore have multiple source rows; these rows must never be collapsed or overwritten.
- Every `protocol_battery` references one canonical candidate. Its `sensitivity_value` and the candidate's value must represent the same final eDPI for the profile DPI.
- `protocol_batteries.sensitivity_value` is the authoritative tested value for all child sessions. Any redundant sensitivity value recorded in shot or Tracking rows must match the parent battery exactly.
- Fatigue is evaluated only after session capture and adaptation finalization. `fatigue_score_change_percentage` stores the first-half versus second-half Performance Score change, `fatigue_flag` is true only for a decline greater than 15%, and `fatigue_algorithm_version` preserves the accepted contract identity. A near-zero first-half denominator produces an undefined percentage and no flag. Fatigue never excludes the session from Winner selection.
- `shots.is_center_hit` supports Center-Hit Percentage as a diagnostic only. Under `sc8-test-geometry-v1`, the center-zone radius is 50% of the projected target radius (25% of projected target area) and is versioned through `target_geometry_json`.
- Every resolved `shots` row stores `resolution_timestamp`, `outcome_reason`, `final_aim_position`, and `final_precision_error`, including miss-clicks and timeouts. This makes the 15-observation P0-R6 Precision/Consistency aggregate complete without pretending a miss has zero error. `hit_timestamp`/`hit_position` remain nullable because they describe actual hits; `submovement_count` remains null on misses. For Far, `preview_timestamp` preserves visible-preview timing while `spawn_timestamp` is the center-reference activation timestamp that starts the Travel-Time contract. For Far missing-onset cases, `first_mouse_movement_timestamp` remains null and the scoring layer applies the explicit 1500 ms fallback without writing a fabricated timestamp.
- For Micro-Correction, `initial_offset_distance` is the frozen reference-pixel radial offset while `final_precision_error` remains angular degrees. A hit requires timing-valid, filter-eligible `sc8-signal-pipeline-v1` evidence; miss-clicks/timeouts retain null `micro_adjustment_count` and `submovement_count`. Because the specification authorizes only one corrective-movement detector, both count columns store that same accepted event count for a Micro hit rather than introducing an unversioned second algorithm.
- `shots.signed_overflick_underflick_deg` preserves raw Close/Far horizontal aim error as `final_aim_azimuth_deg - target_center_azimuth_deg`. Positive and negative values are coordinate directions only; any later Overflick/Underflick presentation must label them relative to the target's intended horizontal movement direction without changing the stored value.
- `mouse_samples` preserves timestamped raw deltas and their calibrated angular representation. Filtering and angular velocity are derived analysis outputs and must not overwrite raw samples.
- `calibration_configs.input_sampling_rate_hz` is the canonical 1000 Hz uniform processing grid frozen by P0-R3, not a claim that every physical mouse reports at that rate. `timing_acceptance_policy` is `integrity-modal-cadence`: timestamp order and nominal modal cadence are hard gates, while burst/gap counts remain diagnostics and gaps split resampling segments. `session_timing_diagnostics` stores the user's configured polling-rate metadata plus automatically measured cadence for every session. Manufacturer/model/firmware may be retained as optional audit metadata but must not participate in scoring or block profile creation.
- `shots.is_outlier` is a denormalized any-metric indicator only. `outlier_analysis_runs` records one immutable pass per canonical `(profile, cycle, phase, mode, sensitivity, metric)` scope, including inclusive and flagged-excluded aggregates. `outlier_flags` stores the metric-level flagged observations linked to that pass. Statistical flags default to inclusion; `excluded_from_authoritative_score` may be true only with a separately confirmed `is_data_quality_error` and documented reason.
- `sensitivity_tests.grade` is assigned once from the worse of the Close Flick Reaction tier and four-mode mean normalized Consistency tier. The two component tiers, source values, and `grade_contract_version` are persisted alongside the final Grade; Grade cannot be silently overwritten.
- `config/scientific-rigor-v1.json` is the immutable operational contract for adaptation ordering, one-pass strict 3-SD behavior, first/second-half fatigue, and Grade combination. Its adaptation values must equal the frozen mode configuration and its outlier multiplier must equal `research-constants-v1`; loading fails closed on drift.
- `config/continuous-cycle-v1.json` fixes the 5-10 Database Session training block, the three-cycle/strictly-below-5% plateau rule, and Phase 3 Winner baseline lineage. A `cycle_checkpoint` uses the latest complete scored/graded training battery after the required session count is reached; it is the one auditable value used for cycle-to-cycle Grade/score comparison.
- `recalibration_events` permit exactly one automatic successor per source cycle/checkpoint. The destination cycle receives a fresh Phase 1 candidate set around the source Phase 3 Winner eDPI; profile `current_sensitivity` is never silently modified.
- `protocol_batteries.purpose` separates exploratory data from fresh confirmatory data and prevents reuse of ranking batteries as confirmation evidence.
- `significance_tests` stores the complete paired confirmatory decision. `significance_test_pairs` links every paired score back to the two source batteries. Test method, alternative, alpha, confidence interval, sample size, versions, and tie/winner result are mandatory for auditability.
- Confirmatory ranking reads only battery-linked score rows whose parent battery is completed and has purpose `exploratory`. Confirmatory pair persistence accepts only completed purpose-`confirmatory` batteries whose persisted battery score exactly matches the pair score under the same configuration, formula, phase, and candidate eDPI. A battery may occur in only one persisted significance pair.
- Under `sc8-confirmatory-v1`, `significance_test_pairs` must contain exactly 10 complete fresh pairs with pair indices 1-10, five A-first and five B-first. Each linked battery score is the unweighted mean of exactly four complete mode scores. The analysis enumerates all 1024 sign assignments with the accepted 1e-12 score-point inclusive-extremeness comparison guard; interrupted attempts remain raw evidence but do not receive a pair row until both batteries complete. The 95% Student-t interval is reported uncertainty, while the exact p-value alone controls Winner versus tie.
- `crosshair_config` stores only the approved high-contrast hex color selected at profile creation: `#FFE600`, `#FF00FF`, `#FF3B30`, or `#FF9500`. Dot style and dot size are fixed by the application and must not be stored as user-editable settings.
- `application_state` stores the persisted active profile singleton. The Service restores it at application startup, clears it when the user exits the active profile, and relies on its cascade when the referenced profile is deleted. The active profile cannot enter the deletion-confirmation flow; inactive deletion requires a Service-issued confirmation object before the Data Layer is called.
- `formula_version` on `sensitivity_tests` is mandatory on every insert. Never leave it null.
- `is_adaptation_shot` must remain null while shots are being captured. After the session ends and the actual shot total for each sensitivity value is known, the Data Layer must compute the cutoff from `RESEARCH.md`, Section 8, and update every shot in a single transaction. Analysis must reject session data containing an unfinalized null adaptation flag.
- `cycle_id` is mandatory on `protocol_batteries`, `sensitivity_tests`, and `phase_history`, preserving an explicit relationship between repeated Phase 1-3 runs and their owning continuous-improvement cycle.
- `tracking_data.sensitivity_value` is mandatory so Tracking results can be grouped and compared by the sensitivity actually tested.
- `tracking_data` stores one row per six-second trial under `sc8-mode-contract-v1`; `target_speed` is intentionally replaced by `path_contract_id` plus `path_parameters_json` because Curved and Variable-Speed paths cannot be represented by one scalar speed. Two nine-trial blocks cross every pattern with every target size once. `is_adaptation_trial` remains null during capture and is finalized transactionally after all 18 trials; block one becomes adaptation and block two authoritative.
- `tracking_windows` stores six exact one-second interval-weighted aggregates per trial. Time-on-Target uses duration rather than render-frame counts, and `tracking_deviation_rms_deg` is the per-window radial RMS error used by Tracking precision/outlier analysis. The authoritative block therefore contributes 54 windows per Tracking session.
- `calibration_configs.signal_pipeline_version = 'sc8-signal-pipeline-v1'` and `tracking_contract_json` must serialize the accepted P0-R5 signal/mode contract, including SOS coefficients, event boundaries, condition sequencing, mode completion, Tracking paths, and metric definitions. Production code must reject a configuration that references only the older timing sub-contract or a draft P0-R5 candidate.
- Production condition sequences are generated from canonical SHA-256 audit material containing only `mode_contract_version`, profile, cycle, phase, mode, and battery repetition. Sensitivity value and blind candidate label are excluded. A compared candidate therefore receives the same per-mode condition sequence for the same repetition, and every sequence exposes its generator, contract version, seed hash, and canonical seed material for later persistence by P3-R5.
- Close/Far sequencing must create three complete 3 x 3 offset-size blocks plus three rotating conditions; Tracking must create one complete pattern-size cross-product in each of its two blocks; Micro-Correction must remain Small and sample only the frozen radial pixel range. Every stationary target center must pass the full-radius edge-margin and top-HUD-reserve check before display.
- Candidate presentation exposes only opaque ordered labels (`Candidate-01`, etc.) and never numeric sensitivity. Exploratory candidate order is deterministic-randomized and four-mode order rotates across battery repetition. Confirmatory order comes only from `sc8-confirmatory-v1`; its stable pairing seed uses the declared sorted candidate-eDPI pair, while the matched condition key binds both batteries in a pair to the same mode order and condition seeds.
- `ProtocolPhase` has explicit persistent identities 1, 2, and 3, matching schema comments and constraints; persistence must never cast an implicit zero-based enum.
- Phase 1 candidate offsets and their generation-rule lineage are loaded from immutable `config/protocol-constants-v1.json`. The complete candidate/source set is one transaction: no partial Phase 1 candidate set may survive an insertion failure.
- Phase 1 completion is evaluated from the accepted frozen mode/formula contracts, not duplicated literals: each shot-based mode requires its full 30 resolved observations before the 15-observation authoritative half is scored; Tracking requires all 18 trials and 108 windows before its 9-trial/54-window authoritative half is scored.
- Phase 2 generation and stabilization load only immutable `config/phase-two-protocol-v1.json`: offsets -10/0/+10%, 5-10 complete narrowing batteries per value, and strict Performance Score CV below 10%. A single Phase 1 winner uses `single_anchor`; a statistical tie uses `tie_union`. Both apply the eDPI floor before canonical deduplication and preserve every source path.
- Phase 2 stabilization reads only battery-linked score rows whose parent is a completed purpose-`narrowing` battery under the accepted configuration/formula. Fewer than five complete batteries is `Collecting`; CV greater than or equal to 10%, or undefined due to the accepted near-zero mean tolerance, requires more evidence through battery 10; a non-passing value at 10 is `MaximumReachedWithoutStabilization`. Incomplete batteries never count.
- Phase 2 Winner selection requires every Phase 2 candidate to stabilize, then selects only a unique highest mean complete-battery Performance Score. An exact highest-mean tie produces no `phase_history` row and may not generate Phase 3 candidates. The `phase_history` repository rejects duplicate `(profile, cycle, phase)` Winners.
- Phase 3 generation loads only immutable `config/phase-three-protocol-v1.json`: offsets -5/0/+5% around the persisted Phase 2 Winner, `single_anchor` lineage, eDPI-floor-before-deduplication, and all source-path preservation. Phase 3 reuses the Phase 2 narrowing repetition/stabilization contract; its unique highest-mean Winner is persisted in Phase 3 history as preliminary Best Sensitivity. An exact tie persists no Best. Neither winner persistence automatically mutates `profiles.current_sensitivity`.
- `session_attempts` preserves the lifecycle of incomplete work independently from a completed `sessions` row. It records profile/cycle/candidate/battery/configuration lineage, opaque blind label, sequence audit identity, attempt ordinal, and capturing/paused/cancelled/faulted/completed disposition. A completed mode may not begin another attempt in the same battery. `session_sequence_audits` stores the same immutable sequence identity for every completed session.
- Completed session persistence is one transaction: raw session/timing/shot/tracking/window/mouse rows, completed-session sequence audit, post-capture adaptation finalization, attempt completion, and optional fourth-mode battery completion either all commit or all roll back. Shot adaptation uses the frozen fraction against the final inserted shot total in insertion order; Tracking adaptation is assigned only after completion using the frozen count of initial blocks. No capture row may provide a non-null adaptation flag before this transaction finalizes it.
- `formula_version = 'sc8-performance-score-v1'` and `normalization_version = 'sc8-normalization-v1'` require 15 authoritative observations per shot mode, 54 Tracking windows, fixed P0-R6 metric bounds, Submovement bounds 1-6, fixed Consistency utility cutpoints 0.8/0.6/0.4/0.2, and scoring-zero tolerance `1e-9`. The shot formula is not final-clamped. A required data-quality omission invalidates the mode score; the only explicit scoring fallbacks are Far missing onset to 1500 ms and zero authoritative hits to Submovement Penalty 1.0, while raw nullable fields remain unchanged.
- `PRAGMA foreign_keys = ON` must be executed and verified separately for every SQLite connection; setting it only during initial schema creation is insufficient.
- `injury_risk_flags` records are informational only and must never block or alter test execution; they exist purely to surface warnings to the user (see `RULES.md`). Dashboard evaluation is profile-scoped: it persists at most one unacknowledged row for a matching flag type and eDPI, and acknowledgement is scoped to that same profile. A later evaluation may record a new row if the still-triggered condition recurs after acknowledgement.

---

## 5. Naming Convention Cross-Reference

Database columns use `snake_case` as shown above. C# class and method names in the Unity layer use `PascalCase`. Full conventions are defined in `SKILL.md`; this file only defines the data shape, not the code style.
