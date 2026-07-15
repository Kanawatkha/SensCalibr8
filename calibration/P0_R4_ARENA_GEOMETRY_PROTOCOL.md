# P0-R4 Arena and Visual-Geometry Calibration

## Document Control

| Field | Value |
|---|---|
| Owning round | `P0-R4` |
| Protocol authority | `sc8-p0-r1-protocol-v1` |
| Candidate geometry | `sc8-test-geometry-candidate-v1` |
| Accepted geometry | `sc8-test-geometry-v1` |
| Current state | Completed and accepted on 2026-07-15 |
| Operator testing | None required; project owner ended further manual calibration on 2026-07-15 |
| Production authorization | Geometry component authorized; P0-R5/P0-R6/P0-R7 dependencies remain blocked |

## 1. Objective

Freeze a fixed, reproducible arena/camera/target contract that jointly varies target distance and width, preserves identical visual geometry across sessions, keeps every target fully inside the safe test viewport, and supplies a versioned Center-Hit zone and fixed crosshair size.

This round owns arena dimensions, camera projection/FOV, reference viewport, fixed Target Frame Rate, Small/Medium/Large target geometry, Close/Far angular placement, spawn safety, crosshair pixel diameter, Center-Hit geometry, and Micro-Correction pixel-to-world projection. Spawn timing, Tracking speed/duration, and signal response remain P0-R5 responsibilities.

## 2. Non-Negotiable Constraints

- Perspective world geometry is fixed and versioned; no per-user target scaling is allowed.
- The test viewport is 16:9. Non-16:9 displays use a fixed 16:9 letterboxed viewport rather than changing FOV or target appearance.
- Target width and center distance are crossed jointly. Distance-only variation is forbidden.
- Fitts difficulty uses `ID = log2(2D/W)` from `RESEARCH.md`, Section 10, where angular center distance and angular target width use the same degree unit.
- Targets are cyan spheres; background geometry is an enclosed unlit checkerboard room with no shadows or decorative objects.
- Crosshair style and size are fixed; only the profile color is configurable.
- Center-Hit Percentage remains diagnostic only.
- Display-mode, viewport, FOV, camera transform, or frame-policy changes during a session invalidate that session.

Unity's perspective camera uses the configured FOV axis and converts between horizontal and vertical FOV using aspect ratio. Screen-space coordinates are pixel-based and can be projected to world space at a specified camera distance. Sources: [Unity 6 Camera manual](https://docs.unity3d.com/cn/current/Manual/class-Camera.html), [Unity HorizontalToVerticalFieldOfView](https://docs.unity3d.com/ja/2019.4/ScriptReference/Camera.HorizontalToVerticalFieldOfView.html), [Unity ScreenToWorldPoint](https://docs.unity3d.com/ja/current/ScriptReference/Camera.ScreenToWorldPoint.html).

## 3. Candidate Selection

The reference host is 1920 x 1080 at 144 Hz. P0-R4 selects a 103-degree horizontal perspective FOV and converts it to Unity's vertical FOV for the 16:9 viewport. Target centers lie on a plane 10 world units in front of the camera.

Target angular diameters are 0.75, 1.50, and 2.25 degrees. At the reference projection these produce approximately 10, 20, and 30 pixels, yielding three clearly distinct sizes without changing color or depth. Close center offsets are 5, 10, and 15 degrees; Far offsets are 20, 30, and 40 degrees. Each family uses the full 3 x 3 distance/size cross-product, producing nine auditable Fitts conditions.

The 20 x 12 x 21 world-unit room encloses the camera and every target edge at the maximum horizontal/vertical placement. The target plane is not a wall collision surface; targets remain independent spheres. Exact derived world diameters, pixel diameters, vertical FOV, screen offsets, and Fitts IDs are generated from the candidate JSON and never duplicated as hand-maintained constants.

Crosshair diameter is 4 pixels. A Center Hit requires radial error no greater than half the target radius, making the center zone one quarter of the target's projected area. Micro-Correction retains the source-authorized 5-20 pixel center-offset range and converts those screen offsets to world offsets at the same target plane.

## 4. Automated Acceptance Matrix

| Gate | Required result |
|---|---|
| Configuration structure | Every required field exists; positive dimensions/FOV/rates; exactly three ordered target sizes and three ordered distances per family |
| FOV conversion | Derived vertical FOV matches the perspective/aspect formula and is used by Unity |
| Target projection | Each world diameter reprojects to its configured angular/pixel diameter within 0.01 px |
| Size separation | Small < Medium < Large and every adjacent projected diameter differs by at least 5 px |
| Joint conditions | Exactly nine unique `(distance, width)` conditions per Close/Far family |
| Fitts coverage | Every ID is finite/positive; increasing distance raises ID at fixed width; increasing width lowers ID at fixed distance |
| Safe viewport | Every horizontal/vertical extreme plus target radius stays outside edge/HUD exclusions |
| Arena containment | Every target edge and camera position lies within the enclosed room bounds |
| Center zone | Ratio is within `(0,1)` and derived area ratio equals the squared radius ratio |
| Micro-Correction | 5 px and 20 px offsets project monotonically to positive world offsets and remain fully inside the safe viewport |
| Frame policy | Target frame rate is 144, VSync count is zero, adaptive sync is required off |
| Repeatability | Re-running the analyzer on the same immutable JSON produces byte-identical derived geometry |
| Unity parity | Unity EditMode projection fixtures match the Python-derived values before acceptance |

## 5. Evidence Workflow

1. Validate and derive geometry from `calibration/plans/p0-r4-geometry-candidate-v1.json`.
2. Write a new immutable derived artifact; never overwrite the candidate.
3. Run Python unit/invariant tests and deterministic-output comparison.
4. Add the same configuration to the calibration-only Unity harness and run EditMode projection tests.
5. Render the checkerboard/cyan-sphere scene at 1920 x 1080 and visually inspect the reference screenshot for clipping, HUD overlap, contrast, and geometry errors.
6. If every gate passes, copy the candidate to an accepted immutable geometry contract, update `RESEARCH.md`/`ARCHITECTURE.md`/`PROGRESS.md`, and proceed to P0-R5.

P0-R4 does not require the project owner to perform repeated physical aim runs.

## 6. Acceptance Result

P0-R4 is accepted as `sc8-test-geometry-v1`.

| Verification | Result |
|---|---|
| Candidate validation and deterministic derivation | Passed; every gate in Section 4 is `true` |
| Python geometry tests | Passed 8/8; combined P0-R3/P0-R4 suite passed 29/29 |
| Unity EditMode parity | Passed 19/19 on Unity 6000.5.3f1 |
| Deterministic repeatability | Passed; identical input produced byte-identical derived output |
| 1920 x 1080 reference-render inspection | Passed; no clipping, HUD overlap, shadow, decoration, or unsafe target edge found |
| Operator aim runs | Not required |

Authoritative artifacts:

- Accepted contract: `calibration/plans/p0-r4-geometry-accepted-v1.json`
- Derived geometry and gate results: `calibration/evidence/p0-r4/p0-r4-geometry-derived-v1.json`
- Unity result: `calibration/evidence/p0-r4/p0-r4-unity-editmode-results-v4.xml`
- Reference render: `calibration/evidence/p0-r4/p0-r4-reference-render-v1.png`

Two earlier Unity parity attempts remain preserved as failed evidence: one exposed an EditMode viewport-default mismatch and one exposed invalid render-texture cleanup. Both defects were corrected before the clean v4 acceptance run; neither failed artifact is relabeled or overwritten.
