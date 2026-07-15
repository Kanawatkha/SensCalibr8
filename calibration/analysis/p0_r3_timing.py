"""P0-R3 timing diagnostics and gap-safe uniform resampling.

This module analyzes immutable P0-R2 evidence. It never edits a raw artifact and
never promotes a measured value automatically. Pilot estimates must be frozen in
a separate confirmation contract before an acceptance-bearing run is evaluated.
"""

from __future__ import annotations

import hashlib
import json
import math
import platform
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Sequence

import numpy as np


class TimingEvidenceError(ValueError):
    """Raised when raw evidence is structurally invalid for timing analysis."""


@dataclass(frozen=True)
class TimingContract:
    sampling_rate_hz: float
    resampling_tolerance_ms: float
    filter_padlen_samples: int

    def validate(self) -> None:
        if not math.isfinite(self.sampling_rate_hz) or self.sampling_rate_hz <= 0:
            raise TimingEvidenceError("sampling_rate_hz must be finite and positive")
        if (
            not math.isfinite(self.resampling_tolerance_ms)
            or self.resampling_tolerance_ms < 0
        ):
            raise TimingEvidenceError(
                "resampling_tolerance_ms must be finite and non-negative"
            )
        if self.filter_padlen_samples < 0:
            raise TimingEvidenceError("filter_padlen_samples must be non-negative")

    @property
    def interval_seconds(self) -> float:
        return 1.0 / self.sampling_rate_hz

    @property
    def tolerance_seconds(self) -> float:
        return self.resampling_tolerance_ms / 1000.0

    @property
    def minimum_filterable_samples(self) -> int:
        # scipy.signal.sosfiltfilt requires padlen < sample_count - 1.
        return self.filter_padlen_samples + 2


@dataclass(frozen=True)
class TimingAcceptanceEnvelope:
    minimum_median_sampling_rate_hz: float
    maximum_median_sampling_rate_hz: float
    maximum_median_absolute_deviation_ms: float
    maximum_burst_interval_fraction: float
    maximum_gap_interval_fraction: float

    def validate(self) -> None:
        finite_values = (
            self.minimum_median_sampling_rate_hz,
            self.maximum_median_sampling_rate_hz,
            self.maximum_median_absolute_deviation_ms,
            self.maximum_burst_interval_fraction,
            self.maximum_gap_interval_fraction,
        )
        if not all(math.isfinite(value) for value in finite_values):
            raise TimingEvidenceError("timing acceptance envelope must be finite")
        if self.minimum_median_sampling_rate_hz <= 0:
            raise TimingEvidenceError("minimum median sampling rate must be positive")
        if (
            self.maximum_median_sampling_rate_hz
            < self.minimum_median_sampling_rate_hz
        ):
            raise TimingEvidenceError("median sampling-rate envelope is reversed")
        if self.maximum_median_absolute_deviation_ms < 0:
            raise TimingEvidenceError("maximum timing MAD must be non-negative")
        for name, value in (
            ("maximum_burst_interval_fraction", self.maximum_burst_interval_fraction),
            ("maximum_gap_interval_fraction", self.maximum_gap_interval_fraction),
        ):
            if not 0 <= value <= 1:
                raise TimingEvidenceError(f"{name} must be within [0, 1]")


@dataclass(frozen=True)
class FrozenTimingContract:
    contract_id: str
    signal_pipeline_version: str
    status: str
    timing: TimingContract
    acceptance_policy: str
    acceptance_envelope: TimingAcceptanceEnvelope | None
    confirmation_required_run_count: int
    pilot_source_run_ids: tuple[str, ...]

    def validate(self) -> None:
        if not self.contract_id.strip():
            raise TimingEvidenceError("contract_id must not be blank")
        if not self.signal_pipeline_version.strip():
            raise TimingEvidenceError("signal_pipeline_version must not be blank")
        if self.status not in {"candidate-frozen", "accepted"}:
            raise TimingEvidenceError(
                "timing contract status must be candidate-frozen or accepted"
            )
        if self.acceptance_policy not in {
            "distribution-envelope",
            "integrity-modal-cadence",
        }:
            raise TimingEvidenceError("unsupported timing acceptance policy")
        if len(self.pilot_source_run_ids) < 2:
            raise TimingEvidenceError(
                "frozen timing contract requires at least two pilot source runs"
            )
        if len(set(self.pilot_source_run_ids)) != len(self.pilot_source_run_ids):
            raise TimingEvidenceError("pilot source run IDs must be unique")
        if any(not item.strip() for item in self.pilot_source_run_ids):
            raise TimingEvidenceError("pilot source run IDs must not be blank")
        if self.confirmation_required_run_count < 2:
            raise TimingEvidenceError(
                "confirmation requires at least two independent runs"
            )
        self.timing.validate()
        if self.acceptance_policy == "distribution-envelope":
            if self.acceptance_envelope is None:
                raise TimingEvidenceError(
                    "distribution policy requires an acceptance envelope"
                )
            self.acceptance_envelope.validate()
        elif self.acceptance_envelope is not None:
            raise TimingEvidenceError(
                "integrity-modal policy must not carry unused numeric bounds"
            )


def load_frozen_timing_contract(path: Path) -> FrozenTimingContract:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        acceptance_policy = str(
            value.get("acceptance_policy", "distribution-envelope")
        )
        envelope_value = value.get("acceptance_envelope")
        acceptance_envelope = (
            TimingAcceptanceEnvelope(
                minimum_median_sampling_rate_hz=float(
                    envelope_value["minimum_median_sampling_rate_hz"]
                ),
                maximum_median_sampling_rate_hz=float(
                    envelope_value["maximum_median_sampling_rate_hz"]
                ),
                maximum_median_absolute_deviation_ms=float(
                    envelope_value["maximum_median_absolute_deviation_ms"]
                ),
                maximum_burst_interval_fraction=float(
                    envelope_value["maximum_burst_interval_fraction"]
                ),
                maximum_gap_interval_fraction=float(
                    envelope_value["maximum_gap_interval_fraction"]
                ),
            )
            if envelope_value is not None
            else None
        )
        contract = FrozenTimingContract(
            contract_id=str(value["contract_id"]),
            signal_pipeline_version=str(value["signal_pipeline_version"]),
            status=str(value["status"]),
            timing=TimingContract(
                sampling_rate_hz=float(value["input_sampling_rate_hz"]),
                resampling_tolerance_ms=float(value["resampling_tolerance_ms"]),
                filter_padlen_samples=int(value["filter_padlen_samples"]),
            ),
            acceptance_policy=acceptance_policy,
            acceptance_envelope=acceptance_envelope,
            confirmation_required_run_count=int(
                value["confirmation_required_run_count"]
            ),
            pilot_source_run_ids=tuple(
                str(item) for item in value["pilot_source_run_ids"]
            ),
        )
    except (KeyError, TypeError, ValueError, json.JSONDecodeError) as error:
        raise TimingEvidenceError(f"invalid timing contract: {path}") from error
    contract.validate()
    return contract


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_jsonl(path: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, start=1):
            text = line.strip()
            if not text:
                raise TimingEvidenceError(
                    f"blank JSONL record at {path.name}:{line_number}"
                )
            try:
                value = json.loads(text)
            except json.JSONDecodeError as error:
                raise TimingEvidenceError(
                    f"invalid JSON at {path.name}:{line_number}: {error}"
                ) from error
            if not isinstance(value, dict):
                raise TimingEvidenceError(
                    f"JSONL record is not an object at {path.name}:{line_number}"
                )
            records.append(value)
    return records


def verify_integrity(run_directory: Path) -> dict[str, Any]:
    manifests = sorted(run_directory.glob("*_integrity-manifest.json"))
    if len(manifests) != 1:
        raise TimingEvidenceError(
            "run directory must contain exactly one integrity manifest"
        )
    manifest = json.loads(manifests[0].read_text(encoding="utf-8"))
    artifacts = manifest.get("Artifacts")
    if not isinstance(artifacts, list) or not artifacts:
        raise TimingEvidenceError("integrity manifest contains no artifacts")
    for artifact in artifacts:
        relative_path = artifact.get("RelativePath")
        expected_size = artifact.get("ByteSize")
        expected_hash = artifact.get("Sha256")
        if not relative_path or expected_size is None or not expected_hash:
            raise TimingEvidenceError("integrity record is incomplete")
        source = run_directory / relative_path
        if not source.is_file():
            raise TimingEvidenceError(f"missing artifact: {relative_path}")
        if source.stat().st_size != expected_size:
            raise TimingEvidenceError(f"artifact size mismatch: {relative_path}")
        if sha256_file(source) != expected_hash:
            raise TimingEvidenceError(f"artifact hash mismatch: {relative_path}")
    return manifest


def _numeric_array(
    records: Sequence[dict[str, Any]], field: str, *, finite: bool = True
) -> np.ndarray:
    try:
        values = np.asarray([record[field] for record in records], dtype=np.float64)
    except (KeyError, TypeError, ValueError) as error:
        raise TimingEvidenceError(f"missing or invalid numeric field: {field}") from error
    if finite and not np.all(np.isfinite(values)):
        raise TimingEvidenceError(f"non-finite value in field: {field}")
    return values


P0_R3_MANUAL_ENVIRONMENT_FIELDS = (
    "VSyncState",
    "AdaptiveSyncState",
    "MouseConnection",
    "MouseDpi",
    "MouseDpiEvidenceSource",
    "ConfiguredPollingRate",
    "PollingRateEvidenceSource",
    "MousePowerState",
    "OperatorId",
    "PowerPlan",
    "BackgroundLoadPolicy",
    "ThermalPowerNotes",
    "NetworkOfflineState",
)


def _require_known(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value.strip() or value.strip().lower() == "unknown":
        raise TimingEvidenceError(f"acceptance field is unresolved: {field}")
    return value.strip()


def validate_acceptance_manifests(
    environment: dict[str, Any],
    capture_plan: dict[str, Any],
    final_run: dict[str, Any],
) -> dict[str, Any]:
    for field in (
        "ProtocolId",
        "EnvironmentId",
        "HarnessVersion",
        "HarnessChecksum",
        "RuntimeBuildType",
        "ExecutableName",
        "ExecutableChecksum",
        "UnityVersion",
        "InputSystemVersion",
        "InputUpdateMode",
        "TimestampSource",
        "OperatingSystem",
        "GraphicsDeviceName",
        "GraphicsDeviceVersion",
        "FullScreenMode",
    ):
        _require_known(environment.get(field), f"environment.{field}")
    if environment.get("RuntimeBuildType") != "windows-standalone":
        raise TimingEvidenceError(
            "acceptance capture must come from the Windows standalone runtime"
        )
    if environment.get("RedundantEventMergingDisabled") is not True:
        raise TimingEvidenceError(
            "acceptance capture requires Unity redundant mouse-event merging to be disabled"
        )
    if environment.get("DedicatedRawInputMessagePump") is not True:
        raise TimingEvidenceError(
            "acceptance capture requires a dedicated native raw-input message pump"
        )
    if environment.get("TimestampSource") != "win32-wm-input-qpc":
        raise TimingEvidenceError(
            "acceptance capture requires the win32-wm-input-qpc timestamp source"
        )
    if environment.get("ApplicationFocused") is not True:
        raise TimingEvidenceError("environment was not focused at capture start")

    manual = environment.get("Manual")
    if not isinstance(manual, dict):
        raise TimingEvidenceError("environment.Manual must be an object")
    for field in P0_R3_MANUAL_ENVIRONMENT_FIELDS:
        _require_known(manual.get(field), f"environment.Manual.{field}")

    for field in (
        "ProtocolId",
        "CapturePlanId",
        "EnvironmentId",
        "ConditionId",
        "ExecutionOrder",
        "ControlledVariablesJson",
        "AcceptanceOwner",
        "ControlledMotionInstruction",
    ):
        _require_known(capture_plan.get(field), f"capture_plan.{field}")
    if capture_plan.get("EvidenceState") not in {"pilot", "confirmation"}:
        raise TimingEvidenceError(
            "capture_plan.EvidenceState must be pilot or confirmation"
        )
    repeats = capture_plan.get("PlannedRepeatCount")
    ordinal = capture_plan.get("RepetitionOrdinal")
    duration = capture_plan.get("TraceDurationSeconds")
    if not isinstance(repeats, int) or repeats < 2:
        raise TimingEvidenceError("capture plan requires at least two repeats")
    if not isinstance(ordinal, int) or not 1 <= ordinal <= repeats:
        raise TimingEvidenceError("capture plan repetition ordinal is invalid")
    if not isinstance(duration, (int, float)) or not math.isfinite(duration) or duration <= 0:
        raise TimingEvidenceError("capture plan trace duration is invalid")
    try:
        controlled = json.loads(capture_plan["ControlledVariablesJson"])
    except json.JSONDecodeError as error:
        raise TimingEvidenceError("ControlledVariablesJson is invalid JSON") from error
    if not isinstance(controlled, dict) or not controlled:
        raise TimingEvidenceError("controlled variables must be a non-empty object")

    if final_run.get("Status") != "completed":
        raise TimingEvidenceError("run final status is not completed")
    relationships = (
        (environment.get("ProtocolId"), capture_plan.get("ProtocolId"), "protocol"),
        (environment.get("ProtocolId"), final_run.get("ProtocolId"), "protocol"),
        (environment.get("EnvironmentId"), capture_plan.get("EnvironmentId"), "environment"),
        (environment.get("EnvironmentId"), final_run.get("EnvironmentId"), "environment"),
        (capture_plan.get("CapturePlanId"), final_run.get("CapturePlanId"), "capture plan"),
        (capture_plan.get("ConditionId"), final_run.get("ConditionId"), "condition"),
        (environment.get("HarnessVersion"), final_run.get("HarnessVersion"), "harness version"),
        (environment.get("HarnessChecksum"), final_run.get("HarnessChecksum"), "harness checksum"),
    )
    for left, right, name in relationships:
        if left != right:
            raise TimingEvidenceError(f"{name} relationship mismatch")
    return {
        "environment_id": environment["EnvironmentId"],
        "capture_plan_id": capture_plan["CapturePlanId"],
        "condition_id": capture_plan["ConditionId"],
        "evidence_state": capture_plan["EvidenceState"],
        "planned_repeat_count": repeats,
        "repetition_ordinal": ordinal,
        "trace_duration_seconds": float(duration),
    }


def _find_one(run_directory: Path, pattern: str, label: str) -> Path:
    matches = sorted(run_directory.glob(pattern))
    if len(matches) != 1:
        raise TimingEvidenceError(f"run directory must contain exactly one {label}")
    return matches[0]


def verify_acceptance_run_package(run_directory: Path) -> dict[str, Any]:
    environment = _load_json_object(
        _find_one(run_directory, "*_environment-manifest.json", "environment manifest")
    )
    plan = _load_json_object(
        _find_one(run_directory, "*_capture-plan.json", "capture-plan manifest")
    )
    final = _load_json_object(
        _find_one(run_directory, "*_run-final.json", "final run manifest")
    )
    summary = validate_acceptance_manifests(environment, plan, final)
    frame_records = load_jsonl(
        _find_one(run_directory, "*_frame-timing.jsonl", "frame-timing artifact")
    )
    if not frame_records:
        raise TimingEvidenceError("frame-timing artifact is empty")
    unfocused = sum(record.get("ApplicationFocused") is not True for record in frame_records)
    if unfocused:
        raise TimingEvidenceError(
            f"run lost application focus in {unfocused} frame records"
        )
    frame_times = _numeric_array(frame_records, "MonotonicTimestampSeconds")
    summary["frame_record_count"] = len(frame_records)
    summary["observed_frame_span_seconds"] = float(frame_times[-1] - frame_times[0])
    summary["run_id"] = final.get("RunId")
    summary["trace_id"] = final.get("TraceId")
    return summary


def validate_raw_mouse_records(records: Sequence[dict[str, Any]]) -> None:
    if len(records) < 2:
        raise TimingEvidenceError("timing analysis requires at least two events")
    sequences = _numeric_array(records, "Sequence").astype(np.int64)
    expected = np.arange(len(records), dtype=np.int64)
    if not np.array_equal(sequences, expected):
        raise TimingEvidenceError("raw event sequence is not contiguous from zero")
    run_ids = {record.get("RunId") for record in records}
    trace_ids = {record.get("TraceId") for record in records}
    device_ids = {record.get("DeviceId") for record in records}
    if len(run_ids) != 1 or None in run_ids:
        raise TimingEvidenceError("raw events do not have one valid run ID")
    if len(trace_ids) != 1 or None in trace_ids:
        raise TimingEvidenceError("raw events do not have one valid trace ID")
    if len(device_ids) != 1 or None in device_ids:
        raise TimingEvidenceError("raw events do not have one stable device ID")
    monotonic_ticks = _numeric_array(records, "MonotonicTimestampTicks")
    if np.any(np.diff(monotonic_ticks) < 0):
        raise TimingEvidenceError("high-resolution timestamps move backward")


def analyze_event_timing(
    records: Sequence[dict[str, Any]], nominal_sampling_rate_hz: float | None = None
) -> dict[str, Any]:
    validate_raw_mouse_records(records)
    if nominal_sampling_rate_hz is not None and (
        not math.isfinite(nominal_sampling_rate_hz)
        or nominal_sampling_rate_hz <= 0
    ):
        raise TimingEvidenceError(
            "nominal_sampling_rate_hz must be finite and positive"
        )
    event_times = _numeric_array(records, "InputEventTimestampSeconds")
    intervals = np.diff(event_times)
    monotonic_times = _numeric_array(records, "MonotonicTimestampSeconds")
    monotonic_intervals = np.diff(monotonic_times)
    duplicate_count = int(np.count_nonzero(intervals == 0))
    reverse_count = int(np.count_nonzero(intervals < 0))
    positive = intervals[intervals > 0]
    if positive.size == 0:
        raise TimingEvidenceError("trace has no positive input-event interval")

    median_interval = float(np.median(positive))
    absolute_deviation = np.abs(positive - median_interval)
    median_absolute_deviation = float(np.median(absolute_deviation))
    cadence_multiple = np.maximum(1.0, np.rint(positive / median_interval))
    cadence_residual = np.abs(positive - cadence_multiple * median_interval)
    paired_positive = (intervals > 0) & (monotonic_intervals >= 0)
    clock_interval_difference = np.abs(
        intervals[paired_positive] - monotonic_intervals[paired_positive]
    )
    timestamp_ulp = np.spacing(np.abs(event_times))

    result = {
        "event_count": len(records),
        "positive_interval_count": int(positive.size),
        "duplicate_timestamp_count": duplicate_count,
        "reverse_timestamp_count": reverse_count,
        "duplicate_monotonic_timestamp_count": int(
            np.count_nonzero(monotonic_intervals == 0)
        ),
        "median_interval_ms": median_interval * 1000.0,
        "median_sampling_rate_hz": 1.0 / median_interval,
        "median_absolute_deviation_ms": median_absolute_deviation * 1000.0,
        "minimum_interval_ms": float(np.min(positive) * 1000.0),
        "maximum_interval_ms": float(np.max(positive) * 1000.0),
        "p95_interval_ms": float(np.percentile(positive, 95) * 1000.0),
        "p99_interval_ms": float(np.percentile(positive, 99) * 1000.0),
        "maximum_nearest_cadence_residual_ms": float(
            np.max(cadence_residual) * 1000.0
        ),
        "multi_cadence_interval_count": int(np.count_nonzero(cadence_multiple > 1)),
        "maximum_event_timestamp_ulp_nanoseconds": float(
            np.max(timestamp_ulp) * 1_000_000_000.0
        ),
        "median_event_vs_monotonic_interval_difference_ms": float(
            np.median(clock_interval_difference) * 1000.0
        )
        if clock_interval_difference.size
        else None,
        "maximum_event_vs_monotonic_interval_difference_ms": float(
            np.max(clock_interval_difference) * 1000.0
        )
        if clock_interval_difference.size
        else None,
        "run_id": records[0]["RunId"],
        "trace_id": records[0]["TraceId"],
        "device_id": records[0]["DeviceId"],
    }
    if nominal_sampling_rate_hz is not None:
        nominal_interval = 1.0 / nominal_sampling_rate_hz
        # Nearest-cadence classification includes zero. Thus intervals below the
        # midpoint to one cadence are receipt bursts, one is normal cadence, and
        # two or more are scheduling/missing-cadence gaps.
        nearest_cadence = np.floor(positive / nominal_interval + 0.5).astype(
            np.int64
        )
        burst = nearest_cadence == 0
        single = nearest_cadence == 1
        gap = nearest_cadence >= 2
        if not np.any(single):
            raise TimingEvidenceError(
                "trace has no single-cadence interval at the nominal rate"
            )
        single_residual = np.abs(positive[single] - nominal_interval)
        positive_count = float(positive.size)
        event_span = float(event_times[-1] - event_times[0])
        result.update(
            {
                "nominal_sampling_rate_hz": nominal_sampling_rate_hz,
                "observed_event_throughput_hz": (
                    float((len(records) - 1) / event_span)
                    if event_span > 0
                    else None
                ),
                "burst_interval_count": int(np.count_nonzero(burst)),
                "single_cadence_interval_count": int(np.count_nonzero(single)),
                "gap_interval_count": int(np.count_nonzero(gap)),
                "burst_interval_fraction": float(
                    np.count_nonzero(burst) / positive_count
                ),
                "single_cadence_interval_fraction": float(
                    np.count_nonzero(single) / positive_count
                ),
                "gap_interval_fraction": float(
                    np.count_nonzero(gap) / positive_count
                ),
                "p95_single_cadence_residual_ms": float(
                    np.percentile(single_residual, 95) * 1000.0
                ),
                "p99_single_cadence_residual_ms": float(
                    np.percentile(single_residual, 99) * 1000.0
                ),
                "maximum_single_cadence_residual_ms": float(
                    np.max(single_residual) * 1000.0
                ),
            }
        )
    return result


def aggregate_pilot_diagnostics(
    diagnostics: Sequence[dict[str, Any]],
) -> dict[str, Any]:
    if len(diagnostics) < 2:
        raise TimingEvidenceError(
            "repeatability estimation requires at least two independent runs"
        )
    run_ids = [item["run_id"] for item in diagnostics]
    trace_ids = [item["trace_id"] for item in diagnostics]
    if len(set(run_ids)) != len(run_ids) or len(set(trace_ids)) != len(trace_ids):
        raise TimingEvidenceError("pilot reports must come from independent runs/traces")
    devices = {item["device_id"] for item in diagnostics}
    environments = {
        item["acceptance_package"]["environment_id"] for item in diagnostics
    }
    plans = {
        item["acceptance_package"]["capture_plan_id"] for item in diagnostics
    }
    conditions = {
        item["acceptance_package"]["condition_id"] for item in diagnostics
    }
    states = {
        item["acceptance_package"]["evidence_state"] for item in diagnostics
    }
    if len(devices) != 1 or len(environments) != 1 or len(plans) != 1 or len(conditions) != 1:
        raise TimingEvidenceError(
            "pilot reports must share one device, environment, plan, and condition"
        )
    if states != {"pilot"}:
        raise TimingEvidenceError("pilot aggregation accepts only pilot-state runs")
    nominal_rates = {
        item.get("nominal_sampling_rate_hz") for item in diagnostics
    }
    if None in nominal_rates or len(nominal_rates) != 1:
        raise TimingEvidenceError(
            "pilot diagnostics require one explicit nominal sampling rate"
        )
    rates = np.asarray(
        [item["median_sampling_rate_hz"] for item in diagnostics], dtype=np.float64
    )
    timing_mads = np.asarray(
        [item["median_absolute_deviation_ms"] for item in diagnostics],
        dtype=np.float64,
    )
    burst_fractions = np.asarray(
        [item["burst_interval_fraction"] for item in diagnostics],
        dtype=np.float64,
    )
    gap_fractions = np.asarray(
        [item["gap_interval_fraction"] for item in diagnostics],
        dtype=np.float64,
    )
    p99_single_residuals = np.asarray(
        [item["p99_single_cadence_residual_ms"] for item in diagnostics],
        dtype=np.float64,
    )
    median_rate = float(np.median(rates))
    nominal_rate = float(next(iter(nominal_rates)))
    envelope = {
        "minimum_median_sampling_rate_hz": float(np.min(rates)),
        "maximum_median_sampling_rate_hz": float(np.max(rates)),
        "maximum_median_absolute_deviation_ms": float(np.max(timing_mads)),
        "maximum_burst_interval_fraction": float(np.max(burst_fractions)),
        "maximum_gap_interval_fraction": float(np.max(gap_fractions)),
    }
    return {
        "run_count": len(diagnostics),
        "run_ids": run_ids,
        "trace_ids": trace_ids,
        "device_id": next(iter(devices)),
        "environment_id": next(iter(environments)),
        "capture_plan_id": next(iter(plans)),
        "condition_id": next(iter(conditions)),
        "configured_nominal_sampling_rate_hz": nominal_rate,
        "measured_median_sampling_rate_hz": median_rate,
        "pilot_candidate_sampling_rate_hz": nominal_rate,
        "rate_median_absolute_deviation_hz": float(
            np.median(np.abs(rates - median_rate))
        ),
        "minimum_run_rate_hz": float(np.min(rates)),
        "maximum_run_rate_hz": float(np.max(rates)),
        "pilot_candidate_resampling_tolerance_ms": float(
            np.max(p99_single_residuals)
        ),
        "pilot_candidate_acceptance_envelope": envelope,
        "candidate_policy": (
            "Canonical grid uses the predeclared configured device rate; "
            "fresh confirmation must reproduce the unexpanded pilot envelope."
        ),
        "requires_fresh_confirmation": True,
    }


def derive_familywise_prediction_contract(
    diagnostics: Sequence[dict[str, Any]],
    *,
    future_run_count: int,
    familywise_alpha: float,
    student_t_critical_value: float,
    critical_value_cumulative_probability: float,
) -> dict[str, Any]:
    aggregate = aggregate_pilot_diagnostics(diagnostics)
    if future_run_count < 2:
        raise TimingEvidenceError("future_run_count must be at least two")
    if not 0 < familywise_alpha < 1:
        raise TimingEvidenceError("familywise_alpha must be within (0, 1)")
    if (
        not math.isfinite(student_t_critical_value)
        or student_t_critical_value <= 0
    ):
        raise TimingEvidenceError("Student-t critical value must be positive")

    metric_fields = {
        "median_sampling_rate_hz": "median_sampling_rate_hz",
        "median_absolute_deviation_ms": "median_absolute_deviation_ms",
        "burst_interval_fraction": "burst_interval_fraction",
        "gap_interval_fraction": "gap_interval_fraction",
        "p99_single_cadence_residual_ms": "p99_single_cadence_residual_ms",
    }
    statement_count = future_run_count * len(metric_fields)
    expected_probability = 1.0 - familywise_alpha / (2.0 * statement_count)
    if not math.isclose(
        critical_value_cumulative_probability,
        expected_probability,
        rel_tol=0.0,
        abs_tol=1e-12,
    ):
        raise TimingEvidenceError(
            "critical-value probability does not match Bonferroni correction"
        )

    pilot_count = len(diagnostics)
    prediction_scale = math.sqrt(1.0 + 1.0 / pilot_count)
    bounds: dict[str, dict[str, float]] = {}
    for output_name, diagnostic_field in metric_fields.items():
        values = np.asarray(
            [item[diagnostic_field] for item in diagnostics], dtype=np.float64
        )
        mean = float(np.mean(values))
        sample_standard_deviation = float(np.std(values, ddof=1))
        half_width = (
            student_t_critical_value
            * sample_standard_deviation
            * prediction_scale
        )
        bounds[output_name] = {
            "pilot_mean": mean,
            "pilot_sample_standard_deviation": sample_standard_deviation,
            "prediction_lower": mean - half_width,
            "prediction_upper": mean + half_width,
        }

    return {
        "pilot_aggregate": aggregate,
        "acceptance_envelope": {
            "minimum_median_sampling_rate_hz": bounds[
                "median_sampling_rate_hz"
            ]["prediction_lower"],
            "maximum_median_sampling_rate_hz": bounds[
                "median_sampling_rate_hz"
            ]["prediction_upper"],
            "maximum_median_absolute_deviation_ms": max(
                0.0,
                bounds["median_absolute_deviation_ms"]["prediction_upper"],
            ),
            "maximum_burst_interval_fraction": min(
                1.0,
                max(0.0, bounds["burst_interval_fraction"]["prediction_upper"]),
            ),
            "maximum_gap_interval_fraction": min(
                1.0,
                max(0.0, bounds["gap_interval_fraction"]["prediction_upper"]),
            ),
        },
        "resampling_tolerance_ms": max(
            0.0,
            bounds["p99_single_cadence_residual_ms"]["prediction_upper"],
        ),
        "metric_prediction_bounds": bounds,
        "method": {
            "name": "bonferroni-familywise-single-future-observation-prediction",
            "pilot_run_count": pilot_count,
            "future_run_count": future_run_count,
            "metric_count": len(metric_fields),
            "simultaneous_statement_count": statement_count,
            "familywise_alpha": familywise_alpha,
            "degrees_of_freedom": pilot_count - 1,
            "student_t_critical_value": student_t_critical_value,
            "critical_value_cumulative_probability": (
                critical_value_cumulative_probability
            ),
            "prediction_scale": prediction_scale,
            "formula": "mean +/- t * sample_sd * sqrt(1 + 1 / pilot_n)",
            "assumption": (
                "Per-run summary metrics are independent and approximately "
                "normally distributed; small pilot n is handled conservatively "
                "but remains a documented limitation."
            ),
        },
    }


def evaluate_against_frozen_contract(
    records: Sequence[dict[str, Any]], contract: FrozenTimingContract
) -> dict[str, Any]:
    contract.validate()
    diagnostics = analyze_event_timing(
        records, contract.timing.sampling_rate_hz
    )
    common_checks = {
        "timestamps_positive": (
            diagnostics["duplicate_timestamp_count"] == 0
            and diagnostics["reverse_timestamp_count"] == 0
        )
    }
    if contract.acceptance_policy == "integrity-modal-cadence":
        interval_ratio = (
            diagnostics["median_interval_ms"]
            / (contract.timing.interval_seconds * 1000.0)
        )
        median_cadence_class = math.floor(interval_ratio + 0.5)
        checks = {
            **common_checks,
            "median_interval_maps_to_nominal_cadence": (
                median_cadence_class == 1
            ),
            "single_cadence_is_modal": (
                diagnostics["single_cadence_interval_count"]
                > diagnostics["burst_interval_count"]
                and diagnostics["single_cadence_interval_count"]
                > diagnostics["gap_interval_count"]
            ),
            "p99_single_residual_within_cadence_partition": (
                diagnostics["p99_single_cadence_residual_ms"]
                <= contract.timing.resampling_tolerance_ms
            ),
        }
        envelope_output = None
    else:
        envelope = contract.acceptance_envelope
        assert envelope is not None
        checks = {
            **common_checks,
        "median_rate_within_pilot_envelope": (
            envelope.minimum_median_sampling_rate_hz
            <= diagnostics["median_sampling_rate_hz"]
            <= envelope.maximum_median_sampling_rate_hz
        ),
        "timing_mad_within_pilot_envelope": (
            diagnostics["median_absolute_deviation_ms"]
            <= envelope.maximum_median_absolute_deviation_ms
        ),
        "burst_fraction_within_pilot_envelope": (
            diagnostics["burst_interval_fraction"]
            <= envelope.maximum_burst_interval_fraction
        ),
        "gap_fraction_within_pilot_envelope": (
            diagnostics["gap_interval_fraction"]
            <= envelope.maximum_gap_interval_fraction
        ),
        "p99_single_residual_within_tolerance": (
            diagnostics["p99_single_cadence_residual_ms"]
            <= contract.timing.resampling_tolerance_ms
        ),
        }
        envelope_output = {
            "minimum_median_sampling_rate_hz": envelope.minimum_median_sampling_rate_hz,
            "maximum_median_sampling_rate_hz": envelope.maximum_median_sampling_rate_hz,
            "maximum_median_absolute_deviation_ms": envelope.maximum_median_absolute_deviation_ms,
            "maximum_burst_interval_fraction": envelope.maximum_burst_interval_fraction,
            "maximum_gap_interval_fraction": envelope.maximum_gap_interval_fraction,
        }
    return {
        "accepted": all(checks.values()),
        "checks": checks,
        "diagnostics": diagnostics,
        "sampling_rate_hz": contract.timing.sampling_rate_hz,
        "resampling_tolerance_ms": contract.timing.resampling_tolerance_ms,
        "acceptance_policy": contract.acceptance_policy,
        "acceptance_envelope": envelope_output,
    }


def resample_cumulative_counts(
    records: Sequence[dict[str, Any]], contract: TimingContract
) -> dict[str, Any]:
    contract.validate()
    validate_raw_mouse_records(records)
    times = _numeric_array(records, "InputEventTimestampSeconds")
    intervals = np.diff(times)
    if np.any(intervals <= 0):
        raise TimingEvidenceError(
            "duplicate/reversed event timestamps cannot be silently resampled"
        )

    delta_x = _numeric_array(records, "RawDeltaX")
    delta_y = _numeric_array(records, "RawDeltaY")
    cumulative_x = np.cumsum(delta_x)
    cumulative_y = np.cumsum(delta_y)
    gap_limit = contract.interval_seconds + contract.tolerance_seconds
    split_after = np.flatnonzero(intervals > gap_limit)
    boundaries = [0, *(int(index) + 1 for index in split_after), len(records)]
    segments: list[dict[str, Any]] = []

    for start, stop in zip(boundaries[:-1], boundaries[1:]):
        segment_times = times[start:stop]
        if segment_times.size == 0:
            continue
        relative = segment_times - segment_times[0]
        full_steps = int(math.floor(relative[-1] / contract.interval_seconds))
        grid = segment_times[0] + np.arange(full_steps + 1) * contract.interval_seconds
        interpolated_x = np.interp(grid, segment_times, cumulative_x[start:stop])
        interpolated_y = np.interp(grid, segment_times, cumulative_y[start:stop])
        segments.append(
            {
                "source_start_index": start,
                "source_stop_index_exclusive": stop,
                "source_event_count": stop - start,
                "grid_sample_count": int(grid.size),
                "grid_start_seconds": float(grid[0]),
                "grid_end_seconds": float(grid[-1]),
                "uncovered_tail_seconds": float(segment_times[-1] - grid[-1]),
                "filter_edge_eligible": bool(
                    grid.size >= contract.minimum_filterable_samples
                ),
                "time_seconds": grid.tolist(),
                "cumulative_x": interpolated_x.tolist(),
                "cumulative_y": interpolated_y.tolist(),
            }
        )

    return {
        "source_run_id": records[0]["RunId"],
        "source_trace_id": records[0]["TraceId"],
        "sampling_rate_hz": contract.sampling_rate_hz,
        "resampling_tolerance_ms": contract.resampling_tolerance_ms,
        "filter_padlen_samples": contract.filter_padlen_samples,
        "minimum_filterable_samples": contract.minimum_filterable_samples,
        "gap_count": int(split_after.size),
        "gap_after_source_indexes": split_after.astype(int).tolist(),
        "segments": segments,
    }


def default_sosfiltfilt_padlen(sos: np.ndarray) -> int:
    coefficients = np.asarray(sos, dtype=np.float64)
    if coefficients.ndim != 2 or coefficients.shape[1] != 6:
        raise TimingEvidenceError("SOS coefficients must have shape (n_sections, 6)")
    numerator_zeros = int(np.count_nonzero(coefficients[:, 2] == 0))
    denominator_zeros = int(np.count_nonzero(coefficients[:, 5] == 0))
    return 3 * (
        2 * coefficients.shape[0]
        + 1
        - min(numerator_zeros, denominator_zeros)
    )


def write_new_json(path: Path, value: dict[str, Any]) -> None:
    if path.exists():
        raise FileExistsError(f"refusing to overwrite derived artifact: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True, allow_nan=False) + "\n",
        encoding="utf-8",
    )


def find_raw_mouse_artifact(run_directory: Path) -> Path:
    matches = sorted(run_directory.glob("*_raw-mouse-events.jsonl"))
    if len(matches) != 1:
        raise TimingEvidenceError(
            "run directory must contain exactly one raw mouse JSONL artifact"
        )
    return matches[0]


def analyze_run_directory(
    run_directory: Path, nominal_sampling_rate_hz: float | None = None
) -> dict[str, Any]:
    integrity = verify_integrity(run_directory)
    package = verify_acceptance_run_package(run_directory)
    raw_path = find_raw_mouse_artifact(run_directory)
    records = load_jsonl(raw_path)
    diagnostics = analyze_event_timing(records, nominal_sampling_rate_hz)
    diagnostics.update(
        {
            "source_raw_relative_path": raw_path.name,
            "source_raw_sha256": sha256_file(raw_path),
            "source_integrity_run_id": integrity.get("RunId"),
            "acceptance_package": package,
        }
    )
    return diagnostics


def confirm_frozen_contract(
    run_directories: Sequence[Path], contract: FrozenTimingContract
) -> dict[str, Any]:
    contract.validate()
    if len(run_directories) != contract.confirmation_required_run_count:
        raise TimingEvidenceError(
            "confirmation run count does not match the frozen contract"
        )

    run_reports: list[dict[str, Any]] = []
    seen_run_ids: set[str] = set()
    seen_trace_ids: set[str] = set()
    environment_ids: set[str] = set()
    capture_plan_ids: set[str] = set()
    condition_ids: set[str] = set()
    device_ids: set[Any] = set()
    repetition_ordinals: set[int] = set()
    pilot_ids = set(contract.pilot_source_run_ids)
    for run_directory in run_directories:
        report = analyze_run_directory(run_directory)
        run_id = str(report["run_id"])
        trace_id = str(report["trace_id"])
        if run_id in pilot_ids:
            raise TimingEvidenceError(
                f"confirmation run reuses pilot evidence: {run_id}"
            )
        if run_id in seen_run_ids or trace_id in seen_trace_ids:
            raise TimingEvidenceError(
                "confirmation inputs must be independent runs and traces"
            )
        seen_run_ids.add(run_id)
        seen_trace_ids.add(trace_id)
        records = load_jsonl(find_raw_mouse_artifact(run_directory))
        package = report["acceptance_package"]
        if package["evidence_state"] != "confirmation":
            raise TimingEvidenceError(
                f"fresh confirmation run is not confirmation-state: {run_id}"
            )
        environment_ids.add(package["environment_id"])
        capture_plan_ids.add(package["capture_plan_id"])
        condition_ids.add(package["condition_id"])
        device_ids.add(report["device_id"])
        if package["planned_repeat_count"] != contract.confirmation_required_run_count:
            raise TimingEvidenceError(
                "confirmation manifest repeat count differs from frozen contract"
            )
        repetition_ordinals.add(package["repetition_ordinal"])
        evaluation = evaluate_against_frozen_contract(records, contract)
        run_reports.append({**report, "contract_evaluation": evaluation})

    if (
        len(environment_ids) != 1
        or len(capture_plan_ids) != 1
        or len(condition_ids) != 1
        or len(device_ids) != 1
    ):
        raise TimingEvidenceError(
            "confirmation runs must share one device, environment, plan, and condition"
        )
    expected_ordinals = set(range(1, contract.confirmation_required_run_count + 1))
    if repetition_ordinals != expected_ordinals:
        raise TimingEvidenceError(
            "confirmation must contain every predeclared repetition ordinal exactly once"
        )

    accepted = all(
        item["contract_evaluation"]["accepted"] for item in run_reports
    )
    return {
        "analysis_version": "p0-r3-timing-analysis-v3",
        "contract_id": contract.contract_id,
        "signal_pipeline_version": contract.signal_pipeline_version,
        "contract_status_at_analysis": contract.status,
        "fresh_confirmation": True,
        "accepted": accepted,
        "run_count": len(run_reports),
        "run_reports": run_reports,
        "analysis_runtime": {
            "python": sys.version.split()[0],
            "numpy": np.__version__,
            "platform": platform.platform(),
        },
    }


def _load_json_object(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise TimingEvidenceError(f"invalid JSON: {path}") from error
    if not isinstance(value, dict):
        raise TimingEvidenceError(f"JSON root must be an object: {path}")
    return value


def _build_parser():
    import argparse

    parser = argparse.ArgumentParser(
        description="SensCalibr8 P0-R3 immutable timing-evidence analyzer"
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    analyze = subparsers.add_parser("analyze-run")
    analyze.add_argument("--run-directory", type=Path, required=True)
    analyze.add_argument("--nominal-sampling-rate-hz", type=float)
    analyze.add_argument("--output", type=Path, required=True)

    aggregate = subparsers.add_parser("aggregate-pilot")
    aggregate.add_argument("--diagnostic", type=Path, action="append", required=True)
    aggregate.add_argument("--output", type=Path, required=True)

    prediction = subparsers.add_parser("derive-prediction-contract")
    prediction.add_argument("--diagnostic", type=Path, action="append", required=True)
    prediction.add_argument("--future-run-count", type=int, required=True)
    prediction.add_argument("--familywise-alpha", type=float, required=True)
    prediction.add_argument("--student-t-critical-value", type=float, required=True)
    prediction.add_argument(
        "--critical-value-cumulative-probability", type=float, required=True
    )
    prediction.add_argument("--output", type=Path, required=True)

    confirm = subparsers.add_parser("confirm")
    confirm.add_argument("--contract", type=Path, required=True)
    confirm.add_argument("--run-directory", type=Path, action="append", required=True)
    confirm.add_argument("--output", type=Path, required=True)

    resample = subparsers.add_parser("resample")
    resample.add_argument("--contract", type=Path, required=True)
    resample.add_argument("--run-directory", type=Path, required=True)
    resample.add_argument("--output", type=Path, required=True)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _build_parser().parse_args(argv)
    if args.command == "analyze-run":
        result = analyze_run_directory(
            args.run_directory, args.nominal_sampling_rate_hz
        )
    elif args.command == "aggregate-pilot":
        diagnostics = [_load_json_object(path) for path in args.diagnostic]
        result = aggregate_pilot_diagnostics(diagnostics)
        result["analysis_version"] = "p0-r3-timing-analysis-v3"
    elif args.command == "derive-prediction-contract":
        diagnostics = [_load_json_object(path) for path in args.diagnostic]
        result = derive_familywise_prediction_contract(
            diagnostics,
            future_run_count=args.future_run_count,
            familywise_alpha=args.familywise_alpha,
            student_t_critical_value=args.student_t_critical_value,
            critical_value_cumulative_probability=(
                args.critical_value_cumulative_probability
            ),
        )
        result["analysis_version"] = "p0-r3-timing-analysis-v3"
    elif args.command == "confirm":
        result = confirm_frozen_contract(
            args.run_directory, load_frozen_timing_contract(args.contract)
        )
    elif args.command == "resample":
        contract = load_frozen_timing_contract(args.contract)
        verify_integrity(args.run_directory)
        records = load_jsonl(find_raw_mouse_artifact(args.run_directory))
        result = resample_cumulative_counts(records, contract.timing)
        result["analysis_version"] = "p0-r3-timing-analysis-v3"
        result["contract_id"] = contract.contract_id
        result["signal_pipeline_version"] = contract.signal_pipeline_version
        result["source_raw_sha256"] = sha256_file(
            find_raw_mouse_artifact(args.run_directory)
        )
    else:  # pragma: no cover - argparse enforces a known command.
        raise AssertionError(args.command)
    write_new_json(args.output, result)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
