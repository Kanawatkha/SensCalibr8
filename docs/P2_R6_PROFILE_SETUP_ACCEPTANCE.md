# P2-R6 — Profile/setup acceptance

## Scope

P2-R6 is the Phase 2 acceptance gate. It validates the complete profile and physical-setup workflow delivered in P2-R1 through P2-R5. It adds acceptance tests only; no Test Engine behavior is implemented in this round.

## Acceptance coverage

- Profile creation, slot selection, setup editing, restart restoration, and locked crosshair color.
- Multiple-profile isolation for active setup and Dashboard warnings.
- Duplicate profile names and invalid DPI, sensitivity, polling-rate, and mousepad inputs.
- Physical Ruler calculation, preview, explicit positive-integer confirmation, and persistence.
- PSA worked example: DPI `1600` produces Starting Sensitivity `0.175` and eDPI `280`.
- eDPI floor adjustment to `160` with an explicit adjustment flag.
- Active-profile deletion prohibition.
- Confirmed inactive deletion and cascading child-cycle removal.
- Profile-scoped warning acknowledgement; warnings remain informational and non-blocking.

## Verification

The completed round passed Unity EditMode 73/73, production Python 11/11, Phase 0 calibration regression 72/72, and the Windows production build. Phase 2 is now complete and the next authorized round is Phase 3 — P3-R1: Engine contracts and state machine.
