"""Immutable, fail-closed reader for the production calibration contract."""

from __future__ import annotations

from dataclasses import dataclass
from hashlib import sha256
import json
from math import isfinite
from pathlib import Path
from typing import Any


EXPECTED_CONTRACT_ID = "sc8-calibration-config-v1"
EXPECTED_CONFIG_VERSION = "calibration_config_v1"
ACCEPTANCE_ENVELOPE_PATH = Path("calibration/plans/p0-r7-calibration-config-accepted-v1.json")
CONFIGURATION_PATH = Path("calibration/plans/calibration-config-v1.json")
REQUIRED_RECORD_FIELDS = frozenset({"config_version", "normalization_version", "signal_pipeline_version", "test_geometry_version", "created_date", "input_sampling_rate_hz", "resampling_tolerance_ms", "timing_acceptance_policy", "butterworth_order", "cutoff_frequency_hz", "submovement_start_deg_per_sec", "submovement_end_deg_per_sec", "refractory_period_ms", "normalization_bounds_json", "submovement_bounds_by_mode_json", "consistency_tier_cutpoints_json", "scoring_zero_tolerance", "target_geometry_json", "tracking_contract_json", "confirmatory_contract_json"})
REQUIRED_SOURCE_ROLES = frozenset({"timing", "timing_owner_waiver", "geometry", "signal_mode", "scoring_statistics_acceptance", "scoring_statistics_payload"})
RESEARCH_CONSTANTS_PATH = Path("config/research-constants-v1.json")


@dataclass(frozen=True)
class CalibrationConfigurationRecord:
    config_version: str; normalization_version: str; signal_pipeline_version: str; test_geometry_version: str; created_date: str
    input_sampling_rate_hz: float; resampling_tolerance_ms: float; timing_acceptance_policy: str; butterworth_order: int
    cutoff_frequency_hz: float; submovement_start_deg_per_sec: float; submovement_end_deg_per_sec: float; refractory_period_ms: float
    normalization_bounds_json: str; submovement_bounds_by_mode_json: str; consistency_tier_cutpoints_json: str; scoring_zero_tolerance: float
    target_geometry_json: str; tracking_contract_json: str; confirmatory_contract_json: str


@dataclass(frozen=True)
class FrozenCalibrationConfiguration:
    config_version: str; formula_version: str; contract_id: str; sha256: str
    record: CalibrationConfigurationRecord
    source_contracts: tuple[tuple[str, str, str], ...]


@dataclass(frozen=True)
class ResearchConstants:
    version: str
    psa_baseline_edpi: float
    edpi_floor: float
    cm_per_inch: float
    degrees_per_turn: float
    valorant_yaw_multiplier: float
    fitts_distance_multiplier: float
    headshot_reference_ceiling_percent: float
    outlier_sample_sd_multiplier: float
    grip_tension_min_percent: float
    grip_tension_max_percent: float
    excessive_grip_tension_percent: float
    wrist_warning_edpi_exclusive_upper: float


def load_frozen_calibration_configuration(repository_root: Path) -> FrozenCalibrationConfiguration:
    root = repository_root.resolve()
    envelope = _load_json(_resolve(root, ACCEPTANCE_ENVELOPE_PATH))
    _require_equal(envelope, "status", "accepted"); _require_equal(envelope, "config_contract_id", EXPECTED_CONTRACT_ID); _require_equal(envelope, "config_version", EXPECTED_CONFIG_VERSION)
    if Path(_required_string(envelope, "config_path")) != CONFIGURATION_PATH: raise ValueError("Acceptance envelope config path mismatch.")
    payload = _resolve(root, CONFIGURATION_PATH).read_bytes(); digest = sha256(payload).hexdigest()
    if digest != _required_string(envelope, "config_sha256"): raise ValueError("Frozen configuration SHA-256 mismatch.")
    return _validate(_parse_json(payload, "Frozen calibration configuration"), root, digest)


def load_research_constants(repository_root: Path) -> ResearchConstants:
    document = _load_json(_resolve(repository_root.resolve(), RESEARCH_CONSTANTS_PATH))
    _require_equal(document, "schema_version", "sc8-research-constants-v1")
    _require_equal(document, "status", "accepted")
    if document.get("immutable") is not True: raise ValueError("Research constants must be immutable.")
    values = _required_object(document, "general")
    names = tuple(ResearchConstants.__annotations__.keys())[1:]
    parsed = []
    for name in names:
        value = values.get(name)
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not isfinite(value) or value <= 0: raise ValueError(f"Research constants number is invalid: {name}")
        parsed.append(float(value))
    return ResearchConstants(_required_string(document, "constants_version"), *parsed)


def _validate(document: dict[str, Any], root: Path, digest: str) -> FrozenCalibrationConfiguration:
    _require_equal(document, "contract_id", EXPECTED_CONTRACT_ID); _require_equal(document, "config_version", EXPECTED_CONFIG_VERSION); _require_equal(document, "status", "accepted")
    if document.get("immutable") is not True: raise ValueError("Configuration must be immutable.")
    if document.get("limitations", {}).get("strict_timing_confirmation_passed") is not False: raise ValueError("The accepted timing limitation must remain explicit.")
    record_data = _required_object(document, "calibration_configs_record")
    if set(record_data) != REQUIRED_RECORD_FIELDS: raise ValueError("Calibration record must project exactly 20 fields.")
    record = _build_record(record_data); formula_version = _required_string(document, "formula_version")
    _validate_embedded(record, formula_version, _required_string(document, "mode_contract_version"), _required_string(document, "consistency_tier_version"), _required_string(document, "confirmatory_contract_version"), document)
    return FrozenCalibrationConfiguration(EXPECTED_CONFIG_VERSION, formula_version, EXPECTED_CONTRACT_ID, digest, record, _validate_sources(document, root))


def _build_record(record: dict[str, Any]) -> CalibrationConfigurationRecord:
    numeric = {"input_sampling_rate_hz", "resampling_tolerance_ms", "cutoff_frequency_hz", "submovement_start_deg_per_sec", "submovement_end_deg_per_sec", "refractory_period_ms", "scoring_zero_tolerance"}
    for field in numeric:
        value = record[field]
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not isfinite(value): raise ValueError(f"Required finite numeric configuration field is invalid: {field}")
    if any(record[field] <= 0 for field in numeric - {"scoring_zero_tolerance"}) or record["scoring_zero_tolerance"] < 0 or not isinstance(record["butterworth_order"], int) or record["butterworth_order"] <= 0: raise ValueError("Calibration scalar is invalid.")
    for field in REQUIRED_RECORD_FIELDS - numeric - {"butterworth_order"}: _required_string(record, field)
    for field in ("normalization_bounds_json", "submovement_bounds_by_mode_json", "consistency_tier_cutpoints_json", "target_geometry_json", "tracking_contract_json", "confirmatory_contract_json"): _canonical_json(_required_string(record, field), field)
    return CalibrationConfigurationRecord(**record)


def _validate_embedded(record: CalibrationConfigurationRecord, formula_version: str, mode_contract_version: str, consistency_tier_version: str, confirmatory_contract_version: str, document: dict[str, Any]) -> None:
    normalization = _canonical_json(record.normalization_bounds_json, "normalization_bounds_json"); submovement = _canonical_json(record.submovement_bounds_by_mode_json, "submovement_bounds_by_mode_json"); tiers = _canonical_json(record.consistency_tier_cutpoints_json, "consistency_tier_cutpoints_json"); geometry = _canonical_json(record.target_geometry_json, "target_geometry_json"); tracking = _canonical_json(record.tracking_contract_json, "tracking_contract_json"); confirmatory = _canonical_json(record.confirmatory_contract_json, "confirmatory_contract_json")
    _require_equal(normalization, "normalization_version", record.normalization_version); _require_float_equal(normalization["scoring_zero_tolerance_points"], record.scoring_zero_tolerance); _require_equal(submovement, "normalization_version", record.normalization_version); _require_equal(tiers, "consistency_tier_version", consistency_tier_version); _require_equal(geometry, "status", "accepted"); _require_equal(geometry, "test_geometry_version", record.test_geometry_version); _require_equal(tracking, "status", "accepted"); _require_equal(tracking, "signal_pipeline_version", record.signal_pipeline_version); _require_equal(tracking, "mode_contract_version", mode_contract_version)
    pipeline = _required_object(tracking, "signal_pipeline"); _require_float_equal(pipeline["sampling_rate_hz"], record.input_sampling_rate_hz)
    if pipeline["filter_order"] != record.butterworth_order: raise ValueError("Filter-order drift.")
    for embedded, scalar in (("cutoff_frequency_hz", record.cutoff_frequency_hz), ("start_threshold_deg_per_sec", record.submovement_start_deg_per_sec), ("end_threshold_deg_per_sec", record.submovement_end_deg_per_sec), ("refractory_period_ms", record.refractory_period_ms)): _require_float_equal(pipeline[embedded], scalar)
    _require_equal(confirmatory, "confirmatory_contract_version", confirmatory_contract_version); _require_equal(_required_object(document, "formula_contract"), "formula_version", formula_version)


def _validate_sources(document: dict[str, Any], root: Path) -> tuple[tuple[str, str, str], ...]:
    sources = document.get("source_contracts")
    if not isinstance(sources, list): raise ValueError("Source contract list is required.")
    verified = []
    for source in sources:
        if not isinstance(source, dict): raise ValueError("Source contract is invalid.")
        role = _required_string(source, "role"); path = _required_string(source, "path"); expected_hash = _required_string(source, "sha256")
        if sha256(_resolve(root, Path(path)).read_bytes()).hexdigest() != expected_hash: raise ValueError(f"Frozen source hash mismatch: {role}")
        verified.append((role, path, expected_hash))
    if {source[0] for source in verified} != REQUIRED_SOURCE_ROLES or len(verified) != len({source[0] for source in verified}): raise ValueError("Frozen source role set is incomplete or duplicated.")
    return tuple(verified)


def _canonical_json(value: str, label: str) -> dict[str, Any]:
    parsed = _parse_json(value.encode("utf-8"), label)
    if json.dumps(parsed, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False) != value: raise ValueError(f"{label} must be canonical compact JSON.")
    if not isinstance(parsed, dict): raise ValueError(f"{label} must be an object.")
    return parsed


def _load_json(path: Path) -> dict[str, Any]: return _parse_json(path.read_bytes(), str(path))
def _parse_json(value: bytes, label: str) -> Any:
    try: return json.loads(value)
    except (TypeError, json.JSONDecodeError) as error: raise ValueError(f"{label} is invalid JSON.") from error
def _resolve(root: Path, relative_path: Path) -> Path:
    if relative_path.is_absolute(): raise ValueError("Configuration path must be relative.")
    path = (root / relative_path).resolve()
    if root != path and root not in path.parents: raise ValueError("Configuration path escapes the repository.")
    return path
def _required_object(value: dict[str, Any], field: str) -> dict[str, Any]:
    result = value.get(field)
    if not isinstance(result, dict): raise ValueError(f"Required object configuration field is missing: {field}")
    return result
def _required_string(value: dict[str, Any], field: str) -> str:
    result = value.get(field)
    if not isinstance(result, str) or not result: raise ValueError(f"Required string configuration field is missing: {field}")
    return result
def _require_equal(value: dict[str, Any], field: str, expected: str) -> None:
    if _required_string(value, field) != expected: raise ValueError(f"Configuration identity mismatch: {field}")
def _require_float_equal(left: Any, right: float) -> None:
    if isinstance(left, bool) or not isinstance(left, (int, float)) or abs(left - right) > 1e-12: raise ValueError("Embedded scalar drift.")
