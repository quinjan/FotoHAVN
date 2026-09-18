import {
  ANALYTICS_CONSENT_POLICY_VERSION,
  ANALYTICS_PREFERENCE_STORAGE_KEY,
  type AnalyticsConsentChoice,
  type AnalyticsConsentState,
} from "./contracts";

export type ConsentPreference = Readonly<{
  choice: AnalyticsConsentChoice;
  version: string;
  decidedAt: number;
  expiresAt: number;
}>;

type PreferenceStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

export function sixMonthsAfter(timestamp: number) {
  const expiry = new Date(timestamp);
  expiry.setUTCMonth(expiry.getUTCMonth() + 6);
  return expiry.getTime();
}

export function createConsentPreference(choice: AnalyticsConsentChoice, now = Date.now()): ConsentPreference {
  return {
    choice,
    version: ANALYTICS_CONSENT_POLICY_VERSION,
    decidedAt: now,
    expiresAt: sixMonthsAfter(now),
  };
}

function isPreference(value: unknown): value is ConsentPreference {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<ConsentPreference>;
  return (candidate.choice === "cookieless" || candidate.choice === "cookies")
    && typeof candidate.version === "string"
    && typeof candidate.decidedAt === "number"
    && typeof candidate.expiresAt === "number";
}

export function readConsentPreference(storage: PreferenceStorage, now = Date.now()): AnalyticsConsentState {
  try {
    const raw = storage.getItem(ANALYTICS_PREFERENCE_STORAGE_KEY);
    if (!raw) return "unknown";
    const preference: unknown = JSON.parse(raw);
    if (!isPreference(preference)
      || preference.version !== ANALYTICS_CONSENT_POLICY_VERSION
      || preference.expiresAt <= now) {
      storage.removeItem(ANALYTICS_PREFERENCE_STORAGE_KEY);
      return "unknown";
    }
    return preference.choice;
  } catch {
    return "unknown";
  }
}

export function writeConsentPreference(storage: PreferenceStorage, choice: AnalyticsConsentChoice, now = Date.now()) {
  const preference = createConsentPreference(choice, now);
  try {
    storage.setItem(ANALYTICS_PREFERENCE_STORAGE_KEY, JSON.stringify(preference));
  } catch {
    // Storage is an enhancement. Clarity still receives the current-page choice.
  }
  return preference;
}
