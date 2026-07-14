# RESEARCH.md

## Purpose

This file is the single source of truth for every formula, threshold, constant, and benchmark used anywhere in SensCalibr8. If a number is not defined here, it must not be used in code. Do not modify any value in this file without updating the source citation alongside it.

---

## 1. Effective DPI (eDPI)

```
eDPI = In-game Sensitivity x Hardware DPI
```

eDPI is the normalized unit used for all cross-user and cross-profile comparisons, since raw in-game sensitivity values are meaningless without accounting for hardware DPI differences.

Source: Consolidated Research Report — "A Comprehensive Analysis of Optimal Technical Configurations and Mechanical Performance Parameters in Valorant" (internal synthesis based on TenZ, CHARLATAN, Rem, and Konpeki settings breakdowns).

---

## 2. PSA Method (Perfect Sensitivity Approximation)

```
Starting Sensitivity = 280 / Hardware DPI
eDPI Floor (hard minimum) = 160
```

Worked example: DPI = 1600 -> Starting Sensitivity = 280 / 1600 = 0.175

The constant 280 is the PSA baseline eDPI value. This value must never be changed without a peer-reviewed or otherwise rigorously verifiable academic source. Forum posts, community blogs, or unverified "pro player data" aggregations are not sufficient justification for changing this constant.

The eDPI Floor of 160 is a hard minimum. Any calculated eDPI below 160 must be auto-adjusted upward to 160, with a warning shown to the user (see `RULES.md`, Input Validation Rules).

Source: Consolidated Research Report, synthesized and fact-checked against four expert player configuration breakdowns.

---

## 3. Physical Movement Distance (cm/360)

```
cm/360 = (2.54 x 360) / (DPI x Sensitivity x 0.0022)
```

This metric expresses the physical hand-travel distance in centimeters required to complete a full 360-degree turn. It is used alongside eDPI to account for ergonomic constraints, such as available mousepad space, and is linked to the Grip Tension concept referenced in the source research.

Source: Consolidated Research Report.

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

This score determines the "Winner" of each phase in the testing protocol. Weighting was deliberately calibrated as follows:

- Consistency and Accuracy carry the highest weight (0.35 and 0.30) because they are the most direct indicators of sensitivity suitability.
- Reaction Speed carries moderate weight (0.20).
- Headshot%/Precision carries the lowest positive weight (0.15) because real tournament data (VCT Masters Reykjavik) shows that even the player with the highest headshot percentage in that tournament (ScreaM) achieved only 33.12%. A metric with such a low practical ceiling should not be weighted heavily.
- Submovement Penalty (0.10) is subtracted. A higher submovement count during a single aiming action indicates the sensitivity value required excessive micro-correction, which is a negative signal for that sensitivity value. See Section 5 below for how Submovement Count is calculated.

Every stored Performance Score result must be tagged with a `formula_version` value (see `ARCHITECTURE.md`, `sensitivity_tests` table) so that historical results remain comparable even if formula weights are revised in the future.

Source: Consolidated Research Report; VCT Masters Reykjavik 2022 official tournament statistics (Riot Games).

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

Source: Boudaoud, Spjut, and Kim, "Analyzing Mouse Sensitivity Preferences in First-Person Aiming Tasks," IEEE Conference on Games (CoG) 2022. The relationship between submovement count and sensitivity was confirmed as monotonic in that study; the discard proportion (50%) is drawn directly from that paper's methodology ("we discard the first 50% of trials from each session as an adaptation period"), scaled to this project's smaller per-value sample size (30+ shots here vs. 500 trials per session in the original study).

---

## 6. Reaction Time Benchmark Tiers

| Tier | Reaction Time Range | Classification |
|---|---|---|
| S | < 200 ms | Professional / Pro-level |
| A | 200-250 ms | Above Average |
| B | 250-350 ms | Average Gamer |
| C | 350-500 ms | Below Average |
| D | > 500 ms | Needs Improvement |

Benchmark values are consolidated from multiple external sources referencing average human visual reaction time (approximately 250 ms), average FPS player range (300-500 ms), and professional esports player range (100-250 ms, or 150-180 ms depending on source).

Source: Consolidated Research Report; general human factors literature on visual-motor reaction time.

---

## 7. Headshot % Usage Constraint

Because real tournament data from the highest competitive level shows a practical maximum of approximately 30-33% (see Performance Score section above), Headshot%/Precision must only ever be used as a secondary indicator (weight 0.15 in the Performance Score formula). The user-facing Target Threshold for headshot percentage must never be set above 35%, since anything higher is not supported by empirical competitive data and would mislead the user about a realistic target.

Source: VCT Masters Reykjavik 2022 official tournament statistics (Riot Games).

---

## 8. Adaptation Period

For every tested sensitivity value, the system must discard the first 50% of recorded shots for that value before computing any aggregate metric (Performance Score, Submovement Count average, Accuracy%, etc.). This is not a fixed shot count — it scales with the actual sample size tested for that value.

```
adaptation_cutoff = floor(total_shots_per_value x 0.5)
valid_shots = shots[adaptation_cutoff:]
```

Worked example: if 30 shots are recorded for a given sensitivity value, the first 15 shots are discarded and only the remaining 15 are used in calculations.

Source: Boudaoud, Spjut, and Kim, IEEE CoG 2022.

---

## 9. Mousepad Constraint Validation

```
IF cm_360 > mousepad_width_cm:
    TRIGGER warning_flag("mousepad_constraint_violation")
```

If the physical distance required for a full 360-degree turn (cm/360, see Section 3) exceeds the user's stated mousepad width, the system must raise a warning. This does not block the sensitivity value from being tested, but flags a likely ergonomic issue (frequent mouse lifting mid-turn).

Source: Derived directly from the cm/360 formula in Section 3, combined with user-provided mousepad dimensions (see `ARCHITECTURE.md`, `profiles` table).

---

## 10. Fitts's Law — Index of Difficulty

```
Index of Difficulty (ID) = log2(2 x Distance / Target Width)
```

All test modes must randomize target size (Small / Medium / Large) together with distance, not distance alone, so that the Index of Difficulty varies according to established motor-control theory rather than distance alone.

Source: Fitts, P. M. (1954). "The Information Capacity of the Human Motor System in Controlling the Amplitude of Movement." Journal of Experimental Psychology.

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
| Reaction Time Tier S | < 200 ms | 6 |
| Reaction Time Tier A | 200-250 ms | 6 |
| Reaction Time Tier B | 250-350 ms | 6 |
| Reaction Time Tier C | 350-500 ms | 6 |
| Reaction Time Tier D | > 500 ms | 6 |
