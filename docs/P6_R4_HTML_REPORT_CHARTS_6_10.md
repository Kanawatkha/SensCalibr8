# P6-R4: Offline HTML report charts 6-10

P6-R4 completes the ten required report-chart generators. The report input advances to sc8-html-report-input-v2 to carry persisted Grade, raw primary-time observations, nullable raw Submovement Count, eDPI projected by the C# Data Layer, and the latest Phase 3 Winner lineage needed for cross-profile eDPI comparison.

## Charts

6. **Reaction Time Distribution** is a histogram of post-adaptation primary-time observations grouped by Database Session. Close Flick uses target activation to resolution, Far Flick uses recorded movement onset to resolution, and Micro-Correction uses reference activation to resolution. A Far row without a raw movement onset is omitted; the scoring fallback is not presented as raw reaction data.
7. **Performance Grade Timeline** uses only persisted Grade values and completed-battery eDPI in chronological order. It never assigns or recalculates a Grade.
8. **Reaction Time vs Sensitivity Scatter Plot** plots the same raw primary-time observations against the tested sensitivity value, separated by mode. It is descriptive and does not assert correlation or causation.
9. **Submovement Count vs eDPI Curve** groups persisted post-adaptation Submovement Count observations by C#-projected eDPI and plots their mean. It renders only when raw non-null counts exist. Misses and other null raw values are omitted, never converted to zero.
10. **Profile Comparison Chart** compares the latest persisted Phase 3 Winner eDPI of each available profile. eDPI is the common normalized unit; the chart makes no skill-ranking or causal claim. It renders only when at least two profiles have a Phase 3 Winner.

## Evidence controls

- The C# repository remains the only SQL boundary. Python receives versioned JSON and neither queries the production database nor recalculates authoritative scores, Grade, Winner, or eDPI.
- All local raw shot and Tracking inputs still require finalized adaptation status.
- Each chart emits an explicit insufficient-data panel when its evidence is missing, partial, or unavailable.
- The report remains self-contained and offline, with chart images embedded as data URIs.
