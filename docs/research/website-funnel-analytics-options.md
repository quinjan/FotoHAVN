# FotoHAVN website funnel analytics options

Research date: 2026-09-18

Scope: measure the public website journey from page view, through meaningful use of the online booth, to an outbound Instagram inquiry click, with priority on no additional cash cost, low implementation complexity, and privacy-conscious collection

Status: source-backed architecture recommendation, not an implementation or legal opinion

## Selected direction after decision interview

FotoHAVN selected **Microsoft Clarity** for the first analytics implementation. The selection accepts Clarity's smaller long-term product-analysis surface in exchange for a permanently free service, native heatmaps and attention maps, anonymous session activity, and a lower operational burden.

The accepted collection model is hybrid:

- Clarity may collect privacy-masked, cookieless page activity and fixed non-personal events before Analytics Cookie Consent.
- Analytics Cookie Consent permits Clarity's anonymous first-party cookie to connect activity across pages as one Consented Anonymous Journey.
- Declining cookies does not mean no analytics; the consent interface and privacy page must say that basic cookieless measurement continues.
- The entire online booth is masked. Customer-created photographs, previews, filenames, blob URLs, camera details, imported-image metadata, contact information, and free text are prohibited from analytics.
- The cross-page funnel is reported only for consented anonymous journeys. Cookieless page activity uses a separate denominator.

The canonical terminology and measurement definitions are in [`CONTEXT.md`](../../CONTEXT.md). A development-only, three-variant visual prototype is available on the `codex/clarity-consent-prototype` branch at `/?analytics-prototype=1&variant=A`, with `B` and `C` as alternate structures. It does not load Clarity or persist a consent choice.

### Approved prototype decision

Direction **C** is the approved consent and privacy presentation. Its dark editorial prompt rail keeps Privacy details as an inline `Click here for more information.` action, right-aligns the remaining desktop and tablet actions, and uses the warm-ivory settings dialog and privacy presentation already exercised by the prototype.

After a visitor chooses, Analytics settings are reopened through the approved **Edge Notch**, not a footer link or a large floating tab. The notch rests at the right viewport edge with an ivory surface, muted-brass outline, softened exposed corners, and a 44-pixel accessible interaction target behind a 16-pixel desktop or 14-pixel tablet/mobile painted surface. Desktop reveals the label on hover or keyboard focus. Tablet and mobile use first tap to reveal and second tap to open; keyboard activation opens directly.

This is a prototype decision, not production authorization. Any implementation or repair that changes visible UI must pass a fresh QA Audit at desktop, tablet, 390-pixel mobile, and 320-pixel narrow-mobile widths. Automated tests, lint, type checking, builds, and screenshots do not replace that blocking UI validation gate.

## Decision-ready answer

For the current FotoHAVN site, **PostHog Cloud is the best overall first implementation**. Its free plan includes one million product analytics events per month, one project, and one-year retention, and collection stops at the free limit unless billing is enabled. It has first-class Next.js guidance, custom events, automatic SPA page views, and flexible sequential funnels. Most importantly for this site, it can be configured as an event-only, cookieless integration with session replay disabled, broad autocapture disabled, IP capture disabled, and no identified-person profiles. That provides the requested journey measurement without undermining the online booth's promise that photographs stay on the visitor's device. [PostHog pricing](https://posthog.com/pricing), [Next.js integration](https://posthog.com/docs/libraries/next-js), [funnels](https://posthog.com/docs/product-analytics/funnels), [privacy controls](https://posthog.com/docs/privacy/data-collection)

**Google Analytics 4 is the best free alternative when campaign attribution is more important than understanding the online booth.** It automatically supports outbound link clicks through Enhanced Measurement and has powerful funnel explorations, but its reporting and consent setup are less direct for this small product flow. [Google Analytics overview](https://marketingplatform.google.com/about/analytics/), [outbound-click measurement](https://support.google.com/analytics/answer/13566436), [funnel explorations](https://support.google.com/analytics/answer/9327974)

**Microsoft Clarity has the lowest initial installation effort, but it is not the safest default for this particular site.** It is free forever without traffic limits and supports page visits, custom events, no-code funnels, heatmaps, and session recordings. However, safe use requires strict masking of the entire online booth, consent configuration for relevant jurisdictions, and confirmation that the site is not targeted to people under 18. Once the booth is masked, Clarity's replay advantage is largely outside the core experience. Use it later for marketing-page UX diagnosis if that need justifies the additional privacy surface. [Microsoft Clarity FAQ](https://learn.microsoft.com/en-us/clarity/faq), [funnels](https://learn.microsoft.com/en-us/clarity/setup-and-installation/funnels), [consent mode](https://learn.microsoft.com/en-us/clarity/setup-and-installation/consent-mode), [masking](https://learn.microsoft.com/en-us/clarity/setup-and-installation/clarity-masking)

Do not start with a two-provider stack. GA4 plus Clarity would provide excellent attribution plus behavioral diagnosis at no license cost, but it doubles consent, privacy-policy, QA, dashboard, and maintenance work. Start with one provider and add a second only when a real unanswered question justifies it.

## Current architecture and tracking seams

The present website is well suited to client-side analytics:

- It is a Next.js 16 App Router application with a shared root layout in [`website/src/app/layout.tsx`](../../website/src/app/layout.tsx).
- It is built as a standalone Docker image rather than as a Vercel-specific deployment; the repository's [`website/Dockerfile`](../../website/Dockerfile) runs the generated Next.js standalone server.
- The main site is `/`; the online booth is a separate `/online` route in [`website/src/app/online/page.tsx`](../../website/src/app/online/page.tsx).
- The booth is a client component with an explicit four-stage state machine: `layout`, `capture`, `look`, and `download`. The central transition function and completed download state in [`OnlinePhotobooth.tsx`](../../website/src/components/onlinePhotobooth/OnlinePhotobooth.tsx) are more reliable instrumentation points than generic click tracking.
- Inquiry calls to action are outbound links to the Instagram message URL defined in [`website/site.config.ts`](../../website/site.config.ts) and rendered in [`ClosingExperience.tsx`](../../website/src/components/ClosingExperience.tsx).
- No analytics SDK is currently listed in [`website/package.json`](../../website/package.json).

This architecture means a provider should be initialized once in the root layout, while the booth and inquiry components emit a small, stable event contract. It does not require a new backend merely to count the funnel.

## What can and cannot be measured

The site can reliably measure:

1. a page or route view;
2. opening the online booth;
3. beginning a booth session;
4. reaching a completed keepsake or clicking download; and
5. clicking an Instagram inquiry link.

An Instagram outbound click is **lead intent**, not a confirmed lead or customer. Once the browser opens Instagram, the FotoHAVN site cannot know whether the visitor sent a message, received a quote, booked, or paid. Closing that loop requires a separate business process, such as a dedicated inquiry form with a success state, a CRM/status record, or manual source tagging when an Instagram conversation becomes a qualified lead. None of the page analytics products below can infer that conversion from the outbound click alone.

## Recommended minimal event contract

Use a deliberately small schema and avoid sending photos, image data, imported filenames, email addresses, phone numbers, message content, or camera details.

| Event | When to emit | Why it matters | Safe optional property |
| --- | --- | --- | --- |
| `page_view` | Automatic on `/` and `/online` | Funnel entry and route traffic | normalized path only |
| `booth_started` | First transition from template selection into capture | Distinguishes use from a route visit | `template_id` only if it is a fixed public enum |
| `booth_completed` | First arrival at the download stage | Strongest signal that the experience delivered value | `template_id`, `filter_id` as fixed public enums |
| `keepsake_downloaded` | Download PNG activation | Stronger completion signal, useful but optional | none |
| `inquiry_clicked` | Before navigation to the Instagram message link | Lead-intent proxy | `placement`, such as `closing_primary` |

The core decision funnel should be:

`page_view /` -> `booth_started` -> `booth_completed` -> `inquiry_clicked`

Also keep a shorter acquisition funnel:

`page_view /` -> `inquiry_clicked`

This separates visitors who were persuaded directly by the marketing page from visitors who first experienced the online booth.

## Ranked options

The ranking prioritizes: complete requested funnel at $0, implementation effort, current architecture fit, and privacy/governance burden.

| Rank | Option | Cash cost at current scale | Complexity | Complete requested funnel? | Best use |
| --- | --- | --- | --- | --- | --- |
| 1 | PostHog Cloud | $0 up to 1M analytics events/month; collection stops at the free limit without billing | Medium | Yes | Best fit for a privacy-minimized product funnel and future segmentation |
| 2 | Google Analytics 4 | $0 for standard Analytics | Medium | Yes | Campaign, referral, acquisition, and outbound-click analysis |
| 3 | Microsoft Clarity | $0; vendor states free forever with no traffic limits | Low to install; medium to configure safely | Yes | Marketing-page behavior diagnosis, subject to masking and audience constraints |
| 4 | Self-hosted Umami | Software is $0/MIT; infrastructure and operations may not be | High | Yes | Maximum data control when spare server/database capacity and ops ownership already exist |
| 5 | Cloudflare Web Analytics | $0 and available on all plans | Very low | No | Privacy-first page traffic and web performance only |
| 6 | Vercel Web Analytics Hobby | $0 for 50,000 events/month | Low only on Vercel | No at $0 | Basic page traffic for a Vercel-hosted site |

### 1. PostHog Cloud

Why it ranks first:

- The free plan includes one million analytics events per month, 5,000 replay recordings per month, unlimited members, one project, and one-year retention. With no card, collection stops at the limit rather than generating a surprise charge. [PostHog pricing](https://posthog.com/pricing)
- The official Next.js guide covers App Router client initialization, custom event capture, server events, and a free managed reverse proxy for Cloud users. [PostHog Next.js guide](https://posthog.com/docs/libraries/next-js)
- Page views and page leaves are automatically captured on history changes, including single-page applications. [PostHog autocapture](https://posthog.com/docs/product-analytics/autocapture)
- Funnels support event or page-view steps, sequential/strict/any order, time-to-convert, trends, properties, and user paths. [PostHog funnels](https://posthog.com/docs/product-analytics/funnels)
- Cookieless mode can avoid browser cookies and local/session storage and use a privacy-preserving server hash for anonymous counts. This does not remove FotoHAVN's disclosure and legal-assessment duties, but it reduces the default data footprint. [PostHog privacy controls](https://posthog.com/docs/privacy/data-collection)

Why it is medium complexity:

- Its default product analytics surface is broader than FotoHAVN currently needs. Autocapture, person profiles, replay, and IP capture must be deliberately configured rather than accepted without review.
- PostHog's privacy guidance says the site owner is responsible for what is collected and for communicating it. It provides opt-out-by-default, consent-manager hooks, masking, cookieless tracking, and IP-capture controls. [PostHog privacy controls](https://posthog.com/docs/privacy/data-collection)
- PostHog's GDPR guide advises explicit consent for analytics that processes personal data and a cookie banner when cookies are used. Cloud EU disables IP capture by default for new projects; the project setting should still be verified. [PostHog GDPR guidance](https://posthog.com/docs/privacy/gdpr-compliance)

Recommended PostHog configuration: use Cloud EU if EU traffic matters; enable `cookieless_mode: "always"`; set `person_profiles: "never"`; disable session replay, broad autocapture, surveys, and IP capture; do not call `identify`; and emit only the explicit funnel events. Confirm the resulting legal/consent position for FotoHAVN's actual audience and jurisdictions before launch.

### 2. Google Analytics 4

Why it ranks second:

- Google describes the standard Analytics tools as free of charge. [Google Analytics](https://marketingplatform.google.com/about/analytics/)
- Enhanced Measurement can automatically record outbound clicks without code changes and exposes link domain, link URL, and outbound dimensions. This directly fits the Instagram inquiry link. [GA4 outbound clicks](https://support.google.com/analytics/answer/13566436)
- GA4 supports recommended and custom events, event parameters, and funnel explorations of up to ten steps. [GA4 events](https://support.google.com/analytics/answer/9322688), [GA4 event parameters](https://support.google.com/analytics/answer/13675006), [GA4 funnels](https://support.google.com/analytics/answer/9327974)
- Google provides explicit SPA guidance for automatic history-change page views or custom virtual page views. [GA4 SPA measurement](https://developers.google.com/analytics/devguides/collection/ga4/single-page-applications)
- Standard-property limits are far above likely FotoHAVN needs: 30 key events, 50 event-scoped custom dimensions, up to 14 months retention, and a 10-million-event exploration sampling threshold. [GA4 configuration limits](https://support.google.com/analytics/answer/12229528)

Privacy and complexity caveats:

- The default web implementation stores a client ID in the first-party `_ga` cookie. Analytics does not store it when analytics storage is deactivated through Consent Mode. [GA4 data collection](https://support.google.com/analytics/answer/11593727)
- Basic Consent Mode blocks Google tags and sends no data before consent; advanced mode can send cookieless pings while consent is denied. Consent Mode does not supply the banner itself. [Google Consent Mode](https://support.google.com/analytics/answer/10000067)
- GA4 is the most capable option here for traffic-source and advertising analysis, but configuring events, custom definitions, funnel explorations, consent, and report interpretation is heavier than a minimal PostHog event contract.

Choose GA4 first only if the initial business question is "which campaign or referrer produces inquiry clicks?" rather than "how many visitors progress through the online booth before showing inquiry intent?"

### 3. Microsoft Clarity

Why it ranks third:

- Microsoft states that Clarity is free forever, has no traffic limit, and does not sample traffic. [Clarity FAQ](https://learn.microsoft.com/en-us/clarity/faq)
- It supports session recordings, heatmaps, event tracking, and funnels. Funnels can combine page visits, automatically recognized events, Smart Events, and explicit events. [Clarity overview](https://learn.microsoft.com/en-us/clarity/setup-and-installation/about-clarity), [funnels](https://learn.microsoft.com/en-us/clarity/setup-and-installation/funnels)
- The client API accepts explicit events such as `window.clarity("event", "booth_completed")`; these events appear in filters, dashboard, settings, and recordings. [Clarity client API](https://learn.microsoft.com/en-us/clarity/setup-and-installation/clarity-api)
- Installation is one asynchronous script or an npm package in the site's shared layout. Microsoft says any site architecture is supported. [Clarity setup](https://learn.microsoft.com/en-us/clarity/setup-and-installation/clarity-setup)

Important constraints:

- Clarity cannot render content inside canvas elements or third-party iframes. The booth flow therefore needs explicit start/complete events; recordings should be treated as interaction context, not evidence of the captured photo. [Clarity FAQ](https://learn.microsoft.com/en-us/clarity/faq)
- It captures detailed interaction data such as mouse movement, clicks, scrolls, and page rendering. Its default Balanced masking focuses on content classified as sensitive; FotoHAVN should explicitly mask the entire online-booth root with `data-clarity-mask="true"` or use Strict mode so customer photographs and previews are never uploaded. Masked data is not sent to Clarity. [Clarity masking](https://learn.microsoft.com/en-us/clarity/setup-and-installation/clarity-masking)
- Full session continuity relies on first-party cookies. Microsoft requires explicit consent before cookies for EEA/UK/Switzerland visitors; without consent, funnel tracking and recordings may be affected. [Clarity consent mode](https://learn.microsoft.com/en-us/clarity/setup-and-installation/consent-mode), [Clarity FAQ](https://learn.microsoft.com/en-us/clarity/faq)
- Microsoft says Clarity should not be used on websites or apps targeting users under 18. FotoHAVN must confirm that the website targets adult purchasers/event organizers, even if photographs may include families. [Clarity FAQ](https://learn.microsoft.com/en-us/clarity/faq)
- Recorded data is available for up to 30 days according to the FAQ, so Clarity is more useful for current diagnosis than long-range trend storage. [Clarity FAQ](https://learn.microsoft.com/en-us/clarity/faq)

Recommended Clarity configuration if added later: restrict it to the marketing route if possible, enable consent mode, use Strict masking or explicitly mask the entire `/online` experience, do not identify visitors, and verify from an actual recording that no customer photograph or preview leaves the device.

### 4. Self-hosted Umami

Why it is attractive:

- Umami is MIT-licensed open-source software; self-hosting gives FotoHAVN control of the analytics data. [Umami license](https://github.com/umami-software/umami/blob/master/LICENSE), [Umami overview](https://docs.umami.is/docs/about)
- Umami says it uses no cookies and collects no personal data by default, and presents itself as GDPR/CCPA compliant out of the box. This is a vendor claim, not a substitute for FotoHAVN's own privacy assessment. [Umami overview](https://docs.umami.is/docs/about)
- It automatically tracks Next.js/SPA page navigation, supports custom events and properties, documents outbound-link events, and provides ordered funnels with page-view and event steps. [Collect data](https://docs.umami.is/docs/collect-data), [track events](https://docs.umami.is/docs/track-events), [funnels](https://docs.umami.is/docs/funnel)

Why it ranks fourth:

- The software is free, but the official install requires a Node server and PostgreSQL database, plus proxying, updates, backups, monitoring, security, and incident ownership. Docker Compose simplifies installation but does not remove the operational work. [Umami installation](https://docs.umami.is/docs/install)
- It is only "no additional cost" if FotoHAVN already has safe spare compute, storage, backups, and database capacity. Otherwise it shifts vendor subscription cost into infrastructure and operator time.
- It adds another deployed application to a site that currently needs only a few events. This is disproportionate until data residency or ownership becomes a firm requirement.

### 5. Cloudflare Web Analytics

Why it is attractive:

- Cloudflare Web Analytics is free and can be added with a JavaScript beacon without moving DNS or proxying the site. [Cloudflare Web Analytics](https://developers.cloudflare.com/web-analytics/about/)
- Cloudflare says it does not use cookies or local storage, fingerprint visitors, or collect/use visitors' personal data for Web Analytics. [Cloudflare Web Analytics privacy](https://www.cloudflare.com/web-analytics/)
- It automatically tracks SPA route changes. [Cloudflare SPA analytics](https://developers.cloudflare.com/web-analytics/get-started/web-analytics-spa/)

Why it cannot meet this request alone:

- Cloudflare's official FAQ states that Web Analytics does not support custom events or UTM parameters. It therefore cannot distinguish a booth completion from an ordinary `/online` view or build the requested booth-to-inquiry event funnel. Data is accessible for the prior six months. [Cloudflare Web Analytics FAQ](https://developers.cloudflare.com/web-analytics/faq/)

Use it only for a privacy-first traffic/performance baseline, not as the funnel system.

### 6. Vercel Web Analytics Hobby

Why it ranks last for the current site:

- The Hobby plan includes 50,000 events per month and a one-month reporting window, but custom events are unavailable. Vercel defines an event as a page view or custom event; Hobby collection pauses after the allowance rather than charging. [Vercel Web Analytics pricing](https://vercel.com/docs/analytics/limits-and-pricing)
- Custom events are explicitly restricted to Pro and Enterprise plans, so the $0 tier cannot track `booth_completed` or `inquiry_clicked` as first-class events. [Vercel custom events](https://vercel.com/docs/analytics/custom-events)
- Vercel describes Web Analytics as aggregated and anonymous, without third-party cookies; it automatically tracks client-side transitions. [Vercel privacy and compliance](https://vercel.com/docs/analytics/privacy-policy)
- The official quickstart assumes a Vercel project and deployment. FotoHAVN currently ships a standalone Docker server, so adopting this option would also introduce a hosting-platform dependency. [Vercel quickstart](https://vercel.com/docs/analytics/quickstart)
- Vercel's documentation reviewed for this report does not advertise a sequential funnel report. Treat this as an undocumented capability, not proof that no workaround is possible.

At $0, Vercel Analytics is a page-view counter for this use case, not a funnel solution.

## Recommended rollout

### Phase 1: one provider and five events

Use **PostHog Cloud** for a four-week validation period, subject to privacy review.

1. Add the client SDK once through the Next.js client-instrumentation seam.
2. Configure cookieless, event-only collection; disable replay, broad autocapture, person profiles, identification, surveys, and IP capture.
3. Add the appropriate privacy notice and consent handling for FotoHAVN's actual audience and jurisdictions before production collection.
4. Emit `booth_started`, `booth_completed`, optional `keepsake_downloaded`, and `inquiry_clicked` without personal data.
5. Build the full and short funnels defined above.
6. Verify production requests and events on both desktop and mobile, including a real outbound Instagram navigation.
7. Review weekly counts, conversion rates, and stage drop-off without collecting the booth's photographs or replaying the experience.

### Phase 2: decide from evidence

After four weeks:

- Keep PostHog if the useful questions are about booth progression, completion, CTA intent, or template/filter correlations.
- Replace it with or add GA4 if campaign/source attribution becomes the main need.
- Consider Clarity on the marketing route only if heatmaps or recordings are needed, after the audience-policy and privacy checks.
- Consider self-hosted Umami only when control/data-residency requirements justify running a database-backed service.

## Privacy and governance guardrails

- Publish an analytics disclosure before enabling production tracking. Name the provider, purpose, categories of data, retention, recipients/processor, choices, and contact path appropriate to FotoHAVN's applicable law.
- Do not send captured photos, previews, camera streams, file names, free text, Instagram handles, email addresses, phone numbers, or exact message contents to analytics. If the provider supports replay, explicitly mask or disable replay for the online-booth root; do not assume generic sensitive-text masking also protects every image.
- Do not create persistent identities for anonymous visitors. Aggregate funnel measurement is sufficient for the stated goal.
- Keep event names and properties fixed and documented. Treat additions as privacy-impacting changes that need review.
- Gate or configure analytics according to the selected provider's consent mechanism. "Cookie-free" lowers risk but does not by itself prove that no notice, lawful basis, or consent is required in every jurisdiction.
- Test with browser developer tools that denied consent actually prevents the disallowed collection.
- Keep a clear distinction in reports between `inquiry_clicked` (intent), qualified lead, booking, and paid customer.

## Evidence limits

All product capabilities, prices, and limits were checked against first-party documentation on 2026-09-18 and can change. "Free" refers to provider/license price within the stated limits; it does not include engineering time, compliance work, or self-hosting infrastructure. Vendor compliance claims are not legal advice. The rankings are an architectural inference for this repository's current size and flow, not a general ranking of analytics products.
