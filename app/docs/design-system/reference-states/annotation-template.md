# Reference-state annotation template

Copy this block for each visual reference or WinUI verification capture.

```yaml
id: surface.state.mode
contractVersion: 1.0.0
surface: Saved Events | Event setup | Guest Start | Guest Start unavailable | Capture | Operator Assistance | Photo Strip | Confirmation
state: canonical semantic state
viewport: 1280x720
effectiveMode: standard | compact | stress
audience: operator | guest
visibleText: []
heading:
  text: ""
  level: 1
automation:
  name: ""
  role: ""
  description: ""
  state: ""
  value: ""
readingOrder: []
focus:
  initial: ""
  order: []
  returnTarget: ""
  reflowBehavior: ""
keyboard: []
touch:
  minimumTarget: 48x48
  activation: release
  gesture: none
announcements:
  - text: ""
    trigger: ""
    priority: polite | assertive
    deduplicate: semantic-transition
contrast:
  - foreground: color.text.primary
    background: color.surface.panel
    ratio: ""
highContrast: ""
reducedMotion: ""
stress200: ""
winuiPattern: ""
verification: [keyboard, narrator, touch, visual]
evidence: ""
```

Decorative elements use `Hidden from accessibility`. An `AA` claim without its tested semantic token pair and measured ratio is not evidence.
