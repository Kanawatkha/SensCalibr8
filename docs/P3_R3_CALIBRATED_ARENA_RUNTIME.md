# P3-R3 Calibrated Arena Runtime

`CalibratedArenaRuntime` and `FrozenArenaGeometry` load only the accepted immutable `calibration_config_v1` geometry. The parser fails closed unless it receives the accepted geometry version, enclosed unlit shadow-free room, spherical targets, and fixed-dot crosshair contract.

The runtime applies the frozen frame-policy scope, Unity's frozen vertical FOV, and the frozen reference aspect by letterboxing. It creates all six enclosing checkerboard faces. `ArenaTargetService` accepts only frozen Small, Medium, or Large target names and uses the frozen cyan color and world diameter.

The profile's approved palette color is the only crosshair input. Style and diameter remain frozen. The HUD occupies the frozen top reserve. First-person shoot feedback is visual-only and responds to a click; P3-R2 remains the authoritative input/timing path, while P3-R4 and Phase 4 own target sequencing and shot behavior.

Verification passed: Unity EditMode 94/94; Windows production build; Python production tests 11/11; Phase 0 calibration regressions 72/72.
