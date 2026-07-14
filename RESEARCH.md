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
Performance Score = 100 x (
    (Consistency         x 0.35) +
    (Accuracy%           x 0.30) +
    (Reaction Speed      x 0.20) +
    (Precision Score     x 0.15) -
    (Submovement Penalty x 0.10)
)
```

```
Submovement Penalty = normalized_submovement_count
Required range = 0.0 to 1.0 (higher = more penalty)
```

This score determines the "Winner" of each phase in the testing protocol. Weighting was deliberately calibrated as follows:

- Consistency and Accuracy carry the highest weight (0.35 and 0.30) because they are the most direct indicators of sensitivity suitability.
- Reaction Speed carries moderate weight (0.20).
- Headshot%/Precision carries the lowest positive weight (0.15) because real tournament data (VCT Masters Reykjavik) shows that even the player with the highest headshot percentage in that tournament (ScreaM) achieved only 33.12%. A metric with such a low practical ceiling should not be weighted heavily.
- Submovement Penalty (0.10) is subtracted. A higher submovement count during a single aiming action indicates the sensitivity value required excessive micro-correction, which is a negative signal for that sensitivity value. See Section 5 below for how Submovement Count is calculated.

Every stored Performance Score result must be tagged with a `formula_version` value (see `ARCHITECTURE.md`, `sensitivity_tests` table) so that historical results remain comparable even if formula weights are revised in the future.

Consistency is the standard deviation of the mode's primary error metric after the adaptation cutoff: Final Precision Error for shot-based modes and Tracking Deviation for Tracking. Lower raw Consistency values are better and must be inverted before normalization. Precision Score is derived from Final Precision Error; lower raw error is better and must likewise be inverted before normalization. Center-Hit Percentage is retained as a diagnostic only and does not enter Performance Score.

All positive components and the Submovement Penalty must be normalized to `[0,1]` before weights are applied. The weighted result is multiplied by 100 for the stored and displayed Performance Score. Normalization bounds and versions are fixed by Phase 0 and must never be inferred from the currently selected comparison data.

### 4.1 Tracking-Mode Component Mapping

Tracking maps its native metrics into the shared component model:

- Time-on-Target Percentage replaces Accuracy.
- Inverted Tracking Deviation supplies both the Tracking precision signal and the raw metric used for Tracking Consistency.
- Reaction Speed is omitted because Tracking has no discrete reaction event comparable to Flick modes.
- Submovement Penalty is omitted because Tracking has no discrete shot-level corrective action contract.

The removed Reaction Speed weight is redistributed proportionally across the remaining positive weights, preserving their original relative proportions:

```
Tracking Performance Score = 100 x (
    (Consistency               x 0.4375) +
    (Time-on-Target Percentage x 0.3750) +
    (Precision Score           x 0.1875)
)
```

These weights are derived exactly by dividing the original remaining positive weights (0.35, 0.30, 0.15) by their sum (0.80). They are not independently invented empirical weights.

### 4.2 Fixed, Versioned Metric Normalization

Each metric uses fixed lower and upper bounds, `L` and `U`, selected and validated during Phase 0. Inherent mathematical bounds may be used for bounded percentages; empirical metrics such as Reaction Time, Final Precision Error, Tracking Deviation, and Consistency require mode-specific calibrated bounds. Every bound set must be immutable and identified by `normalization_version`.

For a metric where higher values are better:

```
normalized_high(x, L, U) = clamp((x - L) / (U - L), 0, 1)
```

For a metric where lower values are better:

```
normalized_low(x, L, U) = 1 - clamp((x - L) / (U - L), 0, 1)
```

`U` must be strictly greater than `L`; otherwise the configuration is invalid and scoring must stop. Bounds must not be recalculated from the current user, session, candidate set, or newly appended history because doing so would change historical score meaning. A result must store both `formula_version` and `normalization_version`.

Source: project-owner approval dated 2026-07-14; [OECD/JRC Handbook on Constructing Composite Indicators](https://www.oecd.org/en/publications/handbook-on-constructing-composite-indicators-methodology-and-user-guide_9789264043466-en.html), normalization and robustness guidance.

### 4.3 Submovement Penalty Mapping

Submovement Count uses a capped linear mapping with mode-specific Phase 0 bounds:

```
Submovement Penalty = clamp((count - L_mode) / (U_mode - L_mode), 0, 1)
```

`L_mode` and `U_mode` must be calibrated, frozen, and stored under the active `normalization_version`. Only valid post-adaptation observations contribute to the aggregated count. Percentile, z-score, and nonlinear penalty mappings are not permitted without a new versioned specification and supporting evidence.

Source: project-owner approval dated 2026-07-14; [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 6.4 (required 0.0-1.0 output); [Boudaoud, Spjut, and Kim, IEEE CoG 2022](https://ieee-cog.org/2022/assets/papers/paper_64.pdf) (Submovement Count measurement); [OECD/JRC Composite Indicators Handbook](https://www.oecd.org/en/publications/handbook-on-constructing-composite-indicators-methodology-and-user-guide_9789264043466-en.html) (normalization).

Source for the base formula and weights: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 6.4; VCT Masters Reykjavik 2022 official tournament statistics (Riot Games), as cited by the proposal.

---

## 5. Submovement Count Algorithm

Submovement count measures how many distinct corrective mouse movements occur during a single aiming action. A lower count indicates a more efficient, "one-shot" aim adjustment; a higher count indicates repeated overshoot/undershoot corrections, which this project treats as evidence that the tested sensitivity is not well suited to the user.

Approved detection pipeline:

1. Capture every raw mouse delta with a high-resolution timestamp; do not use the Unity render frame rate as the input sampling rate.
2. Convert cumulative deltas into angular azimuth/elevation in degrees using the tested sensitivity and input calibration before filtering.
3. Resample the angular trace onto a uniform time grid using the stable measured input-event rate validated in Phase 0. A session whose event timing does not satisfy the calibrated stability contract is invalid for Submovement analysis.
4. Apply a fifth-order, 7 Hz low-pass Butterworth filter represented as second-order sections (SOS), using forward-backward offline filtering to avoid phase displacement of movement boundaries.
5. Calculate angular velocity from the filtered angular trace.
6. Start a submovement when angular velocity crosses 8 degrees/second and end it when velocity falls below 4 degrees/second.
7. Enforce an 80 ms refractory period between counted submovements.

The sampling rate is intentionally not hardcoded here: Phase 0 must measure, validate, and freeze it together with resampling tolerance and edge-handling behavior. The complete configuration must be stored under a `signal_pipeline_version`. The 7 Hz response and 8/4 degrees-per-second thresholds must also be validated against recorded traces during Phase 0 before production use.

```
adaptation_cutoff = floor(total_shots_per_value x 0.5)
valid_shots = shots[adaptation_cutoff:]  # only these shots contribute to Performance Score
```

This same adaptation cutoff logic (discarding the first 50% of shots per tested value) also applies to Submovement Count aggregation, not only to Performance Score — see Section 8 below.

Source: project-owner approval dated 2026-07-14; [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Sections 6.4 and 7.2; [Boudaoud, Spjut, and Kim, IEEE CoG 2022](https://ieee-cog.org/2022/assets/papers/paper_64.pdf) (fifth-order 7 Hz Butterworth filter, 8/4 degrees-per-second thresholds, 80 ms refractory period, and 50% adaptation discard); [SciPy Butterworth documentation](https://docs.scipy.org/doc/scipy-1.13.1/reference/generated/scipy.signal.butter.html) (SOS numerical-stability guidance); [SciPy forward-backward filtering documentation](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.filtfilt.html) (zero-phase filtering); Challis, [automatic cutoff-frequency selection for biomechanical data](https://pure.psu.edu/en/publications/a-procedure-for-the-automatic-determination-of-filter-cutoff-freq/) (residual/autocorrelation validation).

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

## 7. Precision Weight and Headshot Reference Constraint

Because real tournament data from the highest competitive level shows a practical Headshot Percentage maximum of approximately 30-33%, the related Precision component retains the lower positive weight of 0.15. SensCalibr8 does not measure literal Valorant Headshot Percentage in its spherical-target arena. Final Precision Error is the scoring input, renamed Precision Score after inversion and normalization, while Center-Hit Percentage is diagnostic only.

The 35% ceiling applies only if the application displays an external Valorant Headshot Percentage reference. It must not be applied to Center-Hit Percentage because the two measurements are not equivalent.

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
- Candidate offsets around the PSA baseline: -20%, -10%, -5%, 0%, +5%, +10%, +20%
- Candidate eDPI multipliers: 0.80, 0.90, 0.95, 1.00, 1.05, 1.10, 1.20
- Minimum sample size: 30 shots per sensitivity value for each shot-based mode
- Tracking uses a separate duration/trial-based sample contract to be calibrated and locked in Phase 0: Signal Calibration

The offset set is a project-owner decision that combines the seven-value Proposal V3.0 requirement with the +/-20%, +/-10%, and +/-5% progression documented by the 9-Week Rule in Section 15.

After exploratory scoring identifies the top two candidates, the system must collect fresh confirmatory matched blocks that were not used to rank those candidates. Each pair must use the same mode and controlled target sequence/condition block. Compare paired block-level Performance Scores using a two-sided paired randomization/permutation test at `alpha = 0.05`.

The confirmation report must include the paired effect estimate, a 95% confidence interval, the p-value, sample size, and analysis/configuration versions. If the difference is not statistically significant, the result is a statistical tie: do not force a Phase 1 Winner, and carry both candidates into Phase 2. Phase 0 must determine and freeze the confirmatory block/sample contract before protocol implementation.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Sections 7.2 and 8 (seven-value Phase 1, minimum 30 shots, and required significance gate); [Consolidated Research Report](reference/Valorant_Research_Report.md), Section 2, "The 9-Week Rule" (+/-20%, +/-10%, and +/-5% progression); project-owner decisions dated 2026-07-14 for the exact seven-value mapping, per-mode sample scope, and significance design; Ernst, [Permutation Methods: A Basis for Exact Inference](https://doi.org/10.1214/088342304000000396); [NIST paired signed-rank guidance](https://www.itl.nist.gov/div898/software/dataplot/refman1/auxillar/signrank.htm) (paired alternatives and typical 0.05 significance level); [ASA Statement on p-values](https://doi.org/10.1080/00031305.2016.1154108) (p-values do not measure effect size or practical importance).

### 11.2 Phase 2 — Progressive Narrowing

- Normal candidate range: Phase 1 Winner, Winner +10%, and Winner -10%
- Statistical-tie candidate range: the deduplicated union of each tied Phase 1 anchor at -10%, 0%, and +10%
- Minimum repetition: 5 complete Protocol Batteries per sensitivity value (5 Database Sessions per mode)
- Maximum repetition: 10 complete Protocol Batteries per sensitivity value (10 Database Sessions per mode)
- Stabilization threshold: Performance Score coefficient of variation below 10%

When Phase 1 produces tied anchors `A` and `B`, generate the Phase 2 set as follows:

```
raw_candidates = {
    A x 0.90, A x 1.00, A x 1.10,
    B x 0.90, B x 1.00, B x 1.10
}

floored_candidate = max(raw_candidate, 160)
phase_2_candidates = distinct(floored_candidate)
```

Generate without intermediate rounding. Apply the eDPI floor before deduplication, then deduplicate by the canonical stored eDPI value. The resulting set may contain fewer than six values. Every surviving candidate must retain all source records `(anchor_edpi, offset_percent, pre_floor_edpi, floor_applied)`; when multiple sources collapse to one final eDPI, none of their provenance records may be discarded.

For complete Protocol Batteries at one sensitivity value:

```
CV_percent = 100 x sample_SD(Performance Score) / abs(mean(Performance Score))
stabilized = CV_percent < 10
```

The calculation uses sample standard deviation. If the mean is zero or too close to zero for numerically stable division under the approved scoring precision, CV is undefined and the candidate is not stabilized. It must never pass by substituting zero, suppressing the error, or using raw SD units. The scoring-precision tolerance must be frozen in Phase 0 configuration.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 7.3 (Phase 2 — Progressive Narrowing); project-owner approvals dated 2026-07-14 for CV interpretation and statistical-tie candidate expansion; [NIST Coefficient of Variation](https://itl.nist.gov/div898/software/dataplot/refman2/auxillar/coefvari.htm).

### 11.3 Phase 3 — Final Narrowing

- Candidate range: Phase 2 Winner, Winner +5%, and Winner -5%

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 7.4 (Phase 3 — Final Narrowing).

### 11.4 Continuous Training Cycle

- Training block: 5-10 sessions at the current Best Sensitivity before evaluating Grade progression, fatigue, or plateau behavior

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 7.6 (Continuous Improvement Cycle).

---

## 12. Outlier Detection Threshold

A valid post-adaptation observation is flagged for a specific metric when:

```
abs(observed_value - group_mean) > 3 x sample_SD
```

Apply the adaptation cutoff first. Then construct each distribution separately within the homogeneous scope `(profile, cycle, phase, mode, sensitivity_value, metric_name)`; never pool profiles, modes, sensitivity values, or unlike metrics. Shot-based modes use valid shot-level metric observations. Tracking uses Phase 0-defined trial/window aggregate metrics rather than individual autocorrelated deviation samples.

The 3-SD rule is flag-first:

- Preserve every raw observation and store a metric-level audit record containing the value, group scope, group mean, sample SD, threshold, algorithm version, and disposition.
- The authoritative Winner calculation includes statistically flagged observations by default.
- Reports must show the inclusive aggregate and a sensitivity-analysis aggregate excluding statistically flagged observations.
- Exclusion from the authoritative Winner calculation is permitted only when a separate documented acquisition/data-quality error is confirmed. Statistical extremeness alone is not a data-quality error.
- Perform one documented detection pass against the complete eligible group; do not repeatedly remove a point and recompute until no flags remain.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 8 (3-SD Scientific Rigor requirement); project-owner approval dated 2026-07-14; [NIST Detection of Outliers](https://www.itl.nist.gov/div898/handbook/eda/section3/eda35h.htm) (investigate and retain potential outliers rather than automatically deleting them).

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

This source supports the progressive use of +/-20%, +/-10%, and +/-5%. By project-owner decision dated 2026-07-14, those symmetric offsets plus the PSA baseline define the exact seven Phase 1 candidates documented in Section 11.1.

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

## 17. Approved Operational Definitions

The following definitions are project-owner engineering and product decisions dated 2026-07-14. They define project behavior where the two source reports did not prescribe a complete implementation.

### 17.1 Session and Protocol Battery

- Database Session: exactly one Test Mode at exactly one sensitivity value.
- Protocol Battery: the complete set of all four Test Modes at one sensitivity value.
- Every Database Session belongs to one Protocol Battery through `battery_id`.

### 17.2 Fatigue Detection

After applying the adaptation cutoff, split the remaining valid observations in one Database Session into chronological first and second halves. Compute Performance Score separately for both halves.

```
fatigue_drop_percentage =
    ((first_half_score - second_half_score) / first_half_score) x 100

IF fatigue_drop_percentage > 15:
    FLAG session_fatigue
```

A fatigue flag is informational. It must not exclude the session from Winner selection or alter its stored Performance Score.

### 17.3 Grade Combination

Assign separate Reaction Time and Consistency tiers, then use the worse of the two as the final Grade. Phase 0 must calibrate and freeze versioned Consistency-tier cutpoints on the normalized Consistency Score before Grade implementation; they must not be derived dynamically from the current comparison set.

### 17.4 Plateau Detection

A plateau occurs only when both conditions are true:

- Final Grade is unchanged for 3 consecutive cycles.
- The absolute Performance Score change between the first and third cycle is less than 5% relative to the first cycle's score.

When a plateau is detected, automatically begin a new Phase 1-3 recalibration using the current value as the new baseline.

### 17.5 Target Geometry Policy

Tests use fixed world geometry with locked camera FOV, camera configuration, and Target Frame Rate. Exact dimensions, speeds, durations, FOV, and frame-rate values must be established, validated, versioned, and added to this file during Phase 0: Signal Calibration before production Test Engine implementation.

### 17.6 Phase 1 Sample Contract

The 30-shot minimum applies separately to each shot-based Test Mode at each sensitivity value. Shots are not pooled across modes. Tracking uses a separate duration/trial-based contract established during Phase 0: Signal Calibration.

Source for Sections 17.1-17.6: project-owner decisions dated 2026-07-14, resolving OQ-002, OQ-006, OQ-009, OQ-013, OQ-014, OQ-015, and OQ-017. Values assigned to Phase 0 remain implementation-blocked until calibration is completed and versioned.

---

## Summary Table of Constants

| Constant | Value | Section |
|---|---|---|
| PSA Baseline eDPI | 280 | 2 |
| eDPI Floor | 160 | 2 |
| Performance Score weight — Consistency | 0.35 | 4 |
| Performance Score weight — Accuracy% | 0.30 | 4 |
| Performance Score weight — Reaction Speed | 0.20 | 4 |
| Performance Score weight — Precision Score | 0.15 | 4 |
| Performance Score weight — Submovement Penalty | 0.10 (subtracted) | 4 |
| Normalized component range | 0.0-1.0 | 4.2 |
| Stored/displayed Performance Score multiplier | 100 | 4 |
| Submovement Butterworth order | 5 | 5 |
| Submovement cutoff frequency | 7 Hz | 5 |
| Submovement start threshold | 8 deg/s | 5 |
| Submovement end threshold | 4 deg/s | 5 |
| Submovement refractory period | 80 ms | 5 |
| Adaptation discard proportion | 50% of shots per value | 8 |
| Headshot% target threshold ceiling | 35% | 7 |
| Reaction Time Tier S | `t < 200 ms` | 6 |
| Reaction Time Tier A | `200 <= t < 250 ms` | 6 |
| Reaction Time Tier B | `250 <= t < 350 ms` | 6 |
| Reaction Time Tier C | `350 <= t <= 500 ms` | 6 |
| Reaction Time Tier D | `t > 500 ms` | 6 |
| Phase 1 sensitivity values | 7 | 11.1 |
| Phase 1 minimum sample | 30 shots per value per shot-based mode | 11.1 |
| Phase 2 narrowing range | +/- 10% | 11.2 |
| Phase 2 tie candidate maximum before deduplication | 6 | 11.2 |
| Phase 2 minimum repetition | 5 complete batteries per value | 11.2 |
| Phase 2 maximum repetition | 10 complete batteries per value | 11.2 |
| Phase 1 significance level | two-sided `alpha = 0.05` | 11.1 |
| Phase 1 confidence interval | 95% | 11.1 |
| Phase 2 stabilization threshold | Performance Score CV < 10% | 11.2 |
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
| Phase 1 candidate offsets | 0%, +/-5%, +/-10%, +/-20% | 11.1 |
| Tracking weight — Consistency | 0.4375 | 4.1 |
| Tracking weight — Time-on-Target | 0.3750 | 4.1 |
| Tracking weight — Precision Score | 0.1875 | 4.1 |
| Fatigue flag threshold | Performance Score drop > 15% | 17.2 |
| Plateau Grade window | 3 consecutive cycles | 17.4 |
| Plateau Performance Score change | < 5% | 17.4 |
