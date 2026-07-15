"""Deterministic P0-R4 perspective-geometry derivation and acceptance gates."""

from __future__ import annotations

import argparse
import json
import math
import platform
import sys
from pathlib import Path
from typing import Any, Sequence


class GeometryEvidenceError(ValueError):
    """Raised when a geometry candidate is incomplete or internally invalid."""


def load_json_object(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise GeometryEvidenceError(f"invalid JSON: {path}") from error
    if not isinstance(value, dict):
        raise GeometryEvidenceError("geometry candidate root must be an object")
    return value


def write_new_json(path: Path, value: dict[str, Any]) -> None:
    if path.exists():
        raise FileExistsError(f"refusing to overwrite derived artifact: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True, allow_nan=False) + "\n",
        encoding="utf-8",
    )


def _positive(value: Any, field: str) -> float:
    try:
        number = float(value)
    except (TypeError, ValueError) as error:
        raise GeometryEvidenceError(f"{field} must be numeric") from error
    if not math.isfinite(number) or number <= 0:
        raise GeometryEvidenceError(f"{field} must be finite and positive")
    return number


def _vector3(value: Any, field: str) -> tuple[float, float, float]:
    if not isinstance(value, list) or len(value) != 3:
        raise GeometryEvidenceError(f"{field} must contain three values")
    result = tuple(float(item) for item in value)
    if not all(math.isfinite(item) for item in result):
        raise GeometryEvidenceError(f"{field} must be finite")
    return result  # type: ignore[return-value]


def horizontal_to_vertical_fov_deg(horizontal_fov_deg: float, aspect: float) -> float:
    horizontal_radians = math.radians(horizontal_fov_deg)
    return math.degrees(2.0 * math.atan(math.tan(horizontal_radians / 2.0) / aspect))


def perspective_focal_length_px(width_px: float, horizontal_fov_deg: float) -> float:
    return width_px / (2.0 * math.tan(math.radians(horizontal_fov_deg) / 2.0))


def angular_diameter_to_world(angular_diameter_deg: float, distance: float) -> float:
    return 2.0 * distance * math.tan(math.radians(angular_diameter_deg) / 2.0)


def angular_diameter_to_pixels(angular_diameter_deg: float, focal_px: float) -> float:
    return 2.0 * focal_px * math.tan(math.radians(angular_diameter_deg) / 2.0)


def angular_offset_to_pixels(angle_deg: float, focal_px: float) -> float:
    return focal_px * math.tan(math.radians(angle_deg))


def fitts_index_of_difficulty(distance_deg: float, width_deg: float) -> float:
    return math.log2(2.0 * distance_deg / width_deg)


def derive_geometry(candidate: dict[str, Any]) -> dict[str, Any]:
    if candidate.get("status") != "candidate-frozen":
        raise GeometryEvidenceError("geometry candidate must be candidate-frozen")
    geometry_version = str(candidate.get("geometry_version", "")).strip()
    if not geometry_version:
        raise GeometryEvidenceError("geometry_version must not be blank")

    display = candidate["display"]
    camera = candidate["camera"]
    arena = candidate["arena"]
    target = candidate["target"]
    flick = candidate["flick_conditions"]
    micro = candidate["micro_correction"]
    crosshair = candidate["crosshair"]
    safety = candidate["spawn_safety"]
    acceptance = candidate["acceptance"]

    width_px = _positive(display["reference_width_px"], "display width")
    height_px = _positive(display["reference_height_px"], "display height")
    aspect = _positive(display["aspect_ratio_width"], "aspect width") / _positive(
        display["aspect_ratio_height"], "aspect height"
    )
    if not math.isclose(width_px / height_px, aspect, rel_tol=0.0, abs_tol=1e-12):
        raise GeometryEvidenceError("reference viewport does not match aspect policy")
    horizontal_fov = _positive(camera["horizontal_fov_deg"], "horizontal FOV")
    if horizontal_fov >= 180.0:
        raise GeometryEvidenceError("horizontal FOV must be below 180 degrees")
    target_distance = _positive(
        camera["target_plane_distance_world"], "target-plane distance"
    )
    focal_px = perspective_focal_length_px(width_px, horizontal_fov)
    vertical_fov = horizontal_to_vertical_fov_deg(horizontal_fov, aspect)

    size_map = target["angular_diameter_deg"]
    expected_size_names = ("small", "medium", "large")
    if tuple(size_map.keys()) != expected_size_names:
        raise GeometryEvidenceError("target sizes must be ordered small/medium/large")
    angular_sizes = {name: _positive(size_map[name], name) for name in expected_size_names}
    size_values = list(angular_sizes.values())
    if size_values != sorted(size_values) or len(set(size_values)) != len(size_values):
        raise GeometryEvidenceError("target angular sizes must be strictly increasing")

    target_geometry: dict[str, dict[str, float]] = {}
    for name, angle in angular_sizes.items():
        world_diameter = angular_diameter_to_world(angle, target_distance)
        pixel_diameter = angular_diameter_to_pixels(angle, focal_px)
        reprojected = 2.0 * focal_px * (world_diameter / 2.0) / target_distance
        target_geometry[name] = {
            "angular_diameter_deg": angle,
            "world_diameter": world_diameter,
            "projected_pixel_diameter": pixel_diameter,
            "reprojected_pixel_diameter": reprojected,
            "projection_error_px": abs(pixel_diameter - reprojected),
        }

    condition_sets: dict[str, list[dict[str, float | str]]] = {}
    for family, key in (("close", "close_center_offset_deg"), ("far", "far_center_offset_deg")):
        distances = [_positive(value, f"{family} distance") for value in flick[key]]
        if distances != sorted(distances) or len(set(distances)) != len(distances):
            raise GeometryEvidenceError(f"{family} distances must be strictly increasing")
        conditions: list[dict[str, float | str]] = []
        for distance in distances:
            for size_name, width in angular_sizes.items():
                conditions.append(
                    {
                        "family": family,
                        "distance_deg": distance,
                        "size": size_name,
                        "width_deg": width,
                        "fitts_id": fitts_index_of_difficulty(distance, width),
                        "center_offset_px": angular_offset_to_pixels(distance, focal_px),
                        "center_offset_world": target_distance
                        * math.tan(math.radians(distance)),
                    }
                )
        condition_sets[family] = conditions

    maximum_target_radius_px = target_geometry["large"]["projected_pixel_diameter"] / 2.0
    edge_margin_px = _positive(safety["edge_margin_px"], "edge margin")
    hud_top_px = _positive(safety["hud_reserved_top_px"], "HUD reserve")
    horizontal_limit_px = width_px / 2.0 - edge_margin_px - maximum_target_radius_px
    vertical_bottom_limit_px = height_px / 2.0 - edge_margin_px - maximum_target_radius_px
    vertical_top_limit_px = (
        height_px / 2.0 - hud_top_px - maximum_target_radius_px
    )
    maximum_horizontal_offset_px = max(
        item["center_offset_px"]
        for family in condition_sets.values()
        for item in family
    )
    vertical_offset_px = angular_offset_to_pixels(
        _positive(flick["vertical_center_limit_deg"], "vertical center limit"),
        focal_px,
    )

    camera_position = _vector3(camera["position_world"], "camera position")
    arena_center = _vector3(arena["center_world"], "arena center")
    arena_dimensions = _vector3(arena["dimensions_world"], "arena dimensions")
    if any(value <= 0 for value in arena_dimensions):
        raise GeometryEvidenceError("arena dimensions must be positive")
    arena_min = tuple(
        center - dimension / 2.0
        for center, dimension in zip(arena_center, arena_dimensions)
    )
    arena_max = tuple(
        center + dimension / 2.0
        for center, dimension in zip(arena_center, arena_dimensions)
    )
    camera_inside = all(
        lower <= value <= upper
        for value, lower, upper in zip(camera_position, arena_min, arena_max)
    )
    maximum_world_radius = target_geometry["large"]["world_diameter"] / 2.0
    maximum_horizontal_world = target_distance * math.tan(
        math.radians(max(flick["far_center_offset_deg"]))
    ) + maximum_world_radius
    maximum_vertical_world = target_distance * math.tan(
        math.radians(flick["vertical_center_limit_deg"])
    ) + maximum_world_radius
    target_extents_inside = (
        camera_position[0] - maximum_horizontal_world >= arena_min[0]
        and camera_position[0] + maximum_horizontal_world <= arena_max[0]
        and camera_position[1] - maximum_vertical_world >= arena_min[1]
        and camera_position[1] + maximum_vertical_world <= arena_max[1]
        and camera_position[2] + target_distance - maximum_world_radius >= arena_min[2]
        and camera_position[2] + target_distance + maximum_world_radius <= arena_max[2]
    )

    micro_min_px = _positive(micro["minimum_center_offset_px"], "micro minimum")
    micro_max_px = _positive(micro["maximum_center_offset_px"], "micro maximum")
    if micro_min_px >= micro_max_px:
        raise GeometryEvidenceError("micro-correction offsets must be increasing")
    micro_projection = {
        "minimum_world_offset": target_distance * micro_min_px / focal_px,
        "maximum_world_offset": target_distance * micro_max_px / focal_px,
        "minimum_angular_offset_deg": math.degrees(math.atan(micro_min_px / focal_px)),
        "maximum_angular_offset_deg": math.degrees(math.atan(micro_max_px / focal_px)),
    }

    projection_tolerance = _positive(
        acceptance["projection_tolerance_px"], "projection tolerance"
    )
    minimum_size_separation = _positive(
        acceptance["minimum_distinct_target_pixel_diameter_px"],
        "minimum target-size separation",
    )
    pixel_sizes = [target_geometry[name]["projected_pixel_diameter"] for name in expected_size_names]
    size_separations = [
        right - left for left, right in zip(pixel_sizes[:-1], pixel_sizes[1:])
    ]
    required_conditions = int(acceptance["required_condition_count_per_flick_family"])
    center_ratio = _positive(target["center_hit_radius_ratio"], "center-hit ratio")
    fitts_monotonic = all(
        fitts_index_of_difficulty(farther, width)
        > fitts_index_of_difficulty(nearer, width)
        for width in angular_sizes.values()
        for distances in (
            flick["close_center_offset_deg"],
            flick["far_center_offset_deg"],
        )
        for nearer, farther in zip(distances[:-1], distances[1:])
    ) and all(
        fitts_index_of_difficulty(distance, narrower)
        > fitts_index_of_difficulty(distance, wider)
        for distance in (
            *flick["close_center_offset_deg"],
            *flick["far_center_offset_deg"],
        )
        for narrower, wider in zip(size_values[:-1], size_values[1:])
    )

    gates = {
        "projection_within_tolerance": all(
            item["projection_error_px"] <= projection_tolerance
            for item in target_geometry.values()
        ),
        "target_sizes_distinct": all(
            separation >= minimum_size_separation
            for separation in size_separations
        ),
        "joint_condition_counts": all(
            len(items) == required_conditions for items in condition_sets.values()
        ),
        "fitts_ids_positive_and_monotonic": fitts_monotonic
        and all(
            math.isfinite(float(item["fitts_id"])) and float(item["fitts_id"]) > 0
            for items in condition_sets.values()
            for item in items
        ),
        "horizontal_safe_viewport": maximum_horizontal_offset_px
        <= horizontal_limit_px,
        "vertical_safe_viewport": vertical_offset_px
        <= min(vertical_bottom_limit_px, vertical_top_limit_px),
        "arena_contains_camera_and_targets": camera_inside and target_extents_inside,
        "center_zone_valid": 0.0 < center_ratio < 1.0,
        "micro_projection_valid": 0.0
        < micro_projection["minimum_world_offset"]
        < micro_projection["maximum_world_offset"]
        and micro_max_px + target_geometry["small"]["projected_pixel_diameter"] / 2.0
        <= horizontal_limit_px,
        "frame_policy_frozen": int(display["target_frame_rate_hz"]) == 144
        and int(display["vsync_count"]) == 0
        and display["adaptive_sync_required_off"] is True,
        "fixed_crosshair_valid": _positive(crosshair["diameter_px"], "crosshair")
        < target_geometry["small"]["projected_pixel_diameter"],
        "letterbox_policy_frozen": display["non_16_9_policy"]
        == "letterbox-fixed-16-9-test-viewport",
    }

    return {
        "analysis_version": "p0-r4-geometry-analysis-v1",
        "source_geometry_version": geometry_version,
        "accepted": all(gates.values()),
        "gates": gates,
        "derived": {
            "aspect_ratio": aspect,
            "horizontal_fov_deg": horizontal_fov,
            "vertical_fov_deg": vertical_fov,
            "focal_length_px": focal_px,
            "target_plane_distance_world": target_distance,
            "target_geometry": target_geometry,
            "condition_sets": condition_sets,
            "safe_viewport": {
                "horizontal_center_limit_px": horizontal_limit_px,
                "vertical_bottom_center_limit_px": vertical_bottom_limit_px,
                "vertical_top_center_limit_px": vertical_top_limit_px,
                "maximum_condition_horizontal_offset_px": maximum_horizontal_offset_px,
                "configured_vertical_offset_px": vertical_offset_px,
            },
            "arena_bounds": {"minimum": arena_min, "maximum": arena_max},
            "maximum_target_extents_world": {
                "horizontal_from_camera": maximum_horizontal_world,
                "vertical_from_camera": maximum_vertical_world,
            },
            "center_hit_area_ratio": center_ratio**2,
            "micro_correction": micro_projection,
            "crosshair_diameter_px": float(crosshair["diameter_px"]),
            "target_frame_rate_hz": int(display["target_frame_rate_hz"]),
        },
        "analysis_runtime": {
            "python": sys.version.split()[0],
            "platform": platform.platform(),
        },
    }


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="SensCalibr8 P0-R4 geometry analyzer")
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    write_new_json(args.output, derive_geometry(load_json_object(args.candidate)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
