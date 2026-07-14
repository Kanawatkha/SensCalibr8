# RESEARCH.md

## Purpose

This file is the single source of truth for every formula, threshold, constant, and benchmark used anywhere in SensCalibr8. If a number is not defined here, it must not be used in code. Do not modify any value in this file without updating the source citation alongside it.

The two primary project sources are stored locally at `reference/SensCalibr8_Project_Proposal_v3.md` and `reference/Valorant_Research_Report.md`. Any citation to either source must be checked against the actual file before it is added or changed; summaries and memory are not acceptable substitutes.

---

## 1. Effective DPI (eDPI)

```
eDPI = In-game Sensitivity x Hardware DPI
```

eDPI is the normalized unit used for all cross-user and cross-profile comparisons, since raw in-game sensitivity values are meaningless without accounting for hardware DPI differences.

Source: [Consolidated Research Report](reference/Valorant_Research_Report.md), Section 2 (Sensitivity Calculation and Methodology); [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 6.1.

---

## 2. PSA Method (Perfect Sensitivity Approximation)

```
Starting Sensitivity = 280 / Hardware DPI
eDPI Floor (hard minimum) = 160
```

Worked example: DPI = 1600 -> Starting Sensitivity = 280 / 1600 = 0.175

The constant 280 is the PSA baseline eDPI value. This value must never be changed without a peer-reviewed or otherwise rigorously verifiable academic source. Forum posts, community blogs, or unverified "pro player data" aggregations are not sufficient justification for changing this constant.

The eDPI Floor of 160 is a hard minimum. Any calculated eDPI below 160 must be auto-adjusted upward to 160, with a warning shown to the user (see `RULES.md`, Input Validation Rules).

Source: [Consolidated Research Report](reference/Valorant_Research_Report.md), Section 2 (Sensitivity Calculation and Methodology); [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Sections 6.2 and 13.2.

---

## 3. Physical Movement Distance (cm/360)

```
cm/360 = (2.54 x 360) / (DPI x Sensitivity x 0.0022)
```

This metric expresses the physical hand-travel distance in centimeters required to complete a full 360-degree turn. It is used alongside eDPI to account for ergonomic constraints, such as available mousepad space, and is linked to the Grip Tension concept referenced in the source research.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 6.3.

---

## 4. Performance Score Formula

```
Performance Score =
    (Consistency         x 0.35) +
    (Accuracy%           x 0.30) +
    (Reaction Speed      x 0.20) +
    (Precision/Headshot% x 0.15) -
    (Submovement Penalty x 0.10)
```

```
Submovement Penalty = normalized_submovement_count
Required range = 0.0 to 1.0 (higher = more penalty)
```

The source proposal defines the required normalized range, but not the transformation from raw Submovement Count into that range. The transformation remains blocked by `PROGRESS.md`, OQ-004.

This score determines the "Winner" of each phase in the testing protocol. Weighting was deliberately calibrated as follows:

- Consistency and Accuracy carry the highest weight (0.35 and 0.30) because they are the most direct indicators of sensitivity suitability.
- Reaction Speed carries moderate weight (0.20).
- Headshot%/Precision carries the lowest positive weight (0.15) because real tournament data (VCT Masters Reykjavik) shows that even the player with the highest headshot percentage in that tournament (ScreaM) achieved only 33.12%. A metric with such a low practical ceiling should not be weighted heavily.
- Submovement Penalty (0.10) is subtracted. A higher submovement count during a single aiming action indicates the sensitivity value required excessive micro-correction, which is a negative signal for that sensitivity value. See Section 5 below for how Submovement Count is calculated.

Every stored Performance Score result must be tagged with a `formula_version` value (see `ARCHITECTURE.md`, `sensitivity_tests` table) so that historical results remain comparable even if formula weights are revised in the future.

The operational definition of Consistency, cross-metric normalization/scaling, and the transformation of raw Submovement Count into Submovement Penalty are not defined by the source proposal. These are blocked open questions; see `PROGRESS.md`, OQ-002 through OQ-004. Do not implement the aggregate formula until they are resolved.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 6.4; VCT Masters Reykjavik 2022 official tournament statistics (Riot Games), as cited by the proposal.

---

## 5. Submovement Count Algorithm

Submovement count measures how many distinct corrective mouse movements occur during a single aiming action. A lower count indicates a more efficient, "one-shot" aim adjustment; a higher count indicates repeated overshoot/undershoot corrections, which this project treats as evidence that the tested sensitivity is not well suited to the user.

Recommended detection parameters:

- Movement start threshold: approximately 8 degrees/second of angular mouse velocity
- Movement end threshold: approximately 4 degrees/second of angular mouse velocity
- Refractory period between counted submovements: approximately 80 ms (movements occurring within this window of a previous movement are not counted as new submovements)
- Apply a Butterworth low-pass filter to raw mouse delta samples before velocity calculation, to remove sensor noise that would otherwise be miscounted as intentional movement

```
adaptation_cutoff = floor(total_shots_per_value x 0.5)
valid_shots = shots[adaptation_cutoff:]  # only these shots contribute to Performance Score
```

This same adaptation cutoff logic (discarding the first 50% of shots per tested value) also applies to Submovement Count aggregation, not only to Performance Score — see Section 8 below.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Sections 6.4 and 7.2; Boudaoud, Spjut, and Kim, "Analyzing Mouse Sensitivity Preferences in First-Person Aiming Tasks," IEEE Conference on Games (CoG) 2022. The relationship between submovement count and sensitivity was confirmed as monotonic in that study; the discard proportion (50%) is drawn directly from that paper's methodology ("we discard the first 50% of trials from each session as an adaptation period"), scaled to this project's smaller per-value sample size (30+ shots here vs. 500 trials per session in the original study).

---

## 6. Reaction Time Benchmark Tiers

| Tier | Reaction Time Range | Classification |
|---|---|---|
| S | `t < 200 ms` | Professional / Pro-level |
| A | `200 ms <= t < 250 ms` | Above Average |
| B | `250 ms <= t < 350 ms` | Average Gamer |
| C | `350 ms <= t <= 500 ms` | Below Average |
| D | `t > 500 ms` | Needs Improvement |

The boundaries above are intentionally non-overlapping and exhaustive: 200 ms belongs to A, 250 ms to B, 350 ms to C, and 500 ms to C.

Benchmark values are consolidated from multiple external sources referencing average human visual reaction time (approximately 250 ms), average FPS player range (300-500 ms), and professional esports player range (100-250 ms, or 150-180 ms depending on source).

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 6.5; general human factors literature on visual-motor reaction time as summarized by that proposal.

---

## 7. Headshot % Usage Constraint

Because real tournament data from the highest competitive level shows a practical maximum of approximately 30-33% (see Performance Score section above), Headshot%/Precision must only ever be used as a secondary indicator (weight 0.15 in the Performance Score formula). The user-facing Target Threshold for headshot percentage must never be set above 35%, since anything higher is not supported by empirical competitive data and would mislead the user about a realistic target.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 6.6; VCT Masters Reykjavik 2022 official tournament statistics (Riot Games).

---

## 8. Adaptation Period

For every tested sensitivity value, the system must discard the first 50% of recorded shots for that value before computing any aggregate metric (Performance Score, Submovement Count average, Accuracy%, etc.). This is not a fixed shot count — it scales with the actual sample size tested for that value.

```
adaptation_cutoff = floor(total_shots_per_value x 0.5)
valid_shots = shots[adaptation_cutoff:]
```

Worked example: if 30 shots are recorded for a given sensitivity value, the first 15 shots are discarded and only the remaining 15 are used in calculations.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 7.2; Boudaoud, Spjut, and Kim, IEEE CoG 2022.

---

## 9. Mousepad Constraint Validation

```
IF cm_360 > mousepad_width_cm:
    TRIGGER warning_flag("mousepad_constraint_violation")
```

If the physical distance required for a full 360-degree turn (cm/360, see Section 3) exceeds the user's stated mousepad width, the system must raise a warning. This does not block the sensitivity value from being tested, but flags a likely ergonomic issue (frequent mouse lifting mid-turn).

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 6.3; derived from the cm/360 formula in Section 3 combined with user-provided mousepad dimensions (see `ARCHITECTURE.md`, `profiles` table).

---

## 10. Fitts's Law — Index of Difficulty

```
Index of Difficulty (ID) = log2(2 x Distance / Target Width)
```

All test modes must randomize target size (Small / Medium / Large) together with distance, not distance alone, so that the Index of Difficulty varies according to established motor-control theory rather than distance alone.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 5.1; Fitts, P. M. (1954). "The Information Capacity of the Human Motor System in Controlling the Amplitude of Movement." Journal of Experimental Psychology.

---

## 11. Testing Protocol Constants

The following values define the required execution structure of the three-phase sensitivity testing protocol and the continuous training cycle. They are fixed project protocol values and must be centralized in configuration rather than embedded as magic numbers in test logic.

### 11.1 Phase 1 — PSA Method

- Number of sensitivity values: 7
- Minimum sample size: 30 shots per sensitivity value

The method for generating and spacing the seven values around the PSA baseline is not defined by the source proposal; see `PROGRESS.md`, OQ-007.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 7.2 (Phase 1 — PSA Method).

### 11.2 Phase 2 — Progressive Narrowing

- Candidate range: Phase 1 Winner, Winner +10%, and Winner -10%
- Minimum sessions: 5 sessions per sensitivity value
- Maximum sessions: 10 sessions per sensitivity value
- Stabilization threshold: Performance Score variability below 10%

The 10% threshold is authoritative. The exact statistical definition of "variability below 10%" (raw standard deviation versus coefficient of variation) is not specified by the source proposal and must be resolved before implementation; see `PROGRESS.md`, Open Questions / Ambiguities.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 7.3 (Phase 2 — Progressive Narrowing).

### 11.3 Phase 3 — Final Narrowing

- Candidate range: Phase 2 Winner, Winner +5%, and Winner -5%

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 7.4 (Phase 3 — Final Narrowing).

### 11.4 Continuous Training Cycle

- Training block: 5-10 sessions at the current Best Sensitivity before evaluating Grade progression, fatigue, or plateau behavior

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 7.6 (Continuous Improvement Cycle).

---

## 12. Outlier Detection Threshold

A shot is flagged as an outlier when its measured result lies beyond 3 standard deviations from the applicable distribution. Outlier shots are treated as accidental observations rather than representative sensitivity performance and must be marked through `shots.is_outlier`.

The metric, sample scope, and handling details used to construct the applicable distribution must remain consistent within an analysis and be documented by the implementation.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 8 (Scientific Rigor & Confound Control).

---

## 13. Micro-Correction Target Offset

Micro-Correction mode must spawn a stationary, small target at an offset of 5-20 pixels from the current crosshair position. The offset range must be randomized within these bounds during the mode.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 5 (Test Mode Specifications), Micro-Correction row.

---

## 14. Grip Tension Range

The source research recommends managing mouse grip pressure within a 60-80% range. It states that excessive tension at 100% produces shaky, robotic movements, while insufficient tension reduces control during high-velocity tracking.

This range is contextual ergonomic guidance for the Tracking mode research basis. SensCalibr8 does not currently have an objective grip-pressure sensor, so the range must not be converted into an automatic score, threshold, or causal sensitivity adjustment without a separately approved measurement method.

Source: [Consolidated Research Report](reference/Valorant_Research_Report.md), Section 2 (Mouse Dynamics and Sensitivity Calibration), subsection "Ergonomics and Physiological Alignment — Grip Tension."

---

## 15. The 9-Week Rule — Source Progression

The source research describes an iterative refinement sequence that compares the current baseline against variants 20% higher and 20% lower, then repeats the process at 10% and 5% variances. Warm-up and aim-training routines must remain identical during the comparison period, and performance is evaluated weekly.

This source supports the progressive use of +/-10% and +/-5% in the operational protocol. It does not define how the seven Phase 1 PSA values are generated or spaced; that issue remains open in `PROGRESS.md`, OQ-007. The source's initial +/-20% comparison must not be silently substituted for the Proposal V3.0 seven-value Phase 1 protocol.

Source: [Consolidated Research Report](reference/Valorant_Research_Report.md), Section 2 (Mouse Dynamics and Sensitivity Calibration), subsection "The 9-Week Rule (Iterative Refinement)."

---

## 16. Informational Wrist-Strain Warning Threshold

```
IF edpi < 200 AND movement_strategy == "wrist":
    SHOW warning_flag("low_edpi_wrist_strain")
```

This threshold is an informational, non-diagnostic warning condition only. It must never block testing, alter Performance Score, or be presented as a medical conclusion.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 9 (Injury Risk & Ergonomic Safety Notice).

---

## Summary Table of Constants

| Constant | Value | Section |
|---|---|---|
| PSA Baseline eDPI | 280 | 2 |
| eDPI Floor | 160 | 2 |
| Performance Score weight — Consistency | 0.35 | 4 |
| Performance Score weight — Accuracy% | 0.30 | 4 |
| Performance Score weight — Reaction Speed | 0.20 | 4 |
| Performance Score weight — Precision/Headshot% | 0.15 | 4 |
| Performance Score weight — Submovement Penalty | 0.10 (subtracted) | 4 |
| Submovement start threshold | ~8 deg/s | 5 |
| Submovement end threshold | ~4 deg/s | 5 |
| Submovement refractory period | ~80 ms | 5 |
| Adaptation discard proportion | 50% of shots per value | 8 |
| Headshot% target threshold ceiling | 35% | 7 |
| Reaction Time Tier S | `t < 200 ms` | 6 |
| Reaction Time Tier A | `200 <= t < 250 ms` | 6 |
| Reaction Time Tier B | `250 <= t < 350 ms` | 6 |
| Reaction Time Tier C | `350 <= t <= 500 ms` | 6 |
| Reaction Time Tier D | `t > 500 ms` | 6 |
| Phase 1 sensitivity values | 7 | 11.1 |
| Phase 1 minimum sample | 30 shots per value | 11.1 |
| Phase 2 narrowing range | +/- 10% | 11.2 |
| Phase 2 minimum sessions | 5 per value | 11.2 |
| Phase 2 maximum sessions | 10 per value | 11.2 |
| Phase 2 stabilization threshold | Performance Score variability < 10% | 11.2 |
| Phase 3 narrowing range | +/- 5% | 11.3 |
| Continuous training block | 5-10 sessions | 11.4 |
| Outlier detection threshold | Beyond 3 standard deviations | 12 |
| Micro-Correction target offset | 5-20 px | 13 |
| Recommended grip pressure | 60-80% | 14 |
| Excessive grip-pressure reference | 100% | 14 |
| 9-Week Rule initial comparison | Baseline and +/- 20% | 15 |
| 9-Week Rule later comparisons | +/- 10%, then +/- 5% | 15 |
| Informational wrist-warning threshold | eDPI < 200 with Wrist strategy | 16 |
| Submovement Penalty output range | 0.0-1.0 | 4 |
