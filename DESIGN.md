# DESIGN.md

## Purpose

This file defines the visual and layout design for every screen and scene in SensCalibr8: the Test Arena (where aim tests are performed) and the Menu/UI Screens (Slot Selection, Setup, Dashboard, Comparison Page). The guiding principle throughout is simplicity — this project measures sensitivity, not graphics quality. Do not build anything more visually complex than what is specified here.

---

## 1. Test Arena Design

### 1.1 Scene Layout

The accepted `sc8-test-geometry-v1` layout is an unlit, shadow-free 20 x 12 x 21 world-unit room centered at `[0, 1.6, 9.5]`, viewed by a perspective camera at `[0, 1.6, 0]` with zero rotation and a 103-degree horizontal FOV. The test viewport is fixed to 16:9 at a 1920 x 1080 reference resolution; non-16:9 displays must letterbox the test viewport rather than alter FOV or target scale. The reference frame policy is 144 Hz with VSync and adaptive sync disabled during acceptance-bearing sessions.

An enclosed box room (fully closed on all sides — floor, walls, and ceiling). All surfaces use a gray/white (or gray/dark-gray) checkerboard pattern, in the style of Aim Lab and KovaaK's.

This is a deliberate choice for two reasons:

- It is simple to build and requires no complex 3D environment work, textures, or lighting setup.
- It is the established standard in the aim-training genre precisely because it introduces no visual distraction that could interfere with measuring sensitivity performance.

Do not add additional geometry, decorative objects, complex lighting, or shadows to the arena. The arena exists only to provide spatial reference and a consistent background contrast for targets.

### 1.2 Target Design

- Shape: sphere.
- Color: bright cyan, chosen for high contrast against the gray checkerboard background.
- Size varies according to the Small / Medium / Large randomization required by Fitts's Law compliance (see `FEATURES.md`, Section 2.1). Do not use any color other than the defined cyan across size variants — only size should change, not color.

- Small / Medium / Large angular diameters are fixed at 0.75 / 1.50 / 2.25 degrees on a target plane 10 world units from the camera, projecting to approximately 10 / 20 / 30 pixels at 1920 x 1080.
- Close centers use 5 / 10 / 15 degree offsets; Far centers use 20 / 30 / 40 degree offsets. Each family must use all nine size-and-offset combinations. Vertical centers are limited to 25 degrees.
- Keep every target fully inside a safe viewport with a 32-pixel edge margin and a 64-pixel top HUD reserve. The Center-Hit diagnostic radius is 50% of the target radius.
- Close Flick targets remain hidden during the 500-1000 ms foreperiod. Far Flick and Micro-Correction may show the upcoming cyan target while the center reference is active, but target color must not change on activation; use reference disappearance and the minimal HUD state instead of a new target color.
- Tracking target positions are evaluated from the accepted analytic path and high-resolution elapsed time. Render frames display that state but never integrate or advance the path themselves.

### 1.3 Crosshair

- Style: a small dot.
- Size: a fixed four-pixel filled dot; the user cannot resize or restyle it.
- Color: selected by the user during profile creation from this project-owner-approved high-contrast palette: Yellow `#FFE600`, Magenta `#FF00FF`, Red `#FF3B30`, or Orange `#FF9500`. These warm saturated colors remain distinct from the fixed cyan target and grayscale arena. Color is the only configurable crosshair property.
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

The standalone application starts in a resizable 960 x 540 window and remembers the user's last menu-window position/size. Starting a test performs a timing/display preflight and automatically enters native-resolution `FullScreenWindow` (borderless fullscreen). F11 is an optional fullscreen toggle outside an active test; Escape pauses/interrupts an active test and returns to the previous windowed state. A display-mode change during active evidence capture invalidates that run. These values and controls are project-owner-approved product/UX decisions dated 2026-07-15, not measured arena constants.

### 2.2 Slot Selection Screen

A grid or list of existing profile slots, each showing: profile name, last active date, and a delete action. A "Create New Slot" option is always visible. Selecting a slot proceeds to the Dashboard scoped to that profile.

### 2.3 Setup Screen

A simple form-style table of fields matching the Physical Profile Setup fields defined in `FEATURES.md`, Section 1.2: Hardware DPI (with a link/button to trigger the Physical Ruler Test fallback), current in-game sensitivity, configured mouse polling rate in Hz, dominant hand, crosshair color, grip style, movement strategy, mousepad width/height, and ADS multiplier. Crosshair color is a palette-only choice: Yellow `#FFE600`, Magenta `#FF00FF`, Red `#FF3B30`, or Orange `#FF9500`; free-form colors are not accepted. Crosshair style and size are displayed as fixed application values rather than editable inputs. Use plain labeled input rows, arranged as a table — no multi-step wizard is required.

### 2.4 Dashboard Screen

A grid layout showing, at minimum: the current Best Sensitivity result, current Grade, a summary of recent session activity, and access points to start any of the four Test Modes. Any injury risk warnings (see `RULES.md`) should be shown here as a simple, non-intrusive banner row within the grid, not a blocking modal.

### 2.5 Comparison Page

A table where rows represent profiles and columns represent the comparison metrics defined in `FEATURES.md`, Section 1.3 (Consistency, Reaction Time Tier, Performance Score), all normalized via eDPI. No charts are required on this screen — the exported HTML report (see `FEATURES.md`, Section 4.2) is where visual charts belong. This page is meant to be a fast, scannable data table.
