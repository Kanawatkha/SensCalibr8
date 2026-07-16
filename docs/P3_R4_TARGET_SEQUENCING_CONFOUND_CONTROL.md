# P3-R4 Target Sequencing and Confound Control

`FrozenSequenceContract` loads sequence, geometry, target-size, foreperiod, Tracking-block, and spawn-safety values from the accepted immutable calibration configuration. Unsupported or incomplete contracts fail before a sequence is created.

`DeterministicTargetSequencer` hashes canonical seed material made from mode-contract version, profile, cycle, phase, canonical mode name, and battery repetition. Sensitivity and blind candidate label are not accepted by the seed API. Close/Far sequences balance every offset-size condition, Micro-Correction uses the frozen Small target and radial pixel range, and Tracking crosses every pattern with every size once per block. Projected target centers include the target radius in edge/HUD safety validation.

Candidate presentation contains only opaque `Candidate-01...` labels. Candidate order is deterministically randomized, mode order rotates across four repetitions, and confirmatory pair order comes directly from `sc8-confirmatory-v1`. Pair plans expose stable pairing seeds and matched-condition keys for P3-R5 persistence.

Verification passed: Unity EditMode 103/103; Windows production build; Python production tests 11/11; Phase 0 calibration regressions 72/72.
