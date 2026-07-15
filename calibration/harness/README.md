# SensCalibr8 Phase 0 Calibration Harness

This is an isolated, calibration-only Unity project created in `P0-R2` and extended for `P0-R3` input/timing capture. It captures evidence required by `../P0_R1_CALIBRATION_PROTOCOL.md`; it is not the production Test Engine and contains no scoring, Winner selection, protocol progression, or calibrated target behavior.

## Pinned Toolchain

- Unity Editor: `6000.3.18f1`, changeset `5ebeb53e4c07` ([official release](https://unity.com/releases/editor/whats-new/6000.3.18f1)).
- Unity Input System: `1.17.0`, the released package line documented for Unity 6 ([official package manual](https://docs.unity3d.com/6000.0/Manual/com.unity.inputsystem.html)).
- Unity Test Framework: Editor-matched core package `1.6.0` ([official core-package manual](https://docs.unity3d.com/6000.0/Manual/com.unity.test-framework.html)).
- Raw input path: `InputSystem.onEvent` with values read from `StateEvent` / `DeltaStateEvent` ([official event documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/Events.html)).

## Captured Artifacts

Each run creates a new directory and never overwrites an existing artifact:

- environment manifest;
- capture-plan manifest;
- started and final run manifests;
- raw mouse events as JSON Lines;
- frame timing as JSON Lines;
- target/camera events as JSON Lines;
- SHA-256 integrity manifest.

Raw event serialization occurs before enqueueing. Disk writes happen on dedicated background writer threads so the input callback does not perform synchronous file I/O. Finalization drains every queue, flushes files to disk, writes the immutable final manifest, then hashes the evidence package.

## Runtime Use

1. Open this directory as a Unity project with the pinned Editor.
2. Create an empty scene and add `CalibrationHarnessController` to a GameObject.
3. Fill every run/capture-plan field, including `pilot` or `confirmation`, acceptance owner, planned repeats, current repetition, positive predeclared trace duration, controlled motion instruction, controlled-variable JSON, and every manual environment field. Use `not-available` with evidence when a field genuinely cannot be read; blank or `unknown` is rejected.
4. Call `StartCapture()` from a temporary calibration UI or inspector integration. The P0-R3 gate rejects fewer than two planned independent runs and any unresolved environment field.
5. Use `RecordTargetCameraEvent(...)` from calibration target/camera instrumentation.
6. The controller completes automatically when the frozen trace duration elapses. `CompleteCapture()` remains available for controlled integration, and `InterruptCapture(reason)` records an interruption.

If the component is disabled or the application quits during capture, it finalizes the run as interrupted. `CalibrationRecoveryService` can finalize an abandoned run directory after a process-level interruption while preserving all partial raw evidence.

## Verification

Run EditMode tests in batch mode:

```powershell
<Unity.exe> -batchmode -nographics -projectPath <this-directory> -runTests -testPlatform EditMode -testResults <results.xml> -logFile <log.txt>
```

Do not add `-quit`; the Test Framework exits Unity after writing the result file. The tests cover ID/name normalization, append-only creation, asynchronous drain, high-resolution clock ordering, stream relationship/order enforcement, completed-run hashes and integrity metadata, dispose interruption, abandoned-run recovery, and P0-R3 capture-plan/environment gating.

The P0-R3 analysis commands and acceptance procedure are in `../P0_R3_INPUT_TIMING_PROTOCOL.md`. Pilot-derived values must be frozen in a separate contract before fresh confirmation; the analyzer refuses to reuse pilot run IDs.
