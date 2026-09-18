"use client";

import { usePathname } from "next/navigation";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  initializeClarity,
  emitAnalyticsEvent,
  resetPageEventDedupe,
  setClarityConsent,
} from "@/analytics/client";
import { readConsentPreference, writeConsentPreference } from "@/analytics/consent";
import {
  marketingSections,
  type AnalyticsConsentChoice,
  type AnalyticsConsentState,
} from "@/analytics/contracts";
import { SectionTimingTracker } from "@/analytics/sectionTiming";
import AnalyticsConsentExperience from "./AnalyticsConsentExperience";

type AnalyticsConsentContextValue = {
  consent: AnalyticsConsentState;
  enabled: boolean;
  ready: boolean;
  choose: (choice: AnalyticsConsentChoice) => void;
};

const AnalyticsConsentContext = createContext<AnalyticsConsentContextValue>({
  consent: "unknown",
  enabled: false,
  ready: false,
  choose: () => undefined,
});

export function useAnalyticsConsent() {
  return useContext(AnalyticsConsentContext);
}

function SectionMeasurement({ pathname, enabled }: { pathname: string; enabled: boolean }) {
  useEffect(() => {
    resetPageEventDedupe();
    if (!enabled || pathname !== "/" || typeof IntersectionObserver === "undefined") return;

    const tracker = new SectionTimingTracker();
    const ratios = new Map<(typeof marketingSections)[number]["name"], boolean>();
    let pageVisible = document.visibilityState === "visible";

    const sample = () => {
      const now = performance.now();
      for (const { name } of marketingSections) {
        for (const event of tracker.update(name, ratios.get(name) ?? false, pageVisible, now)) {
          emitAnalyticsEvent(event, `page:${pathname}:${event}`);
        }
      }
    };

    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        const section = marketingSections.find(({ id }) => id === entry.target.id);
        if (section) ratios.set(section.name, entry.isIntersecting && entry.intersectionRatio >= 0.5);
      }
      sample();
    }, { threshold: [0, 0.5, 1] });

    for (const section of marketingSections) {
      const element = document.getElementById(section.id);
      if (element) observer.observe(element);
    }

    const onVisibilityChange = () => {
      pageVisible = document.visibilityState === "visible";
      sample();
    };
    document.addEventListener("visibilitychange", onVisibilityChange);
    const timer = window.setInterval(sample, 100);

    return () => {
      sample();
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisibilityChange);
      observer.disconnect();
    };
  }, [enabled, pathname]);

  return null;
}

export default function AnalyticsProvider({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const [consent, setConsent] = useState<AnalyticsConsentState>("unknown");
  const [ready, setReady] = useState(false);
  const [enabled, setEnabled] = useState(false);
  const projectId = process.env.NEXT_PUBLIC_CLARITY_PROJECT_ID?.trim() ?? "";

  useEffect(() => {
    const prototypeMode = process.env.NODE_ENV !== "production"
      && new URLSearchParams(window.location.search).get("analytics-prototype") === "1";
    const nextEnabled = !prototypeMode && (process.env.NODE_ENV !== "production" || Boolean(projectId));
    let storedConsent: AnalyticsConsentState = "unknown";
    try {
      storedConsent = readConsentPreference(window.localStorage);
    } catch {
      // Unavailable storage is the privacy-safe unknown state.
    }
    let cancelled = false;
    queueMicrotask(() => {
      if (cancelled) return;
      setConsent(storedConsent);
      setEnabled(nextEnabled);
      setReady(true);
      if (nextEnabled && projectId) initializeClarity(projectId, storedConsent);
      else if (nextEnabled) setClarityConsent(storedConsent);
    });
    return () => { cancelled = true; };
  }, [projectId]);

  const choose = useCallback((choice: AnalyticsConsentChoice) => {
    if (!enabled) return;
    try {
      writeConsentPreference(window.localStorage, choice);
    } catch {
      // The current-page choice still reaches Clarity when storage is unavailable.
    }
    if (choice === "cookies" && consent !== "cookies") {
      // Close the current cookieless page activity before starting continuity.
      setClarityConsent("cookieless");
    }
    setClarityConsent(choice);
    setConsent(choice);
  }, [consent, enabled]);

  const value = useMemo(() => ({ consent, enabled, ready, choose }), [consent, enabled, ready, choose]);

  return (
    <AnalyticsConsentContext.Provider value={value}>
      {children}
      <SectionMeasurement pathname={pathname} enabled={enabled} />
      <AnalyticsConsentExperience />
    </AnalyticsConsentContext.Provider>
  );
}
