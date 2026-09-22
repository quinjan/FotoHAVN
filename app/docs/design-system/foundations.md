# Foundations

## Principles

1. **Calm and direct.** Use restrained monochrome surfaces, purposeful status color, generous whitespace, and plain language.
2. **Guest clarity first.** A guest stage has one obvious next action and no scrolling or operator jargon.
3. **Operator confidence.** Consequential work identifies the Event, names progress, preserves context, and offers the next safe action.
4. **Accessible by contract.** Keyboard, Narrator, touch, focus, contrast, High Contrast, reduced motion, and scaling behavior are designed states, not implementation afterthoughts.
5. **Semantic before literal.** Components consume semantic tokens. A raw value is not a component API.

## Token model

Tokens form a strict `primitive → semantic → component or app element` chain. Canonical IDs are platform-neutral, lowercase, and dot-separated. The machine-readable source is [tokens.json](tokens.json).

### Color

- Canvas is off-white; panels and raised surfaces are white; primary/media content is near-black.
- Status color communicates state, never decoration. Text, shape, iconography, or stroke must carry the same meaning.
- Focus remains semantically separate from information blue even when values coincide.
- Destructive actions use the dark-red direction led by `#D4382E`; the retired `#FF4D4F` is prohibited.
- Routine panels and controls have no elevation. A scrim and one subtle dialog shadow are the only default depth treatments.

### Typography

Inter is the only v1 typeface. Roles are `display.countdown`, `display.guest`, `heading.page`, `heading.dialog`, `body.default`, `label.control`, `caption`, and `eyebrow`. The 190 px countdown is a deliberate display exception. Responsive compositions may choose another approved role or clamp the countdown, but may not invent an ad-hoc style.

### Spacing and geometry

Use the mostly 4 px rhythm in `tokens.json`. Interactive operator targets are at least 48 × 48 effective pixels. Guest-prominent actions are at least 64 px high. Independent hit regions have at least 8 px separation. V1 has no compact interactive variant: layouts reflow instead of shrinking targets.

### Focus

Keyboard focus uses a 2 px outer blue ring with a 2 px gap. Add a thin white separator only where a dark or visually busy background requires it. Focus is independent from selection, invalidity, and warning. Windows High Contrast may replace authored colors with system colors while preserving meaning and boundaries.

### Motion

Use instant (0 ms), fast (120 ms), standard (200 ms), or slow (300 ms) with one ease-out curve. Motion never delays Camera feedback, failure presentation, or Guest Cycle completion. When Windows animations are disabled, nonessential motion resolves to instant.

## Responsive contract

Layout follows the effective content area, not device labels or individual Windows scaling percentages.

| Mode | Effective content area |
|---|---|
| Standard | at least 1180 × 650 |
| Compact | at least 800 × 500 but below either Standard threshold |
| Stress | below either Compact threshold, down to 640 × 360 |

FotoHAVN launches centered and borderless at 1280 × 720 when possible. On smaller workspaces it fits available bounds and reflows; it never crops or scales the entire canvas. Horizontal scrolling is prohibited.

Operator surfaces may vertically scroll only their main content; headers, consequential identity, progress/recovery, and actions remain fixed as specified. Guest Start, Guest Start unavailable, Capture, Operator Assistance, Photo Strip preview/return, and Exit Event confirmation are zero-scroll.

Verification references are 1280 × 720, 1024 × 768, 1024 × 576, 853 × 480, and 640 × 360.

## Language

- Operator-facing UI uses precise domain terms when needed: Event, Guest Cycle, Capture, Photo Strip, and Operator Assistance.
- Guest-facing UI uses familiar terms: event, photo session, photo, photo strip, and `Please ask the operator for help.`
- User-facing headings, labels, buttons, and accessible names use sentence case.
- Buttons begin with concise verbs. Headings, labels, and buttons omit terminal punctuation; instructions, errors, and statuses use complete sentences.
- Ellipses appear only while work is active, for example `Starting event…`.
- Errors identify the affected item, explain the problem, and state the next available action. Raw errors and generic `Something went wrong` copy are prohibited.

## Accessibility annotations

Every interactive component and reference state records:

1. visible text and audience;
2. heading level, where applicable;
3. accessible name, role, description, state, and value;
4. reading and focus order;
5. initial and returned focus;
6. keyboard inputs and outcomes;
7. touch-target bounds and gesture behavior;
8. announcement text, trigger, priority, and deduplication;
9. foreground/background token pair and measured contrast;
10. High Contrast, reduced-motion, and 200% stress behavior;
11. expected WinUI control or UI Automation pattern;
12. keyboard, Narrator, touch, and visual verification checks.

Decorative elements use `Hidden from accessibility`.

### Measurable criteria

- Normal text: at least 4.5:1.
- Large text: at least 3:1 using the WCAG large-text threshold.
- Meaningful controls, icons, boundaries, selection, and focus: at least 3:1 against adjacent colors.
- Each screen or modal has exactly one programmatic level-1 heading; section levels do not skip.
- Focus follows visible reading/task order and is revealed when scrolling is allowed.
- Modal focus enters on the safest action, remains contained, and returns to the invoker.
- Tab and Shift+Tab navigate; arrows operate conventional controls; Enter and Space activate native buttons; Escape closes only dismissible idle dialogs.
- Activation occurs on release. Hover, drag, multi-touch, and precision tapping are never prerequisites.

### Guest Cycle announcement cadence

- Countdown start: `Photo 2 of 4. Taking photo in three seconds.` The numerals are not separately announced.
- Preservation: `Photo 2 saved.`
- Photo Strip reveal: `Your photo strip is ready. Returning to start in 10 seconds.`
- Five seconds: `Returning to start in five seconds.` Intervening seconds are visual only.
- Return: `Ready for the next guest.`
- A blocking failure interrupts with an assertive Operator Assistance announcement. Other progress and completion announcements are polite and occur once per semantic transition.
