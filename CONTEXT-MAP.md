# FotoHAVN context map

Use this map when a task names product behavior or introduces product language. Read the shared context and every affected scoped context.

## Shared product language

Read [`CONTEXT.md`](CONTEXT.md) for concepts shared across the physical booth, website, analytics, and marketing: Event, Capture, Photo Strip, Print Sheet, and Customer.

## Desktop booth

Read [`app/CONTEXT.md`](app/CONTEXT.md) for the Windows booth lifecycle, Camera readiness and binding, Guest Cycles, and Operator Assistance.

For Camera stream ownership, format selection, or capture timing, also read [`app/docs/adr/0001-capture-from-one-windows-video-stream.md`](app/docs/adr/0001-capture-from-one-windows-video-stream.md).

## Website and online booth

Read [`website/CONTEXT.md`](website/CONTEXT.md) for the Website Intro Experience, FOTOHAVN Brand Strip, Online Booth Cycle, inquiry funnel, consent, and website analytics meanings.

For production topology or public route behavior, also read [`deploy/fotohavn/docs/adr/0002-deploy-website-as-an-immutable-container.md`](deploy/fotohavn/docs/adr/0002-deploy-website-as-an-immutable-container.md).

## Boundaries

- A Guest Cycle is a physical-booth lifecycle; an Online Booth Cycle is an in-browser lifecycle.
- A FOTOHAVN Brand Strip is an introductory brand object; a Photo Strip is a keepsake made from Captures.
- An Inquiry Intent is an anonymous link activation; a Qualified Lead and a Customer require evidence outside website analytics.

When one change crosses contexts, preserve each context's term and describe the boundary explicitly.
