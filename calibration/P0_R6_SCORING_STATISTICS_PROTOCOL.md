# P0-R6 Scoring and Statistical Calibration

## Document Control

| Field | Value |
|---|---|
| Owning round | `P0-R6` |
| Protocol authority | `sc8-p0-r1-protocol-v1` |
| Geometry dependency | `sc8-test-geometry-v1` |
| Signal/mode dependency | `sc8-p0-r5-signal-mode-contract-v1` |
| Candidate | `sc8-p0-r6-scoring-statistics-candidate-v1` |
| Accepted contract | `sc8-p0-r6-scoring-statistics-contract-v1` |
| Operator testing | None; deterministic derivation and parity fixtures only |
| Production authorization | Calibration-only; Phase 0 remains blocked until P0-R7 |

## 1. Objective and Scope

P0-R6 freezes every scoring/statistical field that remained undefined after P0-R5: fixed normalization bounds, Submovement penalty bounds, component aggregation, Consistency tiers, scoring-zero tolerance, four-mode battery aggregation, and the fresh paired confirmation contract. It does not implement the production Test Engine, protocol controller, database, or Winner UI.

The candidate is acceptable only if every value derives from an accepted project contract, an inherent mathematical bound, a cited source, or an explicitly documented engineering scale choice. No bound may be learned from the current user or comparison set.

## 2. Fixed Score and Aggregation Contract

The Proposal V3.0 shot-mode weights and the previously approved Tracking redistribution remain unchanged. All component values are normalized to `[0,1]`. The shot formula is not clamped after weighting, so its theoretical range is `-10` to `100`; Tracking ranges from `0` to `100`. Adding a final clamp would silently change the source formula and is therefore prohibited under `sc8-performance-score-v1`.

One mode score is produced only from a complete authoritative observation set. A Protocol Battery score is the unweighted arithmetic mean of its four complete mode scores. This preserves the Proposal V3.0 requirement to retain each mode separately and avoids introducing unsupported mode weights.

For the 15 post-adaptation opportunities in each shot mode:

- Accuracy is `100 x hits / 15`; miss-clicks and timeouts are misses.
- Precision uses mean Final Precision Error across all 15 resolved opportunities.
- Consistency is sample SD across the same 15 Final Precision Error values.
- Close Reaction Speed uses mean target-visible-to-resolution time.
- Far Reaction Speed uses mean movement-onset-to-click Travel Time. A missing onset retains null raw Travel Time but contributes the 1.5-second worst scoring anchor; it is never replaced with zero.
- Micro-Correction Reaction Speed uses mean reference-activation-to-resolution Correction Time.
- Submovement Count averages authoritative hits only. Every hit must be signal-eligible under P0-R5. If there are zero hits, raw count remains null and the component penalty is `1.0` as an explicit fail-closed fallback, preventing missing data from becoming a favorable zero.

Tracking uses the arithmetic mean of 54 equal one-second Time-on-Target percentages, the mean of 54 window RMS deviations for Precision, and sample SD of those deviations for Consistency.

## 3. Normalization Bounds

| Mode / metric | L | U | Basis |
|---|---:|---:|---|
| Accuracy / Time-on-Target (%) | 0 | 100 | Inherent percentage bounds |
| Close Reaction Time (ms) | 100 | 500 | Proposal V3.0 Reaction benchmark anchors; slower values clamp to zero utility |
| Far Travel Time (ms) | 0 | 1500 | P0-R5 accepted trial ceiling |
| Micro Correction Time (ms) | 0 | 1500 | P0-R5 accepted trial ceiling |
| Close Final Precision Error (deg) | 0 | 15 | Maximum accepted Close center offset; no-movement task-span anchor |
| Far Final Precision Error (deg) | 0 | 40 | Maximum accepted Far center offset; no-movement task-span anchor |
| Micro Final Precision Error (deg) | 0 | 1.500295901168436 | Angular projection of accepted 20 px maximum offset |
| Tracking Deviation (deg RMS) | 0 | 15 | Maximum accepted Tracking path amplitude |

The error/deviation upper values are scale anchors, not hard validity ceilings. Raw observations remain unchanged; min-max utility clamping alone handles values outside the scale.

Consistency uses sample SD. For `n` values scaled over `[0,U]`, the largest two-point sample SD is:

```
U x sqrt(floor(n/2) x ceil(n/2) / (n x (n - 1)))
```

Applying `n=15` to shot modes and `n=54` to Tracking yields fixed upper anchors `7.745966692414833`, `20.655911179772886`, `0.7747494719478134`, and `7.570424080242598` degrees respectively. Raw SD is not Winsorized before normalization; an SD above its anchor simply receives zero Consistency utility.

The scoring-zero tolerance is `1e-9` score points. It is solely a floating-point guard for CV division/equality, many orders below any displayed or operational score difference; `abs(mean score) <= 1e-9` makes CV undefined and cannot pass stabilization.

## 4. Submovement Penalty and Grade

Close, Far, and Micro use `L=1`, `U=6` in the approved capped-linear penalty. Figure 8 of Boudaoud, Spjut, and Kim plots completion-time behavior for Submovement Counts 1 through 6 and shows increasing count associated with longer completion time. Counts above 6 remain valid raw observations and clamp to penalty `1`; a count of 0 or 1 clamps to `0`. Source: [Mouse Sensitivity in First-person Targeting Tasks, IEEE CoG 2022](https://ieee-cog.org/2022/assets/papers/paper_64.pdf), Section IV-D and Figure 8.

Consistency Grade tiers partition the normalized Consistency utility into fixed equal-width bands: S `[0.8,1]`, A `[0.6,0.8)`, B `[0.4,0.6)`, C `[0.2,0.4)`, D `[0,0.2)`. This is an engineering interpretation scale, not a population percentile. Battery Consistency is the arithmetic mean of the four mode Consistency utilities. Reaction Grade uses authoritative mean Close Reaction Time because that mode measures the visual-response interval used by the Proposal benchmark; Far's primary speed metric is Travel Time and Tracking has no reaction event. The final Grade remains the worse of Reaction and Consistency tiers.

## 5. Confirmatory Contract

After exploratory ranking, collect exactly 10 new matched pairs. Each pair consists of one complete four-mode battery for candidate A and one for candidate B, using the same mode order and condition seeds. Exploratory batteries may not be reused. Five pairs run A then B and five run B then A according to the frozen deterministic sequence; numeric sensitivity and candidate identity remain blind.

The test enumerates all `2^10 = 1024` within-pair sign flips of the mean A-minus-B battery-score difference. The two-sided p-value is the fraction whose absolute permuted mean is greater than or equal to the absolute observed mean. The accepted numerical envelope freezes a `1e-12` score-point guard only for this inclusive floating-point comparison. Exhaustive enumeration includes the observed assignment, so no Monte Carlo plus-one correction is added. The minimum attainable two-sided p-value is `2/1024 = 0.001953125`. Select the candidate indicated by the effect sign only when `p < 0.05`; otherwise return a statistical tie.

The report also includes the paired mean effect and a 95% Student-t confidence interval `mean difference +/- t(0.975,9) x sample_SD/sqrt(10)`, with `t=2.2621571628540993`. The interval is reported uncertainty, not a second Winner gate; the exact randomization p-value controls the decision. This distinction avoids presenting the parametric interval as the exact test itself. Sources: Ernst, [Permutation Methods: A Basis for Exact Inference](https://doi.org/10.1214/088342304000000396); [NIST confidence interval for an unknown mean](https://www.itl.nist.gov/div898/handbook/prc/section2/prc221.htm); [NIST Student-t critical values](https://www.itl.nist.gov/div898/handbook/eda/section3/eda3672.htm).

Zero differences are retained. Interrupted/incomplete evidence is retained but does not count toward 10 pairs; the same pair index is repeated. There is no early stopping and no claim that fixed `n=10` guarantees power for a particular effect. Ten was selected because it permits a balanced 5/5 order and exact two-sided p-value resolution substantially below 0.05 while bounding confirmation burden.

## 6. Automated Acceptance Matrix

| Gate | Required result |
|---|---|
| Dependency identity | Accepted P0-R4 and P0-R5 IDs match candidate |
| Formula weights | Exact source/approved weights and theoretical ranges |
| Geometry bounds | Close/Far/Micro/Tracking anchors reproduce from dependencies |
| Consistency bounds | Reproduce from sample counts and bounded-SD derivation |
| Normalization boundaries | Correct high/low direction and clamp behavior |
| Submovement boundaries | 0/1 -> 0, 6+ -> 1, midpoint -> expected linear penalty |
| Component completeness | No favorable zero from miss/null; explicit fail-closed zero-hit behavior |
| Worked score examples | Shot = 77.0 and Tracking = 81.875 exactly within machine tolerance |
| Formula range | Shot minimum -10 retained; no undocumented final clamp |
| Grade boundaries | Every exact Reaction/Consistency boundary is exhaustive and non-overlapping |
| CV zero guard | `abs(mean) <= 1e-9` is undefined; valid nonzero example matches formula |
| Confirmatory enumeration | Exactly 1024 sign assignments and inclusive two-sided extremeness |
| Confirmatory positive fixture | Ten +5 differences -> p 0.001953125, effect 5, CI [5,5], A Winner |
| Confirmatory tie fixture | Symmetric differences -> p 1 and statistical tie |
| Order balance | Exactly five A-first and five B-first pairs |
| Python reproducibility | Same candidate/dependencies produce byte-identical derived evidence |
| Unity parity | Plain C# normalization/score/grade/CV/permutation fixtures match Python |

## 7. Evidence Workflow

1. Validate candidate and immutable dependency IDs.
2. Derive every bound, worked fixture, and acceptance gate into a new JSON artifact.
3. Run all Python tests including prior P0-R3/P0-R4/P0-R5 suites.
4. Run Unity EditMode tests against plain C# parity logic.
5. If every gate passes, publish an immutable accepted contract with artifact hashes, update authoritative Markdown, and mark P0-R6 complete.

## 8. Acceptance Result

P0-R6 is accepted under formula version `sc8-performance-score-v1`, normalization version `sc8-normalization-v1`, Consistency-tier version `sc8-consistency-tier-v1`, confirmatory version `sc8-confirmatory-v1`, and combined contract `sc8-p0-r6-scoring-statistics-contract-v1`.

| Verification | Result |
|---|---|
| Deterministic acceptance gates | Passed 15/15 |
| Python suite | Passed 57/57 total; 13 P0-R6-specific tests |
| Unity EditMode parity | Passed 35/35 total; 8 P0-R6-specific tests |
| Score worked fixtures | Shot 77.0; Tracking 81.875 |
| Formula range fixture | Shot -10 to 100 retained without an undocumented clamp |
| Confirmatory positive fixture | 1024 assignments; p 0.001953125; effect 5; CI [5,5]; candidate A |
| Confirmatory tie fixture | p 1.0; statistical tie |
| Operator capture | Not required |

Authoritative artifacts:

- Accepted contract: `calibration/plans/p0-r6-scoring-statistics-accepted-v1.json`
- Accepted candidate payload: `calibration/plans/p0-r6-scoring-statistics-candidate-v1.json`
- Derived evidence: `calibration/evidence/p0-r6/p0-r6-scoring-statistics-derived-v1.json`
- Unity result: `calibration/evidence/p0-r6/p0-r6-unity-editmode-results-v1.xml`

The accepted envelope pins the candidate payload by SHA-256. Production must never load an edited candidate under the same accepted envelope.
