# Larger inline booth — selected portrait implementation plan

## Authority and scope

The user selected **option 1, Larger inline studio**, from `.handoffs/booth-portrait-ideation/option-1-inline.png`. That selection supersedes the ideation README's earlier unselected status. The image establishes composition and control hierarchy; `DESIGN.md`, `tokens.json`, `variables.css`, and `theme.css` govern actual typography, color, and geometry. Preserve the existing booth model, materials, photographs, six viewpoints, curtain behavior, fallback, and reduced-motion behavior.

Apply the new presentation only at **`(max-width: 767px) and (orientation: portrait)`**. Existing desktop, tablet-width, and phone-landscape presentation and interaction remain intact. No fullscreen mode or new pinch gesture.

## Portrait composition

1. **Compact introduction.** Retain the eyebrow and existing two-part serif heading; fit the heading into approximately three lines at 320px and two where width permits. Start around 38–44px at 320px, scaling toward 48–52px at 390px; verify actual font metrics. Use 24px text gutters, 12px eyebrow spacing, 16px supporting-copy spacing, and 16–24px before the stage. Portrait supporting copy can be “Come a little closer.” The full existing line remains outside this presentation.
2. **Full-width model stage.** Let the studio escape the section's 24px side gutters using container-relative width/margins, avoiding `100vw` scrollbar overflow. Remove the portrait inset-card border and stage-label overlay. Match the stage to the warm section surface. The **canvas itself**, excluding instructions and controls, should be approximately **450–500 CSS px tall**, including at 320px. Remove the existing 42px top offset and 145px canvas-height subtraction in this mode. Retain clear space around the model silhouette; the photo fallback gets the same generous stage.
3. **One manipulation row below the model.** A short “Swipe to turn.” hint precedes a single grouped control row ordered **rotate left / zoom out / zoom in / rotate right**. Use existing scene commands. Four distinct 48px-minimum targets, readable icons, explicit accessible names, quiet hairlines, 4px radius, and visible focus. Controls and instructions occupy normal flow and never cover the booth.
4. **Chooser and curtain side by side.** Within 24px gutters, use a two-column row with an 8px gap. A labeled native select exposes **Overview, Front, Side, Back, Inside, Bench**. When dragging or button rotation produces `custom`, show a disabled “Custom view” placeholder so the chooser never falsely claims a preset. Preserve existing selection and auto-open behavior for Inside/Bench. The adjacent primary button reads **Open curtain / Close curtain**, using Ebony and Off-white; omit the decorative arrow in portrait. Use 16px control text, at least 48px height, and `min-width: 0` so both controls remain readable at 320px.
5. **Quiet supporting content.** Center the underlined Reset view control beneath that row, then retain live status, current-view description, retry action when needed, and model disclaimer. Keep the current portrait suppression of the secondary decorative heading. Normal page scrolling is expected; do not compress everything into one viewport.

## Framing and interaction contract

Coordinate camera framing with measured canvas aspect ratio and the actual model bounds. Enlarge the overview while keeping its roof, floor, and full width in frame at default zoom; all six presets must show their intended subject clearly. Preserve existing intentional close-up semantics for Inside/Bench. Recompute on resize/orientation changes without rebuilding geometry or resetting user state. Parent implementation owns camera math and focused regression checks.

Retain `touch-action: pan-y`, vertical touch scrolling starting on the canvas, horizontal yaw dragging, pointer cancellation cleanup, keyboard rotation/zoom, and bounded zoom. Keep select state and status synchronized with shared scene state. Hide inactive alternative controls from layout, keyboard navigation, and accessibility; avoid duplicate IDs. Preserve accessible loading/failure instructions and photograph alt text.

## Acceptance evidence

Capture rendered 320px and 390px portrait evidence for overview, every preset, custom rotation, zoom, curtain, reset, and fallback; confirm no horizontal overflow, clipped labels, or obstructed focus. Verify vertical touch scroll through the canvas and viewport/orientation changes. Check representative phone landscape and desktop for regressions. Complete independent plan-conformance review before fresh responsive visual QA, repeating conformance after repairs.
