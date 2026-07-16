# P2-R2: Profile Lifecycle Services

## Scope

P2-R2 provides the application Service boundary for profile creation, listing, active selection, update, and deletion. It has no Unity UI, target spawning, scoring, or Test Engine behavior.

## Lifecycle contract

- Profile names are unique locally; duplicate creation or rename returns `profile_name_duplicate` before persistence.
- Selecting a profile makes it active in the in-memory active-profile store and updates its ISO-8601 UTC `last_active_date`.
- An active profile cannot be deleted (`active_profile_deletion_forbidden`). After the active context is exited, deletion is delegated to `ProfileRepository`; the schema's `ON DELETE CASCADE` removes its dependent history.
- Update retains the original creation date and last-active date. It changes only editable setup fields.
- Crosshair color is set only when creating a profile. `ProfileUpdateRequest` has no crosshair field, and the repository update SQL deliberately omits `crosshair_config`.

## Physical Ruler confirmation

OQ-020 is resolved by the owner-approved confirmation policy. A `PhysicalRulerHardwareDpiSelection` contains three distinct values:

- `ExactEstimatedDpi`: the unrounded formula result for display/audit by the caller.
- `SuggestedDpi`: the nearest positive integer shown as a suggestion.
- `ConfirmedDpi`: the user-confirmed, editable positive integer that alone may be stored.

An unconfirmed selection fails before any write with `physical_ruler_dpi_confirmation_required`. Manual DPI follows the same positive-integer persistence rule without this additional confirmation step.

## Verification

Unity 6000.5.3f1 EditMode/NUnit passed **53/53** with no failures, skips, or inconclusive tests. The six P2-R2 fixtures cover lifecycle creation/listing/selection, duplicate protection, Physical Ruler confirmation and user-selected integer persistence, locked crosshair update behavior, active-deletion protection plus inactive schema cascade, and unknown-profile handling.

## Deferred work

P2-R3 owns visual slot/setup screens and the confirmation controls. P2-R4 owns restart-resume persistence and explicit UI deletion confirmation. The active-profile store is intentionally in-memory until those rounds define durable application-state behavior.
