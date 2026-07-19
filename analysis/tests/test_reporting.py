import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parents[1] / "src"))

from senscalibr8_analysis.reporting import CHART_IDS, build_chart_artifacts, write_html_report


def _input():
    document = {
        "reportInputVersion": "sc8-html-report-input-v3",
        "profile": {"profileId": 7, "name": "Pilot", "mouseDpi": 1600},
        "scores": [
            {"cycleId": 1, "phase": 1, "edpi": 280, "sensitivityValue": 0.175, "performanceScore": 70, "grade": "B", "formulaVersion": "performance-score-v1", "configurationVersion": "calibration-v1", "completedDate": "2026-07-17"},
            {"cycleId": 1, "phase": 2, "edpi": 300, "sensitivityValue": 0.1875, "performanceScore": 75, "grade": "A", "formulaVersion": "performance-score-v1", "configurationVersion": "calibration-v1", "completedDate": "2026-07-18"},
        ],
        "shots": [
            {"sessionId": 11, "date": "2026-07-17", "cycleId": 1, "phase": 1, "mode": "flick_close", "sensitivityValue": 0.175, "edpi": 280, "finalPrecisionErrorDeg": 0.5, "signedAimErrorDeg": 0.3, "submovementCount": 2, "targetCenterPosition": "5,0", "configurationVersion": "calibration-v1"},
            {"sessionId": 12, "date": "2026-07-17", "cycleId": 1, "phase": 1, "mode": "flick_far", "sensitivityValue": 0.175, "edpi": 280, "finalPrecisionErrorDeg": 0.7, "signedAimErrorDeg": 0.2, "submovementCount": 3, "targetCenterPosition": "-5,0", "configurationVersion": "calibration-v1"},
            {"sessionId": 13, "date": "2026-07-18", "cycleId": 1, "phase": 2, "mode": "flick_close", "sensitivityValue": 0.1875, "edpi": 300, "finalPrecisionErrorDeg": 0.4, "signedAimErrorDeg": -0.1, "submovementCount": 1, "targetCenterPosition": "5,0", "configurationVersion": "calibration-v1"},
            {"sessionId": 14, "date": "2026-07-18", "cycleId": 1, "phase": 2, "mode": "flick_far", "sensitivityValue": 0.1875, "edpi": 300, "finalPrecisionErrorDeg": 0.6, "signedAimErrorDeg": -0.2, "submovementCount": 2, "targetCenterPosition": "-5,0", "configurationVersion": "calibration-v1"},
        ],
        "trackingErrors": [
            {"date": "2026-07-17", "cycleId": 1, "phase": 1, "sensitivityValue": 0.175, "deviationRmsDeg": 0.8, "configurationVersion": "calibration-v1"},
            {"date": "2026-07-18", "cycleId": 1, "phase": 2, "sensitivityValue": 0.1875, "deviationRmsDeg": 0.5, "configurationVersion": "calibration-v1"},
        ],
        "winners": [
            {"cycleNumber": 1, "phase": 1, "edpi": 280, "sensitivityValue": 0.175, "timestamp": "2026-07-17T00:00:00Z"},
            {"cycleNumber": 1, "phase": 2, "edpi": 300, "sensitivityValue": 0.1875, "timestamp": "2026-07-18T00:00:00Z"},
        ],
    }
    document["reactions"] = [
        {"sessionId": 11, "date": "2026-07-17", "cycleId": 1, "phase": 1, "mode": "flick_close", "sensitivityValue": 0.175, "reactionTimeMs": 230, "configurationVersion": "calibration-v1"},
        {"sessionId": 12, "date": "2026-07-17", "cycleId": 1, "phase": 1, "mode": "flick_far", "sensitivityValue": 0.175, "reactionTimeMs": 310, "configurationVersion": "calibration-v1"},
        {"sessionId": 13, "date": "2026-07-18", "cycleId": 1, "phase": 2, "mode": "flick_close", "sensitivityValue": 0.1875, "reactionTimeMs": 210, "configurationVersion": "calibration-v1"},
    ]
    document["profileComparisons"] = [
        {"profileId": 7, "profileName": "Pilot", "cycleNumber": 1, "winnerEdpi": 300},
        {"profileId": 8, "profileName": "Peer", "cycleNumber": 1, "winnerEdpi": 280},
    ]
    document["outliers"] = [
        {"phase": 1, "mode": "flick_close", "sensitivityValue": 0.175, "metricName": "final_precision_error_deg", "inclusiveMean": 0.6, "flaggedExcludedMean": 0.5, "observationCount": 15, "flaggedCount": 1, "algorithmVersion": "scientific-rigor-v1", "configurationVersion": "calibration-v1"},
    ]
    document["fatigue"] = [
        {"sessionId": 11, "date": "2026-07-17", "mode": "flick_close", "fatigueFlag": False, "scoreChangePercentage": -2, "algorithmVersion": "scientific-rigor-v1"},
    ]
    document["warnings"] = [
        {"flagType": "low_edpi_wrist_strain", "triggeredDate": "2026-07-17", "edpi": 180, "acknowledged": False},
    ]
    document["significance"] = [
        {"phase": 1, "candidateAEdpi": 280, "candidateBEdpi": 300, "alpha": 0.05, "pValue": 0.03, "isSignificant": True, "result": "candidate_a_wins", "method": "paired-sign-flip", "formulaVersion": "performance-score-v1"},
    ]
    return document


class ReportingTests(unittest.TestCase):
    def test_all_five_charts_are_generated_from_persisted_evidence(self):
        charts = build_chart_artifacts(_input())
        self.assertEqual(CHART_IDS, tuple(chart.chart_id for chart in charts))
        self.assertTrue(all(chart.status == "ready" for chart in charts))
        self.assertTrue(all(chart.image_data_uri.startswith("data:image/png;base64,") for chart in charts))

    def test_zero_direction_rows_are_not_relabelled_as_over_or_underflick(self):
        document = _input()
        document["shots"] = [dict(document["shots"][0], targetCenterPosition="0,0")]
        chart = build_chart_artifacts(document)[1]
        self.assertEqual("insufficient_data", chart.status)

    def test_report_is_offline_and_contains_all_chart_sections(self):
        with tempfile.TemporaryDirectory() as directory:
            path = write_html_report(_input(), Path(directory) / "report.html")
            html = path.read_text(encoding="utf-8")
        self.assertIn("SensCalibr8 evidence report", html)
        self.assertIn("data:image/png;base64,", html)
        self.assertNotIn("https://", html)
        for chart_id in CHART_IDS:
            self.assertIn(f"id='{chart_id}'", html)
        for section_id in ("decision-context", "authoritative-results", "outlier-sensitivity-analysis", "fatigue-status", "ergonomic-warnings", "scientific-notes"):
            self.assertIn(f"id='{section_id}'", html)

    def test_missing_version_is_rejected(self):
        document = _input()
        del document["scores"][0]["formulaVersion"]
        with self.assertRaises(ValueError):
            build_chart_artifacts(document)

    def test_composition_never_infers_missing_winner_or_fills_empty_evidence(self):
        document = _input()
        document["winners"] = []
        document["outliers"] = []
        document["fatigue"] = []
        document["warnings"] = []
        document["significance"] = []
        with tempfile.TemporaryDirectory() as directory:
            html = write_html_report(document, Path(directory) / "report.html").read_text(encoding="utf-8")
        self.assertIn("does not infer", html)
        self.assertIn("No persisted outlier analysis run", html)
        self.assertIn("No persisted fatigue evaluation", html)

    def test_empty_partial_and_statistical_tie_evidence_remains_explicitly_non_authoritative(self):
        document = _input()
        document["scores"] = []
        document["shots"] = []
        document["trackingErrors"] = []
        document["winners"] = []
        document["reactions"] = []
        document["profileComparisons"] = []
        document["outliers"] = []
        document["fatigue"] = []
        document["warnings"] = []
        document["significance"] = [{"phase": 1, "candidateAEdpi": 280, "candidateBEdpi": 300,
                                      "alpha": 0.05, "pValue": 0.5, "isSignificant": False,
                                      "result": "statistical_tie", "method": "paired-sign-flip",
                                      "formulaVersion": "performance-score-v1"}]
        charts = build_chart_artifacts(document)
        self.assertTrue(all(chart.status == "insufficient_data" for chart in charts))
        with tempfile.TemporaryDirectory() as directory:
            html = write_html_report(document, Path(directory) / "report.html").read_text(encoding="utf-8")
        self.assertIn("does not infer", html)
        self.assertIn("statistical_tie", html)
        self.assertIn("Insufficient data: chart not estimated.", html)

    def test_persisted_score_outlier_and_fatigue_values_are_presented_without_recalculation(self):
        document = _input()
        document["scores"] = [{"cycleId": 1, "phase": 1, "edpi": 280,
                               "sensitivityValue": 0.175, "performanceScore": 77, "grade": "A",
                               "formulaVersion": "performance-score-v1", "configurationVersion": "calibration-v1",
                               "completedDate": "2026-07-19"}]
        document["outliers"][0]["inclusiveMean"] = 77
        document["outliers"][0]["flaggedExcludedMean"] = 70
        document["fatigue"][0]["fatigueFlag"] = True
        document["fatigue"][0]["scoreChangePercentage"] = -16
        with tempfile.TemporaryDirectory() as directory:
            html = write_html_report(document, Path(directory) / "report.html").read_text(encoding="utf-8")
        self.assertIn("<td>280</td><td>77</td><td>A</td>", html)
        self.assertIn("<td>77</td><td>70</td>", html)
        self.assertIn("<td>-16</td><td>Flagged</td>", html)
        self.assertIn("parallel diagnostic only", html)
        self.assertIn("does not exclude a session from Winner selection", html)
