# FotoHAVN Event Camera Tuning Prototype

> THROWAWAY PROTOTYPE — Issue #18 visual exploration. Do not ship this code.

Three progressive-disclosure variants of the approved centered Event setup modal.
Live preview and Camera Tuning start hidden behind one advanced control. Switch
with `?variant=A`, `?variant=B`, or `?variant=C`.
Use `&mode=create` or `&mode=edit` to compare New Event and Edit Event states.

- A — Camera section: Camera Tuning lives behind a gear beside the Camera selector
- B — Focused step: switch between Event details and a dedicated tuning workspace
- C — Tuning dialog: open Camera Tuning in a focused dialog above Event setup

Run from the repository root:

```powershell
npm run prototype:event-camera-tuning
```

Then open <http://localhost:4180/prototypes/event-camera-tuning/?variant=A&mode=create>.

Everything is in memory. The preview is simulated, and switching Cameras proves
that the Event remembers distinct Camera Tuning for each Camera. Add
`&advanced=open` to share a variant with its advanced workspace visible.

In Edit Event mode, change the Event name, Camera, or Camera Tuning to try the
dirty-state confirmations for `Cancel`, `Save & Close`, and `Save & Start Event`.
For direct review, use `&dialog=discard`, `&dialog=save`, or
`&dialog=save-start`; each loads a simulated dirty edit with that dialog open.
