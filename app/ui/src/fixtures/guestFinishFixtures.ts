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
  type PhaseTimerOverlay,
} from "../protocol/generated.ts";
import { fixtureStrip } from "../components/shared/fixturePhotographs.ts";
import { guestStart } from "./inlineFixtures.ts";

/**
 * Hand-written fixtures for the Filters, Keepsake, and Operator Assistance states the brain's fixtures (`guest-cycle-*`,
 * `guest-assistance-*`, `operator-running-assistance-*`) don't write: the longest single-column list and the two-column ones, a
 * Look change that keeps the strip on screen, a failure with a previous strip, a Look list without thumbnails, a skipped Template
 * or Filters step, the Phase Timer in its last seconds, and Operator Assistance while it retries. Names start with
 * `guest-ui-` and never repeat a brain-generated name.
 */

const noOverlays: GuestOverlays = guestStart.presentation.overlays;

type StepName = GuestCycleStepEntry["step"];

function step(name: StepName, state: GuestCycleStepEntry["state"]): GuestCycleStepEntry {
  return { step: name, state };
}

function cycle(current: StepName, steps: readonly GuestCycleStepEntry[]): GuestCycleView {
  return { id: "cycle-1", step: current, steps: [...steps] };
}

/** Filters reached with every step offered: Template and Photographs done. */
const filtersCycle = cycle("filters", [step("template", "done"), step("photographs", "done"), step("filters", "current"), step("keepsake", "upcoming")]);

/** A one-Template Event: Template is skipped and left out of the step progress. */
const skippedTemplateCycle = cycle("filters", [step("template", "skipped"), step("photographs", "done"), step("filters", "current"), step("keepsake", "upcoming")]);

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
    width: 600,
    height: 1800,
    landscape: false,
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
    overlays: { ...noOverlays, phaseTimer: timer(), ...parts.overlays },
  });
}

function keepsakeWelcome(view: KeepsakeView, cycleView: GuestCycleView): GuestWelcomeMessage {
  return welcome({ surface: "keepsake", guestCycle: cycleView, keepsake: view, overlays: { ...noOverlays, phaseTimer: timer({ phase: "keepsake", remainingSeconds: 14 }) } });
}

function assistanceWelcome(reason: "camera" | "storage" | "guestDisplay", retrying: boolean): GuestWelcomeMessage {
  return welcome({
    surface: "operatorAssistance",
    guestCycle: filtersCycle,
    operatorAssistance: { reason, retrying },
  });
}

const portraitStrip = fixtureStrip("signature-strip", 4);

function timer(parts: Partial<PhaseTimerOverlay> = {}): PhaseTimerOverlay {
  return { phase: "filters", remainingSeconds: 38, paused: false, ...parts };
}

export const guestFinishFixtures: Readonly<Record<string, GuestWelcomeMessage>> = {
  "guest-ui-filters-four-looks": filtersWelcome(filters({ looks: pick(["naturale-v1", "monochrome-v1", "sepia-v1", "classic-v1"]) })),
  "guest-ui-filters-composing": filtersWelcome(filters({ selectedLookId: "sepia-v1", composing: true, canFinish: false })),
  "guest-ui-filters-composition-failed": filtersWelcome(filters({ selectedLookId: "sepia-v1", compositionFailed: true, canFinish: false })),
  "guest-ui-filters-many-looks": filtersWelcome(filters({ looks: looks.slice() })),
  "guest-ui-filters-five-looks": filtersWelcome(filters({ looks: pick(["monochrome-v1", "naturale-v1", "soft-color-v1", "sepia-v1", "classic-v1"]) })),
  "guest-ui-filters-no-thumbnails": filtersWelcome(filters({ looks: threeLooks.map((look) => ({ ...look, thumbnailKey: null })) })),
  "guest-ui-filters-skipped-template": filtersWelcome(filters(), { cycle: skippedTemplateCycle }),
  "guest-ui-filters-timer-last-second": filtersWelcome(filters(), { overlays: { phaseTimer: timer({ remainingSeconds: 1 }) } }),
  "guest-ui-filters-timer-paused": filtersWelcome(filters({ selectedLookId: "sepia-v1", composing: true, canFinish: false }), { overlays: { phaseTimer: timer({ remainingSeconds: 21, paused: true }) } }),
  "guest-ui-filters-no-timer": filtersWelcome(filters({ looks: looks.slice() }), { overlays: { phaseTimer: null } }),
  "guest-ui-keepsake-skipped-filters": keepsakeWelcome(
    { stripUrl: portraitStrip.src, width: portraitStrip.width, height: portraitStrip.height, landscape: false },
    skippedFiltersKeepsakeCycle,
  ),
  "guest-ui-assistance-retrying": assistanceWelcome("camera", true),
};