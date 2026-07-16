using System.Collections.Generic;

namespace SensCalibr8.Data.Migrations
{
    public static class SchemaMigrationCatalog
    {
        public static IReadOnlyList<SchemaMigration> All { get; } = new[]
        {
            new SchemaMigration(1, "initial_schema", InitialSchemaSql)
        };

        private const string InitialSchemaSql = @"
CREATE TABLE profiles (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    created_date TEXT NOT NULL,
    mouse_dpi INTEGER NOT NULL,
    current_sensitivity REAL NOT NULL,
    configured_polling_rate_hz REAL NOT NULL,
    dominant_hand TEXT NOT NULL,
    crosshair_config TEXT NOT NULL,
    grip_style TEXT NOT NULL,
    movement_strategy TEXT NOT NULL,
    mousepad_width_cm REAL NOT NULL,
    mousepad_height_cm REAL NOT NULL,
    ads_multiplier REAL NOT NULL,
    last_active_date TEXT NOT NULL
);

CREATE TABLE calibration_configs (
    id INTEGER PRIMARY KEY,
    config_version TEXT NOT NULL UNIQUE,
    normalization_version TEXT NOT NULL UNIQUE,
    signal_pipeline_version TEXT NOT NULL UNIQUE,
    test_geometry_version TEXT NOT NULL UNIQUE,
    created_date TEXT NOT NULL,
    input_sampling_rate_hz REAL NOT NULL,
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
);

CREATE TABLE cycles (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_number INTEGER NOT NULL,
    start_date TEXT NOT NULL,
    end_date TEXT,
    outcome TEXT
);

CREATE TABLE protocol_candidates (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    phase INTEGER NOT NULL CHECK (phase IN (1, 2, 3)),
    edpi REAL NOT NULL,
    sensitivity_value REAL NOT NULL,
    generation_rule TEXT NOT NULL CHECK (generation_rule IN ('phase1_offsets', 'single_anchor', 'tie_union')),
    created_date TEXT NOT NULL,
    UNIQUE (cycle_id, phase, edpi)
);

CREATE TABLE protocol_candidate_sources (
    id INTEGER PRIMARY KEY,
    candidate_id INTEGER NOT NULL REFERENCES protocol_candidates(id) ON DELETE CASCADE,
    anchor_edpi REAL NOT NULL,
    offset_percent REAL NOT NULL,
    pre_floor_edpi REAL NOT NULL,
    floor_applied INTEGER NOT NULL CHECK (floor_applied IN (0, 1)),
    UNIQUE (candidate_id, anchor_edpi, offset_percent)
);

CREATE TABLE protocol_batteries (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    candidate_id INTEGER NOT NULL REFERENCES protocol_candidates(id) ON DELETE CASCADE,
    sensitivity_value REAL NOT NULL,
    phase INTEGER NOT NULL CHECK (phase IN (1, 2, 3)),
    purpose TEXT NOT NULL CHECK (purpose IN ('exploratory', 'confirmatory', 'narrowing', 'training')),
    started_date TEXT NOT NULL,
    completed_date TEXT
);

CREATE TABLE sessions (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    battery_id INTEGER NOT NULL REFERENCES protocol_batteries(id) ON DELETE CASCADE,
    calibration_config_id INTEGER NOT NULL REFERENCES calibration_configs(id) ON DELETE CASCADE,
    date TEXT NOT NULL,
    mode TEXT NOT NULL CHECK (mode IN ('flick_close', 'flick_far', 'tracking', 'micro_correction')),
    duration_sec INTEGER NOT NULL,
    fatigue_score_change_percentage REAL,
    fatigue_flag INTEGER NOT NULL DEFAULT 0 CHECK (fatigue_flag IN (0, 1)),
    UNIQUE (battery_id, mode)
);

CREATE TABLE session_timing_diagnostics (
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
    timing_contract_passed INTEGER NOT NULL CHECK (timing_contract_passed IN (0, 1)),
    disposition_reason TEXT NOT NULL
);

CREATE TABLE shots (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    target_id INTEGER NOT NULL,
    distance_zone TEXT NOT NULL,
    target_size TEXT NOT NULL,
    spawn_position TEXT NOT NULL,
    spawn_timestamp REAL NOT NULL,
    first_mouse_movement_timestamp REAL,
    resolution_timestamp REAL NOT NULL,
    hit_timestamp REAL,
    hit_position TEXT,
    is_hit INTEGER NOT NULL CHECK (is_hit IN (0, 1)),
    outcome_reason TEXT NOT NULL CHECK (outcome_reason IN ('hit', 'miss_click', 'timeout')),
    final_aim_position TEXT NOT NULL,
    is_outlier INTEGER CHECK (is_outlier IS NULL OR is_outlier IN (0, 1)),
    is_adaptation_shot INTEGER CHECK (is_adaptation_shot IS NULL OR is_adaptation_shot IN (0, 1)),
    sensitivity_value REAL NOT NULL,
    initial_offset_distance REAL NOT NULL,
    micro_adjustment_count INTEGER,
    submovement_count INTEGER,
    final_precision_error REAL NOT NULL,
    is_center_hit INTEGER NOT NULL CHECK (is_center_hit IN (0, 1))
);

CREATE TABLE tracking_data (
    id INTEGER PRIMARY KEY,
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    sensitivity_value REAL NOT NULL,
    trial_index INTEGER NOT NULL,
    block_index INTEGER NOT NULL,
    is_adaptation_trial INTEGER CHECK (is_adaptation_trial IS NULL OR is_adaptation_trial IN (0, 1)),
    pattern_type TEXT NOT NULL CHECK (pattern_type IN ('linear', 'curved', 'variable_speed')),
    target_size TEXT NOT NULL CHECK (target_size IN ('small', 'medium', 'large')),
    path_contract_id TEXT NOT NULL,
    path_parameters_json TEXT NOT NULL,
    duration_ms INTEGER NOT NULL,
    deviation_samples TEXT NOT NULL,
    time_on_target_ms REAL NOT NULL,
    time_on_target_percentage REAL NOT NULL,
    UNIQUE (session_id, trial_index)
);

CREATE TABLE tracking_windows (
    id INTEGER PRIMARY KEY,
    tracking_trial_id INTEGER NOT NULL REFERENCES tracking_data(id) ON DELETE CASCADE,
    window_index INTEGER NOT NULL,
    window_start_ms INTEGER NOT NULL,
    window_end_ms INTEGER NOT NULL,
    time_on_target_ms REAL NOT NULL,
    time_on_target_percentage REAL NOT NULL,
    tracking_deviation_rms_deg REAL NOT NULL,
    UNIQUE (tracking_trial_id, window_index)
);

CREATE TABLE mouse_samples (
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
);

CREATE TABLE outlier_flags (
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
    is_statistical_outlier INTEGER NOT NULL CHECK (is_statistical_outlier IN (0, 1)),
    is_data_quality_error INTEGER NOT NULL DEFAULT 0 CHECK (is_data_quality_error IN (0, 1)),
    excluded_from_authoritative_score INTEGER NOT NULL DEFAULT 0 CHECK (excluded_from_authoritative_score IN (0, 1)),
    disposition_reason TEXT,
    CHECK ((shot_id IS NOT NULL) <> (tracking_data_id IS NOT NULL))
);

CREATE TABLE sensitivity_tests (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    calibration_config_id INTEGER NOT NULL REFERENCES calibration_configs(id) ON DELETE CASCADE,
    edpi REAL NOT NULL,
    cm_360 REAL NOT NULL,
    avg_performance_score REAL NOT NULL,
    performance_score_by_mode TEXT NOT NULL,
    grade TEXT CHECK (grade IS NULL OR grade IN ('S', 'A', 'B', 'C', 'D')),
    formula_version TEXT NOT NULL,
    phase INTEGER NOT NULL CHECK (phase IN (1, 2, 3)),
    sample_size INTEGER NOT NULL
);

CREATE TABLE significance_tests (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    calibration_config_id INTEGER NOT NULL REFERENCES calibration_configs(id) ON DELETE CASCADE,
    phase INTEGER NOT NULL CHECK (phase IN (1, 2, 3)),
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
    is_significant INTEGER NOT NULL CHECK (is_significant IN (0, 1)),
    formula_version TEXT NOT NULL,
    result TEXT NOT NULL
);

CREATE TABLE significance_test_pairs (
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
);

CREATE TABLE phase_history (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    cycle_id INTEGER NOT NULL REFERENCES cycles(id) ON DELETE CASCADE,
    phase_number INTEGER NOT NULL CHECK (phase_number IN (1, 2, 3)),
    winner_edpi REAL NOT NULL,
    timestamp TEXT NOT NULL
);

CREATE TABLE injury_risk_flags (
    id INTEGER PRIMARY KEY,
    profile_id INTEGER NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    flag_type TEXT NOT NULL,
    triggered_date TEXT NOT NULL,
    edpi_at_trigger REAL NOT NULL,
    acknowledged INTEGER NOT NULL DEFAULT 0 CHECK (acknowledged IN (0, 1))
);

CREATE INDEX idx_cycles_profile_id ON cycles(profile_id);
CREATE INDEX idx_protocol_candidates_profile_id ON protocol_candidates(profile_id);
CREATE INDEX idx_protocol_candidates_cycle_id ON protocol_candidates(cycle_id);
CREATE INDEX idx_candidate_sources_candidate_id ON protocol_candidate_sources(candidate_id);
CREATE INDEX idx_protocol_batteries_profile_id ON protocol_batteries(profile_id);
CREATE INDEX idx_protocol_batteries_cycle_id ON protocol_batteries(cycle_id);
CREATE INDEX idx_protocol_batteries_candidate_id ON protocol_batteries(candidate_id);
CREATE INDEX idx_sessions_profile_id ON sessions(profile_id);
CREATE INDEX idx_sessions_calibration_config_id ON sessions(calibration_config_id);
CREATE INDEX idx_shots_session_id ON shots(session_id);
CREATE INDEX idx_shots_profile_id ON shots(profile_id);
CREATE INDEX idx_tracking_data_session_id ON tracking_data(session_id);
CREATE INDEX idx_tracking_data_profile_id ON tracking_data(profile_id);
CREATE INDEX idx_mouse_samples_session_id ON mouse_samples(session_id);
CREATE INDEX idx_mouse_samples_shot_id ON mouse_samples(shot_id);
CREATE INDEX idx_outlier_flags_session_id ON outlier_flags(session_id);
CREATE INDEX idx_sensitivity_tests_profile_id ON sensitivity_tests(profile_id);
CREATE INDEX idx_sensitivity_tests_cycle_id ON sensitivity_tests(cycle_id);
CREATE INDEX idx_significance_tests_profile_id ON significance_tests(profile_id);
CREATE INDEX idx_significance_tests_cycle_id ON significance_tests(cycle_id);
CREATE INDEX idx_phase_history_profile_id ON phase_history(profile_id);
CREATE INDEX idx_phase_history_cycle_id ON phase_history(cycle_id);
CREATE INDEX idx_injury_risk_flags_profile_id ON injury_risk_flags(profile_id);

PRAGMA user_version = 1;
";
    }
}
