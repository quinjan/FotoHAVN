# Event identification

`Event ID` is the canonical term for an Event's stable UUIDv7 identifier. Duplicate Event names are allowed; editing does not change the ID.

## Saved Event card

Show, in order:

1. Event name;
2. `EVENT ID`;
3. uppercase final eight hexadecimal UUID characters grouped `XXXX · XXXX`;
4. saved-recency metadata.

The compact value is a recognition aid, not a uniqueness guarantee. V1 adds no collision handling or copy action. Narrator announces the canonical example as `Event ID ending in 2 F 9 1, C 4 E 8`.

## Consequential flows

Edit, Start confirmation and progress/failure, and every permanent-deletion state show a neutral bordered Identity panel containing Event name, `EVENT ID`, and the complete lowercase hyphenated UUID. The UUID wraps at safe character boundaries and remains fully visible.

Narrator announces `Full Event ID` followed by the UUID character-by-character in its five groups. Accessible names for repeated Edit, Start, and Delete actions include the Event name and compact ID. New Event setup omits the panel; Active Event and guest stages omit Event ID.
