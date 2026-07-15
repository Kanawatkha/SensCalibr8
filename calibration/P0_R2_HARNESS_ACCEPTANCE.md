# P0-R2 Minimal Calibration Harness Acceptance

## Decision

`P0-R2 — Minimal calibration harness` is **accepted** on 2026-07-15 as `p0-r2-harness-v1`.

This acceptance applies only to calibration instrumentation and evidence handling. It does not authorize production target behavior, protocol progression, Performance Score, Grade, Winner selection, or any other production Test Engine behavior.

## Pinned Runtime

| Component | Accepted identity |
|---|---|
| Unity Editor | `6000.3.18f1`, changeset `5ebeb53e4c07` |
| Input System | `1.17.0` |
| Test Framework | Editor-matched core package `1.6.0` |
| Harness | `p0-r2-harness-v1`; the runtime assembly SHA-256 is captured in each environment/run/integrity manifest |

Primary references are the [Unity 6000.3.18f1 release](https://unity.com/releases/editor/whats-new/6000.3.18f1), [Unity Input System manual](https://docs.unity3d.com/6000.0/Manual/com.unity.inputsystem.html), [Input System event documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/Events.html), and [Unity Test Framework manual](https://docs.unity3d.com/6000.0/Manual/com.unity.test-framework.html).

## Acceptance Evidence

| P0-R1 requirement | Implemented evidence | Result |
|---|---|---|
| Raw input path | Project active input handler is Input System; capture uses `InputSystem.onEvent`, `StateEvent`/`DeltaStateEvent`, `ReadValueFromEvent`, event and high-resolution timestamps, and expanded device identity | Pass |
| Frame timing | Independent `Stopwatch` timestamps plus Unity frame index, unscaled frame duration, focus, display dimensions, refresh, and environment linkage | Pass |
| Target/camera events | Append-only target lifecycle/condition/position and camera-angle events linked to run and condition | Pass |
| Environment identity | Automatic Unity/Input System/OS/CPU/GPU/display/input identity; operator-only blanks become literal `unknown`; runtime assembly SHA-256 recorded | Pass |
| Append-only evidence | New run directory and `FileMode.CreateNew`; separate background JSONL writers; no raw-file reopen/resume or overwrite | Pass |
| Ordering and linkage | Store rejects mismatched run/trace/environment/condition IDs, non-contiguous sequence indexes, and non-monotonic timestamps | Pass |
| Finalization | Writer queues drain and force-flush before final manifest and integrity generation; append after finalization is rejected | Pass |
| Integrity | Every evidence artifact record contains artifact ID, relative path, byte size, SHA-256, creation time, producer version/checksum, and protocol/environment/plan/condition/run/trace source IDs | Pass |
| Interruption | Disable/quit/dispose finalization plus abandoned-run recovery; partial evidence retained and hashed with explicit `interrupted` status | Pass |
| Isolation | Static scan found no `Input.GetAxis`, `Time.time`, production scoring, Winner selection, protocol progression, or calibrated production target behavior | Pass |

## Automated Verification

- Unity EditMode result: **9 passed, 0 failed, 0 skipped**.
- NUnit result file: `harness/editmode-test-results.xml`.
- Unity log: `harness/editmode-test.log`.
- Covered fixtures: ID normalization, UTC run-ID validation, append-only writer drain/overwrite refusal, high-resolution clock ordering, automatic runtime identity/checksum and explicit unknowns, completed raw/frame/target evidence with full hash/metadata verification, broken relationship/order rejection, dispose interruption, and process-interruption recovery.
- Compiler result: no C# compiler errors in the accepted run.
- Forbidden-API/static-scope scan: pass, no matches.

## Remaining Gate Before P0-R3 Capture

P0-R2 is complete, but production-quality trace collection remains blocked until the operator completes the mandatory manual fields in `ENVIRONMENT_INVENTORY.md`, including verified mouse DPI/polling evidence, mouse/firmware/USB path, display/native refresh/scaling/adaptive-sync state, pointer settings, power/background-load policy, and physical operating descriptors. `unknown` remains valid only for harness fixtures, not for an acceptance-bearing P0-R3 run.

The next execution round is `P0-R3 — Input and timing calibration`.
