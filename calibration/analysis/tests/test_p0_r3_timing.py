import json
import tempfile
import unittest
from pathlib import Path

import numpy as np

from calibration.analysis.p0_r3_timing import (
    FrozenTimingContract,
    TimingAcceptanceEnvelope,
    TimingContract,
    TimingEvidenceError,
    aggregate_pilot_diagnostics,
    analyze_event_timing,
    default_sosfiltfilt_padlen,
    derive_familywise_prediction_contract,
    evaluate_against_frozen_contract,
    load_frozen_timing_contract,
    resample_cumulative_counts,
    validate_acceptance_manifests,
    write_new_json,
)


def make_records(times):
    return [
        {
            "TraceId": "trace-a",
            "RunId": "run-a",
            "Sequence": index,
            "MonotonicTimestampTicks": index,
            "MonotonicTimestampSeconds": value,
            "InputEventTimestampSeconds": value,
            "DeviceId": 1,
            "RawDeltaX": 1.0,
            "RawDeltaY": -0.5,
        }
        for index, value in enumerate(times)
    ]


def make_frozen_contract(**envelope_overrides):
    envelope = {
        "minimum_median_sampling_rate_hz": 990.0,
        "maximum_median_sampling_rate_hz": 1010.0,
        "maximum_median_absolute_deviation_ms": 0.1,
        "maximum_burst_interval_fraction": 0.25,
        "maximum_gap_interval_fraction": 0.25,
    }
    envelope.update(envelope_overrides)
    return FrozenTimingContract(
        contract_id="timing-a",
        signal_pipeline_version="signal-a",
        status="candidate-frozen",
        timing=TimingContract(1000.0, 0.1, 18),
        acceptance_policy="distribution-envelope",
        acceptance_envelope=TimingAcceptanceEnvelope(**envelope),
        confirmation_required_run_count=5,
        pilot_source_run_ids=("pilot-a", "pilot-b"),
    )


class P0R3TimingTests(unittest.TestCase):
    def test_stable_trace_reports_exact_median_rate(self):
        records = make_records([0.000, 0.001, 0.002, 0.003, 0.004])
        result = analyze_event_timing(records)
        self.assertAlmostEqual(result["median_sampling_rate_hz"], 1000.0)
        self.assertEqual(result["duplicate_timestamp_count"], 0)
        self.assertEqual(result["reverse_timestamp_count"], 0)
        self.assertEqual(result["multi_cadence_interval_count"], 0)

    def test_duplicate_timestamp_is_reported_and_rejected_by_contract(self):
        records = make_records([0.000, 0.001, 0.001, 0.002])
        diagnostics = analyze_event_timing(records)
        self.assertEqual(diagnostics["duplicate_timestamp_count"], 1)
        contract = make_frozen_contract(maximum_gap_interval_fraction=0.1)
        confirmation = evaluate_against_frozen_contract(records, contract)
        self.assertFalse(confirmation["accepted"])
        with self.assertRaises(TimingEvidenceError):
            resample_cumulative_counts(records, contract.timing)

    def test_stable_nominal_trace_passes_distribution_contract(self):
        records = make_records([0.000, 0.001, 0.002, 0.003, 0.004])
        result = evaluate_against_frozen_contract(records, make_frozen_contract())
        self.assertTrue(result["accepted"])
        self.assertTrue(all(result["checks"].values()))

    def test_excess_receipt_bursts_fail_distribution_contract(self):
        records = make_records([0.0000, 0.0001, 0.0010, 0.0011, 0.0020])
        contract = make_frozen_contract(maximum_burst_interval_fraction=0.25)
        result = evaluate_against_frozen_contract(records, contract)
        self.assertFalse(result["accepted"])
        self.assertFalse(result["checks"]["burst_fraction_within_pilot_envelope"])

    def test_integrity_modal_policy_retains_bursts_as_diagnostics(self):
        records = make_records([0.0000, 0.0001, 0.0010, 0.0011, 0.0020, 0.0030])
        contract = FrozenTimingContract(
            contract_id="timing-accepted",
            signal_pipeline_version="signal-accepted",
            status="accepted",
            timing=TimingContract(1000.0, 0.5, 18),
            acceptance_policy="integrity-modal-cadence",
            acceptance_envelope=None,
            confirmation_required_run_count=5,
            pilot_source_run_ids=("pilot-a", "pilot-b"),
        )
        result = evaluate_against_frozen_contract(records, contract)
        self.assertTrue(result["accepted"])
        self.assertEqual(result["diagnostics"]["burst_interval_count"], 2)
        self.assertIsNone(result["acceptance_envelope"])

    def test_multi_cadence_gap_is_exposed_and_split_before_resampling(self):
        records = make_records([0.000, 0.001, 0.002, 0.005, 0.006])
        contract = make_frozen_contract(maximum_gap_interval_fraction=0.1)
        confirmation = evaluate_against_frozen_contract(records, contract)
        self.assertFalse(confirmation["accepted"])
        self.assertEqual(
            confirmation["diagnostics"]["gap_interval_count"], 1
        )
        derived = resample_cumulative_counts(records, contract.timing)
        self.assertEqual(derived["gap_count"], 1)
        self.assertEqual(len(derived["segments"]), 2)

    def test_short_segments_are_not_filter_edge_eligible(self):
        records = make_records([0.000, 0.001, 0.002, 0.003])
        contract = TimingContract(1000.0, 0.1, 3)
        derived = resample_cumulative_counts(records, contract)
        self.assertEqual(contract.minimum_filterable_samples, 5)
        self.assertFalse(derived["segments"][0]["filter_edge_eligible"])

    def test_sos_padlen_matches_documented_structure_formula(self):
        sos = np.asarray(
            [
                [1.0, 1.0, 0.0, 1.0, -0.5, 0.0],
                [1.0, 2.0, 1.0, 1.0, -0.5, 0.25],
                [1.0, 2.0, 1.0, 1.0, -0.5, 0.25],
            ]
        )
        self.assertEqual(default_sosfiltfilt_padlen(sos), 18)

    def test_pilot_aggregation_requires_independent_repeats(self):
        first = analyze_event_timing(
            make_records([0.000, 0.001, 0.002]), 1000.0
        )
        with self.assertRaises(TimingEvidenceError):
            aggregate_pilot_diagnostics([first])
        first["acceptance_package"] = make_acceptance_package()
        second_records = make_records([0.100, 0.101, 0.102])
        for record in second_records:
            record["RunId"] = "run-b"
            record["TraceId"] = "trace-b"
        second = analyze_event_timing(second_records, 1000.0)
        second["acceptance_package"] = make_acceptance_package()
        aggregate = aggregate_pilot_diagnostics([first, second])
        self.assertAlmostEqual(aggregate["pilot_candidate_sampling_rate_hz"], 1000.0)
        self.assertTrue(aggregate["requires_fresh_confirmation"])

    def test_pilot_aggregation_rejects_cross_device_evidence(self):
        first = analyze_event_timing(
            make_records([0.000, 0.001, 0.002]), 1000.0
        )
        first["acceptance_package"] = make_acceptance_package()
        second_records = make_records([0.100, 0.101, 0.102])
        for record in second_records:
            record["RunId"] = "run-b"
            record["TraceId"] = "trace-b"
            record["DeviceId"] = 2
        second = analyze_event_timing(second_records, 1000.0)
        second["acceptance_package"] = make_acceptance_package()
        with self.assertRaisesRegex(TimingEvidenceError, "one device"):
            aggregate_pilot_diagnostics([first, second])

    def test_prediction_contract_uses_predeclared_bonferroni_inputs(self):
        first = analyze_event_timing(
            make_records([0.0000, 0.0010, 0.0020, 0.0030]), 1000.0
        )
        first["acceptance_package"] = make_acceptance_package()
        second_records = make_records([0.1000, 0.1011, 0.1022, 0.1033])
        for record in second_records:
            record["RunId"] = "run-b"
            record["TraceId"] = "trace-b"
        second = analyze_event_timing(second_records, 1000.0)
        second["acceptance_package"] = make_acceptance_package()

        result = derive_familywise_prediction_contract(
            [first, second],
            future_run_count=2,
            familywise_alpha=0.2,
            student_t_critical_value=1.0,
            critical_value_cumulative_probability=0.99,
        )
        rates = np.asarray(
            [first["median_sampling_rate_hz"], second["median_sampling_rate_hz"]]
        )
        expected_half_width = np.std(rates, ddof=1) * np.sqrt(1.5)
        self.assertAlmostEqual(
            result["acceptance_envelope"]["maximum_median_sampling_rate_hz"],
            np.mean(rates) + expected_half_width,
        )
        self.assertEqual(result["method"]["simultaneous_statement_count"], 10)

    def test_prediction_contract_rejects_wrong_critical_probability(self):
        first = analyze_event_timing(
            make_records([0.000, 0.001, 0.002]), 1000.0
        )
        first["acceptance_package"] = make_acceptance_package()
        second_records = make_records([0.100, 0.101, 0.102])
        for record in second_records:
            record["RunId"] = "run-b"
            record["TraceId"] = "trace-b"
        second = analyze_event_timing(second_records, 1000.0)
        second["acceptance_package"] = make_acceptance_package()
        with self.assertRaisesRegex(TimingEvidenceError, "Bonferroni"):
            derive_familywise_prediction_contract(
                [first, second],
                future_run_count=2,
                familywise_alpha=0.2,
                student_t_critical_value=1.0,
                critical_value_cumulative_probability=0.95,
            )

    def test_derived_writer_refuses_overwrite(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "derived.json"
            write_new_json(path, {"state": "candidate"})
            self.assertEqual(json.loads(path.read_text())["state"], "candidate")
            with self.assertRaises(FileExistsError):
                write_new_json(path, {"state": "overwritten"})

    def test_frozen_contract_requires_repeated_pilot_sources(self):
        contract = FrozenTimingContract(
            contract_id="timing-a",
            signal_pipeline_version="signal-a",
            status="candidate-frozen",
            timing=TimingContract(1000.0, 0.1, 18),
            acceptance_policy="distribution-envelope",
            acceptance_envelope=make_frozen_contract().acceptance_envelope,
            confirmation_required_run_count=5,
            pilot_source_run_ids=("run-a",),
        )
        with self.assertRaises(TimingEvidenceError):
            contract.validate()

    def test_contract_loader_rejects_unreviewed_draft(self):
        value = {
            "contract_id": "timing-a",
            "signal_pipeline_version": "signal-a",
            "status": "draft",
            "input_sampling_rate_hz": 1000.0,
            "resampling_tolerance_ms": 0.1,
            "filter_padlen_samples": 18,
            "acceptance_policy": "distribution-envelope",
            "confirmation_required_run_count": 5,
            "acceptance_envelope": {
                "minimum_median_sampling_rate_hz": 990.0,
                "maximum_median_sampling_rate_hz": 1010.0,
                "maximum_median_absolute_deviation_ms": 0.1,
                "maximum_burst_interval_fraction": 0.25,
                "maximum_gap_interval_fraction": 0.25,
            },
            "pilot_source_run_ids": ["run-a", "run-b"],
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "contract.json"
            path.write_text(json.dumps(value), encoding="utf-8")
            with self.assertRaises(TimingEvidenceError):
                load_frozen_timing_contract(path)

    def test_acceptance_manifest_rejects_unknown_manual_field(self):
        environment, plan, final = make_manifests()
        environment["Manual"]["MouseDpi"] = "unknown"
        with self.assertRaisesRegex(TimingEvidenceError, "MouseDpi"):
            validate_acceptance_manifests(environment, plan, final)

    def test_acceptance_manifest_validates_complete_relationships(self):
        environment, plan, final = make_manifests()
        result = validate_acceptance_manifests(environment, plan, final)
        self.assertEqual(result["evidence_state"], "pilot")
        self.assertEqual(result["planned_repeat_count"], 2)

    def test_acceptance_manifest_allows_unknown_audit_only_mouse_metadata(self):
        environment, plan, final = make_manifests()
        environment["Manual"]["MouseManufacturer"] = "unknown"
        environment["Manual"]["MouseModel"] = "unknown"
        environment["Manual"]["MouseFirmware"] = "unknown"
        result = validate_acceptance_manifests(environment, plan, final)
        self.assertEqual(result["environment_id"], "environment-a")

    def test_acceptance_manifest_rejects_editor_capture(self):
        environment, plan, final = make_manifests()
        environment["RuntimeBuildType"] = "unity-editor"
        with self.assertRaisesRegex(TimingEvidenceError, "standalone"):
            validate_acceptance_manifests(environment, plan, final)

    def test_acceptance_manifest_rejects_merged_mouse_events(self):
        environment, plan, final = make_manifests()
        environment["RedundantEventMergingDisabled"] = False
        with self.assertRaisesRegex(TimingEvidenceError, "merging"):
            validate_acceptance_manifests(environment, plan, final)

    def test_acceptance_manifest_rejects_frame_batched_timestamp_source(self):
        environment, plan, final = make_manifests()
        environment["DedicatedRawInputMessagePump"] = False
        environment["TimestampSource"] = "unity-input-event-time"
        with self.assertRaisesRegex(TimingEvidenceError, "message pump"):
            validate_acceptance_manifests(environment, plan, final)


def make_manifests():
    known = "fixture-known"
    manual_fields = (
        "DisplayModel",
        "NativeResolution",
        "DisplayRefreshRate",
        "DisplayScaling",
        "VSyncState",
        "AdaptiveSyncState",
        "MouseManufacturer",
        "MouseModel",
        "MouseConnection",
        "MouseFirmware",
        "MouseDpi",
        "MouseDpiEvidenceSource",
        "ConfiguredPollingRate",
        "PollingRateEvidenceSource",
        "UsbPathOrHub",
        "MousePowerState",
        "PointerSpeed",
        "PointerAccelerationState",
        "MousepadDescription",
        "OperatorId",
        "DominantHand",
        "GripDescriptor",
        "MovementDescriptor",
        "PostureNotes",
        "WarmupProcedure",
        "PowerPlan",
        "BackgroundLoadPolicy",
        "ThermalPowerNotes",
        "NetworkOfflineState",
    )
    environment = {
        "ProtocolId": "protocol-a",
        "EnvironmentId": "environment-a",
        "HarnessVersion": "harness-a",
        "HarnessChecksum": "checksum-a",
        "RuntimeBuildType": "windows-standalone",
        "ExecutableName": "SensCalibr8Calibration.exe",
        "ExecutableChecksum": "executable-checksum-a",
        "UnityVersion": known,
        "InputSystemVersion": known,
        "InputUpdateMode": "ProcessEventsInDynamicUpdate",
        "TimestampSource": "win32-wm-input-qpc",
        "RedundantEventMergingDisabled": True,
        "DedicatedRawInputMessagePump": True,
        "OperatingSystem": known,
        "GraphicsDeviceName": known,
        "GraphicsDeviceVersion": known,
        "FullScreenMode": known,
        "ApplicationFocused": True,
        "Manual": {field: known for field in manual_fields},
    }
    plan = {
        "ProtocolId": "protocol-a",
        "CapturePlanId": "plan-a",
        "EnvironmentId": "environment-a",
        "ConditionId": "condition-a",
        "ExecutionOrder": known,
        "ControlledVariablesJson": '{"fixture": true}',
        "AcceptanceOwner": known,
        "ControlledMotionInstruction": known,
        "EvidenceState": "pilot",
        "PlannedRepeatCount": 2,
        "RepetitionOrdinal": 1,
        "TraceDurationSeconds": 1.0,
    }
    final = {
        "ProtocolId": "protocol-a",
        "EnvironmentId": "environment-a",
        "CapturePlanId": "plan-a",
        "ConditionId": "condition-a",
        "HarnessVersion": "harness-a",
        "HarnessChecksum": "checksum-a",
        "Status": "completed",
        "RunId": "run-a",
        "TraceId": "trace-a",
    }
    return environment, plan, final


def make_acceptance_package():
    return {
        "environment_id": "environment-a",
        "capture_plan_id": "plan-a",
        "condition_id": "condition-a",
        "evidence_state": "pilot",
    }


if __name__ == "__main__":
    unittest.main()
