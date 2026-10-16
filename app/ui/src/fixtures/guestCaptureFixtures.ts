import { fixtureCapture } from "../components/shared/fixturePhotographs.ts";
import {
  PROTOCOL_VERSION,
  type CaptureSequenceView,
  type GuestCycleView,
  type GuestOverlays,
  type GuestWelcomeMessage,
  type PhotographSelectionView,
  type PhotographsView,
  type PhotographsWindowView,
  type TemplateOptionView,
  type TemplateView,
} from "../protocol/generated.ts";

/**
 * Hand-written Guest Screen fixtures for the Template and Photographs states the brain's fixtures (`guest-cycle-*`) don't write:
 * other Templates, a camera that isn't ready, a stopped sequence, the Camera tab during a Retake, a partial or retained arrangement,
 * and the Panorama Move and Retake. Names start with `guest-ui-` and never repeat a brain-generated name. The states the brain's
 * fixtures do write (the default Camera, countdown, pause, Arrange, Move, Retake, one-Look Arrange) come from those files.
 */

const noOverlays: GuestOverlays = {
  exitHold: null,
  pinDialog: null,
  phaseTimerWarning: null,
  startOverConfirmation: null,
  notice: null,
};

function windows(count: number, width: number, height: number, layout: "stack" | "row"): TemplateOptionView["windows"] {
  return Array.from({ length: count }, (_, index) =>
    layout === "stack"
      ? { x: 0, y: index * (height / count), width, height: height / count }
      : { x: index * (width / count), y: 0, width: width / count, height },
  );
}

const signatureStrip: TemplateOptionView = {
  id: "signature-strip",
  name: "Signature Strip",
  description: "Four edge-to-edge photographs with a signature across the middle.",
  width: 600,
  height: 1800,
  landscape: false,
  windows: windows(4, 600, 1800, "stack"),
  blankPreviewKey: null,
};

const panoramaFour: TemplateOptionView = {
  id: "panorama-four",
  name: "Panorama Four",
  description: "Four portraits side by side, with a quiet corner signature.",
  width: 1800,
  height: 600,
  landscape: true,
  windows: windows(4, 1800, 600, "row"),
  blankPreviewKey: null,
};

const butterGingham: TemplateOptionView = {
  id: "butter-gingham",
  name: "Butter Gingham",
  description: "Three framed photographs on soft yellow gingham.",
  width: 600,
  height: 1800,
  landscape: false,
  windows: [0, 1, 2].map((index) => ({ x: 26.5, y: 96 + index * 562, width: 547, height: 522 })),
  blankPreviewKey: null,
};

const polkaKeepsake: TemplateOptionView = {
  id: "polka-keepsake",
  name: "Polka Keepsake",
  description: "Four small photographs on a polka-dot keepsake.",
  width: 600,
  height: 1800,
  landscape: false,
  windows: [0, 1, 2, 3].map((index) => ({ x: 43.5, y: 80 + index * 420, width: 513, height: 390 })),
  blankPreviewKey: null,
};

const offered: readonly TemplateOptionView[] = [signatureStrip, panoramaFour, butterGingham, polkaKeepsake];

function cycle(step: "template" | "photographs"): GuestCycleView {
  return {
    id: "cycle-1",
    step,
    steps: [
      { step: "template", state: step === "template" ? "current" : "done", canGoBack: step !== "template" },
      { step: "photographs", state: step === "photographs" ? "current" : "upcoming", canGoBack: false },
      { step: "filters", state: "upcoming", canGoBack: false },
      { step: "keepsake", state: "upcoming", canGoBack: false },
    ],
    startOverAvailable: true,
  };
}

function welcome(step: "template" | "photographs", fields: Partial<GuestWelcomeMessage["presentation"]>): GuestWelcomeMessage {
  return {
    type: "welcome",
    role: "guest",
    protocolVersion: PROTOCOL_VERSION,
    revision: 1,
    presentation: {
      surface: step,
      start: null,
      startUnavailable: null,
      overlays: noOverlays,
      fatal: null,
      guestCycle: cycle(step),
      template: null,
      photographs: null,
      filters: null,
      keepsake: null,
      operatorAssistance: null,
      ...fields,
    },
  };
}

function templateStep(selectedId: string, templates: readonly TemplateOptionView[] = offered): GuestWelcomeMessage {
  const template: TemplateView = { templates: [...templates], selectedId, canContinue: true };
  return welcome("template", { template });
}

const idleSequence: CaptureSequenceView = {
  running: false,
  targetWindow: null,
  countdownRemaining: null,
  flashSequence: 0,
  capturedInSequence: 0,
  totalInSequence: 0,
  pausing: false,
};

const noSelection: PhotographSelectionView = { index: null, mode: "none" };

/** The photograph in window `index`: a placeholder camera frame at the window's own proportion, named like the store names it. */
function captured(template: TemplateOptionView, index: number): PhotographsWindowView {
  const rect = template.windows[index];
  const ratio = rect === undefined ? 4 / 3 : rect.width / rect.height;
  const image = fixtureCapture(index, Math.round(ratio * 720), 720);
  return { index, captureId: `capture-${String(index + 1)}`, imageUrl: image.src };
}

function slots(template: TemplateOptionView, filled: number): PhotographsWindowView[] {
  return template.windows.map((_, index) =>
    index < filled ? captured(template, index) : { index, captureId: null, imageUrl: null },
  );
}

function photographs(template: TemplateOptionView, fields: Partial<PhotographsView> & { readonly filled?: number }): GuestWelcomeMessage {
  const { filled = 0, ...rest } = fields;
  const rect = template.windows[Math.min(filled, template.windows.length - 1)];
  const view: PhotographsView = {
    tab: "camera",
    template: {
      id: template.id,
      width: template.width,
      height: template.height,
      landscape: template.landscape,
      windows: template.windows,
      blankPreviewKey: template.blankPreviewKey,
    },
    windows: slots(template, filled),
    retainedCount: 0,
    sequence: idleSequence,
    mirrored: true,
    cropAspect: rect === undefined ? 4 / 3 : rect.width / rect.height,
    selection: noSelection,
    retakeCandidate: null,
    revision: filled,
    canTakePhotographs: filled < template.windows.length,
    canReview: filled > 0,
    canContinue: filled === template.windows.length,
    ...rest,
  };
  return welcome("photographs", { photographs: view });
}

function arrange(template: TemplateOptionView, fields: Partial<PhotographsView> = {}): GuestWelcomeMessage {
  return photographs(template, {
    filled: template.windows.length,
    tab: "arrange",
    canTakePhotographs: false,
    canReview: true,
    canContinue: true,
    revision: 6,
    sequence: { ...idleSequence, flashSequence: template.windows.length },
    ...fields,
  });
}

export const guestCaptureFixtures: Readonly<Record<string, GuestWelcomeMessage>> = {
  "guest-ui-template-signature": templateStep("signature-strip"),
  "guest-ui-template-panorama": templateStep("panorama-four"),
  "guest-ui-template-gingham": templateStep("butter-gingham"),
  "guest-ui-camera-panorama": photographs(panoramaFour, { mirrored: false }),
  "guest-ui-camera-waiting": photographs(signatureStrip, { canTakePhotographs: false }),
  "guest-ui-camera-stopped": photographs(signatureStrip, { filled: 2 }),
  "guest-ui-camera-retaking": photographs(signatureStrip, {
    filled: 4,
    selection: { index: 1, mode: "retaking" },
    sequence: { ...idleSequence, running: true, targetWindow: 1, countdownRemaining: 4, flashSequence: 4, totalInSequence: 1 },
    cropAspect: 600 / 450,
    canTakePhotographs: false,
    canReview: false,
  }),
  "guest-ui-arrange-gingham": arrange(butterGingham),
  "guest-ui-arrange-polka": arrange(polkaKeepsake),
  "guest-ui-arrange-partial": arrange(signatureStrip, {
    windows: slots(signatureStrip, 3),
    canTakePhotographs: true,
    canContinue: false,
  }),
  "guest-ui-arrange-retained": arrange(butterGingham, { retainedCount: 1 }),
  "guest-ui-arrange-moving-panorama": arrange(panoramaFour, { selection: { index: 1, mode: "moving" }, canContinue: false }),
  "guest-ui-arrange-retaking-retained": arrange(signatureStrip, {
    selection: { index: 2, mode: "retaking" },
    retakeCandidate: { index: 2, imageUrl: fixtureCapture(11, 960, 720).src },
    canContinue: false,
    retainedCount: 1,
  }),
  "guest-ui-arrange-retaking-panorama": arrange(panoramaFour, {
    selection: { index: 1, mode: "retaking" },
    retakeCandidate: { index: 1, imageUrl: fixtureCapture(12, 540, 720).src },
    canContinue: false,
  }),
};