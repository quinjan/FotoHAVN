# App Header

Semantic ID: `component.app-header`

## Contract

- Anatomy: the square FotoHAVN `F` mark and product name lockup, context slot, optional trailing action.
- Use the same mark-and-name lockup on every application page; dialogs and setup overlays do not duplicate it inside their panels.
- Contexts: Operator and Guest Cycle. The Saved Events operator header shows the FotoHAVN mark and product name at left and the uppercase `Operator console` context at right.
- Uses a neutral divider, no elevation, and no transient status.
- Transient setup or navigation messages never appear in the header or directly beneath it; the destination surface owns any status that still needs attention.
- Every Guest Cycle screen leaves the header context slot empty. Guest Start places the Event name immediately above its guest-facing heading; later stages prioritize their current status and task inside the stage. Guest Start unavailable and Operator Assistance use the guarded Exit Event action in the trailing slot.

## Accessibility and behavior

The product mark is one named element; decorative mark details are hidden. Header actions keep minimum 48 × 48 targets and follow the visible reading order.

## Responsive

Remain fixed on guest stages. Compact and Stress may reduce spacing, but preserve the brand and guarded Exit Event control where applicable. Guest Cycle headers never use the Event name as header context.
