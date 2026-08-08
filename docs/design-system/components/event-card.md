# Event Card

Semantic ID: `component.event-card`

## Contract

- Anatomy: Event name, compact Event ID, saved metadata, readiness/status, a whole-card Start action, and separate Edit and Delete Icon Actions.
- States: ready, hover, focus, unavailable, busy, deletion-incomplete.
- Start uses the entire card hit area and is visually quiet at rest, matching the approved target. Hover/focus reveals the card affordance; the accessible Start action remains exposed in every ready state.
- Duplicate names are allowed. Compact ID content is `EVENT ID` plus the uppercase final eight UUID hexadecimal characters grouped `XXXX · XXXX`.
- Unavailable disables the whole-card Start action and exposes explicit setup/recovery. Busy names the work and disables conflicts. Delete is spatially separated from Start/Edit.

## Accessibility and behavior

Expose one navigable card group; do not make nested actions ambiguous. Edit, Start, and Delete names include Event name and compact ID. Narrator speaks the canonical example as `Event ID ending in 2 F 9 1, C 4 E 8`. Truncated visual names retain their complete tooltip and accessible name.

## Responsive

Standard uses three equal columns, Compact two, Stress one full-width shorter horizontal card. Identity, readiness, Edit, and Delete remain visible; Start remains available as the accessible whole-card action. Creation/edit/cancel preserves list position; a changed card is brought into view.
