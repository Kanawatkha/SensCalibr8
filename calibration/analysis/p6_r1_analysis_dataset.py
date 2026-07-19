"""Validation-only reader for the versioned SensCalibr8 analysis dataset.

This module accepts JSON objects produced by the C# Data/Service layer.  It never
opens the production SQLite database and never recomputes an authoritative score.
"""

ANALYSIS_DATASET_VERSION = "sc8-analysis-dataset-v1"


def validate_dataset(dataset):
    """Return a validated dataset or raise ValueError when its lineage is incomplete."""
    if not isinstance(dataset, dict):
        raise ValueError("Analysis dataset must be an object.")
    if dataset.get("analysisDatasetVersion") != ANALYSIS_DATASET_VERSION:
        raise ValueError("Unsupported analysis dataset version.")
    for field in (
        "profile",
        "authoritativeScores",
        "sessions",
        "outlierAggregates",
        "edpiUnit",
        "cm360Unit",
        "performanceScoreUnit",
        "reactionTimeUnit",
    ):
        if field not in dataset:
            raise ValueError("Analysis dataset is missing " + field + ".")
    profile = dataset["profile"]
    if not isinstance(profile, dict) or profile.get("profileId", 0) <= 0:
        raise ValueError("Analysis dataset profile identity is invalid.")
    for score in dataset["authoritativeScores"]:
        _require_positive(score, "sensitivityTestId")
        _require_positive(score, "cycleId")
        _require_positive(score, "batteryId")
        _require_non_empty(score, "formulaVersion")
        _require_non_empty(score, "calibrationConfigurationVersion")
        _require_non_empty(score, "completedDate")
    for session in dataset["sessions"]:
        _require_positive(session, "sessionId")
        _require_non_empty(session, "calibrationConfigurationVersion")
        if "isCompleteBattery" not in session:
            raise ValueError("Analysis session must state complete-battery status.")
    for aggregate in dataset["outlierAggregates"]:
        _require_positive(aggregate, "analysisRunId")
        _require_non_empty(aggregate, "algorithmVersion")
        _require_non_empty(aggregate, "calibrationConfigurationVersion")
    return dataset


def _require_positive(value, field):
    if value.get(field, 0) <= 0:
        raise ValueError(field + " must be positive.")


def _require_non_empty(value, field):
    if not value.get(field):
        raise ValueError(field + " is required.")
