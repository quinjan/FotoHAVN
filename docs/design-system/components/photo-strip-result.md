# Photo Strip Result

Semantic ID: `component.photo-strip-result`

## Contract

- States: preparing, visible, returning, failed.
- Properties: Photo Strip image, remaining seconds, return progress, optional eyebrow, title, supporting message.
- Preparing reserves final image bounds and shows progress. Visible uses a 10-second interval with text and determinate progress. Returning keeps geometry and may fade without delaying completion. Failed routes to Operator Assistance while preserving Captures.
- Return to Start is automatic and requires no guest action.

## Accessibility and behavior

Expose the image as `Photo strip preview`. Reveal announces `Your photo strip is ready. Returning to start in 10 seconds.`; announce five seconds and the completed return cadence defined in Foundations. Decorative tilt is hidden and may be removed.

## Responsive

Never crop or stretch the complete Photo Strip. Standard uses message/progress beside the strip; Compact tightens side-by-side; Stress maximizes strip height with a narrow adjacent status column. Celebration/supporting copy disappears before essential status.
