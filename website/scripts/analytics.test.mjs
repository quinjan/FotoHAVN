import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";
import ts from "typescript";

function compile(relativePath, dependencies = {}, globals = {}) {
  const source = readFileSync(new URL(`../src/${relativePath}`, import.meta.url), "utf8");
  const result = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 },
  });
  const exports = {};
  vm.runInNewContext(result.outputText, {
    exports,
    require: (id) => {
      assert.ok(id in dependencies, `Unmapped import ${id}`);
      return dependencies[id];
    },
    ...globals,
  });
  return exports;
}

const contracts = compile("analytics/contracts.ts");
const consent = compile("analytics/consent.ts", { "./contracts": contracts });
const timing = compile("analytics/sectionTiming.ts", { "./contracts": contracts });
const serial = (value) => JSON.parse(JSON.stringify(value));

function storage(initial = {}) {
  const values = new Map(Object.entries(initial));
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: (key) => values.delete(key),
    values,
  };
}

test("analytics cookie preference is versioned, explicit, and expires six calendar months later", () => {
  const now = Date.UTC(2026, 8, 18, 4, 0, 0);
  const store = storage();
  const saved = consent.writeConsentPreference(store, "cookies", now);
  assert.equal(saved.choice, "cookies");
  assert.equal(saved.version, contracts.ANALYTICS_CONSENT_POLICY_VERSION);
  assert.equal(saved.expiresAt, Date.UTC(2027, 2, 18, 4, 0, 0));
  assert.equal(consent.readConsentPreference(store, saved.expiresAt - 1), "cookies");
  assert.equal(consent.readConsentPreference(store, saved.expiresAt), "unknown");
  assert.equal(store.getItem(contracts.ANALYTICS_PREFERENCE_STORAGE_KEY), null);
});

test("missing, unreadable, and superseded preferences are never treated as cookie permission", () => {
  assert.equal(consent.readConsentPreference(storage()), "unknown");
  const unreadable = { getItem() { throw new Error("blocked"); }, setItem() {}, removeItem() {} };
  assert.equal(consent.readConsentPreference(unreadable), "unknown");
  const store = storage({
    [contracts.ANALYTICS_PREFERENCE_STORAGE_KEY]: JSON.stringify({
      choice: "cookies", version: "older-policy", decidedAt: 1, expiresAt: Date.now() + 10_000,
    }),
  });
  assert.equal(consent.readConsentPreference(store), "unknown");
  assert.equal(store.getItem(contracts.ANALYTICS_PREFERENCE_STORAGE_KEY), null);
});

test("Section Reach needs one continuous visible second and resets when visibility is lost", () => {
  const tracker = new timing.SectionTimingTracker();
  assert.deepEqual(serial(tracker.update("experience", true, true, 0)), []);
  assert.deepEqual(serial(tracker.update("experience", true, true, 800)), []);
  assert.deepEqual(serial(tracker.update("experience", false, true, 900)), []);
  assert.deepEqual(serial(tracker.update("experience", true, true, 1_500)), []);
  assert.deepEqual(
    serial(tracker.update("experience", true, true, 2_501)),
    [contracts.analyticsEvents.sectionReach("experience")],
  );
  assert.deepEqual(serial(tracker.update("experience", true, true, 4_000)), []);
});

test("Section Engagement accumulates five visible seconds but ignores hidden-page time", () => {
  const tracker = new timing.SectionTimingTracker();
  tracker.update("prints", true, true, 0);
  tracker.update("prints", true, true, 2_000);
  tracker.update("prints", true, false, 2_500);
  tracker.update("prints", true, false, 20_000);
  tracker.update("prints", true, true, 21_000);
  const events = tracker.update("prints", true, true, 23_500);
  assert.deepEqual(serial(events), [contracts.analyticsEvents.sectionEngagement("prints")]);
});

test("the analytics boundary deduplicates policy events and isolates provider failures", () => {
  const records = [];
  const calls = [];
  const window = {
    __fotohavnAnalyticsTestSink: (record) => records.push(record),
    clarity: (...args) => calls.push(args),
  };
  const document = {
    getElementById: () => ({ id: "already-installed" }),
    createElement: () => ({}),
    head: { appendChild() {} },
  };
  const client = compile("analytics/client.ts", { "./contracts": contracts }, { window, document });
  client.emitAnalyticsEvent(contracts.analyticsEvents.sectionReach("experience"), "once");
  client.emitAnalyticsEvent(contracts.analyticsEvents.sectionReach("experience"), "once");
  assert.equal(records.filter((record) => record.kind === "event").length, 1);
  assert.equal(calls.filter(([command]) => command === "event").length, 1);

  window.clarity = () => { throw new Error("provider blocked"); };
  window.__fotohavnAnalyticsTestSink = () => { throw new Error("spy failed"); };
  assert.doesNotThrow(() => client.emitAnalyticsEvent(contracts.analyticsEvents.findBoothInquiryIntent));
  assert.doesNotThrow(() => client.setClarityConsent("cookieless"));
});

test("Clarity receives denied consent before its script loads and advertising storage stays denied", () => {
  const appended = [];
  const window = {};
  const document = {
    getElementById: () => null,
    createElement: () => ({}),
    head: { appendChild: (script) => appended.push(script) },
  };
  const client = compile("analytics/client.ts", { "./contracts": contracts }, { window, document });
  client.initializeClarity("project123", "unknown");
  assert.equal(appended.length, 1);
  assert.equal(appended[0].src, "https://www.clarity.ms/tag/project123");
  assert.deepEqual(serial(window.clarity.q.slice(0, 2)), [
    ["consentv2", { ad_Storage: "denied", analytics_Storage: "denied" }],
    ["consent", false],
  ]);
  client.setClarityConsent("cookies");
  assert.deepEqual(serial(window.clarity.q.at(-1)), [
    "consentv2", { ad_Storage: "denied", analytics_Storage: "granted" },
  ]);
});

test("Online Booth Cycle events deduplicate within a cycle and reset only for a new keepsake", () => {
  const records = [];
  const window = { __fotohavnAnalyticsTestSink: (record) => records.push(record) };
  const client = compile("analytics/client.ts", { "./contracts": contracts }, { window, document: {} });
  const cycle = client.createOnlineBoothCycleTracker();
  cycle.complete();
  cycle.start(); cycle.start();
  cycle.complete(); cycle.complete();
  cycle.downloaded(); cycle.downloaded();
  cycle.reset();
  cycle.start(); cycle.complete(); cycle.downloaded();
  assert.deepEqual(records.filter(({ kind }) => kind === "event").map(({ name }) => name), [
    contracts.analyticsEvents.onlineBoothStarted,
    contracts.analyticsEvents.onlineBoothCompleted,
    contracts.analyticsEvents.onlineBoothDownloaded,
    contracts.analyticsEvents.onlineBoothStarted,
    contracts.analyticsEvents.onlineBoothCompleted,
    contracts.analyticsEvents.onlineBoothDownloaded,
  ]);
});

test("the public event contract stays fixed and contains no customer-created or identity properties", () => {
  const clientSource = readFileSync(new URL("../src/analytics/client.ts", import.meta.url), "utf8");
  const boothSource = readFileSync(new URL("../src/components/onlinePhotobooth/OnlinePhotobooth.tsx", import.meta.url), "utf8");
  assert.ok(boothSource.includes('data-clarity-mask="true"'));
  assert.ok(!clientSource.includes('"identify"'));
  assert.ok(!clientSource.includes('"set"'));
  assert.equal(contracts.analyticsReportContracts.homepageProgression.events.length, 10);
  assert.equal(new Set(contracts.analyticsReportContracts.homepageProgression.events).size, 10);
});
