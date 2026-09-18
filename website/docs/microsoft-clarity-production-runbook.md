# Microsoft Clarity production setup runbook

Use this runbook once for the FotoHAVN production Clarity project, then repeat the verification section before every analytics-affecting release. The website already owns the tracking script, consent interface, fixed custom-event contract, and complete Online Booth masking. Do not paste a second tracking snippet into the site, Caddy, or another tag manager.

## Expected result

- Clarity measures privacy-masked Cookieless Analytics Page Activity when analytics-cookie permission is unknown or declined.
- Clarity uses its anonymous first-party analytics cookie only after the visitor selects `Allow analytics cookies`.
- Advertising storage remains denied in every FotoHAVN consent state.
- The complete Online Booth is masked. Customer-created photographs, previews, canvas pixels, filenames, blob or data URLs, camera details, contact information, and free text must not appear in recordings or collection requests.
- Only the event names documented in `../../docs/website-analytics.md` are emitted by FotoHAVN.

## 1. Create the Clarity project

1. Sign in at [clarity.microsoft.com](https://clarity.microsoft.com/).
2. Select **New project**.
3. Use a clear production name such as `FotoHAVN Production`.
4. Set the website to `https://fotohavn.com`.
5. Finish project creation, then open **Settings > Overview**.
6. Copy the **Project ID** exactly. It is a public configuration value, not a password or API secret.

Do not install Clarity's copied tracking snippet. The Next.js application loads `https://www.clarity.ms/tag/<project-id>` through its typed analytics boundary.

Reference: [Microsoft Clarity getting started](https://learn.microsoft.com/en-us/clarity/setup-and-installation/getting-started).

## 2. Require explicit cookie permission

1. Open the project and go to **Settings > Setup**.
2. Turn the Clarity cookie setting **Off** so the provider waits for an explicit consent signal.
3. Do not connect a second Consent Management Platform. FotoHAVN already sends the current `consentv2` signal.

The website always sends `ad_Storage: denied`. It sends `analytics_Storage: denied` for unknown or cookieless states and `analytics_Storage: granted` only after the visitor allows analytics cookies.

Reference: [Clarity Consent Mode](https://learn.microsoft.com/en-us/clarity/setup-and-installation/consent-mode).

## 3. Configure masking

1. Go to **Settings > Masking**.
2. Keep the project-wide mode at **Balanced**. Do not select Relaxed.
3. Do not add any `data-clarity-unmask` rule or dashboard unmask selector.
4. Confirm the application-owned `/online` root appears masked in a recording. The source marks the complete root with `data-clarity-mask="true"`, which overrides the project-wide mode for that subtree.
5. After changing a dashboard masking rule, allow up to one hour before judging a new recording. Masking changes are not retroactive.

Reference: [Clarity masking](https://learn.microsoft.com/en-us/clarity/setup-and-installation/clarity-masking).

## 4. Add the GitHub production environment variable

The deployment workflow reads this environment-scoped configuration:

```text
Environment: production
Variable: FOTOHAVN_CLARITY_PROJECT_ID
Value: <the exact Clarity Project ID>
```

The workflow maps it to `NEXT_PUBLIC_CLARITY_PROJECT_ID` during both the Next.js verification build and immutable container build. The `publish` job references the `production` environment with `deployment: false`, so it can read the variable without creating a duplicate GitHub deployment record. Existing production protection rules still apply.

Manual GitHub setup:

1. Open `quinjan/FotoHAVN` on GitHub.
2. Go to **Settings > Environments > production**.
3. Under **Environment variables**, select **Add variable**.
4. Enter `FOTOHAVN_CLARITY_PROJECT_ID` and the copied Project ID.
5. Save the variable.

GitHub CLI fallback for an authenticated repository owner:

```powershell
gh variable set FOTOHAVN_CLARITY_PROJECT_ID --env production --repo quinjan/FotoHAVN --body "<project-id>"
gh variable get FOTOHAVN_CLARITY_PROJECT_ID --env production --repo quinjan/FotoHAVN
```

The variable is intentionally not a GitHub secret because the Project ID is embedded in public browser JavaScript. Never place Microsoft account credentials or unrelated tokens in this variable.

References: [GitHub environment variables](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments) and [GitHub Actions variable API](https://docs.github.com/en/rest/actions/variables).

## 5. Release from GitHub Actions

1. Merge the verified implementation into `main`.
2. Open **Actions > Deploy FotoHAVN production**.
3. Run `publish-only` first if you want to build and publish the immutable candidate without changing production.
4. Run `deploy` when ready to update `https://fotohavn.com/`.
5. Approve the `production` environment gate when GitHub requests it.

The workflow performs lint, type checking, analytics contract tests, a production Next.js build, immutable container build, and container smoke test before publishing the digest. Deployment remains isolated to FotoHAVN and must not restart unrelated VPS services.

## 6. Verify consent and cookies on production HTTPS

Use a fresh private browser window for each scenario. In developer tools, keep the **Network**, **Application/Storage**, and **Console** panels open.

### Unknown preference

1. Clear FotoHAVN site data.
2. Load `https://fotohavn.com/`.
3. Confirm the dark consent rail appears.
4. Confirm Clarity `/collect` requests may occur.
5. Confirm `_clck` and `_clsk` do not exist.
6. Run:

```js
clarity("metadata", (data, upgrade, consent) => {
  console.log(consent);
}, false, true, true);
```

Expected analytics and advertising storage: denied.

### Continue without cookies

1. Select **Continue without cookies**.
2. Reload and navigate between `/` and `/online`.
3. Confirm the prompt stays dismissed and the Edge Notch remains available.
4. Confirm `_clck` and `_clsk` remain absent.
5. Confirm separate pages are not represented as one Consented Anonymous Journey.

### Allow analytics cookies

1. Start from cleared site data.
2. Select **Allow analytics cookies**.
3. Confirm the metadata reports granted analytics storage and denied advertising storage.
4. Confirm `_clck` and `_clsk` appear only after the choice.
5. Navigate between `/` and `/online` and confirm future page activity can form one Consented Anonymous Journey.
6. Confirm the earlier cookieless page activity was not joined retroactively.

### Withdraw permission

1. Open the Edge Notch, choose **Continue without cookies**, and save.
2. Confirm the explicit withdrawal explanation appears before the final action.
3. Select **Stop using cookies**.
4. Confirm `_clck` and `_clsk` are removed and subsequent activity returns to cookieless measurement.
5. Confirm previously retained anonymous analytics are not described as immediately deleted; they follow Clarity's configured retention.

Microsoft documents `_clck` and `_clsk` as the principal first-party Clarity cookies. Reference: [Clarity cookies](https://learn.microsoft.com/en-us/clarity/setup-and-installation/clarity-cookies).

## 7. Inspect Online Booth privacy

This is a blocking release gate.

1. Start a fresh `/online` recording.
2. Complete one synthetic Online Booth Cycle using non-personal test photographs.
3. Exercise camera capture or import, preview, arrangement, filters, completed keepsake, first download, and the physical-booth invitation.
4. Watch the recording and confirm the whole booth remains masked before any photograph renders.
5. Inspect every `clarity.ms/collect` request generated during the flow.
6. Stop the release if any request or recording contains photograph content, canvas pixels, preview data, a data URL, blob URL, filename, file metadata, camera/device detail, contact information, free text, Instagram handle, or another identifier.

Do not use real customer photographs for this validation.

## 8. Confirm the fixed event contract

After Clarity has processed the synthetic journey, confirm these events appear with no custom properties:

- `section-reach-experience`
- `section-engagement-experience`
- `section-reach-the-booth`
- `section-engagement-the-booth`
- `section-reach-prints`
- `section-engagement-prints`
- `section-reach-guest-album`
- `section-engagement-guest-album`
- `section-reach-inquiry`
- `section-engagement-inquiry`
- `online-booth-started`
- `online-booth-completed`
- `online-booth-downloaded`
- `online-physical-booth-invitation`
- `find-booth-inquiry-intent`
- `rent-fotohavn-inquiry-intent`

Do not call Clarity's Identify API, add custom tags, create a visitor ID, or add another event/property without privacy review and a documented contract change.

## 9. Build the three reports

Use the exact denominators from `../../docs/website-analytics.md`:

1. **Homepage Progression**: all appropriate cookieless and consented homepage activity; compare Section Reach and Section Engagement across the five named sections.
2. **Online Booth Completion**: all appropriate cookieless and consented `/online` activity; compare page activity, cycle starts, completions, and first downloads.
3. **Consented Inquiry Journey**: granted analytics-storage journeys only; compare prior page/booth activity with the two Inquiry Intent events.

Never use Clarity's cookieless user or cross-page funnel counts as a journey denominator. Never rename Inquiry Intent as a lead, message, booking, sale, or customer.

## 10. Record the release evidence

Capture and retain:

- the deployed immutable image digest;
- the Clarity Project ID and the date Consent Mode was checked;
- screenshots of unknown, cookieless, allowed, and withdrawn cookie storage;
- a redacted `/collect` payload inspection showing no prohibited values;
- one masked Online Booth recording identifier;
- synthetic results for all three report contracts;
- desktop, tablet, 390-pixel, and 320-pixel consent/settings screenshots;
- production console and HTTPS results.

Do not store Microsoft credentials, customer photographs, raw customer recordings, or other personal data in the repository.
