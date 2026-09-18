"use client";

// PROTOTYPE ONLY: three visual directions for analytics cookie consent.
// Question: Which consent and privacy presentation feels unmistakably FOTOHAVN
// while remaining honest, calm, and easy to decline?
// No cookies are written and Microsoft Clarity is never loaded.

import { ArrowLeftIcon, ArrowRightIcon, CheckIcon, XIcon } from "@phosphor-icons/react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent as ReactMouseEvent, type ReactNode } from "react";
import styles from "./AnalyticsConsentPrototype.module.css";

type Variant = "A" | "B" | "C";
type Consent = "undecided" | "cookieless" | "cookies";
type Surface = "prompt" | "settings" | "privacy" | null;

const variants: Array<{ id: Variant; name: string }> = [
  { id: "A", name: "Editorial dock" },
  { id: "B", name: "Margin note" },
  { id: "C", name: "Footer rail" },
];

const consentLabels: Record<Consent, string> = {
  undecided: "No choice yet",
  cookieless: "Continuing without cookies",
  cookies: "Analytics cookies allowed",
};

function ChoiceButtons({ onAllow, onCookieless }: {
  onAllow: () => void;
  onCookieless: () => void;
}) {
  return (
    <div className={styles.choiceActions}>
      <button className={styles.primaryAction} type="button" onClick={onAllow}>Allow analytics cookies</button>
      <button className={styles.secondaryAction} type="button" onClick={onCookieless}>Continue without cookies</button>
    </div>
  );
}

function ConsentPrompt({ variant, onAllow, onCookieless, onPrivacy }: {
  variant: Variant;
  onAllow: () => void;
  onCookieless: () => void;
  onPrivacy: () => void;
}) {
  if (variant === "C") {
    return (
      <section className={styles.prompt} aria-labelledby="analytics-prompt-heading">
        <div className={styles.railCopy}>
          <h2 id="analytics-prompt-heading">Analytics cookies</h2>
          <p>Basic privacy-masked analytics continue without cookies. Cookies let us understand one anonymous journey across pages. <button className={styles.inlinePrivacy} type="button" onClick={onPrivacy}>Click here for more information.</button></p>
        </div>
        <ChoiceButtons onAllow={onAllow} onCookieless={onCookieless} />
      </section>
    );
  }

  return (
    <section className={styles.prompt} aria-labelledby="analytics-prompt-heading">
      <div className={styles.promptHeading}>
        {variant === "B" && <p className={styles.kicker}>A NOTE ABOUT ANALYTICS</p>}
        <h2 id="analytics-prompt-heading">
          {variant === "A" ? <>Help us understand<br /><em>the journey.</em></> : <>A clearer picture.</>}
        </h2>
      </div>
      <div className={styles.promptBody}>
        <p>We use privacy-masked analytics to understand which parts of the site are reached and whether the online booth is completed. Allowing analytics cookies lets us connect this activity across pages as one anonymous journey. <button className={styles.inlinePrivacy} type="button" onClick={onPrivacy}>Click here for more information.</button></p>
        <p className={styles.photoPromise}>Your photographs stay on this device and are never uploaded.</p>
      </div>
      <ChoiceButtons onAllow={onAllow} onCookieless={onCookieless} />
    </section>
  );
}

function ConsentOption({ checked, title, copy, value, onChange }: {
  checked: boolean;
  title: string;
  copy: string;
  value: Exclude<Consent, "undecided">;
  onChange: (value: Exclude<Consent, "undecided">) => void;
}) {
  return (
    <label className={`${styles.consentOption} ${checked ? styles.selectedOption : ""}`}>
      <input type="radio" name="analytics-choice" checked={checked} value={value} onChange={() => onChange(value)} />
      <span><strong>{title}</strong><span>{copy}</span></span>
      {checked && <CheckIcon aria-hidden="true" size={20} />}
    </label>
  );
}

function AnalyticsSettings({ consent, onClose, onSave, onPrivacy }: {
  consent: Consent;
  onClose: () => void;
  onSave: (choice: Exclude<Consent, "undecided">) => void;
  onPrivacy: () => void;
}) {
  const [draft, setDraft] = useState<Exclude<Consent, "undecided">>(consent === "cookies" ? "cookies" : "cookieless");
  const [confirmWithdrawal, setConfirmWithdrawal] = useState(false);
  const heading = useRef<HTMLHeadingElement>(null);

  useEffect(() => { heading.current?.focus(); }, []);

  function save() {
    if (consent === "cookies" && draft === "cookieless" && !confirmWithdrawal) {
      setConfirmWithdrawal(true);
      return;
    }
    onSave(draft);
  }

  return (
    <div className={styles.surfaceBackdrop} role="presentation" onMouseDown={(event) => {
      if (event.target === event.currentTarget) onClose();
    }}>
      <section className={styles.settingsPanel} role="dialog" aria-modal="true" aria-labelledby="analytics-settings-heading">
        <button className={styles.closeButton} type="button" onClick={onClose} aria-label="Close analytics settings"><XIcon size={20} aria-hidden="true" /></button>
        <p className={styles.kicker}>YOUR PRIVACY CHOICE</p>
        <h2 id="analytics-settings-heading" ref={heading} tabIndex={-1}>Analytics settings</h2>
        <p className={styles.settingsIntro}>Basic privacy-masked activity is measured without cookies. Choose whether we may connect pages into one anonymous journey. <button className={styles.inlinePrivacy} type="button" onClick={onPrivacy}>Click here for more information.</button></p>
        <fieldset className={styles.options}>
          <legend>Choose how analytics work</legend>
          <ConsentOption checked={draft === "cookies"} title="Allow analytics cookies" copy="Connect activity across pages for up to six months." value="cookies" onChange={(value) => { setDraft(value); setConfirmWithdrawal(false); }} />
          <ConsentOption checked={draft === "cookieless"} title="Continue without cookies" copy="Keep each measured page visit separate." value="cookieless" onChange={(value) => { setDraft(value); setConfirmWithdrawal(false); }} />
        </fieldset>
        {confirmWithdrawal && (
          <div className={styles.withdrawalNote} role="alert">
            <strong>Stop connecting future activity?</strong>
            <p>Existing anonymous analytics remain until Microsoft Clarity&apos;s standard retention expires.</p>
          </div>
        )}
        <div className={styles.settingsActions}>
          <button className={styles.primaryAction} type="button" onClick={save}>{confirmWithdrawal ? "Stop using cookies" : "Save choice"}</button>
          <button className={styles.secondaryAction} type="button" onClick={onClose}>Cancel</button>
        </div>
      </section>
    </div>
  );
}

function PrivacySection({ title, children }: { title: string; children: ReactNode }) {
  return <section className={styles.privacySection}><h2>{title}</h2><div>{children}</div></section>;
}

function PrivacyPage({ variant, onBack }: { variant: Variant; onBack: () => void }) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => { heading.current?.focus(); }, []);

  const content = (
    <>
      <PrivacySection title="What we measure"><p>We use Microsoft Clarity to understand which website sections are reached, whether the online booth is completed, and when an inquiry link is chosen.</p></PrivacySection>
      <PrivacySection title="What stays with you"><p>Your photographs, camera view, imported files, previews, and finished keepsakes stay on your device. They are never provided to Clarity.</p></PrivacySection>
      <PrivacySection title="Cookies and anonymous journeys"><p>Without analytics cookies, each measured page visit remains separate. With permission, Clarity uses an anonymous first-party cookie to connect activity across pages for up to six months.</p></PrivacySection>
      <PrivacySection title="Your choice"><p>You can change your choice at any time through the Analytics settings control at the page edge. Withdrawing permission stops future pages from joining the previous anonymous journey.</p></PrivacySection>
      <PrivacySection title="Inquiry and contact"><p>Clicking an Instagram inquiry link shows interest. It does not tell us who you are or whether you sent a message. For privacy questions, contact FOTOHAVN through its official Instagram account.</p></PrivacySection>
    </>
  );

  return (
    <div className={styles.privacyPage} role="dialog" aria-modal="true" aria-labelledby="privacy-page-heading">
      <header className={styles.privacyHeader}>
        <button className={styles.backButton} type="button" onClick={onBack}><ArrowLeftIcon size={18} aria-hidden="true" /> Back</button>
        <span>FOTOHAVN</span>
      </header>
      <main className={styles.privacyMain}>
        <div className={styles.privacyIntroduction}>
          <p className={styles.kicker}>WEBSITE PRIVACY</p>
          <h1 id="privacy-page-heading" ref={heading} tabIndex={-1}>How website<br /><em>analytics work.</em></h1>
          <p>A plain-language account of what is measured, what remains private, and the choice available to every visitor.</p>
        </div>
        {variant === "A" && (
          <div className={styles.privacySplit}>
            <nav aria-label="Privacy topics"><span>What we measure</span><span>What stays with you</span><span>Cookies</span><span>Your choice</span><span>Contact</span></nav>
            <div className={styles.privacyArticle}>{content}</div>
          </div>
        )}
        {variant === "B" && (
          <div className={styles.privacyLetter}>
            <aside><strong>In short</strong><p>Photographs stay local. Analytics remain anonymous.</p></aside>
            <div className={styles.privacyArticle}>{content}</div>
          </div>
        )}
        {variant === "C" && <div className={styles.privacyQuestions}>{content}</div>}
      </main>
      <footer className={styles.privacyFooter}><span>© 2026 FOTOHAVN</span><button type="button" onClick={onBack}>Return to your choice</button></footer>
    </div>
  );
}

function PrototypeSwitcher({ variant, surface, consent, onVariant, onSurface, onReset }: {
  variant: Variant;
  surface: Surface;
  consent: Consent;
  onVariant: (variant: Variant) => void;
  onSurface: (surface: Exclude<Surface, null>) => void;
  onReset: () => void;
}) {
  const current = variants.findIndex((item) => item.id === variant);
  const cycle = (direction: -1 | 1) => onVariant(variants[(current + direction + variants.length) % variants.length].id);
  return (
    <div className={styles.prototypeBar} aria-label="Prototype controls">
      <div className={styles.variantControls}>
        <button type="button" onClick={() => cycle(-1)} aria-label="Previous variant"><ArrowLeftIcon size={17} aria-hidden="true" /></button>
        <div><strong>{variant} - {variants[current].name}</strong><span>{consentLabels[consent]}</span></div>
        <button type="button" onClick={() => cycle(1)} aria-label="Next variant"><ArrowRightIcon size={17} aria-hidden="true" /></button>
      </div>
      <div className={styles.surfaceControls}>
        <button type="button" aria-pressed={surface === "prompt"} onClick={() => onSurface("prompt")}>Prompt</button>
        <button type="button" aria-pressed={surface === "settings"} onClick={() => onSurface("settings")}>Settings</button>
        <button type="button" aria-pressed={surface === "privacy"} onClick={() => onSurface("privacy")}>Privacy</button>
        <button type="button" onClick={onReset}>Reset</button>
      </div>
    </div>
  );
}

export default function AnalyticsConsentPrototype() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const enabled = process.env.NODE_ENV !== "production" && searchParams.get("analytics-prototype") === "1";
  const requestedVariant = searchParams.get("variant")?.toUpperCase();
  const variant: Variant = requestedVariant === "B" || requestedVariant === "C" ? requestedVariant : "A";
  const [consent, setConsent] = useState<Consent>("undecided");
  const [surface, setSurface] = useState<Surface>("prompt");
  const [edgeExpanded, setEdgeExpanded] = useState(false);
  const variantClass = useMemo(() => ({ A: styles.variantA, B: styles.variantB, C: styles.variantC })[variant], [variant]);

  const changeVariant = useCallback((next: Variant) => {
    const params = new URLSearchParams(searchParams.toString());
    params.set("analytics-prototype", "1");
    params.set("variant", next);
    router.replace(`${pathname}?${params.toString()}`, { scroll: false });
  }, [pathname, router, searchParams]);

  useEffect(() => {
    if (!enabled) return;
    const keydown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && surface && surface !== "prompt") {
        event.preventDefault();
        setSurface(consent === "undecided" ? "prompt" : null);
        return;
      }
      const target = event.target as HTMLElement | null;
      if (target?.matches("input, textarea, [contenteditable='true']")) return;
      if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
      const current = variants.findIndex((item) => item.id === variant);
      const direction = event.key === "ArrowLeft" ? -1 : 1;
      changeVariant(variants[(current + direction + variants.length) % variants.length].id);
    };
    window.addEventListener("keydown", keydown);
    return () => {
      window.removeEventListener("keydown", keydown);
    };
  }, [enabled, surface, consent, variant, changeVariant]);

  if (!enabled) return null;
  const choose = (choice: Exclude<Consent, "undecided">) => { setConsent(choice); setSurface(null); };
  const returnFromPrivacy = () => setSurface(consent === "undecided" ? "prompt" : "settings");
  const handleEdgeSettings = (event: ReactMouseEvent<HTMLButtonElement>) => {
    const usesTouchInteraction = window.matchMedia("(hover: none), (pointer: coarse)").matches || window.innerWidth <= 960;
    const wasKeyboardActivated = event.detail === 0;
    if (usesTouchInteraction && !wasKeyboardActivated && !edgeExpanded) {
      setEdgeExpanded(true);
      return;
    }
    setEdgeExpanded(false);
    setSurface("settings");
  };

  return (
    <div className={`${styles.prototypeRoot} ${variantClass}`} data-prototype-variant={variant}>
      {surface === "prompt" && <ConsentPrompt variant={variant} onAllow={() => choose("cookies")} onCookieless={() => choose("cookieless")} onPrivacy={() => setSurface("privacy")} />}
      {surface === "settings" && <AnalyticsSettings consent={consent} onClose={() => setSurface(consent === "undecided" ? "prompt" : null)} onSave={choose} onPrivacy={() => setSurface("privacy")} />}
      {surface === "privacy" && <PrivacyPage variant={variant} onBack={returnFromPrivacy} />}
      {consent !== "undecided" && surface === null && (
        <button
          className={`${styles.edgeSettings} ${edgeExpanded ? styles.edgeExpanded : ""}`}
          type="button"
          aria-label="Analytics settings"
          onBlur={() => setEdgeExpanded(false)}
          onClick={handleEdgeSettings}
        >
          <span className={styles.edgeSettingsLabel}>Analytics settings</span>
        </button>
      )}
      <PrototypeSwitcher variant={variant} surface={surface} consent={consent} onVariant={changeVariant} onSurface={setSurface} onReset={() => { setConsent("undecided"); setSurface("prompt"); setEdgeExpanded(false); }} />
    </div>
  );
}
