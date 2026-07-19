# P6-R3: Offline HTML report charts 1-5

P6-R3 adds the first five report charts required by FEATURES.md. The report is generated from the versioned sc8-html-report-input-v1 JSON contract produced by the C# Data/Service boundary. The Python module reads that JSON only: it does not open the production SQLite database and does not recalculate any authoritative score.

## Evidence and traceability

- Every report identifies the profile, report-input version, report version, persisted Performance Score formula version(s), and calibration configuration version(s).
- Source rows are finalized, post-adaptation observations only. An unfinalized shot or Tracking trial makes the C# report-input read fail closed.
- A missing data series produces an explicit **insufficient data** panel. No chart estimates values, fills gaps, or invents a score.
- The HTML is self-contained: chart PNGs are embedded as data URIs and no network resource is required.

## Charts

1. **Sensitivity vs Performance Score Curve** uses persisted complete-battery Performance Scores, plotted against persisted eDPI. It never recomputes Performance Score.
2. **Overflick vs Underflick Balance** uses stored raw signed aim error. Its stored sign is a coordinate direction, not a presentation label. The report derives intended horizontal direction only from a non-zero target-center azimuth in persisted spawn position, multiplies that direction by the raw sign for display classification, and leaves the raw value untouched. Rows with a zero or unavailable intended horizontal direction are omitted and counted in the report note.
3. **Movement vs Stationary Error** compares post-adaptation stationary final precision error with moving Tracking deviation RMS, grouped by sensitivity. Both remain angular errors in degrees.
4. **Progressive Narrowing Timeline** shows persisted phase winners in cycle/phase order using their stored Winner eDPI.
5. **Consistency Trend Over Time** shows the sample standard deviation of available post-adaptation angular-error observations by session date. Lower SD indicates greater consistency; it is a descriptive chart and does not recalculate an authoritative score or Grade.

## Boundary

The report-input repository validates profile ownership and rejects unfinalized adaptation evidence. Its service serializes immutable report input with camel-case JSON names. The Python report module validates the versioned shape and creates the offline report only when an explicit output path is provided.
