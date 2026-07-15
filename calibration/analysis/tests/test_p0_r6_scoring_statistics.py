import json
import math
import tempfile
import unittest
from pathlib import Path

from calibration.analysis.p0_r6_scoring_statistics import (
    ScoringStatisticsEvidenceError,
    battery_performance_score,
    coefficient_of_variation_percent,
    consistency_tier,
    derive_scoring_statistics_evidence,
    exact_paired_sign_flip_test,
    load_json_object,
    maximum_bounded_sample_sd,
    normalize_high,
    normalize_low,
    reaction_time_tier,
    shot_performance_score,
    submovement_penalty,
    tracking_performance_score,
    worse_grade,
    write_new_json,
)


ROOT = Path(__file__).resolve().parents[3]
CANDIDATE_PATH = ROOT / "calibration/plans/p0-r6-scoring-statistics-candidate-v1.json"
GEOMETRY_PATH = ROOT / "calibration/plans/p0-r4-geometry-accepted-v1.json"
SIGNAL_MODE_PATH = ROOT / "calibration/plans/p0-r5-signal-mode-accepted-v1.json"


class P0R6ScoringStatisticsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.candidate = load_json_object(CANDIDATE_PATH)
        cls.geometry = load_json_object(GEOMETRY_PATH)
        cls.signal_mode = load_json_object(SIGNAL_MODE_PATH)

    def test_normalization_direction_clamping_and_invalid_bounds(self):
        self.assertEqual(normalize_high(-1, 0, 100), 0)
        self.assertEqual(normalize_high(50, 0, 100), 0.5)
        self.assertEqual(normalize_high(101, 0, 100), 1)
        self.assertEqual(normalize_low(-1, 0, 100), 1)
        self.assertEqual(normalize_low(50, 0, 100), 0.5)
        self.assertEqual(normalize_low(101, 0, 100), 0)
        with self.assertRaises(ScoringStatisticsEvidenceError):
            normalize_high(1, 1, 1)

    def test_bounded_sample_sd_derivation_matches_extreme_fixtures(self):
        for n in (15, 54):
            values = [0.0] * (n // 2) + [1.0] * (n - n // 2)
            mean = sum(values) / n
            observed = math.sqrt(sum((value - mean) ** 2 for value in values) / (n - 1))
            self.assertAlmostEqual(maximum_bounded_sample_sd(1.0, n), observed, places=15)

    def test_submovement_linear_mapping_boundaries(self):
        self.assertEqual(submovement_penalty(0, 1, 6), 0)
        self.assertEqual(submovement_penalty(1, 1, 6), 0)
        self.assertEqual(submovement_penalty(3.5, 1, 6), 0.5)
        self.assertEqual(submovement_penalty(6, 1, 6), 1)
        self.assertEqual(submovement_penalty(8, 1, 6), 1)

    def test_worked_scores_and_unclamped_formula_range(self):
        self.assertAlmostEqual(shot_performance_score(0.8, 0.9, 0.75, 0.6, 0.2), 77.0)
        self.assertAlmostEqual(tracking_performance_score(0.8, 0.9, 0.7), 81.875)
        self.assertEqual(shot_performance_score(0, 0, 0, 0, 1), -10)
        self.assertAlmostEqual(shot_performance_score(1, 1, 1, 1, 0), 100)
        self.assertEqual(tracking_performance_score(0, 0, 0), 0)
        self.assertEqual(tracking_performance_score(1, 1, 1), 100)
        with self.assertRaises(ScoringStatisticsEvidenceError):
            shot_performance_score(1.01, 1, 1, 1, 0)

    def test_battery_requires_exactly_four_complete_mode_scores(self):
        self.assertEqual(battery_performance_score([60, 70, 80, 90]), 75)
        with self.assertRaises(ScoringStatisticsEvidenceError):
            battery_performance_score([60, 70, 80])

    def test_reaction_and_consistency_tier_boundaries(self):
        self.assertEqual(
            [reaction_time_tier(value) for value in [199.999, 200, 250, 350, 500, 500.001]],
            ["S", "A", "B", "C", "C", "D"],
        )
        self.assertEqual(
            [consistency_tier(value) for value in [1, 0.8, 0.799, 0.6, 0.4, 0.2, 0.199, 0]],
            ["S", "S", "A", "A", "B", "C", "D", "D"],
        )
        self.assertEqual(worse_grade("S", "D"), "D")
        self.assertEqual(worse_grade("B", "A"), "B")

    def test_cv_uses_sample_sd_and_zero_tolerance(self):
        self.assertAlmostEqual(coefficient_of_variation_percent([95, 100, 105], 1e-9), 5.0)
        self.assertIsNone(coefficient_of_variation_percent([-1e-10, 1e-10], 1e-9))
        self.assertIsNotNone(coefficient_of_variation_percent([-1e-8, 3e-8], 1e-9))

    def test_exact_sign_flip_positive_fixture_enumerates_every_assignment(self):
        result = exact_paired_sign_flip_test(
            [75] * 10, [70] * 10, alpha=0.05, t_critical=2.2621571628540993
        )
        self.assertEqual(result["assignment_count"], 1024)
        self.assertEqual(result["extreme_count"], 2)
        self.assertEqual(result["p_value"], 0.001953125)
        self.assertEqual(result["effect_estimate"], 5)
        self.assertEqual(result["confidence_interval_95"], [5.0, 5.0])
        self.assertEqual(result["result"], "candidate_a")

    def test_exact_sign_flip_is_two_sided_and_retains_zero_differences(self):
        a = [71, 69, 72, 68, 73, 67, 74, 66, 75, 65]
        result = exact_paired_sign_flip_test(
            a, [70] * 10, alpha=0.05, t_critical=2.2621571628540993
        )
        self.assertEqual(result["effect_estimate"], 0)
        self.assertEqual(result["p_value"], 1)
        self.assertEqual(result["result"], "statistical_tie")
        zero_result = exact_paired_sign_flip_test(
            [1, 1, 1], [1, 1, 1], alpha=0.05, t_critical=4.302652729911275
        )
        self.assertEqual(zero_result["differences"], [0.0, 0.0, 0.0])
        self.assertEqual(zero_result["p_value"], 1)

    def test_candidate_derives_all_gates_and_dependency_values(self):
        evidence = derive_scoring_statistics_evidence(
            self.candidate, self.geometry, self.signal_mode
        )
        self.assertTrue(evidence["accepted"])
        self.assertEqual(evidence["gate_count"], 15)
        self.assertTrue(all(evidence["acceptance_gates"].values()))
        self.assertAlmostEqual(
            evidence["derived_bounds"]["micro_correction"]["precision_upper"],
            1.500295901168436,
            places=15,
        )

    def test_candidate_rejects_dependency_and_bound_drift(self):
        drifted = json.loads(json.dumps(self.candidate))
        drifted["dependencies"]["geometry_contract_id"] = "wrong"
        with self.assertRaises(ScoringStatisticsEvidenceError):
            derive_scoring_statistics_evidence(drifted, self.geometry, self.signal_mode)
        drifted = json.loads(json.dumps(self.candidate))
        drifted["normalization"]["bounds"]["flick_far"]["final_precision_error_deg"][1] = 39
        with self.assertRaises(ScoringStatisticsEvidenceError):
            derive_scoring_statistics_evidence(drifted, self.geometry, self.signal_mode)

    def test_append_only_evidence_writer_refuses_overwrite(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "evidence.json"
            write_new_json(path, {"accepted": True})
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), {"accepted": True})
            with self.assertRaises(FileExistsError):
                write_new_json(path, {"accepted": False})

    def test_derivation_is_byte_reproducible(self):
        first = derive_scoring_statistics_evidence(self.candidate, self.geometry, self.signal_mode)
        second = derive_scoring_statistics_evidence(self.candidate, self.geometry, self.signal_mode)
        encoded_first = json.dumps(first, indent=2, sort_keys=True, allow_nan=False)
        encoded_second = json.dumps(second, indent=2, sort_keys=True, allow_nan=False)
        self.assertEqual(encoded_first, encoded_second)


if __name__ == "__main__":
    unittest.main()
