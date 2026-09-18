export const ANALYTICS_CONSENT_POLICY_VERSION = "2026-09-18.2";
export const ANALYTICS_PREFERENCE_STORAGE_KEY = "fotohavn.analytics-cookie-consent";

export const marketingSections = [
  { id: "experience", name: "experience" },
  { id: "the-booth", name: "the-booth" },
  { id: "prints", name: "prints" },
  { id: "guest-album", name: "guest-album" },
  { id: "make-something-worth-keeping", name: "inquiry" },
] as const;

export type MarketingSectionName = (typeof marketingSections)[number]["name"];

export const analyticsEvents = {
  sectionReach: (section: MarketingSectionName) => `section-reach-${section}` as const,
  sectionEngagement: (section: MarketingSectionName) => `section-engagement-${section}` as const,
  onlineBoothStarted: "online-booth-started",
  onlineBoothCompleted: "online-booth-completed",
  onlineBoothDownloaded: "online-booth-downloaded",
  onlinePhysicalBoothInvitation: "online-physical-booth-invitation",
  findBoothInquiryIntent: "find-booth-inquiry-intent",
  rentFotohavnInquiryIntent: "rent-fotohavn-inquiry-intent",
} as const;

export type AnalyticsEventName =
  | ReturnType<typeof analyticsEvents.sectionReach>
  | ReturnType<typeof analyticsEvents.sectionEngagement>
  | (typeof analyticsEvents)[Exclude<keyof typeof analyticsEvents, "sectionReach" | "sectionEngagement">];

export type AnalyticsConsentChoice = "cookieless" | "cookies";
export type AnalyticsConsentState = "unknown" | AnalyticsConsentChoice;

export type AnalyticsTestRecord = Readonly<{
  kind: "event" | "consent";
  name: AnalyticsEventName | AnalyticsConsentState;
}>;

export const analyticsReportContracts = {
  homepageProgression: {
    denominator: "Cookieless Analytics Page Activity and Consented Anonymous Journeys",
    events: marketingSections.flatMap(({ name }) => [
      analyticsEvents.sectionReach(name),
      analyticsEvents.sectionEngagement(name),
    ]),
  },
  onlineBoothCompletion: {
    denominator: "Cookieless Analytics Page Activity and Consented Anonymous Journeys",
    events: [
      analyticsEvents.onlineBoothStarted,
      analyticsEvents.onlineBoothCompleted,
      analyticsEvents.onlineBoothDownloaded,
    ],
  },
  consentedInquiryJourney: {
    denominator: "Consented Anonymous Journeys only",
    events: [
      analyticsEvents.findBoothInquiryIntent,
      analyticsEvents.rentFotohavnInquiryIntent,
    ],
  },
} as const;
