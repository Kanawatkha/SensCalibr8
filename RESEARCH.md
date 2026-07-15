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

### 4.4 Accepted Scoring and Normalization Contract

P0-R6 accepted combined contract `sc8-p0-r6-scoring-statistics-contract-v1`, formula version `sc8-performance-score-v1`, normalization version `sc8-normalization-v1`, Consistency-tier version `sc8-consistency-tier-v1`, and confirmatory version `sc8-confirmatory-v1`.

The fixed normalization bounds are:

| Mode | Accuracy / Time-on-Target | Reaction component | Precision / Deviation | Consistency sample-SD bound |
|---|---|---|---|---|
| Close Flick | 0-100% | Reaction Time 100-500 ms | Final Precision Error 0-15 deg | 0-7.745966692414833 deg |
| Far Flick | 0-100% | Travel Time 0-1500 ms | Final Precision Error 0-40 deg | 0-20.655911179772886 deg |
| Micro-Correction | 0-100% | Correction Time 0-1500 ms | Final Precision Error 0-1.500295901168436 deg | 0-0.7747494719478134 deg |
| Tracking | 0-100% | Omitted | Tracking Deviation 0-15 deg RMS | 0-7.570424080242598 deg |

Close/Far precision anchors are the maximum accepted target-center offsets. Micro's upper anchor is the angular projection of the accepted 20 px maximum offset. Tracking uses the accepted 15-degree path amplitude. These are fixed task-span anchors, not population percentiles or raw-data validity ceilings. Values beyond an anchor remain unchanged in raw storage and clamp only at normalization.

The Consistency upper anchor for `n` observations over a metric scale `[0,U]` is:

```
U x sqrt(floor(n/2) x ceil(n/2) / (n x (n - 1)))
```

Use `n=15` for each shot mode and `n=54` for Tracking. Do not clip raw error values before computing sample SD; an SD above its anchor receives zero utility through the normal clamp.

Shot-mode aggregation uses all 15 authoritative resolved opportunities for Accuracy, mean Final Precision Error, and Final Precision Error sample SD. Close uses mean target-visible-to-resolution time; Far uses mean movement-onset-to-click Travel Time; Micro uses mean reference-activation-to-resolution Correction Time. A timeout contributes the accepted 1500 ms ceiling. Far missing-onset raw Travel Time remains null but contributes 1500 ms to the scoring aggregate rather than a fabricated zero.

Submovement Count averages authoritative hits only, with bounds `L=1`, `U=6` for Close, Far, and Micro. A count at or below 1 maps to zero penalty and a count at or above 6 maps to one. If a mode has zero authoritative hits, raw Submovement Count remains null and the scoring component fails closed at penalty `1.0`; this is not stored as a fabricated raw count. Every authoritative hit must still be signal-eligible under P0-R5. Figure 8 of the IEEE CoG 2022 paper analyzes completion time across counts 1-6 and reports the positive count/time relationship; values above 6 remain valid raw observations and merely clamp at the scale maximum.

Tracking averages the 54 equal one-second Time-on-Target percentages and 54 RMS deviations; sample SD of the same deviations supplies Consistency. A Protocol Battery score is the unweighted arithmetic mean of four complete mode scores. The source shot formula is not clamped after weighting: its theoretical range remains -10 to 100, while Tracking ranges 0 to 100. The scoring-zero tolerance is `1e-9` score points; `abs(mean score) <= 1e-9` makes CV undefined and cannot pass stabilization.

Worked examples:

```
Shot = 100 x (0.8x0.35 + 0.9x0.30 + 0.75x0.20 + 0.6x0.15 - 0.2x0.10) = 77.0
Tracking = 100 x (0.8x0.4375 + 0.9x0.375 + 0.7x0.1875) = 81.875
```

Source: Phase 0 acceptance dated 2026-07-15; [P0-R6 accepted contract](calibration/plans/p0-r6-scoring-statistics-accepted-v1.json); [P0-R6 protocol](calibration/P0_R6_SCORING_STATISTICS_PROTOCOL.md); [Boudaoud, Spjut, and Kim, IEEE CoG 2022](https://ieee-cog.org/2022/assets/papers/paper_64.pdf), Section IV-D and Figure 8.

Source for the base formula and weights: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Section 6.4; VCT Masters Reykjavik 2022 official tournament statistics (Riot Games), as cited by the proposal.

---

## 5. Submovement Count Algorithm

Submovement count measures how many distinct corrective mouse movements occur during a single aiming action. A lower count indicates a more efficient, "one-shot" aim adjustment; a higher count indicates repeated overshoot/undershoot corrections, which this project treats as evidence that the tested sensitivity is not well suited to the user.

Approved detection pipeline:

1. Capture every raw mouse delta with a high-resolution timestamp; do not use the Unity render frame rate as the input sampling rate.
2. Convert cumulative deltas into angular azimuth/elevation in degrees using the tested sensitivity and input calibration before filtering.
3. Measure the actual input-event cadence for the session, validate timestamp integrity and the modal-cadence policy under the versioned timing contract, and resample the angular trace onto the canonical uniform processing grid frozen in Phase 0. Duplicate/reversed timestamps or loss of the nominal modal cadence invalidate Submovement analysis; receipt bursts and detected gaps remain diagnostics, with gaps splitting the trace rather than being interpolated across.
4. Apply a fifth-order, 7 Hz low-pass Butterworth filter represented as second-order sections (SOS), using forward-backward offline filtering to avoid phase displacement of movement boundaries.
5. Calculate angular velocity from the filtered angular trace.
6. Start a submovement when angular velocity crosses 8 degrees/second and end it when velocity falls below 4 degrees/second.
7. Enforce an 80 ms refractory period between counted submovements.

P0-R3 freezes the canonical processing grid at **1000 Hz** under timing version `sc8-signal-pipeline-timing-v1`. The nominal interval is 1 ms and the resampling tolerance is **0.5 ms**, exactly the midpoint partition between adjacent cadence classes. P0-R5 verified the generated odd fifth-order SOS cascade: it has three stable sections, default pad length **18 samples**, and therefore requires at least **20 samples** per gap-delimited segment.

This timing contract was closed by an explicit project-owner calibration waiver after five Pilot v3 runs and two independent five-run confirmation sets. Neither strict distribution candidate passed, and the project does not represent them as statistical passes. The accepted operational policy instead requires strictly increasing timestamps, a median interval that maps to one nominal cadence, and the one-cadence class to be modal. Receipt bursts and gaps are preserved as diagnostics; gaps split resampling segments and are never bridged. Production records user-entered configured polling rate but measures actual cadence per session, so 1000 Hz is a processing grid rather than a claim that every mouse physically reports at that rate. Hardware DPI and current in-game sensitivity remain required validated user inputs. Evidence and limitations: `calibration/evidence/p0-r3/p0-r3-owner-waiver-closure.json` and `calibration/plans/p0-r3-timing-contract-accepted-v1.json`.

Mouse manufacturer/model/firmware remain optional audit metadata. P0-R5 accepted signal version `sc8-signal-pipeline-v1`. Its single-pass gain is 1.0 at DC and 0.7071067811866065 at 7 Hz; forward-backward application produces gain 0.5000000000000835 at 7 Hz and zero measured impulse-peak displacement. Filter azimuth/elevation separately with odd extension, then calculate first-difference Euclidean angular-velocity magnitude. A start occurs at `velocity >= 8 deg/s`; an end occurs at `velocity < 4 deg/s`. Merge a following onset only when it occurs less than 80 ms after the preceding end; exactly 80 ms starts a separate Submovement. Do not add an undocumented minimum-duration threshold.

The accepted immutable signal/mode contract is [`calibration/plans/p0-r5-signal-mode-accepted-v1.json`](calibration/plans/p0-r5-signal-mode-accepted-v1.json), with derived evidence at [`calibration/evidence/p0-r5/p0-r5-signal-mode-derived-v1.json`](calibration/evidence/p0-r5/p0-r5-signal-mode-derived-v1.json). The canonical coefficients live in that contract and must be loaded from centralized configuration rather than duplicated in calculation logic.

```
adaptation_cutoff = floor(total_shots_per_value x 0.5)
valid_shots = shots[adaptation_cutoff:]  # only these shots contribute to Performance Score
```

This same adaptation cutoff logic (discarding the first 50% of shots per tested value) also applies to Submovement Count aggregation, not only to Performance Score — see Section 8 below.

Source: project-owner approvals dated 2026-07-14 and 2026-07-15; [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Sections 6.4 and 7.2; [Boudaoud, Spjut, and Kim, IEEE CoG 2022](https://ieee-cog.org/2022/assets/papers/paper_64.pdf) (fifth-order 7 Hz Butterworth filter, 8/4 degrees-per-second thresholds, 80 ms refractory period, and 50% adaptation discard); [SciPy Butterworth documentation](https://docs.scipy.org/doc/scipy-1.13.1/reference/generated/scipy.signal.butter.html) (SOS numerical-stability guidance); [SciPy forward-backward filtering documentation](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.filtfilt.html) (zero-phase filtering); Challis, [automatic cutoff-frequency selection for biomechanical data](https://pure.psu.edu/en/publications/a-procedure-for-the-automatic-determination-of-filter-cutoff-freq/) (residual/autocorrelation validation).

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

After exploratory scoring identifies the top two candidates, collect exactly 10 fresh matched pairs that were not used for ranking. One pair is two complete four-mode Protocol Batteries, one per candidate, with matching mode order and condition seeds. Use five A-then-B and five B-then-A pairs under the deterministic `sc8-confirmatory-v1` sequence. Interrupted/incomplete evidence is retained but does not count; repeat the same pair index. Zero differences remain in analysis, exploratory reuse and early stopping are forbidden, and the contract makes no guaranteed-power claim for a particular effect.

Enumerate all `2^10 = 1024` within-pair sign flips of the mean A-minus-B battery-score difference. The two-sided p-value is the fraction with absolute permuted mean greater than or equal to the absolute observed mean, using the accepted `1e-12` score-point guard only for this inclusive floating-point comparison. Exhaustive enumeration includes the observed assignment, so no Monte Carlo correction is added. The minimum attainable two-sided p-value is `0.001953125`. A p-value strictly below `0.05` selects the candidate indicated by the effect sign; otherwise the result is a statistical tie and both candidates continue to Phase 2.

Report the paired mean effect and a two-sided 95% Student-t interval over the 10 paired differences using `t(0.975,9) = 2.2621571628540993`, together with p-value, sample size, and all analysis/configuration versions. The interval is reported effect uncertainty, not a second Winner gate; the exact randomization p-value controls the decision.

Source: [SensCalibr8 Project Proposal V3.0](reference/SensCalibr8_Project_Proposal_v3.md), Sections 7.2 and 8 (seven-value Phase 1, minimum 30 shots, and required significance gate); [Consolidated Research Report](reference/Valorant_Research_Report.md), Section 2, "The 9-Week Rule" (+/-20%, +/-10%, and +/-5% progression); project-owner decisions dated 2026-07-14 for the exact seven-value mapping, per-mode sample scope, and significance design; P0-R6 acceptance dated 2026-07-15; Ernst, [Permutation Methods: A Basis for Exact Inference](https://doi.org/10.1214/088342304000000396); [NIST confidence interval guidance](https://www.itl.nist.gov/div898/handbook/prc/section2/prc221.htm); [NIST Student-t critical values](https://www.itl.nist.gov/div898/handbook/eda/section3/eda3672.htm); [ASA Statement on p-values](https://doi.org/10.1080/00031305.2016.1154108) (p-values do not measure effect size or practical importance).

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

Assign separate Reaction Time and Consistency tiers, then use the worse of the two as the final Grade. Under `sc8-consistency-tier-v1`, average the four complete mode-level normalized Consistency utilities in one Protocol Battery and assign S `[0.8,1]`, A `[0.6,0.8)`, B `[0.4,0.6)`, C `[0.2,0.4)`, or D `[0,0.2)`. These fixed equal-width utility bands are an engineering interpretation scale, not current-user percentiles. Reaction Grade uses the authoritative Close Flick mean Reaction Time because this is the visual-response interval matched to the Section 6 benchmark; Far's primary speed metric is Travel Time, Micro's is Correction Time, and Tracking has no reaction event.

### 17.4 Plateau Detection

A plateau occurs only when both conditions are true:

- Final Grade is unchanged for 3 consecutive cycles.
- The absolute Performance Score change between the first and third cycle is less than 5% relative to the first cycle's score.

When a plateau is detected, automatically begin a new Phase 1-3 recalibration using the current value as the new baseline.

### 17.5 Target Geometry Policy

Tests use fixed world geometry under accepted geometry version `sc8-test-geometry-v1`. The reference test viewport is 1920 x 1080 (16:9); other aspect ratios use a fixed 16:9 letterboxed viewport so FOV and target appearance do not change. The perspective camera is fixed at 103 degrees horizontal FOV (70.53280043291679 degrees vertical at 16:9), position `[0, 1.6, 0]`, zero rotation, and a target plane 10 world units in front of the camera. The arena is an enclosed 20 x 12 x 21 world-unit unlit checkerboard room with no shadows.

Small, Medium, and Large cyan spheres have angular diameters of 0.75, 1.50, and 2.25 degrees. At the reference viewport and target plane, their world diameters are 0.13090156304068037, 0.26181434169670176, and 0.3927495554275395, projecting to approximately 10, 20, and 30 pixels. Close Flick uses 5, 10, and 15 degree center offsets; Far Flick uses 20, 30, and 40 degree center offsets. Each family uses the complete 3 x 3 cross-product of distance and target size. Vertical target centers are limited to 25 degrees.

The Center-Hit diagnostic uses a radius equal to 50% of the target radius, or 25% of projected target area. The crosshair is a fixed four-pixel filled dot; only its color is user-configurable. Micro-Correction preserves the source-authorized 5-20 pixel center offset. The reference frame policy is 144 Hz, `vSyncCount = 0`, with adaptive sync disabled for acceptance-bearing sessions. A 32-pixel edge margin and 64-pixel top HUD reserve keep complete targets within the safe viewport.

P0-R4 acceptance combined deterministic projection/Fitts/containment gates, Unity EditMode projection parity, repeatability verification, and inspection of a 1920 x 1080 reference render. The accepted immutable contract is [`calibration/plans/p0-r4-geometry-accepted-v1.json`](calibration/plans/p0-r4-geometry-accepted-v1.json); derived evidence is [`calibration/evidence/p0-r4/p0-r4-geometry-derived-v1.json`](calibration/evidence/p0-r4/p0-r4-geometry-derived-v1.json). Spawn timing, Tracking speed/duration, and signal-response behavior remain P0-R5 responsibilities and are not defined by this geometry contract.

### 17.6 Phase 1 Sample Contract

The 30-shot minimum applies separately to each shot-based Test Mode at each sensitivity value. Shots are not pooled across modes. Under `sc8-mode-contract-v1`, a minimum session resolves 30 opportunities, the first 15 are adaptation, and the final 15 are authoritative. A hit, miss-click, or 1.5-second timeout resolves an opportunity; timeout is a miss. A post-adaptation miss stores null Submovement Count with its outcome reason and must never be converted into a favorable zero penalty.

Source for Sections 17.1-17.6: project-owner decisions dated 2026-07-14, resolving OQ-002, OQ-006, OQ-009, OQ-013, OQ-014, OQ-015, and OQ-017. Geometry, mode/sample, scoring, and statistical values are accepted under the P0-R4/P0-R5/P0-R6 contracts. Production use remains blocked until P0-R7 compiles and freezes complete Calibration Configuration v1.

### 17.7 Accepted Mode and Tracking Contract

Mode contract `sc8-mode-contract-v1` uses a deterministic versioned sequence that excludes sensitivity value and blind candidate label. Compared sensitivities therefore receive the same condition sequence. Close/Far sessions use three complete 3 x 3 distance-by-size blocks plus three rotating conditions, so each of nine conditions occurs three or four times in 30 trials and the extra conditions rotate by battery repetition.

- Close Flick: hide the target for a deterministic randomized 500-1000 ms foreperiod, then measure Reaction Time from visibility to click or timeout.
- Far Flick: center-reference activation exposes the previewed Far target. Reaction Time is activation to first 8 deg/s movement onset; Travel Time is onset to click.
- Micro-Correction: use deterministic radial/directional offsets within 5-20 px and the fixed Small target from `sc8-test-geometry-v1`. This is the explicit exception to generic three-size variation.
- Tracking: run two balanced blocks. Each block contains all three patterns crossed with Small/Medium/Large once (9 trials). Block one is adaptation and block two is authoritative. Each trial is 6 seconds with six exact 1-second windows, yielding 18 trials and 54 post-adaptation windows per Tracking session.
- Linear Tracking: horizontal triangle path over +/-15 degrees at 15 deg/s; derived round-trip period 4 seconds.
- Curved Tracking: ellipse with 15-degree horizontal and 10-degree vertical radii; 6-second period and derived speed range 10.471975511965978-15.707963267948966 deg/s.
- Variable-Speed Tracking: horizontal 15-degree-amplitude sinusoid with 4-second period; derived speed range 0-23.561944901923447 deg/s.

Evaluate every Tracking path analytically from high-resolution elapsed time, never by frame-integrating position. Time-on-Target is interval-weighted duration with radial error no greater than target radius. Per-window Tracking Deviation is interval-weighted RMS radial center error in degrees. Tracking Consistency is the sample SD across the 54 post-adaptation window deviations. Window intervals are clipped to exact boundaries so equivalent irregular frame partitions produce identical metrics.

The 1.5-second failed-trial ceiling and reference-target separation follow [Boudaoud, Spjut, and Kim, IEEE CoG 2022](https://ieee-cog.org/2022/assets/papers/paper_64.pdf), Section III-C. Foreperiod and Tracking path/sample values are P0-R5 engineering calibration outputs accepted through balance, geometry-safety, interval-invariance, deterministic-reproduction, and Unity-parity gates; they are not attributed to the two primary project reports.

### 17.8 Accepted Scoring and Statistical Contract

The complete P0-R6 payload and acceptance evidence are versioned in [`calibration/plans/p0-r6-scoring-statistics-accepted-v1.json`](calibration/plans/p0-r6-scoring-statistics-accepted-v1.json) and [`calibration/P0_R6_SCORING_STATISTICS_PROTOCOL.md`](calibration/P0_R6_SCORING_STATISTICS_PROTOCOL.md). Production must load the accepted payload by its pinned SHA-256, require all four version IDs, and reject draft, incomplete, or modified configuration. Section 4.4 defines scoring aggregation/bounds and Section 11.1 defines the confirmatory decision.

---

## 18. Frozen Calibration Configuration v1

Phase 0 is complete under immutable configuration `calibration_config_v1` and contract `sc8-calibration-config-v1`. The authoritative file is [`calibration/plans/calibration-config-v1.json`](calibration/plans/calibration-config-v1.json), pinned by [`calibration/plans/p0-r7-calibration-config-accepted-v1.json`](calibration/plans/p0-r7-calibration-config-accepted-v1.json) at SHA-256 `c618a3e50473b072b107d2e2926f4d05e7bbafa33bc04af8beb5eb5f775b3b2e`.

The configuration projects all 20 required non-ID `calibration_configs` fields and binds formula `sc8-performance-score-v1`, normalization `sc8-normalization-v1`, signal pipeline `sc8-signal-pipeline-v1`, geometry `sc8-test-geometry-v1`, mode contract `sc8-mode-contract-v1`, Consistency tiers `sc8-consistency-tier-v1`, and confirmatory contract `sc8-confirmatory-v1`. Its six JSON database fields are canonical serialized copies of the accepted Phase 0 contracts and may not be edited independently.

The timing portion remains an operational project-owner waiver: `strict_timing_confirmation_passed = false`, both strict candidates remain rejected, and 1000 Hz is the uniform processing grid/configured metadata for the current host rather than a universal physical delivery claim. Actual event cadence remains measured and stored per session.

Any change to the frozen bytes, a source contract, a formula/version identity, or an embedded payload requires a new configuration version and a new acceptance envelope. Production code must reject draft, incomplete, hash-mismatched, or internally inconsistent configurations.

Source: P0-R3 through P0-R6 accepted local contracts and the P0-R7 deterministic freeze/acceptance record dated 2026-07-15. See [`calibration/P0_R7_CALIBRATION_CONFIG_FREEZE.md`](calibration/P0_R7_CALIBRATION_CONFIG_FREEZE.md).

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
| Formula / normalization versions | `sc8-performance-score-v1` / `sc8-normalization-v1` | 4.4 |
| Shot / Tracking theoretical score range | -10 to 100 / 0 to 100 | 4.4 |
| Scoring-zero tolerance | 1e-9 score points | 4.4 |
| Submovement Count bounds | 1-6 (Close/Far/Micro) | 4.4 |
| Consistency utility tiers | S >=0.8; A >=0.6; B >=0.4; C >=0.2; D <0.2 | 17.3 |
| Submovement Butterworth order | 5 | 5 |
| Submovement cutoff frequency | 7 Hz | 5 |
| Submovement start threshold | 8 deg/s | 5 |
| Submovement end threshold | 4 deg/s | 5 |
| Submovement refractory period | 80 ms | 5 |
| Canonical input processing grid | 1000 Hz | 5 |
| Single-cadence resampling tolerance | 0.5 ms | 5 |
| Fifth-order SOS structural pad length | 18 samples (20-sample minimum segment) | 5 |
| Timing acceptance policy | Strict timestamp integrity + nominal modal cadence; burst/gap diagnostics retained | 5 |
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
| Confirmatory sample / enumeration | 10 fresh pairs / 1024 sign flips | 11.1 |
| Confirmatory order balance | 5 A-first / 5 B-first | 11.1 |
| Minimum attainable confirmatory p | 0.001953125 | 11.1 |
| Permutation comparison tolerance | 1e-12 score points | 11.1 |
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
| Test geometry version | `sc8-test-geometry-v1` | 17.5 |
| Reference test viewport | 1920 x 1080 (fixed 16:9; letterbox otherwise) | 17.5 |
| Horizontal / vertical FOV | 103 deg / 70.53280043291679 deg at 16:9 | 17.5 |
| Target plane distance | 10 world units | 17.5 |
| Arena dimensions | 20 x 12 x 21 world units | 17.5 |
| Target angular diameters | 0.75 / 1.50 / 2.25 deg | 17.5 |
| Close / Far center offsets | 5/10/15 deg / 20/30/40 deg | 17.5 |
| Center-Hit radius ratio | 0.5 (area ratio 0.25) | 17.5 |
| Fixed crosshair diameter | 4 px | 17.5 |
| Reference Target Frame Rate | 144 Hz | 17.5 |
| Safe viewport exclusions | 32 px edge; 64 px top HUD reserve | 17.5 |
| Accepted signal pipeline | `sc8-signal-pipeline-v1` | 5 |
| Accepted mode contract | `sc8-mode-contract-v1` | 17.7 |
| Shot opportunity timeout | 1.5 s | 17.6 |
| Close Flick foreperiod | 500-1000 ms | 17.7 |
| Tracking trial contract | 2 x 9 trials; first block adaptation; 6 s/trial | 17.7 |
| Tracking metric window | 1 s; 54 post-adaptation windows/session | 17.7 |
| Linear Tracking path | +/-15 deg at 15 deg/s; 4 s round trip | 17.7 |
| Curved Tracking path | radii 15/10 deg; 6 s period | 17.7 |
| Variable-Speed Tracking path | 15 deg amplitude; 4 s period | 17.7 |
| Frozen calibration configuration | `calibration_config_v1` / `sc8-calibration-config-v1` | 18 |
