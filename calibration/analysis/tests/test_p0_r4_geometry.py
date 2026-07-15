import json
import tempfile
import unittest
from pathlib import Path

from calibration.analysis.p0_r4_geometry import (
    GeometryEvidenceError,
    derive_geometry,
    horizontal_to_vertical_fov_deg,
    load_json_object,
    write_new_json,
)


CANDIDATE = Path("calibration/plans/p0-r4-geometry-candidate-v1.json")


class P0R4GeometryTests(unittest.TestCase):
    def test_candidate_passes_all_mathematical_gates(self):
        result = derive_geometry(load_json_object(CANDIDATE))
        self.assertTrue(result["accepted"])
        self.assertTrue(all(result["gates"].values()))

    def test_reference_horizontal_fov_converts_deterministically(self):
        result = derive_geometry(load_json_object(CANDIDATE))
        expected = horizontal_to_vertical_fov_deg(103.0, 16.0 / 9.0)
        self.assertAlmostEqual(result["derived"]["vertical_fov_deg"], expected, places=12)

    def test_target_sizes_project_to_three_distinct_pixel_diameters(self):
        result = derive_geometry(load_json_object(CANDIDATE))
        geometry = result["derived"]["target_geometry"]
        pixels = [geometry[name]["projected_pixel_diameter"] for name in ("small", "medium", "large")]
        self.assertLess(pixels[0], pixels[1])
        self.assertLess(pixels[1], pixels[2])
        self.assertGreaterEqual(pixels[1] - pixels[0], 5.0)
        self.assertGreaterEqual(pixels[2] - pixels[1], 5.0)

    def test_each_flick_family_has_full_distance_size_cross_product(self):
        result = derive_geometry(load_json_object(CANDIDATE))
        for family in ("close", "far"):
            conditions = result["derived"]["condition_sets"][family]
            keys = {(item["distance_deg"], item["size"]) for item in conditions}
            self.assertEqual(len(conditions), 9)
            self.assertEqual(len(keys), 9)

    def test_center_hit_area_is_one_quarter_of_target_area(self):
        result = derive_geometry(load_json_object(CANDIDATE))
        self.assertAlmostEqual(result["derived"]["center_hit_area_ratio"], 0.25)

    def test_invalid_reference_aspect_is_rejected(self):
        candidate = load_json_object(CANDIDATE)
        candidate["display"]["reference_width_px"] = 1600
        with self.assertRaisesRegex(GeometryEvidenceError, "aspect"):
            derive_geometry(candidate)

    def test_derived_writer_refuses_overwrite(self):
        result = derive_geometry(load_json_object(CANDIDATE))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "derived.json"
            write_new_json(path, result)
            self.assertTrue(json.loads(path.read_text(encoding="utf-8"))["accepted"])
            with self.assertRaises(FileExistsError):
                write_new_json(path, result)

    def test_derivation_is_byte_deterministic(self):
        first = derive_geometry(load_json_object(CANDIDATE))
        second = derive_geometry(load_json_object(CANDIDATE))
        first.pop("analysis_runtime")
        second.pop("analysis_runtime")
        self.assertEqual(
            json.dumps(first, sort_keys=True, separators=(",", ":")),
            json.dumps(second, sort_keys=True, separators=(",", ":")),
        )


if __name__ == "__main__":
    unittest.main()
