# P2-R4: Lifecycle Guards and Persistence

## Active profile state

Schema migration 2 adds the singleton `application_state` row for the active profile. `PersistentActiveProfileStore` is the Service-owned adapter over the Data repository; it restores the active profile when the application factory opens the same database and clears the row when the user exits the profile. Its `profile_id` uses `ON DELETE CASCADE`, so stale state cannot survive a profile deletion.

## Deletion safety

Deletion is a two-step Service contract:

1. `BeginDeletion` rejects an active profile and returns a `ProfileDeletionConfirmation` only for an inactive profile.
2. `ConfirmDeletion` requires that object and rechecks the profile identity before cascading the database delete.

The Slot UI exposes Delete, Confirm Delete, and Cancel Delete. It also exposes Exit Active Profile, so the owner can deliberately leave the selected profile before requesting its irreversible deletion.

## Setup resume and edit

Selecting Edit loads persisted setup fields into the same form. The edit request does not contain crosshair color, and the update repository SQL continues to omit `crosshair_config`; the UI displays the saved color as locked. Manual DPI and the explicitly confirmed Physical Ruler path remain available when editing.

## Verification

Unity 6000.5.3f1 EditMode/NUnit passed **63/63** with no failures, skips, or inconclusive tests. The five P2-R4 fixtures cover persistent restore, profile-scoped selection, locked-color update, confirmation-gated deletion and state clearing, and stale-confirmation rejection. Production Python passed **11/11**, Phase 0 regression passed **72/72**, and the Windows production build passed.
