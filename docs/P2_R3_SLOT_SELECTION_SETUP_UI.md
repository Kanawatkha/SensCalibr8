# P2-R3: Slot Selection and Setup UI

## Implemented presentation boundary

The application now has a runtime IMGUI menu bootstrap with the required simple table/grid surfaces:

- Slot Selection lists a profile name and last-active date, provides Select, and keeps Create New Slot visible.
- Setup provides plain labeled rows for name, manual DPI or the Physical Ruler fallback, current sensitivity, configured polling rate, dominant hand, grip style, movement strategy, mousepad width/height, and ADS multiplier.
- Crosshair style is displayed as the application-fixed dot and its size as the application-fixed four-pixel filled dot; neither is an input.
- The Physical Ruler path shows the exact formula estimate, pre-fills the suggested integer, and requires a separate confirmation before a profile can be stored.

`ProfileSetupScreenModel` only performs raw-input parsing/validation and forwards typed requests. `ProfileSetupApplicationService` owns calculation/service calls and `ProfileSetupApplicationFactory` owns Data composition. The UI assembly has no Data reference and contains no SQL or sensitivity formula.

## Verification

Unity 6000.5.3f1 EditMode/NUnit passed **58/58** with no failures, skips, or inconclusive tests. The five new fixtures verify manual profile creation and slot selection, invalid DPI rejection before persistence, Physical Ruler preview/confirmation, required/approved color selection plus unsupported-color rejection, and the absence of editable crosshair style/size input fields.

## Approved palette

The project owner approved a fixed palette on 2026-07-16: Yellow `#FFE600`, Magenta `#FF00FF`, Red `#FF3B30`, and Orange `#FF9500`. The UI presents only these choices and `ProfileSetupApplicationService` rejects any other color before the lifecycle can persist a profile. This keeps the color distinct from the fixed cyan target and grayscale arena without adding an undefined runtime contrast algorithm.

P2-R3 is complete. P2-R4 has not started.
