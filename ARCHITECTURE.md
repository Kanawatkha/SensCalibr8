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
    input_sampling_rate_hz REAL NOT NULL,
    resampling_tolerance_ms REAL NOT NULL,
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
    outcome TEXT
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
    UNIQUE (battery_id, mode)
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
    first_mouse_movement_timestamp REAL,
    hit_timestamp REAL,
    hit_position TEXT,
    is_hit BOOLEAN NOT NULL,
    is_outlier BOOLEAN,
    is_adaptation_shot BOOLEAN,     -- NULL while active; finalized after the session ends (see Schema Notes)
    sensitivity_value REAL NOT NULL,
    initial_offset_distance REAL NOT NULL,
    micro_adjustment_count INTEGER,
    submovement_count INTEGER,       -- computed per algorithm in RESEARCH.md, Section 5
    final_precision_error REAL,
    is_center_hit BOOLEAN             -- diagnostic only; never enters Performance Score directly
)

tracking_data (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    sensitivity_value REAL NOT NULL,
    pattern_type TEXT NOT NULL,      -- linear / curved / variable-speed
    target_speed REAL NOT NULL,
    duration_ms INTEGER NOT NULL,
    deviation_samples TEXT NOT NULL,
    time_on_target_ms INTEGER NOT NULL,
    time_on_target_percentage REAL NOT NULL
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
    CHECK ((shot_id IS NOT NULL) <> (tracking_data_id IS NOT NULL))
)

sensitivity_tests (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    calibration_config_id INTEGER NOT NULL REFERENCES calibration_configs(id) ON DELETE CASCADE,
    edpi REAL NOT NULL,
    cm_360 REAL NOT NULL,
    avg_performance_score REAL NOT NULL,
    performance_score_by_mode TEXT NOT NULL, -- JSON: {mode: score}, one entry per Test Mode
    grade TEXT,                      -- S / A / B / C / D
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
```

### 4.1 Schema Notes

- `grip_style`, `movement_strategy`, `ads_multiplier` are descriptive/reference fields only. They must never be read by any Performance Score calculation or used to bias results (see `CONTEXT.md`, Out of Scope).
- `current_sensitivity` stores the user's current Valorant sensitivity entered during profile setup so that Step 0 can compare it with the PSA baseline.
- `calibration_configs` is immutable after first use. Phase 0 must populate every field from validated measurements; production sessions cannot reference an incomplete or draft configuration. The fixed signal constants must match `RESEARCH.md`, Section 5, while measured sampling, tolerance, bounds, tier cutpoints, geometry, Tracking, and confirmatory contracts are versioned outputs of Phase 0.
- Every `session` and `sensitivity_test` references the exact calibration configuration used. Together with `formula_version`, this preserves the meaning of historical raw data and scores.
- A database `session` is exactly one Test Mode at one sensitivity value. A `protocol_battery` groups exactly four sessions (one per mode) at one sensitivity value. `UNIQUE (battery_id, mode)` prevents duplicate mode runs inside a battery; battery completion requires all four modes.
- `protocol_candidates` stores the canonical final eDPI after floor application and deduplication. `UNIQUE (cycle_id, phase, edpi)` enforces one candidate per final eDPI in a phase. Candidate generation must not use intermediate rounding.
- `protocol_candidate_sources` preserves every anchor/offset path that generated a candidate. A deduplicated candidate may therefore have multiple source rows; these rows must never be collapsed or overwritten.
- Every `protocol_battery` references one canonical candidate. Its `sensitivity_value` and the candidate's value must represent the same final eDPI for the profile DPI.
- `protocol_batteries.sensitivity_value` is the authoritative tested value for all child sessions. Any redundant sensitivity value recorded in shot or Tracking rows must match the parent battery exactly.
- Fatigue is evaluated only after session capture and adaptation finalization. `fatigue_score_change_percentage` stores the first-half versus second-half Performance Score change, and `fatigue_flag` is true only for a decline greater than 15%; the flag must not exclude that session from Winner selection.
- `shots.is_center_hit` supports Center-Hit Percentage as a diagnostic only. The center-zone geometry remains blocked by Phase 0: Signal Calibration and must be versioned with the test configuration.
- `mouse_samples` preserves timestamped raw deltas and their calibrated angular representation. Filtering and angular velocity are derived analysis outputs and must not overwrite raw samples.
- `shots.is_outlier` is a denormalized any-metric indicator only. `outlier_flags` is authoritative and stores one metric-level audit record per flagged observation. Statistical flags default to inclusion; `excluded_from_authoritative_score` may be true only with a separately confirmed `is_data_quality_error` and documented reason.
- `protocol_batteries.purpose` separates exploratory data from fresh confirmatory data and prevents reuse of ranking batteries as confirmation evidence.
- `significance_tests` stores the complete paired confirmatory decision. `significance_test_pairs` links every paired score back to the two source batteries. Test method, alternative, alpha, confidence interval, sample size, versions, and tie/winner result are mandatory for auditability.
- `crosshair_config` stores only the high-contrast color selected at profile creation. Dot style and dot size are fixed by the application and must not be stored as user-editable settings.
- `formula_version` on `sensitivity_tests` is mandatory on every insert. Never leave it null.
- `is_adaptation_shot` must remain null while shots are being captured. After the session ends and the actual shot total for each sensitivity value is known, the Data Layer must compute the cutoff from `RESEARCH.md`, Section 8, and update every shot in a single transaction. Analysis must reject session data containing an unfinalized null adaptation flag.
- `cycle_id` is mandatory on `protocol_batteries`, `sensitivity_tests`, and `phase_history`, preserving an explicit relationship between repeated Phase 1-3 runs and their owning continuous-improvement cycle.
- `tracking_data.sensitivity_value` is mandatory so Tracking results can be grouped and compared by the sensitivity actually tested.
- `PRAGMA foreign_keys = ON` must be executed and verified separately for every SQLite connection; setting it only during initial schema creation is insufficient.
- `injury_risk_flags` records are informational only and must never block or alter test execution; they exist purely to surface warnings to the user (see `RULES.md`).

---

## 5. Naming Convention Cross-Reference

Database columns use `snake_case` as shown above. C# class and method names in the Unity layer use `PascalCase`. Full conventions are defined in `SKILL.md`; this file only defines the data shape, not the code style.
