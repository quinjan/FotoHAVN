# Final local verification

Date: 2026-09-09. Branch: `codex/website-real-booth-redesign`.

Result: **passed for the local redesign scope**. Deployment is not performed; existing dependency-security advisories remain a separate release concern described in `README.md`.

## Build and tests

| Check                     | Result                                                                                 |
| ------------------------- | -------------------------------------------------------------------------------------- |
| `npm run lint`            | Passed, no lint errors or warnings                                                     |
| `npx tsc --noEmit`        | Passed                                                                                 |
| `npm run test:booth`      | 4/4 passed; also independently rerun by reviewers                                      |
| `npm run build`           | Passed; home and not-found statically prerendered                                      |
| `git diff --check -- .`   | Passed; Git emitted only local LF/CRLF conversion notices                              |
| Responsive photo variants | 77/77 present, independently verified                                                  |
| Production console        | No warning/error entries attributable to the final `localhost:3002` production preview |

The machine's existing npm `email` configuration emits a package-manager deprecation warning. It is not an application lint/runtime error.

## Browser evidence

Final production preview was the assembled standalone server at `http://localhost:3002/fotohvn`; development preview at port 3000 was also used during repairs. Final captures were refreshed against production after the last CSS changes. Native pointer/keyboard operations used the in-app Chromium browser.

| CSS viewport, verified with `innerWidth`/`innerHeight` | Document client/scroll width | Returned viewport-image size | Main evidence                                                                                                                     |
| ------------------------------------------------------ | ---------------------------- | ---------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| 1440 × 900                                             | 1425 / 1425                  | 1425 × 891                   | desktop-hero, desktop-experience, desktop-booth, desktop-inside, desktop-prints, desktop-gallery, desktop-inquiry, desktop-footer |
| 820 × 1180                                             | 805 / 805                    | 805 × 1158                   | tablet-820-hero, tablet-820-booth                                                                                                 |
| 390 × 844                                              | 375 / 375                    | 375 × 812                    | mobile-390-hero, mobile-390-booth                                                                                                 |
| 320 × 720                                              | 305 / 305                    | 305 × 686                    | mobile-320-hero, mobile-320-booth, mobile-320-inquiry, mobile-320-inquiry-submit                                                  |

Evidence files above have a `.png` suffix. CUA returned image dimensions different from the configured CSS viewport; the actual CSS dimensions were read from the page and the image dimensions were checked with Sharp. The 15px document-width difference is the browser scrollbar. Do not describe raw images as exact 320/390/820/1440-pixel-wide files.

The provisional full-page screenshot was excluded: an offscreen, suspended WebGL canvas is not valid evidence of an in-viewport renderer failure. Viewport-only model captures show the complete sign, cabinet and plinth. The final small-phone repair reserves space above the rotate/zoom toolbar.

### Verified interactions

- Real photos load from static responsive WebP paths; visible completed images have nonzero natural dimensions. The real-photo comparison loads correctly.
- Overview, Front, Side and Inside presets, curtain open/close, reset, zoom and pointer rotation were exercised. Inside reveals the camera/touchscreen; reset returns to the closed exterior.
- Actual keyboard input on the focused viewer changes orientation; Up marks the state Custom. Button Space operation was checked. Scene regression tests separately verify camera position and reduced-motion settling.
- Mobile menu opens by pointer/Space, closes on Escape, restores menu-button focus, and closes after navigation. The final rental button fits at 320px.
- Guest album Next changes desktop `scrollLeft` from 0 to 440; Previous restores the earlier photos. Mobile gallery buttons/native scrolling and keyboard operation were also checked during implementation.
- Actual GSAP transforms change with scroll: the print moved from a 17.75px translated matrix at its section to zero translation farther down the page; guest-photo transforms settle at their planned positions. Hero entry and scene transitions were visually checked.
- Inquiry validation prevents an empty submit, focuses Intent, and leaves the URL local. Required invalid fields were Intent/Name/Email. No valid submission or external email was sent.
- Rental inquiry and return-to-top links work. Anchor headings remain clear of the sticky navigation after settling.
- Document widths show no horizontal page overflow at all four sizes. At 320px the main CTA and submit CTA compute to 12px; submit is 246.22px wide and remains within the content area.

### Verification limits

- Chromium viewport emulation is not a physical iPhone/Android or cross-browser test.
- Reduced-motion paths were source-reviewed and covered by the scene test harness; an OS-level reduced-motion browser session was not emulated. No-JS progressive enhancement was source/SSR-reviewed, not a separate disabled-JavaScript browser run.
- GPU/context/texture failure cleanup is covered by the focused harness and source review, not a forced real-device GPU failure.
- Independent reviewers reviewed source and supplied rendered screenshots; direct browser interactions were performed by the parent. They did not claim independent live-device testing.
- Email delivery, public hosting, SEO indexing and exact booth dimensions were not verified or changed.

## Lighthouse

Final reports were generated against the production standalone server, with isolated headless Chrome and Lighthouse's mobile/desktop presets. These are local synthetic measurements, not field Core Web Vitals.

| Metric                   | Mobile | Desktop |
| ------------------------ | ------ | ------- |
| Performance              | 93     | 100     |
| Accessibility            | 100    | 100     |
| Best practices           | 100    | 100     |
| SEO                      | 63     | 63      |
| Largest Contentful Paint | 3.3 s  | 0.7 s   |
| Cumulative Layout Shift  | 0      | 0       |
| Total Blocking Time      | 10 ms  | 0 ms    |

Reports: `lighthouse-mobile-final.report.html` / `.json` and `lighthouse-desktop-final.report.html` / `.json`. The retained staging `noindex` policy intentionally limits SEO scoring. Automated accessibility scoring is not an accessibility certification. Mobile LCP remains slower than the 2.5-second good threshold under the synthetic throttle.

Commands used:

```powershell
npx --yes lighthouse http://localhost:3002/fotohvn --port=9227 --quiet --only-categories=performance,accessibility,best-practices,seo --output=json --output=html --output-path=.handoffs/real-booth-redesign/lighthouse-mobile-final
npx --yes lighthouse http://localhost:3002/fotohvn --port=9227 --quiet --preset=desktop --only-categories=performance,accessibility,best-practices,seo --output=json --output=html --output-path=.handoffs/real-booth-redesign/lighthouse-desktop-final
```

## Windows-only audit assembly note

The audit used `.next/standalone/server.js` with `PORT=3002`, `HOSTNAME=localhost`, and copies of `public/` plus `.next/static/` in the standalone tree. Local Windows output tracing omitted Sharp's two sibling libvips DLLs, so those were copied from the installed `@img/sharp-win32-x64/lib` directory into the generated standalone equivalent. This changed only generated audit output, not production source or deployment configuration.

During investigation, the missing DLLs caused the local optimizer to return full-size originals. Final visible event photographs now use pre-generated responsive static WebPs, independently of image-optimizer cold requests. Stop the owned standalone process before rebuilding on Windows to avoid locking its generated native libraries. The production audit server and isolated audit Chrome are temporary; the development server is the user-facing preview.
