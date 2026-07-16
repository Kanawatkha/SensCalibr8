# P4-R4 — Micro-Correction

## Scope

P4-R4 implements the production Micro-Correction mode on the shared `ITestMode` lifecycle and the production projection of the accepted `sc8-signal-pipeline-v1`. It introduces no score, candidate selection, or mode-local calibration values.

## Mode contract

- The deterministic sequence contains 30 Small-target opportunities.
- Every target center uses the accepted deterministic radial/directional sampler and remains within 5–20 reference pixels of the center crosshair.
- The cyan preview target may be visible while the center reference is active. Center-reference activation starts Correction Time; click or the frozen 1.5-second timeout resolves the opportunity.
- Final aim and target center are compared in angular coordinates. Initial offset remains in reference pixels because the authoritative Micro spawn contract is pixel-based.
- Final Precision Error and Center-Hit use the frozen angular target geometry. Center-Hit remains diagnostic only.

## Signal contract

`SubmovementSignalProcessor` loads the uniform-grid rate, SOS coefficients, odd-pad length, minimum segment length, start/end thresholds, and refractory period from the accepted configuration. It requires passed timing diagnostics, processes each gap-delimited segment independently, filters azimuth/elevation with forward-backward SOS, derives Euclidean angular velocity, and counts events at the accepted boundaries.

Any failed timing contract or short segment marks the signal ineligible. A hit cannot be accepted without eligible signal evidence. Miss-clicks and timeouts retain null counts. Raw mouse samples remain in `mouse_samples`; filtered values and counts never overwrite them.

The proposal names both Micro-Adjustment Count and Submovement Count but authorizes only one corrective-movement segmentation algorithm. P4-R4 therefore persists the same accepted event count in both fields for a hit rather than inventing a second detector. Their separate columns remain available for a future versioned specification.

## Persistence and verification

`MicroCorrectionEvidencePersistenceMapper` maps preview/activation/resolution timing, pixel offset, angular aim evidence, counts, precision, and Center-Hit into the existing shot repository contract. Adaptation remains null until transactional session finalization.

Verification covers frozen identity/geometry, exact 8/4 deg/s boundaries, less-than versus exactly 80 ms refractory behavior, timing rejection, short-segment rejection, complete 30-opportunity lifecycle, hit/miss count policy, mapping, full production tests, calibration regressions, and Windows build.
