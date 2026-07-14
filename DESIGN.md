# DESIGN.md

## Purpose

This file defines the visual and layout design for every screen and scene in SensCalibr8: the Test Arena (where aim tests are performed) and the Menu/UI Screens (Slot Selection, Setup, Dashboard, Comparison Page). The guiding principle throughout is simplicity — this project measures sensitivity, not graphics quality. Do not build anything more visually complex than what is specified here.

---

## 1. Test Arena Design

### 1.1 Scene Layout

An enclosed box room (fully closed on all sides — floor, walls, and ceiling). All surfaces use a gray/white (or gray/dark-gray) checkerboard pattern, in the style of Aim Lab and KovaaK's.

This is a deliberate choice for two reasons:

- It is simple to build and requires no complex 3D environment work, textures, or lighting setup.
- It is the established standard in the aim-training genre precisely because it introduces no visual distraction that could interfere with measuring sensitivity performance.

Do not add additional geometry, decorative objects, complex lighting, or shadows to the arena. The arena exists only to provide spatial reference and a consistent background contrast for targets.

### 1.2 Target Design

- Shape: sphere.
- Color: bright cyan, chosen for high contrast against the gray checkerboard background.
- Size varies according to the Small / Medium / Large randomization required by Fitts's Law compliance (see `FEATURES.md`, Section 2.1). Do not use any color other than the defined cyan across size variants — only size should change, not color.

### 1.3 Crosshair

- Style: a small dot.
- Color: a high-contrast color, distinct from both the background and the target color, so it remains visible in all conditions.
- The crosshair configuration is locked per profile and does not change between test modes or sessions (see `FEATURES.md`, Section 2.2).

### 1.4 First-Person View

The player view includes a visible first-person hand and weapon model, with a basic shoot animation triggered on input. This is included for immersion consistency with the aiming context the tool is designed around, but must remain simple — no elaborate weapon customization or animation complexity is required.

### 1.5 HUD Layout

A minimal top bar displays: current test progress (e.g. shot count / target count), a timer if relevant to the mode, and live accuracy percentage for the current session. Keep the HUD text small, high-contrast, and positioned so it never overlaps with the target spawn area.

### 1.6 Color Palette (Fixed)

| Element | Color |
|---|---|
| Arena floor/walls/ceiling | Gray / white checkerboard (light gray + dark gray or white) |
| Target | Bright cyan |
| Crosshair | High-contrast dot (distinct from cyan and gray) |
| HUD text | High-contrast, minimal, small |

Do not deviate from this palette. Consistent color usage across sessions is part of controlling for confounding visual variables, similar in spirit to the Crosshair Consistency rule in `FEATURES.md`.

---

## 2. Menu / UI Screen Design

This covers every non-arena screen: Slot Selection, Setup, Dashboard, and Comparison Page.

### 2.1 Layout Principle

All menu/UI screens use a simple table/grid layout, similar to a spreadsheet. Do not build custom UI components, animated transitions, or complex visual chrome for these screens. The goal is functional clarity, not visual polish — development time should go toward the test engine and analysis layer, not menu aesthetics.

### 2.2 Slot Selection Screen

A grid or list of existing profile slots, each showing: profile name, last active date, and a delete action. A "Create New Slot" option is always visible. Selecting a slot proceeds to the Dashboard scoped to that profile.

### 2.3 Setup Screen

A simple form-style table of fields matching the Physical Profile Setup fields defined in `FEATURES.md`, Section 1.2: Hardware DPI (with a link/button to trigger the Physical Ruler Test fallback), dominant hand, crosshair configuration, grip style, movement strategy, mousepad width/height, and ADS multiplier. Use plain labeled input rows, arranged as a table — no multi-step wizard is required.

### 2.4 Dashboard Screen

A grid layout showing, at minimum: the current Best Sensitivity result, current Grade, a summary of recent session activity, and access points to start any of the four Test Modes. Any injury risk warnings (see `RULES.md`) should be shown here as a simple, non-intrusive banner row within the grid, not a blocking modal.

### 2.5 Comparison Page

A table where rows represent profiles and columns represent the comparison metrics defined in `FEATURES.md`, Section 1.3 (Consistency, Reaction Time Tier, Performance Score), all normalized via eDPI. No charts are required on this screen — the exported HTML report (see `FEATURES.md`, Section 4.2) is where visual charts belong. This page is meant to be a fast, scannable data table.
