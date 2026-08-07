# Event Card

Semantic ID: `component.event-card`

## Contract

- Anatomy: Event name, compact Event ID, saved metadata, readiness/status, persistent Start action, separate Edit and Delete Icon Actions.
- States: ready, hover, focus, unavailable, busy, deletion-incomplete.
- Start is visible at rest. Hover/focus may emphasize the card but never hide essential content.
- Duplicate names are allowed. Compact ID content is `EVENT ID` plus the uppercase final eight UUID hexadecimal characters grouped `XXXX · XXXX`.
- Unavailable replaces Start with explicit setup/recovery. Busy names the work and disables conflicts. Delete is spatially separated from Start/Edit.

## Accessibility and behavior

Expose one navigable card group; do not make nested actions ambiguous. Edit, Start, and Delete names include Event name and compact ID. Narrator speaks the compact value as `Event ID ending in 7 A 2 F, 9 1 C 4`. Truncated visual names retain their complete tooltip and accessible name.

## Responsive

Standard uses three equal columns, Compact two, Stress one full-width shorter horizontal card. Identity, readiness, Start, Edit, and Delete remain visible. Creation/edit/cancel preserves list position; a changed card is brought into view.
