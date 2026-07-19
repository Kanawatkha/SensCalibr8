import unittest

from calibration.analysis.p6_r1_analysis_dataset import (
    ANALYSIS_DATASET_VERSION,
    validate_dataset,
)


class AnalysisDatasetTests(unittest.TestCase):
    def test_validates_versioned_authoritative_dataset_without_recalculation(self):
        dataset = {
            "analysisDatasetVersion": ANALYSIS_DATASET_VERSION,
            "profile": {"profileId": 1, "profileName": "player", "mouseDpi": 1600},
            "authoritativeScores": [{
                "sensitivityTestId": 1,
                "cycleId": 1,
                "batteryId": 1,
                "formulaVersion": "sc8-performance-score-v1",
                "calibrationConfigurationVersion": "calibration_config_v1",
                "completedDate": "2026-07-17",
                "performanceScore": 77.0,
            }],
            "sessions": [{
                "sessionId": 1,
                "calibrationConfigurationVersion": "calibration_config_v1",
                "isCompleteBattery": True,
            }],
            "outlierAggregates": [{
                "analysisRunId": 1,
                "algorithmVersion": "sc8-outlier-v1",
                "calibrationConfigurationVersion": "calibration_config_v1",
            }],
            "edpiUnit": "dpi_x_in_game_sensitivity",
            "cm360Unit": "cm_per_360_degrees",
            "performanceScoreUnit": "performance_score_points",
            "reactionTimeUnit": "milliseconds",
        }
        self.assertIs(validate_dataset(dataset), dataset)
        self.assertEqual(dataset["authoritativeScores"][0]["performanceScore"], 77.0)

    def test_rejects_missing_version_or_authoritative_lineage(self):
        dataset = {
            "analysisDatasetVersion": ANALYSIS_DATASET_VERSION,
            "profile": {"profileId": 1},
            "authoritativeScores": [{"sensitivityTestId": 1, "cycleId": 1, "batteryId": 1}],
            "sessions": [],
            "outlierAggregates": [],
            "edpiUnit": "x",
            "cm360Unit": "x",
            "performanceScoreUnit": "x",
            "reactionTimeUnit": "x",
        }
        with self.assertRaises(ValueError):
            validate_dataset(dataset)


if __name__ == "__main__":
    unittest.main()
