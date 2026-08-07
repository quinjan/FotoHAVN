# Canonical operator modal prototype

> PROTOTYPE — throwaway code. Do not promote directly to production.

Question: **What canonical modal structure and behavior should standardize action hierarchy, destructive styling, focus entry and trapping, Escape behavior, background isolation, busy states, and focus restoration?**

Three variants are mounted over the real operator surfaces and switchable with `?variant=A|B|C`. Scenarios are switchable with `?scenario=start|exit|delete|discard|error`.

- A — Calm footer: conventional content flow and horizontal footer actions.
- B — Decision split: consequence content beside a dedicated decision rail.
- C — Guided stack: labelled review steps and full-width stacked actions.

The Discard scenario hosts the modal inside the approved Event Setup direction from issue #56: seamless form/preview columns, silent successful fields, field-local validation, and persistent compact footer.

Run from this directory:

```powershell
.\run.ps1
```

Then open <http://127.0.0.1:4177/?variant=A&scenario=delete> at 1280 × 720.

Keyboard:

- `Left` / `Right`: previous or next variant, unless a form control has focus.
- `Escape`: dismiss while idle; ignored while an action is busy.
- `Tab` / `Shift+Tab`: focus remains inside the modal.

All mutations are in-memory stubs. Use **Simulate failure** to inspect retained-context Retry behavior.
