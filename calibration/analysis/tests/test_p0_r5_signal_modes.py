import json
import math
import tempfile
import unittest
from pathlib import Path

import numpy as np

from calibration.analysis.p0_r5_signal_modes import (
    SignalModeEvidenceError,
    angular_velocity_magnitude,
    default_sosfiltfilt_padlen,
    derive_signal_mode_evidence,
    design_butterworth_lowpass_sos,
    detect_submovements_from_velocity,
    load_json_object,
    shot_condition_counts,
    sos_frequency_response,
    sosfiltfilt_odd,
    tracking_condition_counts,
    tracking_position,
    tracking_window_metrics,
    write_new_json,
)


ROOT = Path(__file__).resolve().parents[3]
CANDIDATE_PATH = ROOT / "calibration/plans/p0-r5-signal-mode-candidate-v1.json"
TIMING_PATH = ROOT / "calibration/plans/p0-r3-timing-contract-accepted-v1.json"
GEOMETRY_PATH = ROOT / "calibration/plans/p0-r4-geometry-accepted-v1.json"


class P0R5SignalModeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.candidate = load_json_object(CANDIDATE_PATH)
        cls.timing = load_json_object(TIMING_PATH)
        cls.geometry = load_json_object(GEOMETRY_PATH)
        cls.sos = design_butterworth_lowpass_sos(5, 7.0, 1000.0)

    def test_butterworth_design_has_expected_response_and_pad_length(self):
        self.assertEqual(self.sos.shape, (3, 6))
        self.assertEqual(default_sosfiltfilt_padlen(self.sos), 18)
        self.assertAlmostEqual(abs(sos_frequency_response(self.sos, 0.0, 1000.0)), 1.0, places=12)
        self.assertAlmostEqual(
            abs(sos_frequency_response(self.sos, 7.0, 1000.0)), math.sqrt(0.5), places=10
        )

    def test_forward_backward_filter_has_zero_impulse_peak_displacement(self):
        impulse = np.zeros(2001)
        impulse[1000] = 1.0
        filtered = sosfiltfilt_odd(self.sos, impulse, 18)
        self.assertEqual(int(np.argmax(np.abs(filtered))), 1000)
        central_response = filtered[800:1201]
        np.testing.assert_allclose(
            central_response, central_response[::-1], atol=1e-12, rtol=0.0
        )

    def test_filter_rejects_short_segment_for_frozen_pad_length(self):
        with self.assertRaises(SignalModeEvidenceError):
            sosfiltfilt_odd(self.sos, np.zeros(19), 18)
        filtered = sosfiltfilt_odd(self.sos, np.zeros(20), 18)
        self.assertEqual(len(filtered), 20)

    def test_angular_velocity_uses_first_difference_euclidean_magnitude(self):
        velocity = angular_velocity_magnitude([0.0, 0.003, 0.006], [0.0, 0.004, 0.008], 1000.0)
        np.testing.assert_allclose(velocity, [0.0, 5.0, 5.0], atol=1e-12)

    def test_detector_boundaries_are_explicit(self):
        below = np.full(200, 7.999)
        at_start = np.zeros(200)
        at_start[10:50] = 8.0
        at_start[50:] = 3.999
        self.assertEqual(len(detect_submovements_from_velocity(below, 1000, 8, 4, 80)), 0)
        events = detect_submovements_from_velocity(at_start, 1000, 8, 4, 80)
        self.assertEqual(events, [{"onset_sample": 10, "end_sample": 50}])

    def test_refractory_less_than_boundary_merges_and_exact_boundary_separates(self):
        merged = np.zeros(300)
        merged[10:20] = 9.0
        merged[50:60] = 9.0
        exact = np.zeros(300)
        exact[10:20] = 9.0
        exact[100:110] = 9.0
        self.assertEqual(len(detect_submovements_from_velocity(merged, 1000, 8, 4, 80)), 1)
        self.assertEqual(len(detect_submovements_from_velocity(exact, 1000, 8, 4, 80)), 2)

    def test_shot_condition_balance_is_30_with_three_or_four_each(self):
        for ordinal in range(1, 10):
            counts = shot_condition_counts(ordinal)
            self.assertEqual(len(counts), 9)
            self.assertEqual(sum(counts.values()), 30)
            self.assertEqual(min(counts.values()), 3)
            self.assertEqual(max(counts.values()), 4)

    def test_tracking_block_has_complete_pattern_size_cross_product(self):
        counts = tracking_condition_counts()
        self.assertEqual(len(counts), 9)
        self.assertEqual(set(counts.values()), {1})

    def test_tracking_paths_stay_inside_frozen_angular_limits(self):
        times = np.linspace(0.0, 6.0, 6001)
        for pattern in ("linear", "curved", "variable_speed"):
            positions = np.asarray([tracking_position(pattern, float(value)) for value in times])
            self.assertLessEqual(float(np.max(np.abs(positions[:, 0]))), 15.0)
            self.assertLessEqual(float(np.max(np.abs(positions[:, 1]))), 25.0)

    def test_tracking_metrics_are_interval_weighted_not_frame_count_weighted(self):
        regular_times = np.arange(0.0, 6.25, 0.25)
        regular_errors = np.where(regular_times < 3.0, 0.25, 1.0)
        irregular_times = np.asarray(
            [0.0, 0.1, 0.7, 1.0, 1.8, 2.0, 2.6, 3.0, 3.4, 4.0, 4.9, 5.0, 5.8, 6.0]
        )
        irregular_errors = np.where(irregular_times < 3.0, 0.25, 1.0)
        regular = tracking_window_metrics(regular_times, regular_errors, 0.5, 6.0, 1.0)
        irregular = tracking_window_metrics(irregular_times, irregular_errors, 0.5, 6.0, 1.0)
        self.assertEqual(regular, irregular)
        self.assertEqual([row["time_on_target_percent"] for row in regular], [100.0] * 3 + [0.0] * 3)

    def test_tracking_metrics_reject_missing_trial_boundary(self):
        with self.assertRaises(SignalModeEvidenceError):
            tracking_window_metrics([0.1, 1.0, 2.0, 6.0], [0.0, 0.0, 0.0, 0.0], 0.5, 6.0, 1.0)

    def test_dependency_mismatch_is_rejected(self):
        bad_timing = dict(self.timing)
        bad_timing["contract_id"] = "wrong"
        with self.assertRaises(SignalModeEvidenceError):
            derive_signal_mode_evidence(self.candidate, bad_timing, self.geometry)

    def test_candidate_passes_every_automated_gate(self):
        evidence = derive_signal_mode_evidence(self.candidate, self.timing, self.geometry)
        self.assertTrue(evidence["accepted"])
        self.assertTrue(all(evidence["gates"].values()))
        self.assertEqual(evidence["modes"]["shot_trials"]["adaptation"], 15)
        self.assertEqual(evidence["modes"]["tracking"]["post_adaptation_windows"], 54)

    def test_derivation_is_byte_deterministic(self):
        first = derive_signal_mode_evidence(self.candidate, self.timing, self.geometry)
        second = derive_signal_mode_evidence(self.candidate, self.timing, self.geometry)
        self.assertEqual(
            json.dumps(first, sort_keys=True, separators=(",", ":")),
            json.dumps(second, sort_keys=True, separators=(",", ":")),
        )

    def test_derived_writer_refuses_overwrite(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "evidence.json"
            write_new_json(path, {"accepted": True})
            with self.assertRaises(FileExistsError):
                write_new_json(path, {"accepted": False})


if __name__ == "__main__":
    unittest.main()
