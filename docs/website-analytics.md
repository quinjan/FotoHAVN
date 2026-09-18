# Website analytics contract

Production setup and verification: [`website/docs/microsoft-clarity-production-runbook.md`](../website/docs/microsoft-clarity-production-runbook.md).

FotoHAVN uses Microsoft Clarity as its only website analytics provider. The integration is optional at runtime and fails open: rendering, the Online Booth Cycle, downloads, and outbound navigation continue if Clarity is missing, blocked, or throws.

## Production configuration

Set the GitHub repository variable `FOTOHAVN_CLARITY_PROJECT_ID` to the public Clarity project ID. The production workflow passes it into the immutable image as `NEXT_PUBLIC_CLARITY_PROJECT_ID` at build time. Local development may set the same `NEXT_PUBLIC_` variable in an ignored `.env.local` file.

Before release, configure the Clarity project as follows:

- Turn off Clarity's default cookie setting so Consent Mode requires an explicit signal.
- Keep Clarity masking enabled. The complete online-booth root is additionally marked with `data-clarity-mask="true"` before customer-created media can render.
- Do not configure Identify API calls, unmask selectors, or smart events that expand the fixed contract below.
- Block internal testing IP addresses if they would distort the production reports.

Production release is blocked until the project ID, Consent Mode setting, actual cookie behavior, masking recording, and collection payloads have been verified against the HTTPS origin.

## Consent meanings

- `unknown`: no remembered choice. Clarity receives denied analytics and advertising storage signals, so only isolated Cookieless Analytics Page Activity is eligible. The prompt remains visible.
- `cookieless`: the visitor explicitly continued without cookies. The decision is remembered, but Clarity remains in no-consent mode.
- `cookies`: the visitor allowed Clarity's analytics cookie. Advertising storage remains denied. Future pages can form a Consented Anonymous Journey.

The preference is versioned and expires six calendar months after the decision. Missing, unreadable, expired, or older-version storage returns to `unknown`. Changing from cookieless to cookies closes the current cookieless activity before granting continuity. Withdrawal sends denied consent, clears provider cookies through Clarity's supported consent command, and begins new cookieless activity.

## Fixed event contract

Events have names only. They carry no custom properties, identifiers, free text, filenames, URLs, camera details, media data, or contact details.

| Event | Meaning |
| --- | --- |
| `section-reach-experience` | Experience reached the Section Reach threshold. |
| `section-engagement-experience` | Experience reached the Section Engagement threshold. |
| `section-reach-the-booth` | The Booth reached the Section Reach threshold. |
| `section-engagement-the-booth` | The Booth reached the Section Engagement threshold. |
| `section-reach-prints` | Prints reached the Section Reach threshold. |
| `section-engagement-prints` | Prints reached the Section Engagement threshold. |
| `section-reach-guest-album` | Guest Album reached the Section Reach threshold. |
| `section-engagement-guest-album` | Guest Album reached the Section Engagement threshold. |
| `section-reach-inquiry` | Inquiry reached the Section Reach threshold. |
| `section-engagement-inquiry` | Inquiry reached the Section Engagement threshold. |
| `online-booth-started` | First transition from template selection into photographs for one Online Booth Cycle. |
| `online-booth-completed` | First arrival at the finished keepsake state for one Online Booth Cycle. |
| `online-booth-downloaded` | First download activation for one Online Booth Cycle. |
| `online-physical-booth-invitation` | Explicit activation of the physical-booth invitation in the online route. |
| `find-booth-inquiry-intent` | Explicit activation of the Find the Booth Instagram action. |
| `rent-fotohavn-inquiry-intent` | Explicit activation of the Rent FOTOHAVN Instagram action. |

Section Reach requires at least 50 percent visibility for one continuous second while the page is visible. Section Engagement requires five cumulative eligible seconds. Each section event is emitted once per page view. Online Booth Cycle events deduplicate within one in-memory cycle and reset only after starting another keepsake or refreshing.

Adding or renaming an event, adding a property, using an identity API, or changing masking is a privacy-impacting change and requires explicit review.

## Report contracts

### Homepage Progression

- Denominator: all eligible homepage page activity, including cookieless page activity and Consented Anonymous Journeys.
- Compare Section Reach and Section Engagement across Experience, The Booth, Prints, Guest Album, and Inquiry.
- Do not interpret Clarity's cookieless unique-user or multi-page funnel counts as journeys.

### Online Booth Completion

- Denominator: all eligible `/online` page activity, including cookieless page activity and Consented Anonymous Journeys.
- Compare route activity, `online-booth-started`, `online-booth-completed`, and `online-booth-downloaded`.
- A download activation is not proof that the file was retained after the browser handled it.

### Consented Inquiry Journey

- Denominator: Consented Anonymous Journeys only, filtered by Clarity's granted analytics-storage consent status.
- Compare anonymous page and booth activity ending in either Inquiry Intent event.
- Never combine cookieless page views with this denominator. Never call Inquiry Intent a lead, message, booking, sale, or customer.

## Browser acceptance checklist

Use the test sink `window.__fotohavnAnalyticsTestSink = (record) => { ... }` before interaction to observe the fixed consent and event calls without sending customer data. Before release, repeat the checks against real Clarity on production HTTPS:

1. Verify unknown, allow, continue-without-cookies, reload, expiry, policy-version change, and withdrawal states.
2. Confirm `_clck` and `_clsk` are absent before consent and after decline or withdrawal, and present only after allow.
3. Inspect `/collect` requests for prohibited photographs, previews, canvas pixels, data or blob URLs, filenames, metadata, camera details, contact information, and free text.
4. Review an actual `/online` recording and confirm the full booth is masked before capture or import begins.
5. Run synthetic journeys for the three report contracts and keep cookieless and consented denominators separate.
