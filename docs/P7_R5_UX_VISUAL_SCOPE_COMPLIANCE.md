# P7-R5 UX, Visual, and Scope Compliance

## Verified UX policy

- Standalone menu defaults are 960 x 540, windowed, and resizable.
- F11 toggles fullscreen while the menu is active.
- `WindowDisplayPolicy` provides the test-host boundary for native-resolution `FullScreenWindow` entry and restoration to the previous windowed size after Escape/pause handling.
- The menu UI keeps the required table/grid-style screens: profile slots, setup, dashboard, and explicit-selection comparison.
- Ergonomic warnings are rendered as an inline dashboard row with acknowledgement, not as a blocking modal.

## Verified visual contract

- Arena geometry is loaded only from the accepted frozen configuration.
- Arena surfaces remain enclosed, unlit, shadow-free checkerboard geometry.
- Targets remain fixed-color cyan spheres with frozen small/medium/large variants.
- Crosshair configuration is restricted to the approved four-color palette; style remains a fixed dot and size remains the fixed four-pixel filled dot.
- HUD remains minimal and reserves the configured top area; letterboxing preserves the frozen reference aspect.
- No ADS/scoped test, grip-causality scoring, role weighting, decorative arena feature, or unrelated settings UI was added.

## Verified offline and scope boundary

The production C# source contains no `UnityWebRequest`, `HttpClient`, or HTTP URL. The public Data Export service exposes no Import or Restore operation. The product remains sensitivity-only, standalone, offline, and isolated from Valorant/Vanguard.

## Evidence boundary

The automated checks verify the actual project settings, source-level scope surface, frozen geometry/palette, display-policy behavior, and existing UI/service boundaries. A pixel-level human screenshot review of each final standalone build screen is still a release-operator check; automated EditMode tests cannot substitute for visual judgment on a particular monitor/GPU.

## Automated evidence

- `P7R5UxVisualScopeComplianceTests.StandaloneMenuDefaultsMatchTheDocumentedWindowPolicy`
- `P7R5UxVisualScopeComplianceTests.VisualContractKeepsArenaSimpleAndCrosshairColorOnlyConfigurable`
- `P7R5UxVisualScopeComplianceTests.OfflineSensitivityOnlyScopeHasNoNetworkOrRestoreSurface`
- `P7R5UxVisualScopeComplianceTests.DisplayPolicyReturnsToThePreviousWindowAfterTestEscape`
- Unity EditMode: 234/234 passed, 0 failed, skipped, or inconclusive.
