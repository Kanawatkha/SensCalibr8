# P0-R3 Pilot v1 Rejection Record

## Decision

`sc8-p0-r3-input-timing-pilot-v1` is rejected as cadence evidence and cannot source a timing contract or confirmation run.

## Evidence

- Five completed, integrity-verified run packages were retained unchanged.
- Per-run median cadence ranged from 143.98018832595304 Hz to 143.9947009945977 Hz.
- The active display and frame policy were 144 Hz, and each run produced approximately one captured mouse event per frame.
- The pilot aggregate is retained at `p0-r3-pilot-v1-aggregate.json`; it must not be promoted despite its mechanically generated candidate fields.

## Root Cause

Unity Input System `1.19.0` enables redundant event merging by default. Its official package documentation states that consecutive `FastMouse` movement events within one update are combined while deltas are accumulated. The v1 harness did not disable this behavior, so its event timestamps describe Unity's merged update stream rather than the physical mouse report cadence.

## Disposition

- Raw files, manifests, diagnostics, and hashes remain append-only and are not deleted or edited.
- Pilot v1 has status `rejected-instrumentation-confound`.
- Harness `p0-r3-harness-v4` disables redundant event merging before capture and records the setting in every environment manifest.
- Capture plan `sc8-p0-r3-input-timing-pilot-v2` requires five fresh runs. No v1 run ID may be reused for v2 or confirmation.

Decision recorded 2026-07-15 during P0-R3 pilot review.
