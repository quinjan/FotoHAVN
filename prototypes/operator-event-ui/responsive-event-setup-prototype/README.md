# Responsive Event setup UI prototype

> PROTOTYPE — throwaway code. Do not promote directly to production.

Question: **Which responsive Event setup structure should issue #59 adopt?**

Three structurally different Event setup variants are switchable with `?variant=A|B|C`. The effective content-area modes are switchable with `?mode=standard|compact|stress`:

- Standard: 1180 × 650 effective pixels
- Compact: 900 × 600 effective pixels
- Stress: 760 × 460 effective pixels

Run from this directory:

```powershell
.\run.ps1
```

Then open <http://127.0.0.1:4180/responsive-event-setup-prototype/?variant=A&mode=standard&state=ready>.

Variant A also exposes the approved issue #56 reference states with `?state=ready|checking|camera-error|storage-low|name-required|saving|starting`.

Keyboard:

- `Left` / `Right`: previous or next variant
- `1` / `2` / `3`: Standard, Compact, or Stress mode

The rendered state includes the canonical issue #55 identity panel and the issue #56 field-local validation, silent-success, exact-storage, and button-local progress contracts. The prototype is read-only; controls demonstrate hierarchy and reflow rather than real mutations.

Variant A directly expresses the pending handoff proposal: centered two-column modal in Standard, one scrollable column in Compact, and a full-window surface in Stress, always with a sticky title and 80 px footer. Its content follows the approved decisions from issues #55 and #56. Variants B and C remain exploratory layout comparisons only.
