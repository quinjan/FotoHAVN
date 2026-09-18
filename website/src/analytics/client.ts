import {
  analyticsEvents,
  type AnalyticsConsentState,
  type AnalyticsEventName,
  type AnalyticsTestRecord,
} from "./contracts";

type ClarityCommand = (...args: unknown[]) => void;
type QueuedClarityCommand = ClarityCommand & { q?: unknown[][] };

declare global {
  interface Window {
    clarity?: QueuedClarityCommand;
    __fotohavnAnalyticsTestSink?: (record: AnalyticsTestRecord) => void;
  }
}

const SCRIPT_ID = "fotohavn-clarity";
const emittedDedupeKeys = new Set<string>();
const pendingEvents: AnalyticsEventName[] = [];

function reportToTestSink(record: AnalyticsTestRecord) {
  try {
    window.__fotohavnAnalyticsTestSink?.(record);
  } catch {
    // Test instrumentation must obey the same fail-open boundary as Clarity.
  }
}

function clarityStub(): QueuedClarityCommand {
  const command: QueuedClarityCommand = (...args: unknown[]) => {
    (command.q ??= []).push(args);
  };
  return command;
}

function signalFor(consent: AnalyticsConsentState) {
  return {
    ad_Storage: "denied",
    analytics_Storage: consent === "cookies" ? "granted" : "denied",
  } as const;
}

export function setClarityConsent(consent: AnalyticsConsentState) {
  if (typeof window === "undefined") return;
  reportToTestSink({ kind: "consent", name: consent });
  try {
    window.clarity?.("consentv2", signalFor(consent));
    if (consent !== "cookies") {
      window.clarity?.("consent", false);
    }
  } catch {
    // Consent and the visitor's requested navigation remain independent of Clarity.
  }
}

export function initializeClarity(projectId: string, consent: AnalyticsConsentState) {
  if (typeof window === "undefined" || !/^[a-zA-Z0-9]+$/.test(projectId)) return;
  try {
    window.clarity ??= clarityStub();
    setClarityConsent(consent);
    for (const event of pendingEvents.splice(0)) window.clarity("event", event);

    if (document.getElementById(SCRIPT_ID)) return;
    const script = document.createElement("script");
    script.id = SCRIPT_ID;
    script.async = true;
    script.src = `https://www.clarity.ms/tag/${projectId}`;
    script.referrerPolicy = "strict-origin-when-cross-origin";
    script.onerror = () => undefined;
    document.head.appendChild(script);
  } catch {
    // A blocked provider cannot affect rendering or any FotoHAVN workflow.
  }
}

export function emitAnalyticsEvent(event: AnalyticsEventName, dedupeKey?: string) {
  if (typeof window === "undefined") return;
  if (dedupeKey && emittedDedupeKeys.has(dedupeKey)) return;
  if (dedupeKey) emittedDedupeKeys.add(dedupeKey);
  reportToTestSink({ kind: "event", name: event });
  try {
    if (window.clarity) window.clarity("event", event);
    else pendingEvents.push(event);
  } catch {
    // Event collection is never allowed to interrupt the originating action.
  }
}

export function resetPageEventDedupe() {
  emittedDedupeKeys.clear();
}

export function createOnlineBoothCycleTracker() {
  let cycle = 0;
  let active = false;

  return {
    start() {
      if (active) return;
      cycle += 1;
      active = true;
      emitAnalyticsEvent(analyticsEvents.onlineBoothStarted, `booth:${cycle}:started`);
    },
    complete() {
      if (!active) return;
      emitAnalyticsEvent(analyticsEvents.onlineBoothCompleted, `booth:${cycle}:completed`);
    },
    downloaded() {
      if (!active) return;
      emitAnalyticsEvent(analyticsEvents.onlineBoothDownloaded, `booth:${cycle}:downloaded`);
    },
    reset() {
      active = false;
    },
  };
}
