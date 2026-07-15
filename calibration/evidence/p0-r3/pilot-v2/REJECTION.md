# P0-R3 Pilot v2 Non-Acceptance Record

## Decision

`sc8-p0-r3-input-timing-pilot-v2` is structurally valid and proves that Unity redundant mouse-event merging was disabled, but it is not acceptable as physical interval evidence and cannot source the timing contract.

## Evidence

- Five completed packages passed integrity and recorded `RedundantEventMergingDisabled=true` under harness `p0-r3-harness-v4`.
- Each 30-second run retained approximately 31,500 unmerged Unity mouse events, demonstrating that the 144 Hz merge confound from Pilot v1 was removed.
- `InputEventTimestampSeconds` contained large groups of duplicate or microsecond-separated values followed by approximately frame-period gaps. Run 1 contained 31,492 events, 9,254 duplicate timestamps, and a p95 positive interval near 6.94 ms.
- The mechanically generated hundreds-of-kHz median is impossible for the configured 1000 Hz device and reflects batch timestamp assignment, not HID report cadence.

## Root Cause and Disposition

Unity's Windows RawInput backend exposes unmerged deltas but does not provide acceptance-quality per-report arrival timing through `InputEventPtr.time` for this environment. Pilot v2 remains immutable as backend batching evidence with status `analytical-non-acceptance-timestamp-domain`.

P0-R3 proceeds with capture plan `sc8-p0-r3-input-timing-pilot-v3`, which uses a dedicated Win32 `WM_INPUT` message pump and timestamps each received message immediately with QPC/`Stopwatch.GetTimestamp`. The analysis gate now rejects any package without `DedicatedRawInputMessagePump=true` and `TimestampSource=win32-wm-input-qpc`.

Decision recorded 2026-07-15 during P0-R3 Pilot v2 diagnostics.
