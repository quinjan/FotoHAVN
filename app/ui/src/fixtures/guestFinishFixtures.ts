import {
  PROTOCOL_VERSION,
  type FiltersView,
  type GuestCycleStepEntry,
  type GuestCycleView,
  type GuestOverlays,
  type GuestPresentation,
  type GuestWelcomeMessage,
  type KeepsakeView,
  type LookOptionView,
  type OperatorWelcomeMessage,
  type PhaseTimerWarningOverlay,
} from "../protocol/generated.ts";
import { fixtureStrip } from "../components/shared/fixturePhotographs.ts";
import { guestStart } from "./inlineFixtures.ts";
import { eventRows } from "./operatorFixtures.ts";
import { operatorWelcome, runningEventPanel } from "./operatorSetupFixtures.ts";

/**
 * Hand-written fixtures for the Filters, Keepsake, and Operator Assistance views, the cycle chrome, and its overlays (M8 UI-2),
 * so the UI can be built before the brain produces the same states, typed against the generated protocol. Names start with
 * `guest-ui-` (or `operator-ui-` for the operator's panel) and never repeat a brain-generated name (`guest-cycle-*`,
 * `guest-assistance-*`, `operator-running-assistance*`); wave C replaces any that the brain's fixtures cover. Owner: M8 UI-2
 * only; `fixtureLane.ts` already merges this table.
 */

const noOverlays: GuestOverlays = guestStart.presentation.overlays;

type StepName = GuestCycleStepEntry["step"];

function step(name: StepName, state: GuestCycleStepEntry["state"], canGoBack = false): GuestCycleStepEntry {
  return { step: name, state, canGoBack };
}

function cycle(current: StepName, steps: readonly GuestCycleStepEntry[], startOverAvailable = current !== "keepsake"): GuestCycleView {
  return { id: "cycle-1", step: current, steps: [...steps], startOverAvailable };
}

/** Filters reached with every step offered: Template and Photographs done and revisitable. */
const filtersCycle = cycle("filters", [step("template", "done", true), step("photographs", "done", true), step("filters", "current"), step("keepsake", "upcoming")]);

/** A one-Template Event: Template is skipped, so there is nothing before Photographs to go back to. */
const skippedTemplateCycle = cycle("filters", [step("template", "skipped"), step("photographs", "done", true), step("filters", "current"), step("keepsake", "upcoming")]);

const keepsakeCycle = cycle("keepsake", [step("template", "done"), step("photographs", "done"), step("filters", "done"), step("keepsake", "current")]);

/** A one-Look Event reaching Keepsake: Filters was skipped. */
const skippedFiltersKeepsakeCycle = cycle("keepsake", [step("template", "done"), step("photographs", "done"), step("filters", "skipped"), step("keepsake", "current")]);

const looks: readonly LookOptionView[] = [
  { id: "monochrome-v1", name: "Monochrome", description: "Black and white with deep blacks and strong contrast.", thumbnailKey: "signature-strip.monochrome-v1.thumb" },
  { id: "naturale-v1", name: "Naturale", description: "Original colors. No filter.", thumbnailKey: "signature-strip.naturale-v1.thumb" },
  { id: "soft-monochrome-v1", name: "Soft Monochrome", description: "Black and white with softer contrast.", thumbnailKey: "signature-strip.soft-monochrome-v1.thumb" },
  { id: "soft-color-v1", name: "Soft Color", description: "Slightly muted colors and softer highlights.", thumbnailKey: "signature-strip.soft-color-v1.thumb" },
  { id: "sepia-v1", name: "Sepia", description: "Soft brown tones with faded contrast.", thumbnailKey: "signature-strip.sepia-v1.thumb" },
  { id: "warm-monochrome-v1", name: "Warm Monochrome", description: "Black and white with a light warm tint.", thumbnailKey: "signature-strip.warm-monochrome-v1.thumb" },
  { id: "classic-v1", name: "Classic", description: "Warm colors with a golden tint.", thumbnailKey: "signature-strip.classic-v1.thumb" },
];

function pick(ids: readonly string[], template = "signature-strip"): LookOptionView[] {
  return looks
    .filter((look) => ids.includes(look.id))
    .map((look) => ({ ...look, thumbnailKey: look.thumbnailKey === null ? null : look.thumbnailKey.replace("signature-strip", template) }));
}

const threeLooks = pick(["naturale-v1", "sepia-v1", "classic-v1"]);

function filters(parts: Partial<FiltersView> = {}): FiltersView {
  return {
    looks: threeLooks,
    selectedLookId: "naturale-v1",
    stripPreviewKey: "signature-strip.naturale-v1.r1",
    composing: false,
    compositionFailed: false,
    canFinish: true,
    ...parts,
  };
}

function welcome(presentation: Partial<GuestPresentation>): GuestWelcomeMessage {
  return {
    type: "welcome",
    role: "guest",
    protocolVersion: PROTOCOL_VERSION,
    revision: 1,
    presentation: { ...guestStart.presentation, start: null, ...presentation },
  };
}

function filtersWelcome(view: FiltersView, parts: { readonly cycle?: GuestCycleView; readonly overlays?: Partial<GuestOverlays> } = {}): GuestWelcomeMessage {
  return welcome({
    surface: "filters",
    guestCycle: parts.cycle ?? filtersCycle,
    filters: view,
    overlays: { ...noOverlays, ...parts.overlays },
  });
}

function keepsakeWelcome(view: KeepsakeView, cycleView: GuestCycleView = keepsakeCycle): GuestWelcomeMessage {
  return welcome({ surface: "keepsake", guestCycle: cycleView, keepsake: view });
}

function assistanceWelcome(reason: "camera" | "storage" | "guestDisplay", retrying: boolean, withCycle: boolean): GuestWelcomeMessage {
  return welcome({
    surface: "operatorAssistance",
    guestCycle: withCycle ? filtersCycle : null,
    operatorAssistance: { reason, retrying },
  });
}

const portraitStrip = fixtureStrip("signature-strip", 4);
const panoramaStrip = fixtureStrip("panorama-four", 8);

const warning = (parts: Partial<PhaseTimerWarningOverlay> = {}): PhaseTimerWarningOverlay => ({ phase: "filters", remainingSeconds: 18, ...parts });

function assistanceList(reason: "camera" | "storage"): OperatorWelcomeMessage {
  const events = eventRows(3);
  return operatorWelcome("eventList", {
    eventList: { events, newEventAvailable: true },
    runningEvent: runningEventPanel({
      eventId: events[0]?.id ?? "event-3",
      name: events[0]?.name ?? "Event",
      currentStep: "operatorAssistance",
      assistance: { reason },
      camera: reason === "camera" ? { state: "notAvailable", reason: "failed" } : { state: "ready", reason: null },
    }),
  });
}

export const guestFinishFixtures: Readonly<Record<string, GuestWelcomeMessage | OperatorWelcomeMessage>> = {
  "guest-ui-filters": filtersWelcome(filters()),
  "guest-ui-filters-selected": filtersWelcome(filters({ selectedLookId: "sepia-v1", stripPreviewKey: "signature-strip.sepia-v1.r2" })),
  "guest-ui-filters-composing": filtersWelcome(filters({ selectedLookId: "sepia-v1", composing: true, canFinish: false })),
  "guest-ui-filters-first-composing": filtersWelcome(filters({ stripPreviewKey: null, composing: true, canFinish: false })),
  "guest-ui-filters-composition-failed": filtersWelcome(filters({ selectedLookId: "sepia-v1", compositionFailed: true, canFinish: false })),
  "guest-ui-filters-two-looks": filtersWelcome(filters({ looks: pick(["naturale-v1", "sepia-v1"]) })),
  "guest-ui-filters-many-looks": filtersWelcome(filters({ looks: looks.slice() })),
  "guest-ui-filters-five-looks": filtersWelcome(filters({ looks: pick(["monochrome-v1", "naturale-v1", "soft-color-v1", "sepia-v1", "classic-v1"]) })),
  "guest-ui-filters-panorama": filtersWelcome(
    filters({ looks: pick(["naturale-v1", "sepia-v1", "classic-v1"], "panorama-four"), stripPreviewKey: "panorama-four.naturale-v1.r3" }),
  ),
  "guest-ui-filters-no-thumbnails": filtersWelcome(filters({ looks: threeLooks.map((look) => ({ ...look, thumbnailKey: null })) })),
  "guest-ui-filters-skipped-template": filtersWelcome(filters(), { cycle: skippedTemplateCycle }),
  "guest-ui-filters-start-over": filtersWelcome(filters(), { overlays: { startOverConfirmation: {} } }),
  "guest-ui-filters-timer-warning": filtersWelcome(filters(), { overlays: { phaseTimerWarning: warning() } }),
  "guest-ui-filters-timer-warning-last-second": filtersWelcome(filters(), { overlays: { phaseTimerWarning: warning({ remainingSeconds: 1 }) } }),
  "guest-ui-keepsake": keepsakeWelcome({ stripUrl: portraitStrip.src, width: portraitStrip.width, height: portraitStrip.height, landscape: false }),
  "guest-ui-keepsake-panorama": keepsakeWelcome({ stripUrl: panoramaStrip.src, width: panoramaStrip.width, height: panoramaStrip.height, landscape: true }),
  "guest-ui-keepsake-skipped-filters": keepsakeWelcome(
    { stripUrl: portraitStrip.src, width: portraitStrip.width, height: portraitStrip.height, landscape: false },
    skippedFiltersKeepsakeCycle,
  ),
  "guest-ui-assistance-camera": assistanceWelcome("camera", false, true),
  "guest-ui-assistance-storage": assistanceWelcome("storage", false, true),
  "guest-ui-assistance-display": assistanceWelcome("guestDisplay", false, true),
  "guest-ui-assistance-retrying": assistanceWelcome("camera", true, true),
  "guest-ui-assistance-no-cycle": assistanceWelcome("guestDisplay", false, false),
  "operator-ui-list-assistance-camera": assistanceList("camera"),
  "operator-ui-list-assistance-storage": assistanceList("storage"),
};
