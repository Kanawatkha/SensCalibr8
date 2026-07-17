# P5-R2 — Phase 1 exploratory protocol

## Scope

P5-R2 creates and persists the initial seven-value sensitivity set, presents it through blind labels, counterbalances the four test modes, and enforces the accepted Phase 1 sample contract. It does not rank candidates, narrow the Phase 2 range, assign Grades, or select a Winner.

## Candidate generation

`config/protocol-constants-v1.json` is the accepted immutable source for the seven offsets: 0%, ±5%, ±10%, and ±20%. Production applies each offset to the PSA baseline eDPI without intermediate rounding, applies the accepted eDPI floor, and only then converts final eDPI to sensitivity using the profile DPI.

When multiple offsets converge at the floor, one canonical final-eDPI candidate is retained. Every original anchor/offset/pre-floor path remains in `protocol_candidate_sources`, and every floor adjustment remains available as a user notification. The candidate and source set is written atomically.

## Blind and counterbalanced execution

The workflow exposes only `Candidate-NN`, candidate identity, and order. Numeric eDPI and sensitivity remain internal to the persisted plan and battery. The existing deterministic sequence contract randomizes candidate order and rotates the four modes across battery repetitions. Launching the same candidate/repetition twice is rejected.

## Completion contract

Completion counts are projected from the accepted frozen calibration configuration:

- Each shot-based mode: 30 resolved observations, first 15 adaptation, final 15 authoritative.
- Tracking: 18 trials and 108 one-second windows, first 9 trials adaptation, final 9 trials / 54 windows authoritative.

An incomplete count cannot satisfy the Phase 1 protocol contract.

## Verification

- Unity EditMode: 168/168 passed.
- DPI 1600 exact outputs: sensitivity 0.14, 0.1575, 0.16625, 0.175, 0.18375, 0.1925, and 0.21.
- Floor fixture: anchor eDPI 170 collapses two below-floor paths into one eDPI 160 candidate while retaining both source rows and both notifications.
- Persistence fixture: full rollback when any candidate in the batch violates a uniqueness constraint.
- Presentation fixture: no public sensitivity/eDPI property on blind candidate or launch DTOs.
- Counterbalance fixture: all four modes occupy every order position across four repetitions.
- Calibration analysis regression: 72/72 passed.
- Windows production build: passed (`SensCalibr8.exe`, 667648 bytes).
- `git diff --check`: passed.
