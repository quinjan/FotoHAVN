# FotoHAVN context map

Use this map when a task names product behavior or introduces product language. Read the shared context and every affected scoped context.

## Shared product language

Read [`CONTEXT.md`](CONTEXT.md) for concepts shared across the physical booth, website, analytics, and marketing: Event, Capture, Photo Strip, Print Sheet, and Customer.

## Desktop booth

Read [`app/CONTEXT.md`](app/CONTEXT.md) for the Windows booth lifecycle: Event Setup, the Active Event, the Operator Screen and Guest Screen, Guest Display Binding, Camera readiness and binding, Guest Cycles with their Templates, Looks, Arrangement, Retakes, Phase Timers, Start Over, and Keepsake Preview, the Event Gallery, the Operator PIN, and Operator Assistance.

Also read the desktop ADR for the decision you touch:

- Process, host, brain, and UI split: [`app/docs/adr/0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md`](app/docs/adr/0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md)
- Camera stream ownership, preview, format selection, or capture timing: [`app/docs/adr/0004-brain-owned-webcam-through-one-exclusive-mediacapture-stream.md`](app/docs/adr/0004-brain-owned-webcam-through-one-exclusive-mediacapture-stream.md)
- Event storage and the Data folder: [`app/docs/adr/0005-sqlite-metadata-with-file-artifacts-for-event-storage.md`](app/docs/adr/0005-sqlite-metadata-with-file-artifacts-for-event-storage.md)
- Templates, Looks, and Photo Strip composition: [`app/docs/adr/0006-brain-side-composition-with-an-app-owned-catalog.md`](app/docs/adr/0006-brain-side-composition-with-an-app-owned-catalog.md)
- Testing, coverage, and the end-to-end framework: [`app/docs/adr/0007-unit-first-testing-with-real-camera-end-to-end.md`](app/docs/adr/0007-unit-first-testing-with-real-camera-end-to-end.md)
- Operator and Guest Screens on bound displays: [`app/docs/adr/0008-operator-and-guest-screens-on-bound-displays.md`](app/docs/adr/0008-operator-and-guest-screens-on-bound-displays.md)

## Website and online booth

Read [`website/CONTEXT.md`](website/CONTEXT.md) for the Website Intro Experience, FOTOHAVN Brand Strip, Online Booth Cycle, inquiry funnel, consent, and website analytics meanings.

For production topology or public route behavior, also read [`deploy/fotohavn/docs/adr/0002-deploy-website-as-an-immutable-container.md`](deploy/fotohavn/docs/adr/0002-deploy-website-as-an-immutable-container.md).

## Boundaries

- A Guest Cycle is a physical-booth lifecycle; an Online Booth Cycle is an in-browser lifecycle.
- Template and Look are desktop terms, defined in `app/CONTEXT.md`. The booth uses the website's Template and Look IDs from its own catalog copy; promote the terms to `CONTEXT.md` if they become shared language.
- A FOTOHAVN Brand Strip is an introductory brand object; a Photo Strip is a keepsake made from Captures.
- An Inquiry Intent is an anonymous link activation; a Qualified Lead and a Customer require evidence outside website analytics.

When one change crosses contexts, preserve each context's term and describe the boundary explicitly.
