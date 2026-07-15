"""Deterministic P0-R5 signal-response and mode-contract calibration."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from collections import Counter
from pathlib import Path
from typing import Any, Sequence

import numpy as np


class SignalModeEvidenceError(ValueError):
    """Raised when a P0-R5 candidate or fixture violates its contract."""


def load_json_object(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise SignalModeEvidenceError(f"{path} must contain a JSON object")
    return value


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_new_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as target:
        json.dump(value, target, indent=2, sort_keys=True, ensure_ascii=False)
        target.write("\n")


def _positive(value: Any, name: str) -> float:
    if not isinstance(value, (int, float)) or not math.isfinite(value) or value <= 0:
        raise SignalModeEvidenceError(f"{name} must be finite and positive")
    return float(value)


def design_butterworth_lowpass_sos(
    order: int, cutoff_frequency_hz: float, sampling_rate_hz: float
) -> np.ndarray:
    """Design a canonical digital low-pass Butterworth cascade.

    The construction uses the analog Butterworth prototype, frequency
    prewarping, and the bilinear transform. Odd first-order terms are encoded
    in a six-coefficient SOS row with a cancelling origin pole/zero.
    """

    if not isinstance(order, int) or order <= 0:
        raise SignalModeEvidenceError("filter order must be a positive integer")
    cutoff = _positive(cutoff_frequency_hz, "cutoff_frequency_hz")
    sampling = _positive(sampling_rate_hz, "sampling_rate_hz")
    if cutoff >= sampling / 2.0:
        raise SignalModeEvidenceError("cutoff must be below Nyquist")

    warped = 2.0 * sampling * math.tan(math.pi * cutoff / sampling)
    analog_poles = [
        warped
        * np.exp(1j * math.pi * (2 * index + 1 + order) / (2 * order))
        for index in range(order)
    ]
    digital_poles = [
        (2.0 * sampling + pole) / (2.0 * sampling - pole)
        for pole in analog_poles
    ]

    real_poles = sorted(
        (pole.real for pole in digital_poles if abs(pole.imag) < 1e-12)
    )
    positive_imaginary = sorted(
        (pole for pole in digital_poles if pole.imag > 1e-12),
        key=lambda pole: (abs(pole), pole.real),
    )
    expected_real = order % 2
    if len(real_poles) != expected_real or len(positive_imaginary) != order // 2:
        raise SignalModeEvidenceError("unexpected Butterworth pole pairing")

    rows: list[list[float]] = []
    if real_poles:
        pole = real_poles[0]
        rows.append([1.0, 1.0, 0.0, 1.0, -pole, 0.0])
    for pole in positive_imaginary:
        rows.append(
            [1.0, 2.0, 1.0, 1.0, -2.0 * pole.real, abs(pole) ** 2]
        )

    sos = np.asarray(rows, dtype=np.float64)
    numerator_dc = float(np.prod(np.sum(sos[:, :3], axis=1)))
    denominator_dc = float(np.prod(np.sum(sos[:, 3:], axis=1)))
    sos[0, :3] *= denominator_dc / numerator_dc
    return sos


def default_sosfiltfilt_padlen(sos: np.ndarray) -> int:
    coefficients = np.asarray(sos, dtype=np.float64)
    if coefficients.ndim != 2 or coefficients.shape[1] != 6:
        raise SignalModeEvidenceError("SOS coefficients must have shape (n, 6)")
    return int(
        3
        * (
            2 * len(coefficients)
            + 1
            - min(
                int(np.count_nonzero(coefficients[:, 2] == 0.0)),
                int(np.count_nonzero(coefficients[:, 5] == 0.0)),
            )
        )
    )


def sos_frequency_response(
    sos: np.ndarray, frequency_hz: float, sampling_rate_hz: float
) -> complex:
    angular_frequency = 2.0 * math.pi * frequency_hz / sampling_rate_hz
    z_inverse = np.exp(-1j * angular_frequency)
    response = 1.0 + 0.0j
    for row in np.asarray(sos, dtype=np.float64):
        numerator = row[0] + row[1] * z_inverse + row[2] * z_inverse**2
        denominator = row[3] + row[4] * z_inverse + row[5] * z_inverse**2
        response *= numerator / denominator
    return complex(response)


def _sos_steady_state_zi(sos: np.ndarray) -> np.ndarray:
    scale = 1.0
    states = np.zeros((len(sos), 2), dtype=np.float64)
    for index, row in enumerate(np.asarray(sos, dtype=np.float64)):
        b0, b1, b2, a0, a1, a2 = row
        if not math.isclose(a0, 1.0, rel_tol=0.0, abs_tol=1e-15):
            raise SignalModeEvidenceError("canonical SOS requires a0 = 1")
        dc_gain = (b0 + b1 + b2) / (a0 + a1 + a2)
        states[index, 0] = scale * (dc_gain - b0)
        states[index, 1] = scale * (b2 - a2 * dc_gain)
        scale *= dc_gain
    return states


def _sos_filter(sos: np.ndarray, values: np.ndarray, initial: np.ndarray) -> np.ndarray:
    output = np.asarray(values, dtype=np.float64).copy()
    for section_index, row in enumerate(np.asarray(sos, dtype=np.float64)):
        b0, b1, b2, _, a1, a2 = row
        z0, z1 = initial[section_index]
        section_output = np.empty_like(output)
        for sample_index, sample in enumerate(output):
            result = b0 * sample + z0
            z0 = b1 * sample - a1 * result + z1
            z1 = b2 * sample - a2 * result
            section_output[sample_index] = result
        output = section_output
    return output


def sosfiltfilt_odd(
    sos: np.ndarray, values: Sequence[float], pad_length_samples: int
) -> np.ndarray:
    samples = np.asarray(values, dtype=np.float64)
    if samples.ndim != 1:
        raise SignalModeEvidenceError("signal must be one-dimensional")
    if pad_length_samples < 0 or pad_length_samples >= len(samples) - 1:
        raise SignalModeEvidenceError("pad length must be below sample_count - 1")
    if not np.all(np.isfinite(samples)):
        raise SignalModeEvidenceError("signal contains non-finite values")

    if pad_length_samples:
        left = 2.0 * samples[0] - samples[1 : pad_length_samples + 1][::-1]
        right = 2.0 * samples[-1] - samples[-pad_length_samples - 1 : -1][::-1]
        extended = np.concatenate((left, samples, right))
    else:
        extended = samples.copy()

    zi = _sos_steady_state_zi(sos)
    forward = _sos_filter(sos, extended, zi * extended[0])
    backward_input = forward[::-1]
    backward = _sos_filter(sos, backward_input, zi * backward_input[0])[::-1]
    if pad_length_samples:
        return backward[pad_length_samples:-pad_length_samples]
    return backward


def angular_velocity_magnitude(
    azimuth_deg: Sequence[float], elevation_deg: Sequence[float], sampling_rate_hz: float
) -> np.ndarray:
    azimuth = np.asarray(azimuth_deg, dtype=np.float64)
    elevation = np.asarray(elevation_deg, dtype=np.float64)
    if azimuth.shape != elevation.shape or azimuth.ndim != 1:
        raise SignalModeEvidenceError("angular axes must be equal one-dimensional arrays")
    sampling = _positive(sampling_rate_hz, "sampling_rate_hz")
    velocity = np.zeros_like(azimuth)
    if len(velocity) > 1:
        velocity[1:] = np.hypot(np.diff(azimuth), np.diff(elevation)) * sampling
    return velocity


def detect_submovements_from_velocity(
    velocity_deg_per_sec: Sequence[float],
    sampling_rate_hz: float,
    start_threshold_deg_per_sec: float,
    end_threshold_deg_per_sec: float,
    refractory_period_ms: float,
) -> list[dict[str, int]]:
    velocity = np.asarray(velocity_deg_per_sec, dtype=np.float64)
    if velocity.ndim != 1 or not np.all(np.isfinite(velocity)):
        raise SignalModeEvidenceError("velocity must be one-dimensional and finite")
    sampling = _positive(sampling_rate_hz, "sampling_rate_hz")
    start_threshold = _positive(
        start_threshold_deg_per_sec, "start_threshold_deg_per_sec"
    )
    end_threshold = _positive(end_threshold_deg_per_sec, "end_threshold_deg_per_sec")
    if end_threshold >= start_threshold:
        raise SignalModeEvidenceError("end threshold must be below start threshold")
    refractory_samples = int(round(_positive(refractory_period_ms, "refractory") * sampling / 1000.0))

    raw_events: list[dict[str, int]] = []
    onset: int | None = None
    for index, speed in enumerate(velocity):
        if onset is None and speed >= start_threshold:
            onset = index
        elif onset is not None and speed < end_threshold:
            raw_events.append({"onset_sample": onset, "end_sample": index})
            onset = None
    if onset is not None:
        raw_events.append({"onset_sample": onset, "end_sample": len(velocity) - 1})

    merged: list[dict[str, int]] = []
    for event in raw_events:
        if merged and event["onset_sample"] - merged[-1]["end_sample"] < refractory_samples:
            merged[-1]["end_sample"] = event["end_sample"]
        else:
            merged.append(dict(event))
    return merged


def shot_condition_counts(repetition_ordinal: int) -> dict[str, int]:
    if not isinstance(repetition_ordinal, int) or repetition_ordinal <= 0:
        raise SignalModeEvidenceError("repetition ordinal must be positive")
    conditions = [f"d{distance}-s{size}" for distance in range(3) for size in range(3)]
    sequence = conditions * 3
    extra_start = ((repetition_ordinal - 1) * 3) % len(conditions)
    sequence.extend(conditions[(extra_start + offset) % len(conditions)] for offset in range(3))
    return dict(sorted(Counter(sequence).items()))


def tracking_condition_counts() -> dict[str, int]:
    patterns = ("linear", "curved", "variable_speed")
    sizes = ("small", "medium", "large")
    return {f"{pattern}:{size}": 1 for pattern in patterns for size in sizes}


def tracking_position(pattern: str, time_seconds: float) -> tuple[float, float]:
    if not math.isfinite(time_seconds) or time_seconds < 0:
        raise SignalModeEvidenceError("tracking time must be finite and non-negative")
    if pattern == "linear":
        phase = (time_seconds % 4.0) / 4.0
        horizontal = -15.0 + 60.0 * phase if phase < 0.5 else 45.0 - 60.0 * phase
        return horizontal, 0.0
    if pattern == "curved":
        phase = 2.0 * math.pi * time_seconds / 6.0
        return 15.0 * math.cos(phase), 10.0 * math.sin(phase)
    if pattern == "variable_speed":
        phase = 2.0 * math.pi * time_seconds / 4.0
        return 15.0 * math.sin(phase), 0.0
    raise SignalModeEvidenceError(f"unknown Tracking pattern: {pattern}")


def tracking_window_metrics(
    sample_times_seconds: Sequence[float],
    radial_error_deg: Sequence[float],
    target_radius_deg: float,
    trial_duration_seconds: float,
    window_seconds: float,
) -> list[dict[str, float]]:
    times = np.asarray(sample_times_seconds, dtype=np.float64)
    errors = np.asarray(radial_error_deg, dtype=np.float64)
    duration = _positive(trial_duration_seconds, "trial_duration_seconds")
    window = _positive(window_seconds, "window_seconds")
    radius = _positive(target_radius_deg, "target_radius_deg")
    if times.ndim != 1 or errors.shape != times.shape or len(times) < 2:
        raise SignalModeEvidenceError("Tracking samples require equal arrays with boundaries")
    if not np.all(np.isfinite(times)) or not np.all(np.isfinite(errors)):
        raise SignalModeEvidenceError("Tracking samples must be finite")
    if not np.all(np.diff(times) > 0):
        raise SignalModeEvidenceError("Tracking timestamps must be strictly increasing")
    if not math.isclose(times[0], 0.0, abs_tol=1e-12) or not math.isclose(
        times[-1], duration, abs_tol=1e-12
    ):
        raise SignalModeEvidenceError("Tracking samples must cover exact trial boundaries")
    window_count_float = duration / window
    window_count = int(round(window_count_float))
    if not math.isclose(window_count_float, window_count, abs_tol=1e-12):
        raise SignalModeEvidenceError("trial duration must contain whole metric windows")

    results: list[dict[str, float]] = []
    for window_index in range(window_count):
        start = window_index * window
        end = start + window
        covered = 0.0
        on_target = 0.0
        squared_error = 0.0
        for sample_index in range(len(times) - 1):
            overlap = max(
                0.0,
                min(times[sample_index + 1], end) - max(times[sample_index], start),
            )
            if overlap == 0.0:
                continue
            error = errors[sample_index]
            covered += overlap
            squared_error += error * error * overlap
            if error <= radius:
                on_target += overlap
        if not math.isclose(covered, window, rel_tol=0.0, abs_tol=1e-10):
            raise SignalModeEvidenceError("Tracking window has incomplete duration coverage")
        results.append(
            {
                "window_index": float(window_index),
                "time_on_target_percent": 100.0 * on_target / covered,
                "tracking_deviation_rms_deg": math.sqrt(squared_error / covered),
            }
        )
    return results


def _validate_dependencies(
    candidate: dict[str, Any], timing: dict[str, Any], geometry: dict[str, Any]
) -> None:
    dependencies = candidate.get("dependencies", {})
    if timing.get("status") != "accepted" or dependencies.get("timing_contract_id") != timing.get("contract_id"):
        raise SignalModeEvidenceError("candidate does not reference accepted timing contract")
    if geometry.get("status") != "accepted" or dependencies.get("test_geometry_version") != geometry.get("test_geometry_version"):
        raise SignalModeEvidenceError("candidate does not reference accepted geometry contract")


def derive_signal_mode_evidence(
    candidate: dict[str, Any], timing: dict[str, Any], geometry: dict[str, Any]
) -> dict[str, Any]:
    _validate_dependencies(candidate, timing, geometry)
    pipeline = candidate["signal_pipeline"]
    sampling = _positive(pipeline["sampling_rate_hz"], "sampling_rate_hz")
    if not math.isclose(sampling, float(timing["input_sampling_rate_hz"])):
        raise SignalModeEvidenceError("signal grid differs from accepted timing grid")
    order = pipeline["filter_order"]
    cutoff = _positive(pipeline["cutoff_frequency_hz"], "cutoff_frequency_hz")
    sos = design_butterworth_lowpass_sos(order, cutoff, sampling)
    padlen = default_sosfiltfilt_padlen(sos)
    minimum_samples = int(pipeline["minimum_filterable_segment_samples"])

    poles: list[complex] = []
    for row in sos:
        poles.extend(np.roots([row[3], row[4], row[5]]))
    non_cancelled_poles = [pole for pole in poles if abs(pole) > 1e-12]
    dc_gain = abs(sos_frequency_response(sos, 0.0, sampling))
    cutoff_gain = abs(sos_frequency_response(sos, cutoff, sampling))
    cutoff_zero_phase_gain = cutoff_gain**2

    impulse = np.zeros(2001, dtype=np.float64)
    impulse_center = len(impulse) // 2
    impulse[impulse_center] = 1.0
    filtered_impulse = sosfiltfilt_odd(sos, impulse, padlen)
    filtered_peak = int(np.argmax(np.abs(filtered_impulse)))

    below = np.full(200, 7.999, dtype=np.float64)
    threshold = np.zeros(220, dtype=np.float64)
    threshold[20:80] = 8.0
    threshold[80:] = 3.999
    merged_fixture = np.zeros(300, dtype=np.float64)
    merged_fixture[10:20] = 9.0
    merged_fixture[50:60] = 9.0
    boundary_fixture = np.zeros(300, dtype=np.float64)
    boundary_fixture[10:20] = 9.0
    boundary_fixture[100:110] = 9.0
    detector_args = (
        sampling,
        float(pipeline["start_threshold_deg_per_sec"]),
        float(pipeline["end_threshold_deg_per_sec"]),
        float(pipeline["refractory_period_ms"]),
    )
    below_events = detect_submovements_from_velocity(below, *detector_args)
    threshold_events = detect_submovements_from_velocity(threshold, *detector_args)
    merged_events = detect_submovements_from_velocity(merged_fixture, *detector_args)
    boundary_events = detect_submovements_from_velocity(boundary_fixture, *detector_args)

    shot_counts_by_ordinal = {
        str(ordinal): shot_condition_counts(ordinal) for ordinal in range(1, 10)
    }
    tracking_counts = tracking_condition_counts()
    tracking = candidate["modes"]["tracking"]
    trial_duration = float(tracking["trial_duration_seconds"])
    path_times = np.linspace(0.0, trial_duration, int(trial_duration * sampling) + 1)
    path_summaries: dict[str, Any] = {}
    horizontal_limit = max(geometry["flick_conditions"]["close_center_offset_deg"])
    vertical_limit = float(geometry["flick_conditions"]["vertical_center_limit_deg"])
    for pattern in tracking["patterns"]:
        positions = np.asarray([tracking_position(pattern, float(t)) for t in path_times])
        path_summaries[pattern] = {
            "minimum_horizontal_deg": float(np.min(positions[:, 0])),
            "maximum_horizontal_deg": float(np.max(positions[:, 0])),
            "minimum_vertical_deg": float(np.min(positions[:, 1])),
            "maximum_vertical_deg": float(np.max(positions[:, 1])),
            "start_position_deg": positions[0].tolist(),
            "end_position_deg": positions[-1].tolist(),
            "inside_geometry_limits": bool(
                np.max(np.abs(positions[:, 0])) <= horizontal_limit + 1e-12
                and np.max(np.abs(positions[:, 1])) <= vertical_limit + 1e-12
            ),
        }

    regular_times = np.arange(0.0, trial_duration + 0.25, 0.25)
    regular_errors = np.where(regular_times < 3.0, 0.25, 1.0)
    irregular_times = np.asarray([0.0, 0.1, 0.7, 1.0, 1.8, 2.0, 2.6, 3.0, 3.4, 4.0, 4.9, 5.0, 5.8, 6.0])
    irregular_errors = np.where(irregular_times < 3.0, 0.25, 1.0)
    regular_metrics = tracking_window_metrics(regular_times, regular_errors, 0.5, trial_duration, 1.0)
    irregular_metrics = tracking_window_metrics(irregular_times, irregular_errors, 0.5, trial_duration, 1.0)
    metric_invariance = all(
        math.isclose(left["time_on_target_percent"], right["time_on_target_percent"], abs_tol=1e-10)
        and math.isclose(left["tracking_deviation_rms_deg"], right["tracking_deviation_rms_deg"], abs_tol=1e-10)
        for left, right in zip(regular_metrics, irregular_metrics, strict=True)
    )

    shared = candidate["shared_shot_contract"]
    total_trials = int(shared["minimum_resolved_trials"])
    adaptation_trials = math.floor(total_trials * float(shared["adaptation_fraction"]))
    post_adaptation_trials = total_trials - adaptation_trials
    expected_tracking_trials = int(tracking["blocks"]) * int(tracking["trials_per_block"])
    expected_tracking_windows = int(tracking["post_adaptation_trials"]) * int(
        trial_duration / float(tracking["metric_window_seconds"])
    )

    gates = {
        "dependency_identity": True,
        "sos_shape_and_stability": bool(
            sos.shape == (3, 6)
            and len(non_cancelled_poles) == order
            and max(abs(pole) for pole in non_cancelled_poles) < 1.0
        ),
        "unity_dc_gain": math.isclose(dc_gain, 1.0, rel_tol=0.0, abs_tol=1e-12),
        "butterworth_cutoff": math.isclose(cutoff_gain, math.sqrt(0.5), rel_tol=0.0, abs_tol=1e-10),
        "forward_backward_cutoff": math.isclose(cutoff_zero_phase_gain, 0.5, rel_tol=0.0, abs_tol=1e-10),
        "edge_length": padlen == int(pipeline["pad_length_samples"]) == 18 and minimum_samples == padlen + 2,
        "zero_phase_peak": abs(filtered_peak - impulse_center) <= 1,
        "threshold_boundaries": len(below_events) == 0 and len(threshold_events) == 1,
        "refractory_boundaries": len(merged_events) == 1 and len(boundary_events) == 2,
        "shot_trial_contract": total_trials == 30 and adaptation_trials == 15 and post_adaptation_trials == 15,
        "shot_condition_balance": all(
            sum(counts.values()) == 30 and min(counts.values()) == 3 and max(counts.values()) == 4
            for counts in shot_counts_by_ordinal.values()
        ),
        "tracking_condition_balance": len(tracking_counts) == 9 and set(tracking_counts.values()) == {1},
        "tracking_sample_contract": expected_tracking_trials == 18 and expected_tracking_windows == 54,
        "tracking_geometry_safety": all(summary["inside_geometry_limits"] for summary in path_summaries.values()),
        "tracking_metric_partition_invariance": metric_invariance,
    }

    return {
        "analysis_version": "p0-r5-signal-mode-analysis-v1",
        "candidate_id": candidate["candidate_id"],
        "signal_pipeline_version": candidate["signal_pipeline_version"],
        "mode_contract_version": candidate["mode_contract_version"],
        "accepted": all(gates.values()),
        "dependencies": {
            "timing_contract_id": timing["contract_id"],
            "test_geometry_version": geometry["test_geometry_version"],
        },
        "signal": {
            "sos": sos.tolist(),
            "sos_sections": [
                {
                    "b0": float(row[0]),
                    "b1": float(row[1]),
                    "b2": float(row[2]),
                    "a0": float(row[3]),
                    "a1": float(row[4]),
                    "a2": float(row[5]),
                }
                for row in sos
            ],
            "section_count": len(sos),
            "non_cancelled_pole_magnitudes": [float(abs(pole)) for pole in non_cancelled_poles],
            "dc_gain": dc_gain,
            "single_pass_gain_at_7_hz": cutoff_gain,
            "forward_backward_gain_at_7_hz": cutoff_zero_phase_gain,
            "pad_length_samples": padlen,
            "minimum_filterable_segment_samples": minimum_samples,
            "impulse_input_peak_sample": impulse_center,
            "impulse_filtered_peak_sample": filtered_peak,
            "threshold_fixture_event_count": len(threshold_events),
            "below_threshold_fixture_event_count": len(below_events),
            "sub_80_ms_fixture_event_count": len(merged_events),
            "exact_80_ms_fixture_event_count": len(boundary_events),
        },
        "modes": {
            "shot_trials": {
                "total": total_trials,
                "adaptation": adaptation_trials,
                "post_adaptation": post_adaptation_trials,
                "condition_counts_by_repetition_ordinal": shot_counts_by_ordinal,
            },
            "tracking": {
                "condition_counts_per_block": tracking_counts,
                "total_trials": expected_tracking_trials,
                "post_adaptation_windows": expected_tracking_windows,
                "path_summaries": path_summaries,
                "regular_partition_metrics": regular_metrics,
                "irregular_partition_metrics": irregular_metrics,
            },
        },
        "gates": gates,
    }


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--timing-contract", type=Path, required=True)
    parser.add_argument("--geometry-contract", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _build_parser().parse_args(argv)
    candidate = load_json_object(args.candidate)
    timing = load_json_object(args.timing_contract)
    geometry = load_json_object(args.geometry_contract)
    evidence = derive_signal_mode_evidence(candidate, timing, geometry)
    evidence["source_artifacts"] = {
        "candidate": str(args.candidate).replace("\\", "/"),
        "candidate_sha256": sha256_file(args.candidate),
        "timing_contract": str(args.timing_contract).replace("\\", "/"),
        "timing_contract_sha256": sha256_file(args.timing_contract),
        "geometry_contract": str(args.geometry_contract).replace("\\", "/"),
        "geometry_contract_sha256": sha256_file(args.geometry_contract),
    }
    write_new_json(args.output, evidence)
    return 0 if evidence["accepted"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
