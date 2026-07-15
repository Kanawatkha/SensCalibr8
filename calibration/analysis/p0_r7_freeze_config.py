"""Build and validate the immutable P0-R7 Calibration Configuration v1."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import sys
from pathlib import Path
from typing import Any, Sequence

from calibration.analysis.p0_r6_scoring_statistics import (
    coefficient_of_variation_percent,
    exact_paired_sign_flip_test,
    reaction_time_tier,
    shot_performance_score,
    tracking_performance_score,
)


class CalibrationFreezeError(ValueError):
    """Raised when a source or frozen configuration violates the P0-R7 contract."""


REQUIRED_DATABASE_FIELDS = (
    "config_version",
    "normalization_version",
    "signal_pipeline_version",
    "test_geometry_version",
    "created_date",
    "input_sampling_rate_hz",
    "resampling_tolerance_ms",
    "timing_acceptance_policy",
    "butterworth_order",
    "cutoff_frequency_hz",
    "submovement_start_deg_per_sec",
    "submovement_end_deg_per_sec",
    "refractory_period_ms",
    "normalization_bounds_json",
    "submovement_bounds_by_mode_json",
    "consistency_tier_cutpoints_json",
    "scoring_zero_tolerance",
    "target_geometry_json",
    "tracking_contract_json",
    "confirmatory_contract_json",
)

JSON_DATABASE_FIELDS = (
    "normalization_bounds_json",
    "submovement_bounds_by_mode_json",
    "consistency_tier_cutpoints_json",
    "target_geometry_json",
    "tracking_contract_json",
    "confirmatory_contract_json",
)


def load_json_object(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise CalibrationFreezeError(f"cannot load valid JSON object: {path}") from error
    if not isinstance(value, dict):
        raise CalibrationFreezeError(f"JSON root must be an object: {path}")
    return value


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical_json(value: Any) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False)


def pretty_json_bytes(value: Any) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True, allow_nan=False) + "\n").encode("utf-8")


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise CalibrationFreezeError(message)


def _source_map(plan: dict[str, Any]) -> dict[str, dict[str, str]]:
    entries = plan.get("source_contracts")
    _require(isinstance(entries, list), "freeze plan source_contracts must be an array")
    result: dict[str, dict[str, str]] = {}
    for entry in entries:
        _require(isinstance(entry, dict), "source contract entry must be an object")
        role = entry.get("role")
        _require(isinstance(role, str) and role not in result, "source roles must be unique strings")
        _require(isinstance(entry.get("path"), str), f"{role} path is required")
        _require(isinstance(entry.get("sha256"), str), f"{role} SHA-256 is required")
        result[role] = entry
    return result


def load_verified_sources(root: Path, plan: dict[str, Any]) -> dict[str, dict[str, Any]]:
    expected_roles = {
        "timing",
        "timing_owner_waiver",
        "geometry",
        "signal_mode",
        "scoring_statistics_acceptance",
        "scoring_statistics_payload",
    }
    sources = _source_map(plan)
    _require(set(sources) == expected_roles, "freeze plan source-role set is incomplete or unexpected")
    loaded: dict[str, dict[str, Any]] = {}
    for role, entry in sources.items():
        path = root / entry["path"]
        _require(path.is_file(), f"missing source contract: {entry['path']}")
        actual_hash = sha256_file(path)
        _require(actual_hash == entry["sha256"], f"source hash mismatch for {role}")
        loaded[role] = load_json_object(path)
    return loaded


def validate_source_relationships(sources: dict[str, dict[str, Any]]) -> None:
    timing = sources["timing"]
    waiver = sources["timing_owner_waiver"]
    geometry = sources["geometry"]
    signal_mode = sources["signal_mode"]
    envelope = sources["scoring_statistics_acceptance"]
    payload = sources["scoring_statistics_payload"]

    _require(timing.get("status") == "accepted", "P0-R3 timing contract is not accepted")
    _require(geometry.get("status") == "accepted", "P0-R4 geometry contract is not accepted")
    _require(signal_mode.get("status") == "accepted", "P0-R5 signal/mode contract is not accepted")
    _require(envelope.get("status") == "accepted", "P0-R6 acceptance envelope is not accepted")
    _require(payload.get("status") == "candidate", "P0-R6 payload status changed unexpectedly")
    _require(
        waiver.get("decision") == "accepted-by-project-owner-calibration-waiver"
        and waiver.get("strict_confirmation_passed") is False,
        "P0-R3 owner-waiver limitation is missing or altered",
    )
    _require(
        waiver.get("accepted_contract_id") == timing.get("contract_id"),
        "owner waiver does not identify the accepted timing contract",
    )
    _require(
        signal_mode.get("dependencies", {}).get("timing_contract_id") == timing.get("contract_id"),
        "P0-R5 timing dependency mismatch",
    )
    _require(
        signal_mode.get("dependencies", {}).get("test_geometry_version")
        == geometry.get("test_geometry_version"),
        "P0-R5 geometry dependency mismatch",
    )
    dependencies = envelope.get("dependencies", {})
    _require(dependencies.get("geometry_contract_id") == geometry.get("test_geometry_version"), "P0-R6 geometry dependency mismatch")
    _require(dependencies.get("signal_mode_contract_id") == signal_mode.get("contract_id"), "P0-R6 mode dependency mismatch")
    _require(dependencies.get("signal_pipeline_version") == signal_mode.get("signal_pipeline_version"), "P0-R6 signal version mismatch")
    _require(dependencies.get("mode_contract_version") == signal_mode.get("mode_contract_version"), "P0-R6 mode version mismatch")
    _require(envelope.get("accepted_candidate_id") == payload.get("candidate_id"), "P0-R6 accepted payload ID mismatch")
    _require(envelope.get("versions") == payload.get("versions"), "P0-R6 version map mismatch")
    _require(
        timing.get("input_sampling_rate_hz") == signal_mode.get("signal_pipeline", {}).get("sampling_rate_hz"),
        "P0-R3/P0-R5 sampling-rate mismatch",
    )
    _require(
        timing.get("filter_padlen_samples") == signal_mode.get("signal_pipeline", {}).get("pad_length_samples"),
        "P0-R3/P0-R5 filter-pad mismatch",
    )


def _database_payloads(
    timing: dict[str, Any],
    geometry: dict[str, Any],
    signal_mode: dict[str, Any],
    envelope: dict[str, Any],
    scoring: dict[str, Any],
) -> dict[str, str]:
    normalization = {
        "normalization_version": scoring["versions"]["normalization_version"],
        **copy.deepcopy(scoring["normalization"]),
    }
    submovement = {
        "normalization_version": scoring["versions"]["normalization_version"],
        **copy.deepcopy(scoring["submovement_penalty"]),
        "zero_hit_submovement_policy": scoring["component_aggregation"]["zero_hit_submovement_policy"],
    }
    consistency = {
        "consistency_tier_version": scoring["versions"]["consistency_tier_version"],
        **copy.deepcopy(scoring["grading"]),
    }
    confirmatory = {
        "confirmatory_contract_version": scoring["versions"]["confirmatory_contract_version"],
        **copy.deepcopy(scoring["confirmatory"]),
        "numerical_policy": copy.deepcopy(envelope["numerical_policy"]),
    }
    return {
        "normalization_bounds_json": canonical_json(normalization),
        "submovement_bounds_by_mode_json": canonical_json(submovement),
        "consistency_tier_cutpoints_json": canonical_json(consistency),
        "target_geometry_json": canonical_json(geometry),
        "tracking_contract_json": canonical_json(signal_mode),
        "confirmatory_contract_json": canonical_json(confirmatory),
    }


def build_configuration(
    plan: dict[str, Any], sources: dict[str, dict[str, Any]]
) -> dict[str, Any]:
    validate_source_relationships(sources)
    timing = sources["timing"]
    waiver = sources["timing_owner_waiver"]
    geometry = sources["geometry"]
    signal_mode = sources["signal_mode"]
    envelope = sources["scoring_statistics_acceptance"]
    scoring = sources["scoring_statistics_payload"]
    pipeline = signal_mode["signal_pipeline"]
    versions = scoring["versions"]
    embedded = _database_payloads(timing, geometry, signal_mode, envelope, scoring)
    record: dict[str, Any] = {
        "config_version": plan["target_config_version"],
        "normalization_version": versions["normalization_version"],
        "signal_pipeline_version": signal_mode["signal_pipeline_version"],
        "test_geometry_version": geometry["test_geometry_version"],
        "created_date": plan["created_date"],
        "input_sampling_rate_hz": timing["input_sampling_rate_hz"],
        "resampling_tolerance_ms": timing["resampling_tolerance_ms"],
        "timing_acceptance_policy": timing["acceptance_policy"],
        "butterworth_order": pipeline["filter_order"],
        "cutoff_frequency_hz": pipeline["cutoff_frequency_hz"],
        "submovement_start_deg_per_sec": pipeline["start_threshold_deg_per_sec"],
        "submovement_end_deg_per_sec": pipeline["end_threshold_deg_per_sec"],
        "refractory_period_ms": pipeline["refractory_period_ms"],
        "normalization_bounds_json": embedded["normalization_bounds_json"],
        "submovement_bounds_by_mode_json": embedded["submovement_bounds_by_mode_json"],
        "consistency_tier_cutpoints_json": embedded["consistency_tier_cutpoints_json"],
        "scoring_zero_tolerance": scoring["normalization"]["scoring_zero_tolerance_points"],
        "target_geometry_json": embedded["target_geometry_json"],
        "tracking_contract_json": embedded["tracking_contract_json"],
        "confirmatory_contract_json": embedded["confirmatory_contract_json"],
    }
    source_entries = [copy.deepcopy(entry) for entry in plan["source_contracts"]]
    config = {
        "contract_id": plan["target_contract_id"],
        "config_version": plan["target_config_version"],
        "status": "accepted",
        "immutable": True,
        "created_date": plan["created_date"],
        "formula_version": versions["formula_version"],
        "mode_contract_version": signal_mode["mode_contract_version"],
        "consistency_tier_version": versions["consistency_tier_version"],
        "confirmatory_contract_version": versions["confirmatory_contract_version"],
        "database_json_serialization": plan["database_json_serialization"],
        "source_contracts": source_entries,
        "limitations": {
            "timing_acceptance": waiver["decision"],
            "strict_timing_confirmation_passed": waiver["strict_confirmation_passed"],
            "strict_candidate_v1_disposition": waiver["strict_candidate_v1_disposition"],
            "strict_candidate_v2_disposition": waiver["strict_candidate_v2_disposition"],
            "scientific_limitation": waiver["scientific_limitation"],
            "production_safeguards": copy.deepcopy(waiver["production_safeguards"]),
        },
        "formula_contract": {
            "formula_version": versions["formula_version"],
            "formula": copy.deepcopy(scoring["formula"]),
            "component_aggregation": copy.deepcopy(scoring["component_aggregation"]),
        },
        "calibration_configs_record": record,
    }
    validate_configuration(config, plan, sources)
    return config


def validate_configuration(
    config: dict[str, Any],
    plan: dict[str, Any],
    sources: dict[str, dict[str, Any]],
) -> None:
    validate_source_relationships(sources)
    _require(config.get("contract_id") == plan.get("target_contract_id"), "unexpected config contract ID")
    _require(config.get("config_version") == plan.get("target_config_version"), "unexpected config version")
    _require(config.get("status") == "accepted", "draft or incomplete calibration config rejected")
    _require(config.get("immutable") is True, "calibration config must be immutable")
    _require(config.get("database_json_serialization") == plan.get("database_json_serialization"), "serialization policy mismatch")
    _require(config.get("source_contracts") == plan.get("source_contracts"), "source manifest changed")
    _require(config.get("limitations", {}).get("strict_timing_confirmation_passed") is False, "timing waiver limitation must be preserved")

    record = config.get("calibration_configs_record")
    _require(isinstance(record, dict), "calibration_configs_record must be an object")
    _require(tuple(record.keys()) == REQUIRED_DATABASE_FIELDS, "database fields are missing, reordered, or unexpected")
    _require(list(plan.get("required_database_fields", [])) == list(REQUIRED_DATABASE_FIELDS), "freeze plan/schema field mismatch")
    for field, value in record.items():
        _require(value is not None, f"required database field is null: {field}")
        if isinstance(value, float):
            _require(math.isfinite(value), f"required database field is non-finite: {field}")
    for field in JSON_DATABASE_FIELDS:
        value = record[field]
        _require(isinstance(value, str) and value, f"{field} must be non-empty canonical JSON")
        parsed = json.loads(value)
        _require(isinstance(parsed, dict), f"{field} must encode an object")
        _require(canonical_json(parsed) == value, f"{field} is not canonical JSON")

    geometry = sources["geometry"]
    signal_mode = sources["signal_mode"]
    scoring = sources["scoring_statistics_payload"]
    envelope = sources["scoring_statistics_acceptance"]
    _require(json.loads(record["target_geometry_json"]) == geometry, "embedded geometry changed")
    _require(json.loads(record["tracking_contract_json"]) == signal_mode, "embedded tracking/mode contract changed")
    _require(config.get("formula_contract", {}).get("formula") == scoring.get("formula"), "formula payload changed")
    _require(config.get("formula_contract", {}).get("component_aggregation") == scoring.get("component_aggregation"), "aggregation payload changed")
    confirmatory = json.loads(record["confirmatory_contract_json"])
    _require(confirmatory.get("numerical_policy") == envelope.get("numerical_policy"), "confirmatory numerical policy changed")
    _require(record["input_sampling_rate_hz"] == signal_mode["signal_pipeline"]["sampling_rate_hz"], "sampling rate changed")
    pipeline = signal_mode["signal_pipeline"]
    scalar_bindings = {
        "butterworth_order": pipeline["filter_order"],
        "cutoff_frequency_hz": pipeline["cutoff_frequency_hz"],
        "submovement_start_deg_per_sec": pipeline["start_threshold_deg_per_sec"],
        "submovement_end_deg_per_sec": pipeline["end_threshold_deg_per_sec"],
        "refractory_period_ms": pipeline["refractory_period_ms"],
        "scoring_zero_tolerance": scoring["normalization"]["scoring_zero_tolerance_points"],
    }
    for field, expected in scalar_bindings.items():
        _require(record[field] == expected, f"frozen scalar changed: {field}")


def regression_gates(config: dict[str, Any]) -> dict[str, bool]:
    record = config["calibration_configs_record"]
    normalization = json.loads(record["normalization_bounds_json"])
    submovement = json.loads(record["submovement_bounds_by_mode_json"])
    tiers = json.loads(record["consistency_tier_cutpoints_json"])
    geometry = json.loads(record["target_geometry_json"])
    tracking = json.loads(record["tracking_contract_json"])
    confirmatory = json.loads(record["confirmatory_contract_json"])
    cv = coefficient_of_variation_percent([95.0, 100.0, 105.0], record["scoring_zero_tolerance"])
    sign_flip = exact_paired_sign_flip_test(
        [75.0] * 10,
        [70.0] * 10,
        alpha=confirmatory["alpha"],
        t_critical=confirmatory["t_critical_df9"],
        comparison_tolerance=confirmatory["numerical_policy"]["permutation_extreme_comparison_tolerance_points"],
    )
    gates = {
        "schema_field_count_20": len(record) == 20,
        "all_database_fields_populated": all(value is not None for value in record.values()),
        "canonical_embedded_json": all(canonical_json(json.loads(record[field])) == record[field] for field in JSON_DATABASE_FIELDS),
        "psa_worked_example_1600_dpi": 280.0 / 1600.0 == 0.175,
        "shot_worked_score": shot_performance_score(0.8, 0.9, 0.75, 0.6, 0.2) == 77.0,
        "tracking_worked_score": tracking_performance_score(0.8, 0.9, 0.7) == 81.875,
        "negative_shot_floor_retained": shot_performance_score(0, 0, 0, 0, 1) == -10.0,
        "cv_worked_example": cv == 5.0,
        "reaction_boundaries": [reaction_time_tier(value) for value in (199.999, 200, 250, 350, 500, 500.001)] == ["S", "A", "B", "C", "C", "D"],
        "sign_flip_minimum_p": sign_flip["p_value"] == 0.001953125,
        "normalization_version_bound": normalization["normalization_version"] == record["normalization_version"],
        "submovement_version_bound": submovement["normalization_version"] == record["normalization_version"],
        "consistency_version_bound": tiers["consistency_tier_version"] == config["consistency_tier_version"],
        "geometry_version_bound": geometry["test_geometry_version"] == record["test_geometry_version"],
        "signal_version_bound": tracking["signal_pipeline_version"] == record["signal_pipeline_version"],
        "sampling_rate_bound": tracking["signal_pipeline"]["sampling_rate_hz"] == record["input_sampling_rate_hz"],
        "filter_parameters_bound": (
            tracking["signal_pipeline"]["filter_order"] == record["butterworth_order"]
            and tracking["signal_pipeline"]["cutoff_frequency_hz"] == record["cutoff_frequency_hz"]
        ),
        "confirmatory_version_bound": confirmatory["confirmatory_contract_version"] == config["confirmatory_contract_version"],
        "timing_waiver_preserved": config["limitations"]["strict_timing_confirmation_passed"] is False,
        "immutable_accepted_state": config["status"] == "accepted" and config["immutable"] is True,
    }
    _require(all(gates.values()), f"P0-R7 regression gates failed: {[key for key, value in gates.items() if not value]}")
    return gates


def build_evidence(
    plan: dict[str, Any],
    config: dict[str, Any],
    config_sha256: str,
) -> dict[str, Any]:
    gates = regression_gates(config)
    return {
        "analysis_version": "p0-r7-calibration-config-freeze-analysis-v1",
        "plan_id": plan["plan_id"],
        "contract_id": config["contract_id"],
        "config_version": config["config_version"],
        "accepted": True,
        "immutable": True,
        "config_path": plan["output_path"],
        "config_sha256": config_sha256,
        "source_contracts": copy.deepcopy(plan["source_contracts"]),
        "required_database_fields": list(REQUIRED_DATABASE_FIELDS),
        "database_field_count": len(REQUIRED_DATABASE_FIELDS),
        "acceptance_gates": gates,
        "gate_count": len(gates),
        "limitations": copy.deepcopy(config["limitations"]),
    }


def write_new_json(path: Path, value: dict[str, Any]) -> None:
    if path.exists():
        raise FileExistsError(f"refusing to overwrite immutable artifact: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(pretty_json_bytes(value))


def freeze(root: Path, plan_path: Path) -> tuple[Path, Path, dict[str, Any]]:
    plan = load_json_object(plan_path)
    _require(plan.get("status") == "approved", "freeze plan must be approved")
    sources = load_verified_sources(root, plan)
    first = build_configuration(plan, sources)
    second = build_configuration(plan, sources)
    _require(pretty_json_bytes(first) == pretty_json_bytes(second), "configuration build is not deterministic")
    regression_gates(first)
    config_path = root / plan["output_path"]
    evidence_path = root / plan["evidence_path"]
    if config_path.exists() or evidence_path.exists():
        raise FileExistsError("refusing to overwrite immutable P0-R7 output")
    write_new_json(config_path, first)
    try:
        evidence = build_evidence(plan, first, sha256_file(config_path))
        write_new_json(evidence_path, evidence)
    except Exception:
        config_path.unlink(missing_ok=True)
        raise
    return config_path, evidence_path, evidence


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--plan", type=Path, required=True)
    args = parser.parse_args(argv)
    config_path, evidence_path, evidence = freeze(args.root.resolve(), args.plan.resolve())
    print(json.dumps({
        "accepted": evidence["accepted"],
        "gate_count": evidence["gate_count"],
        "database_field_count": evidence["database_field_count"],
        "config": str(config_path),
        "evidence": str(evidence_path),
    }))
    return 0


if __name__ == "__main__":
    sys.exit(main())
