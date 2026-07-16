import hashlib
import importlib.metadata
import json
import sys
import tomllib
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


class ProductionFoundationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.environment = json.loads(
            (ROOT / "config/production-environment-v1.json").read_text(encoding="utf-8")
        )

    def test_python_runtime_matches_pinned_environment(self) -> None:
        self.assertEqual(".".join(map(str, sys.version_info[:3])), self.environment["python"]["version"])

    def test_pyproject_and_requirements_match_dependency_pins(self) -> None:
        project = tomllib.loads((ROOT / "analysis/pyproject.toml").read_text(encoding="utf-8"))
        expected = {
            f"{name}=={version}"
            for name, version in self.environment["python"]["dependencies"].items()
        }
        self.assertEqual(set(project["project"]["dependencies"]), expected)
        requirements = {
            line.strip()
            for line in (ROOT / "analysis/requirements.txt").read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.startswith("#")
        }
        self.assertEqual(requirements, expected)

    def test_frozen_calibration_config_hash_matches_environment(self) -> None:
        calibration = self.environment["calibration"]
        digest = hashlib.sha256((ROOT / calibration["config_path"]).read_bytes()).hexdigest()
        self.assertEqual(digest, calibration["config_sha256"])

    def test_complete_dependency_lock_matches_the_local_environment(self) -> None:
        lock_path = ROOT / self.environment["python"]["requirements_lock_path"]
        locked = {
            name: version
            for name, version in (
                line.strip().split("==", 1)
                for line in lock_path.read_text(encoding="utf-8").splitlines()
                if line.strip() and not line.startswith("#")
            )
        }
        self.assertGreater(len(locked), len(self.environment["python"]["dependencies"]))
        for distribution, expected_version in locked.items():
            self.assertEqual(importlib.metadata.version(distribution), expected_version, distribution)

    def test_required_production_layer_definitions_exist(self) -> None:
        for layer in ("Core", "Data", "Services", "TestLogic", "UI"):
            path = ROOT / "app/Assets/SensCalibr8" / layer / f"SensCalibr8.{layer}.asmdef"
            self.assertTrue(path.is_file(), path)

    def test_analysis_package_does_not_import_unity_or_sqlite(self) -> None:
        source = (ROOT / "analysis/src/senscalibr8_analysis/__init__.py").read_text(encoding="utf-8")
        self.assertNotIn("Unity", source)
        self.assertNotIn("sqlite", source.lower())


if __name__ == "__main__":
    unittest.main()
