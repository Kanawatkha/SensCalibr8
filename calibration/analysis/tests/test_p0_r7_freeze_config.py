import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from calibration.analysis.p0_r7_freeze_config import (
    CalibrationFreezeError,
    JSON_DATABASE_FIELDS,
    REQUIRED_DATABASE_FIELDS,
    build_configuration,
    build_evidence,
    load_json_object,
    load_verified_sources,
    pretty_json_bytes,
    regression_gates,
    validate_configuration,
    validate_source_relationships,
    write_new_json,
)


ROOT = Path(__file__).resolve().parents[3]
PLAN_PATH = ROOT / "calibration/plans/p0-r7-calibration-config-freeze-plan-v1.json"


class P0R7FreezeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.plan = load_json_object(PLAN_PATH)
        cls.sources = load_verified_sources(ROOT, cls.plan)
        cls.config = build_configuration(cls.plan, cls.sources)

    def test_source_hashes_and_dependency_chain_are_valid(self) -> None:
        validate_source_relationships(self.sources)

    def test_database_schema_has_exact_required_field_set_and_order(self) -> None:
        record = self.config["calibration_configs_record"]
        self.assertEqual(tuple(record), REQUIRED_DATABASE_FIELDS)
        self.assertEqual(len(record), 20)

    def test_every_required_database_field_is_populated(self) -> None:
        self.assertTrue(all(value is not None for value in self.config["calibration_configs_record"].values()))

    def test_embedded_json_is_canonical_and_round_trips(self) -> None:
        record = self.config["calibration_configs_record"]
        for field in JSON_DATABASE_FIELDS:
            self.assertEqual(json.dumps(json.loads(record[field]), sort_keys=True, separators=(",", ":")), record[field])

    def test_geometry_and_signal_mode_are_embedded_exactly(self) -> None:
        record = self.config["calibration_configs_record"]
        self.assertEqual(json.loads(record["target_geometry_json"]), self.sources["geometry"])
        self.assertEqual(json.loads(record["tracking_contract_json"]), self.sources["signal_mode"])

    def test_r6_payload_is_bound_through_accepted_envelope(self) -> None:
        payload = self.sources["scoring_statistics_payload"]
        envelope = self.sources["scoring_statistics_acceptance"]
        self.assertEqual(envelope["accepted_candidate_id"], payload["candidate_id"])
        self.assertEqual(self.config["formula_contract"]["formula"], payload["formula"])

    def test_owner_waiver_limitation_is_preserved(self) -> None:
        limitation = self.config["limitations"]
        self.assertFalse(limitation["strict_timing_confirmation_passed"])
        self.assertEqual(limitation["strict_candidate_v1_disposition"], "rejected")
        self.assertEqual(limitation["strict_candidate_v2_disposition"], "rejected")

    def test_draft_incomplete_and_mutated_configs_are_rejected(self) -> None:
        for mutation in ("draft", "incomplete"):
            changed = copy.deepcopy(self.config)
            changed["status"] = mutation
            with self.assertRaises(CalibrationFreezeError):
                validate_configuration(changed, self.plan, self.sources)
        changed = copy.deepcopy(self.config)
        changed["calibration_configs_record"]["cutoff_frequency_hz"] = 8.0
        with self.assertRaises(CalibrationFreezeError):
            validate_configuration(changed, self.plan, self.sources)
        changed = copy.deepcopy(self.config)
        del changed["calibration_configs_record"]["refractory_period_ms"]
        with self.assertRaises(CalibrationFreezeError):
            validate_configuration(changed, self.plan, self.sources)

    def test_mutated_source_hash_is_rejected(self) -> None:
        changed = copy.deepcopy(self.plan)
        changed["source_contracts"][0]["sha256"] = "0" * 64
        with self.assertRaises(CalibrationFreezeError):
            load_verified_sources(ROOT, changed)

    def test_regression_gates_pass(self) -> None:
        gates = regression_gates(self.config)
        self.assertEqual(len(gates), 20)
        self.assertTrue(all(gates.values()))

    def test_build_is_byte_deterministic(self) -> None:
        rebuilt = build_configuration(self.plan, self.sources)
        self.assertEqual(pretty_json_bytes(self.config), pretty_json_bytes(rebuilt))

    def test_evidence_identifies_all_fields_and_limitations(self) -> None:
        evidence = build_evidence(self.plan, self.config, "a" * 64)
        self.assertEqual(evidence["database_field_count"], 20)
        self.assertEqual(tuple(evidence["required_database_fields"]), REQUIRED_DATABASE_FIELDS)
        self.assertFalse(evidence["limitations"]["strict_timing_confirmation_passed"])

    def test_immutable_writer_refuses_overwrite(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "artifact.json"
            write_new_json(path, {"first": True})
            with self.assertRaises(FileExistsError):
                write_new_json(path, {"second": True})

    def test_published_config_and_evidence_match_deterministic_build(self) -> None:
        config_path = ROOT / self.plan["output_path"]
        evidence_path = ROOT / self.plan["evidence_path"]
        published = load_json_object(config_path)
        evidence = load_json_object(evidence_path)
        self.assertEqual(pretty_json_bytes(published), pretty_json_bytes(self.config))
        self.assertEqual(
            hashlib.sha256(config_path.read_bytes()).hexdigest(),
            evidence["config_sha256"],
        )
        self.assertTrue(evidence["accepted"])
        self.assertEqual(evidence["gate_count"], 20)

    def test_acceptance_envelope_pins_published_artifacts(self) -> None:
        envelope = load_json_object(
            ROOT / "calibration/plans/p0-r7-calibration-config-accepted-v1.json"
        )
        self.assertEqual(envelope["status"], "accepted")
        self.assertEqual(envelope["phase_zero_exit"], "passed")
        for path_field, hash_field in (
            ("config_path", "config_sha256"),
            ("derived_path", "derived_sha256"),
            ("unity_editmode_path", "unity_editmode_sha256"),
        ):
            container = envelope if path_field == "config_path" else envelope["evidence"]
            artifact = ROOT / container[path_field]
            self.assertEqual(hashlib.sha256(artifact.read_bytes()).hexdigest(), container[hash_field])


if __name__ == "__main__":
    unittest.main()
