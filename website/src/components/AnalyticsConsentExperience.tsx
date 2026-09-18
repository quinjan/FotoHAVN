"use client";

import { ArrowLeftIcon, CheckIcon, XIcon } from "@phosphor-icons/react";
import {
  useEffect,
  useRef,
  useState,
  type MouseEvent as ReactMouseEvent,
  type ReactNode,
  type RefObject,
} from "react";
import type { AnalyticsConsentChoice, AnalyticsConsentState } from "@/analytics/contracts";
import { useAnalyticsConsent } from "./AnalyticsProvider";
import styles from "./AnalyticsConsentExperience.module.css";

type Surface = "prompt" | "settings" | "privacy" | null;

function useDialogFocus(
  root: RefObject<HTMLElement | null>,
  initial: RefObject<HTMLElement | null>,
  onEscape: () => void,
) {
  const escapeHandler = useRef(onEscape);
  useEffect(() => {
    escapeHandler.current = onEscape;
  }, [onEscape]);
  useEffect(() => {
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    initial.current?.focus({ preventScroll: true });
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        escapeHandler.current();
        return;
      }
      if (event.key !== "Tab" || !root.current) return;
      const focusable = Array.from(root.current.querySelectorAll<HTMLElement>(
        "button:not([disabled]), a[href], input:not([disabled]), [tabindex]:not([tabindex='-1'])",
      )).filter((element) => !element.hasAttribute("hidden"));
      if (!focusable.length) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = previousOverflow;
      previous?.focus({ preventScroll: true });
    };
  }, [initial, root]);
}

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

function ConsentPrompt({ onAllow, onCookieless, onPrivacy }: {
  onAllow: () => void;
  onCookieless: () => void;
  onPrivacy: () => void;
}) {
  return (
    <section className={styles.prompt} aria-labelledby="analytics-prompt-heading" role="region">
      <div className={styles.railCopy}>
        <h2 id="analytics-prompt-heading">Analytics cookies</h2>
        <p>
          Basic privacy-masked analytics continue without cookies. Cookies connect one anonymous journey across pages. Your photographs stay on this device and are never uploaded.{" "}
          <button className={styles.inlinePrivacy} type="button" onClick={onPrivacy}>Click here for more information.</button>
        </p>
      </div>
      <ChoiceButtons onAllow={onAllow} onCookieless={onCookieless} />
    </section>
  );
}

function ConsentOption({ checked, title, copy, value, onChange }: {
  checked: boolean;
  title: string;
  copy: string;
  value: AnalyticsConsentChoice;
  onChange: (value: AnalyticsConsentChoice) => void;
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
  consent: AnalyticsConsentState;
  onClose: () => void;
  onSave: (choice: AnalyticsConsentChoice) => void;
  onPrivacy: () => void;
}) {
  const [draft, setDraft] = useState<AnalyticsConsentChoice>(consent === "cookies" ? "cookies" : "cookieless");
  const [confirmWithdrawal, setConfirmWithdrawal] = useState(false);
  const panel = useRef<HTMLElement>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  useDialogFocus(panel, heading, onClose);

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
      <section ref={panel} className={styles.settingsPanel} role="dialog" aria-modal="true" aria-labelledby="analytics-settings-heading">
        <button className={styles.closeButton} type="button" onClick={onClose} aria-label="Close analytics settings"><XIcon size={20} aria-hidden="true" /></button>
        <p className={styles.kicker}>YOUR PRIVACY CHOICE</p>
        <h2 id="analytics-settings-heading" ref={heading} tabIndex={-1}>Analytics settings</h2>
        <p className={styles.settingsIntro}>
          Basic privacy-masked activity is measured without cookies. Your photographs stay on this device and are never uploaded. Choose whether we may connect pages into one anonymous journey.{" "}
          <button className={styles.inlinePrivacy} type="button" onClick={onPrivacy}>Click here for more information.</button>
        </p>
        <fieldset className={styles.options}>
          <legend>Choose how analytics work</legend>
          <ConsentOption checked={draft === "cookies"} title="Allow analytics cookies" copy="Connect anonymous activity across pages for up to six months." value="cookies" onChange={(value) => { setDraft(value); setConfirmWithdrawal(false); }} />
          <ConsentOption checked={draft === "cookieless"} title="Continue without cookies" copy="Keep each measured page visit separate." value="cookieless" onChange={(value) => { setDraft(value); setConfirmWithdrawal(false); }} />
        </fieldset>
        {confirmWithdrawal && (
          <div className={styles.withdrawalNote} role="alert">
            <strong>Stop connecting future activity?</strong>
            <p>Future activity will stay separate. Previously retained anonymous analytics remain until Microsoft Clarity&apos;s standard retention expires.</p>
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

function PrivacyPage({ onBack }: { onBack: () => void }) {
  const page = useRef<HTMLDivElement>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  useDialogFocus(page, heading, onBack);

  return (
    <div ref={page} className={styles.privacyPage} role="dialog" aria-modal="true" aria-labelledby="privacy-page-heading">
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
        <div className={styles.privacyQuestions}>
          <PrivacySection title="What we measure"><p>We use Microsoft Clarity to understand which major website sections are reached, whether the online booth is completed, and when an inquiry link is chosen.</p></PrivacySection>
          <PrivacySection title="What stays with you"><p>Your photographs, camera view, imported files, previews, filenames, and finished keepsakes stay on your device. They are never provided to Clarity.</p></PrivacySection>
          <PrivacySection title="Cookies and anonymous journeys"><p>Without analytics cookies, each measured page visit remains separate. With permission, Clarity uses an anonymous first-party cookie to connect activity across pages. FotoHAVN does not create or send a visitor identity.</p></PrivacySection>
          <PrivacySection title="Your choice"><p>Your choice is remembered for six months and can be changed through the Analytics settings control. Withdrawing permission stops future connection. Previously retained anonymous analytics follow Clarity&apos;s standard retention.</p></PrivacySection>
          <PrivacySection title="Inquiry and contact"><p>Clicking an Instagram inquiry link shows anonymous Inquiry Intent. It does not tell us who you are, whether you sent a message, or whether you became a customer.</p></PrivacySection>
        </div>
      </main>
      <footer className={styles.privacyFooter}><span>© 2026 FOTOHAVN</span><button type="button" onClick={onBack}>Return to your choice</button></footer>
    </div>
  );
}

export default function AnalyticsConsentExperience() {
  const { consent, enabled, ready, choose } = useAnalyticsConsent();
  const [surface, setSurface] = useState<Surface>(null);
  const [edgeExpanded, setEdgeExpanded] = useState(false);
  const edgeButton = useRef<HTMLButtonElement>(null);

  if (!ready || !enabled) return null;
  const visibleSurface = surface ?? (consent === "unknown" ? "prompt" : null);

  const save = (choice: AnalyticsConsentChoice) => {
    choose(choice);
    setSurface(null);
    requestAnimationFrame(() => edgeButton.current?.focus({ preventScroll: true }));
  };
  const closeSettings = () => {
    setSurface(null);
    requestAnimationFrame(() => edgeButton.current?.focus({ preventScroll: true }));
  };
  const returnFromPrivacy = () => setSurface(consent === "unknown" ? "prompt" : "settings");
  const handleEdgeSettings = (event: ReactMouseEvent<HTMLButtonElement>) => {
    const touchLike = window.matchMedia("(hover: none), (pointer: coarse)").matches || window.innerWidth <= 960;
    const keyboardActivated = event.detail === 0;
    if (touchLike && !keyboardActivated && !edgeExpanded) {
      setEdgeExpanded(true);
      return;
    }
    setEdgeExpanded(false);
    setSurface("settings");
  };

  return (
    <div className={styles.root}>
      {visibleSurface === "prompt" && <ConsentPrompt onAllow={() => save("cookies")} onCookieless={() => save("cookieless")} onPrivacy={() => setSurface("privacy")} />}
      {visibleSurface === "settings" && <AnalyticsSettings consent={consent} onClose={closeSettings} onSave={save} onPrivacy={() => setSurface("privacy")} />}
      {visibleSurface === "privacy" && <PrivacyPage onBack={returnFromPrivacy} />}
      {consent !== "unknown" && visibleSurface === null && (
        <button
          ref={edgeButton}
          className={`${styles.edgeSettings} ${edgeExpanded ? styles.edgeExpanded : ""}`}
          type="button"
          aria-label="Analytics settings"
          aria-expanded={edgeExpanded}
          onBlur={() => setEdgeExpanded(false)}
          onPointerLeave={() => setEdgeExpanded(false)}
          onClick={handleEdgeSettings}
        >
          <span className={styles.edgeSettingsLabel}>Analytics settings</span>
        </button>
      )}
    </div>
  );
}
