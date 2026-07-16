import dataclasses
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

import sys

sys.path.insert(0, str(ROOT / "analysis" / "src"))

from senscalibr8_analysis.configuration import load_frozen_calibration_configuration, load_research_constants


class FrozenConfigurationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.parity = json.loads((ROOT / "config/production-config-parity-v1.json").read_text(encoding="utf-8"))

    def test_loads_the_accepted_complete_projection(self) -> None:
        configuration = load_frozen_calibration_configuration(ROOT)
        self.assertEqual(configuration.config_version, self.parity["config_version"])
        self.assertEqual(configuration.formula_version, self.parity["formula_version"])
        self.assertEqual(configuration.sha256, self.parity["config_sha256"])
        self.assertEqual(len(dataclasses.fields(configuration.record)), self.parity["record_field_count"])
        self.assertEqual(configuration.record.normalization_version, self.parity["normalization_version"])
        self.assertEqual(configuration.record.signal_pipeline_version, self.parity["signal_pipeline_version"])
        self.assertEqual(configuration.record.test_geometry_version, self.parity["test_geometry_version"])

    def test_configuration_contract_is_immutable(self) -> None:
        configuration = load_frozen_calibration_configuration(ROOT)
        with self.assertRaises(dataclasses.FrozenInstanceError):
            configuration.record.cutoff_frequency_hz = 0.0

    def test_loader_rejects_a_mutated_configuration(self) -> None:
        source = ROOT / "calibration/plans/calibration-config-v1.json"
        with tempfile.TemporaryDirectory() as temp:
            temp_root = Path(temp)
            plans = temp_root / "calibration/plans"
            plans.mkdir(parents=True)
            payload = json.loads(source.read_text(encoding="utf-8"))
            payload["status"] = "draft"
            mutated = plans / "calibration-config-v1.json"
            mutated.write_text(json.dumps(payload), encoding="utf-8")
            envelope = json.loads((ROOT / "calibration/plans/p0-r7-calibration-config-accepted-v1.json").read_text(encoding="utf-8"))
            envelope["config_sha256"] = hashlib.sha256(mutated.read_bytes()).hexdigest()
            (plans / "p0-r7-calibration-config-accepted-v1.json").write_text(json.dumps(envelope), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Configuration identity mismatch: status"):
                load_frozen_calibration_configuration(temp_root)

    def test_parity_manifest_agrees_with_the_common_source(self) -> None:
        config_path = ROOT / self.parity["config_path"]
        self.assertEqual(hashlib.sha256(config_path.read_bytes()).hexdigest(), self.parity["config_sha256"])
        document = json.loads(config_path.read_text(encoding="utf-8"))
        record = document["calibration_configs_record"]
        self.assertEqual(len(record), self.parity["record_field_count"])
        self.assertEqual(document["formula_version"], self.parity["formula_version"])
        for key in ("normalization_version", "signal_pipeline_version", "test_geometry_version"):
            self.assertEqual(record[key], self.parity[key])
        self.assertEqual(document["mode_contract_version"], self.parity["mode_contract_version"])
        self.assertEqual(document["consistency_tier_version"], self.parity["consistency_tier_version"])
        self.assertEqual(document["confirmatory_contract_version"], self.parity["confirmatory_contract_version"])

    def test_general_research_constants_are_typed_and_immutable(self) -> None:
        constants = load_research_constants(ROOT)
        self.assertEqual(constants.psa_baseline_edpi, 280.0)
        self.assertEqual(constants.edpi_floor, 160.0)
        with self.assertRaises(dataclasses.FrozenInstanceError):
            constants.edpi_floor = 0.0


if __name__ == "__main__":
    unittest.main()
