"""Deterministic P0-R6 scoring/statistical derivation and acceptance gates."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import platform
import statistics
import sys
from pathlib import Path
from typing import Any, Sequence


class ScoringStatisticsEvidenceError(ValueError):
    """Raised when a P0-R6 candidate or calculation is invalid."""


def load_json_object(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise ScoringStatisticsEvidenceError(f"invalid JSON: {path}") from error
    if not isinstance(value, dict):
        raise ScoringStatisticsEvidenceError("JSON root must be an object")
    return value


def write_new_json(path: Path, value: dict[str, Any]) -> None:
    if path.exists():
        raise FileExistsError(f"refusing to overwrite derived artifact: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True, allow_nan=False) + "\n",
        encoding="utf-8",
    )


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _finite(value: Any, field: str) -> float:
    try:
        number = float(value)
    except (TypeError, ValueError) as error:
        raise ScoringStatisticsEvidenceError(f"{field} must be numeric") from error
    if not math.isfinite(number):
        raise ScoringStatisticsEvidenceError(f"{field} must be finite")
    return number


def _bounds(bounds: Sequence[float], field: str) -> tuple[float, float]:
    if not isinstance(bounds, (list, tuple)) or len(bounds) != 2:
        raise ScoringStatisticsEvidenceError(f"{field} must contain [L,U]")
    lower = _finite(bounds[0], f"{field}.L")
    upper = _finite(bounds[1], f"{field}.U")
    if upper <= lower:
        raise ScoringStatisticsEvidenceError(f"{field} requires U > L")
    return lower, upper


def clamp(value: float, lower: float, upper: float) -> float:
    value = _finite(value, "value")
    lower = _finite(lower, "lower")
    upper = _finite(upper, "upper")
    if upper < lower:
        raise ScoringStatisticsEvidenceError("clamp upper must be >= lower")
    return min(upper, max(lower, value))


def normalize_high(value: float, lower: float, upper: float) -> float:
    lower, upper = _bounds([lower, upper], "normalization")
    return clamp((value - lower) / (upper - lower), 0.0, 1.0)


def normalize_low(value: float, lower: float, upper: float) -> float:
    return 1.0 - normalize_high(value, lower, upper)


def sample_sd(values: Sequence[float]) -> float:
    if len(values) < 2:
        raise ScoringStatisticsEvidenceError("sample SD requires at least two observations")
    checked = [_finite(value, "observation") for value in values]
    return statistics.stdev(checked)


def maximum_bounded_sample_sd(upper: float, observation_count: int) -> float:
    upper = _finite(upper, "upper")
    if upper <= 0 or observation_count < 2:
        raise ScoringStatisticsEvidenceError("bounded SD requires U > 0 and n >= 2")
    lower_count = observation_count // 2
    upper_count = observation_count - lower_count
    return upper * math.sqrt(
        lower_count * upper_count / (observation_count * (observation_count - 1))
    )


def submovement_penalty(count: float, lower: float, upper: float) -> float:
    return normalize_high(count, lower, upper)


def shot_performance_score(
    consistency: float,
    accuracy: float,
    reaction_speed: float,
    precision: float,
    submovement: float,
) -> float:
    components = [consistency, accuracy, reaction_speed, precision, submovement]
    if any(not 0.0 <= _finite(value, "component") <= 1.0 for value in components):
        raise ScoringStatisticsEvidenceError("score components must be within [0,1]")
    return 100.0 * (
        consistency * 0.35
        + accuracy * 0.30
        + reaction_speed * 0.20
        + precision * 0.15
        - submovement * 0.10
    )


def tracking_performance_score(
    consistency: float, time_on_target: float, precision: float
) -> float:
    components = [consistency, time_on_target, precision]
    if any(not 0.0 <= _finite(value, "component") <= 1.0 for value in components):
        raise ScoringStatisticsEvidenceError("score components must be within [0,1]")
    return 100.0 * (
        consistency * 0.4375 + time_on_target * 0.375 + precision * 0.1875
    )


def battery_performance_score(mode_scores: Sequence[float]) -> float:
    if len(mode_scores) != 4:
        raise ScoringStatisticsEvidenceError("a complete battery requires four mode scores")
    return statistics.fmean(_finite(value, "mode score") for value in mode_scores)


def consistency_tier(normalized_consistency: float) -> str:
    value = _finite(normalized_consistency, "normalized consistency")
    if not 0.0 <= value <= 1.0:
        raise ScoringStatisticsEvidenceError("normalized consistency must be within [0,1]")
    if value >= 0.8:
        return "S"
    if value >= 0.6:
        return "A"
    if value >= 0.4:
        return "B"
    if value >= 0.2:
        return "C"
    return "D"


def reaction_time_tier(milliseconds: float) -> str:
    value = _finite(milliseconds, "reaction time")
    if value < 0:
        raise ScoringStatisticsEvidenceError("reaction time must be non-negative")
    if value < 200.0:
        return "S"
    if value < 250.0:
        return "A"
    if value < 350.0:
        return "B"
    if value <= 500.0:
        return "C"
    return "D"


def worse_grade(first: str, second: str) -> str:
    order = {"S": 0, "A": 1, "B": 2, "C": 3, "D": 4}
    if first not in order or second not in order:
        raise ScoringStatisticsEvidenceError("grade must be S, A, B, C, or D")
    return first if order[first] >= order[second] else second


def coefficient_of_variation_percent(
    scores: Sequence[float], zero_tolerance: float
) -> float | None:
    if len(scores) < 2:
        raise ScoringStatisticsEvidenceError("CV requires at least two scores")
    tolerance = _finite(zero_tolerance, "zero tolerance")
    if tolerance < 0:
        raise ScoringStatisticsEvidenceError("zero tolerance must be non-negative")
    checked = [_finite(value, "score") for value in scores]
    mean = statistics.fmean(checked)
    if abs(mean) <= tolerance:
        return None
    return 100.0 * sample_sd(checked) / abs(mean)


def exact_paired_sign_flip_test(
    candidate_a: Sequence[float],
    candidate_b: Sequence[float],
    *,
    alpha: float,
    t_critical: float,
    comparison_tolerance: float = 1e-12,
) -> dict[str, Any]:
    if len(candidate_a) != len(candidate_b) or len(candidate_a) < 2:
        raise ScoringStatisticsEvidenceError("paired arrays must have equal n >= 2")
    alpha = _finite(alpha, "alpha")
    t_critical = _finite(t_critical, "t critical")
    tolerance = _finite(comparison_tolerance, "comparison tolerance")
    if not 0 < alpha < 1 or t_critical <= 0 or tolerance < 0:
        raise ScoringStatisticsEvidenceError("invalid test configuration")
    differences = [
        _finite(left, "candidate A score") - _finite(right, "candidate B score")
        for left, right in zip(candidate_a, candidate_b, strict=True)
    ]
    effect = statistics.fmean(differences)
    assignment_count = 1 << len(differences)
    extreme_count = 0
    threshold = abs(effect)
    for mask in range(assignment_count):
        permuted_sum = 0.0
        for index, difference in enumerate(differences):
            permuted_sum += difference if mask & (1 << index) else -difference
        permuted_mean = permuted_sum / len(differences)
        if abs(permuted_mean) + tolerance >= threshold:
            extreme_count += 1
    p_value = extreme_count / assignment_count
    standard_error = sample_sd(differences) / math.sqrt(len(differences))
    margin = t_critical * standard_error
    significant = p_value < alpha
    if not significant:
        result = "statistical_tie"
    elif effect > 0:
        result = "candidate_a"
    elif effect < 0:
        result = "candidate_b"
    else:
        raise ScoringStatisticsEvidenceError("significant zero effect is impossible")
    return {
        "n": len(differences),
        "differences": differences,
        "effect_estimate": effect,
        "confidence_interval_95": [effect - margin, effect + margin],
        "assignment_count": assignment_count,
        "extreme_count": extreme_count,
        "p_value": p_value,
        "alpha": alpha,
        "is_significant": significant,
        "result": result,
    }


def _assert_close(actual: float, expected: float, field: str, tolerance: float = 1e-12) -> None:
    if not math.isclose(actual, expected, rel_tol=0.0, abs_tol=tolerance):
        raise ScoringStatisticsEvidenceError(
            f"{field} mismatch: expected {expected!r}, got {actual!r}"
        )


def derive_scoring_statistics_evidence(
    candidate: dict[str, Any], geometry: dict[str, Any], signal_mode: dict[str, Any]
) -> dict[str, Any]:
    if candidate.get("candidate_id") != "sc8-p0-r6-scoring-statistics-candidate-v1":
        raise ScoringStatisticsEvidenceError("unexpected candidate ID")
    dependencies = candidate.get("dependencies", {})
    if dependencies.get("geometry_contract_id") != geometry.get("test_geometry_version"):
        raise ScoringStatisticsEvidenceError("geometry dependency mismatch")
    if dependencies.get("signal_mode_contract_id") != signal_mode.get("contract_id"):
        raise ScoringStatisticsEvidenceError("signal/mode dependency mismatch")
    if dependencies.get("signal_pipeline_version") != signal_mode.get("signal_pipeline_version"):
        raise ScoringStatisticsEvidenceError("signal pipeline dependency mismatch")
    if dependencies.get("mode_contract_version") != signal_mode.get("mode_contract_version"):
        raise ScoringStatisticsEvidenceError("mode contract dependency mismatch")

    bounds = candidate["normalization"]["bounds"]
    close_upper = max(geometry["flick_conditions"]["close_center_offset_deg"])
    far_upper = max(geometry["flick_conditions"]["far_center_offset_deg"])
    focal = geometry["camera"]["reference_focal_length_px"]
    micro_px = geometry["micro_correction"]["maximum_center_offset_px"]
    micro_upper = math.degrees(math.atan(micro_px / focal))
    tracking_upper = max(
        signal_mode["modes"]["tracking"]["linear"]["horizontal_half_range_deg"],
        signal_mode["modes"]["tracking"]["curved"]["horizontal_radius_deg"],
        signal_mode["modes"]["tracking"]["variable_speed"]["horizontal_amplitude_deg"],
    )
    shot_n = signal_mode["shared_shot_contract"]["post_adaptation_trials_at_minimum"]
    tracking_n = signal_mode["modes"]["tracking"]["post_adaptation_metric_windows"]
    expected = {
        "flick_close": {
            "precision_upper": close_upper,
            "consistency_upper": maximum_bounded_sample_sd(close_upper, shot_n),
        },
        "flick_far": {
            "precision_upper": far_upper,
            "consistency_upper": maximum_bounded_sample_sd(far_upper, shot_n),
        },
        "micro_correction": {
            "precision_upper": micro_upper,
            "consistency_upper": maximum_bounded_sample_sd(micro_upper, shot_n),
        },
        "tracking": {
            "precision_upper": tracking_upper,
            "consistency_upper": maximum_bounded_sample_sd(tracking_upper, tracking_n),
        },
    }
    for mode, values in expected.items():
        precision_field = (
            "tracking_deviation_rms_deg" if mode == "tracking" else "final_precision_error_deg"
        )
        _assert_close(bounds[mode][precision_field][1], values["precision_upper"], f"{mode} precision")
        _assert_close(
            bounds[mode]["consistency_sample_sd_deg"][1],
            values["consistency_upper"],
            f"{mode} consistency",
        )

    submovement = candidate["submovement_penalty"]["bounds_by_mode"]
    if any(value != [1.0, 6.0] for value in submovement.values()):
        raise ScoringStatisticsEvidenceError("Submovement bounds must be [1,6]")
    order = candidate["confirmatory"]["order_sequence"]
    if len(order) != 10 or order.count("A_then_B") != 5 or order.count("B_then_A") != 5:
        raise ScoringStatisticsEvidenceError("confirmatory order must balance five/five")

    shot_worked = shot_performance_score(0.8, 0.9, 0.75, 0.6, 0.2)
    tracking_worked = tracking_performance_score(0.8, 0.9, 0.7)
    positive = exact_paired_sign_flip_test(
        [75.0] * 10,
        [70.0] * 10,
        alpha=0.05,
        t_critical=candidate["confirmatory"]["t_critical_df9"],
    )
    symmetric = exact_paired_sign_flip_test(
        [71, 69, 72, 68, 73, 67, 74, 66, 75, 65],
        [70] * 10,
        alpha=0.05,
        t_critical=candidate["confirmatory"]["t_critical_df9"],
    )
    cv_fixture = coefficient_of_variation_percent([95, 100, 105], 1e-9)
    gates = {
        "dependency_identity": True,
        "formula_weights_and_ranges": (
            _close(shot_performance_score(1, 1, 1, 1, 0), 100)
            and _close(shot_performance_score(0, 0, 0, 0, 1), -10)
            and _close(tracking_performance_score(1, 1, 1), 100)
        ),
        "geometry_bounds": all(
            _close(bounds[mode]["tracking_deviation_rms_deg" if mode == "tracking" else "final_precision_error_deg"][1], values["precision_upper"])
            for mode, values in expected.items()
        ),
        "consistency_bounds": all(
            _close(bounds[mode]["consistency_sample_sd_deg"][1], values["consistency_upper"])
            for mode, values in expected.items()
        ),
        "normalization_boundaries": (
            normalize_high(-1, 0, 100) == 0
            and normalize_high(50, 0, 100) == 0.5
            and normalize_high(101, 0, 100) == 1
            and normalize_low(-1, 0, 100) == 1
            and normalize_low(50, 0, 100) == 0.5
            and normalize_low(101, 0, 100) == 0
        ),
        "submovement_boundaries": (
            submovement_penalty(0, 1, 6) == 0
            and submovement_penalty(1, 1, 6) == 0
            and submovement_penalty(3.5, 1, 6) == 0.5
            and submovement_penalty(6, 1, 6) == 1
            and submovement_penalty(7, 1, 6) == 1
        ),
        "component_completeness_policy": candidate["component_aggregation"]["zero_hit_submovement_policy"].endswith("penalty-is-1.0-fail-closed"),
        "worked_score_examples": _close(shot_worked, 77.0) and _close(tracking_worked, 81.875),
        "formula_negative_floor_retained": _close(shot_performance_score(0, 0, 0, 0, 1), -10),
        "grade_boundaries": (
            [reaction_time_tier(value) for value in [199.999, 200, 250, 350, 500, 500.001]]
            == ["S", "A", "B", "C", "C", "D"]
            and [consistency_tier(value) for value in [1, 0.8, 0.799, 0.6, 0.4, 0.2, 0.199, 0]]
            == ["S", "S", "A", "A", "B", "C", "D", "D"]
            and worse_grade("A", "C") == "C"
        ),
        "cv_zero_guard": (
            coefficient_of_variation_percent([-1e-10, 1e-10], 1e-9) is None
            and cv_fixture is not None
            and _close(cv_fixture, 5.0)
        ),
        "confirmatory_enumeration": positive["assignment_count"] == 1024,
        "confirmatory_positive_fixture": (
            positive["extreme_count"] == 2
            and positive["p_value"] == 0.001953125
            and positive["effect_estimate"] == 5
            and positive["confidence_interval_95"] == [5.0, 5.0]
            and positive["result"] == "candidate_a"
        ),
        "confirmatory_tie_fixture": symmetric["p_value"] == 1 and symmetric["result"] == "statistical_tie",
        "order_balance": order.count("A_then_B") == order.count("B_then_A") == 5,
    }
    if not all(gates.values()):
        failed = [name for name, passed in gates.items() if not passed]
        raise ScoringStatisticsEvidenceError(f"acceptance gates failed: {failed}")
    return {
        "analysis_version": "p0-r6-scoring-statistics-analysis-v1",
        "candidate_id": candidate["candidate_id"],
        "accepted": True,
        "dependencies": dependencies,
        "versions": candidate["versions"],
        "derived_bounds": expected,
        "fixtures": {
            "shot_worked_score": shot_worked,
            "tracking_worked_score": tracking_worked,
            "shot_formula_minimum": shot_performance_score(0, 0, 0, 0, 1),
            "shot_formula_maximum": shot_performance_score(1, 1, 1, 1, 0),
            "tracking_formula_minimum": tracking_performance_score(0, 0, 0),
            "tracking_formula_maximum": tracking_performance_score(1, 1, 1),
            "cv_percent": cv_fixture,
            "confirmatory_positive": positive,
            "confirmatory_symmetric": symmetric,
        },
        "acceptance_gates": gates,
        "gate_count": len(gates),
        "runtime": {
            "python": platform.python_version(),
            "platform": platform.platform(),
        },
    }


def _close(actual: float, expected: float) -> bool:
    return math.isclose(actual, expected, rel_tol=0.0, abs_tol=1e-12)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--geometry", type=Path, required=True)
    parser.add_argument("--signal-mode", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    evidence = derive_scoring_statistics_evidence(
        load_json_object(args.candidate),
        load_json_object(args.geometry),
        load_json_object(args.signal_mode),
    )
    evidence["inputs"] = {
        "candidate": str(args.candidate.as_posix()),
        "candidate_sha256": sha256_file(args.candidate),
        "geometry": str(args.geometry.as_posix()),
        "geometry_sha256": sha256_file(args.geometry),
        "signal_mode": str(args.signal_mode.as_posix()),
        "signal_mode_sha256": sha256_file(args.signal_mode),
    }
    write_new_json(args.output, evidence)
    print(json.dumps({"accepted": True, "gate_count": evidence["gate_count"], "output": str(args.output)}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
