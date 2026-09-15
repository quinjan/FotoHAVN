# Review context

- Approved target: `selected-option-1.png`, 853 x 1844 source pixels, approximately 390 x 844 CSS pixels.
- Plan: `gpt-taste-design-plan.md`. Exact first displayed ImageGen result, not the earlier textual option numbered 1.
- Local route already returns HTTP 200: `http://localhost:3000/fotohavn#guest-album`.
- This is an existing Next.js website section, not a new mobile app. Preserve unrelated files and historical QA entries.
- All actual photographs, clips, and metal texture already exist. Use originals; no generated replacement faces.
- Baseline eight board tests passed using `node --test --test-isolation=none scripts/guest-board.test.mjs` from `website/`. Plain `node --test` encounters sandbox spawn EPERM.
- Browser: use `mcp__cua_repl`, create your own hidden tab in `iab`, inspect its initial docs. Browser viewport capability is shared, so reviewers must own browser use sequentially.
- CUA APIs expose viewport capability through `cua.getBrowser({id:"1"})`, then read `(await browser.capabilities.get("viewport")).documentation()` before use. Set viewport only for required responsive checks.
- Screenshot saving works using `await import("node:fs/promises")` then `writeFile(absolutePath, await tab.screenshot({fullPage:false}))`.
- UI reads can use read-only `tab.playwright.evaluate`; interactions use CUA actions or supported locators, never mutating evaluate.
- After layout settles, the existing `SEE THE GOOD COMPANY` anchor link provides a stable jump to the section. Its initial hash position can shift during preceding section hydration.
- Plan conformance precedes responsive QA. Each reviewer reports actual checked states, limitations, and evidence; do not claim skipped gestures or console checks passed.
- Final root `design-qa.md` entry should be appended, preserving unrelated previous work.
