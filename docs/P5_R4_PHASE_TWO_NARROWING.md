# P5-R4 — Phase 2 progressive narrowing

## Scope

P5-R4 creates the Phase 2 candidate set from the persisted Phase 1 confirmatory decision, runs blind narrowing batteries, and determines whether each candidate's Performance Score has stabilized. It intentionally does not choose the Phase 2 Winner because the between-candidate rule is not defined by the authoritative specification.

## Candidate generation

The immutable `sc8-phase-two-protocol-v1` contract defines -10%, 0%, and +10% offsets. A significant Phase 1 result produces three values around its winning anchor. A statistical tie produces the union of both three-value sets. Generation preserves full floating-point precision, applies the accepted eDPI floor before deduplication, notifies every floor adjustment, and stores every source path.

## Evidence and stabilization

Only a complete purpose-`narrowing` battery with four distinct sessions and a battery-linked score under the accepted configuration/formula counts. For each candidate:

- 0-4 complete batteries: `Collecting`.
- 5-10 batteries and CV below 10%: `Stabilized`.
- 5-9 batteries and CV at/above 10%, or undefined near-zero mean: `RequiresMoreEvidence`.
- 10 batteries and still non-passing: `MaximumReachedWithoutStabilization`.

CV uses sample SD and `100 × SD / abs(mean)`. The boundary is strict; exactly 10% does not pass. The overall Phase 2 evidence set is ready only when every candidate has stabilized.

## Verification

- Unity EditMode: 184/184 passed.
- Winner fixture at eDPI 280: 252, 280, and 308; DPI 1600 sensitivities 0.1575, 0.175, and 0.1925.
- Tie/floor fixture: six source paths collapse to four canonical candidates while all provenance remains.
- CV worked fixture: scores 95, 95, 100, 105, 105 produce mean 100, sample SD 5, CV 5%, stabilized.
- Boundary fixtures: CV exactly 10% requires more evidence; zero mean is undefined; incomplete battery is ignored; non-passing evidence at 10 reaches maximum failure.
- Calibration analysis regression: 72/72 passed.
- Windows production build: passed (`SensCalibr8.exe`, 667648 bytes).
- `git diff --check`: passed.

## Deferred decision

OQ-022 must define how stabilized candidates produce one Phase 2 Winner and how exact equal means are handled before P5-R5 can generate Phase 3 candidates.
