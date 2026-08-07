# Progress Indicator

Semantic ID: `component.progress-indicator`

## Contract

- Modes: indeterminate ring for unknown duration; determinate line or step progress for measurable completion.
- Properties: mode, value, maximum, accessible label, visibility.
- Progress accompanies specific text; it never substitutes for naming the work.

## Accessibility and behavior

Use WinUI `ProgressRing` or `ProgressBar`. Expose range/value only when determinate. Spinner frames are never announced. Progress updates are deduplicated to meaningful milestones.

## Responsive

Reserve bounds where progress replaces media or content so appearance does not cause layout movement.
