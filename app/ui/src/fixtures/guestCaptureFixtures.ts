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
 * other Templates, a camera that isn't ready, an idle sequence with windows filled, the Camera tab during a Retake, the Phase
 * Timer in its last seconds, a partial arrangement,
 * and the Panorama Move and Retake. Names start with `guest-ui-` and never repeat a brain-generated name. The states the brain's
 * fixtures do write (the default Camera, countdown, pause, Arrange, Move, Retake, one-Look Arrange) come from those files.
 */

type TimedPhase = NonNullable<GuestOverlays["phaseTimer"]>["phase"];

/** The overlays with the step's Phase Timer running, as the brain sends them whenever the phase's timer is on. */
function timed(phase: TimedPhase, remainingSeconds: number): GuestOverlays {
  return { exitHold: null, pinDialog: null, phaseTimer: { phase, remainingSeconds, paused: false }, notice: null };
}

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
      { step: "template", state: step === "template" ? "current" : "done" },
      { step: "photographs", state: step === "photographs" ? "current" : "upcoming" },
      { step: "filters", state: "upcoming" },
      { step: "keepsake", state: "upcoming" },
    ],
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
      overlays: step === "template" ? timed("template", 42) : timed("photographsCapture", 45),
      fatal: null,
      guestCycle: cycle(step),
      template: null,
      photographs: null,
      filters: null,
      keepsake: null,
      operatorAssistance: null,
      guestGallery: null,
      guestStripDetail: null,
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
  soundsOn: true,
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
  return welcome("photographs", {
    photographs: view,
    ...(view.tab === "arrange" ? { overlays: timed("photographsReview", 52) } : {}),
  });
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

/** The same state in the phase's last seconds, when the Phase Timer fills (and, once, is announced). */
function withTimer(message: GuestWelcomeMessage, phase: TimedPhase, remainingSeconds: number): GuestWelcomeMessage {
  return { ...message, presentation: { ...message.presentation, overlays: timed(phase, remainingSeconds) } };
}

export const guestCaptureFixtures: Readonly<Record<string, GuestWelcomeMessage>> = {
  "guest-ui-template-signature": templateStep("signature-strip"),
  "guest-ui-template-panorama": templateStep("panorama-four"),
  "guest-ui-template-gingham": templateStep("butter-gingham"),
  // The Phase Timer in its last seconds over the tallest Template list (four offered): the pill fills, nothing moves.
  "guest-ui-template-timer-low": withTimer(templateStep("signature-strip"), "template", 8),
  "guest-ui-camera-panorama": photographs(panoramaFour, { mirrored: false }),
  "guest-ui-camera-waiting": photographs(signatureStrip, { canTakePhotographs: false }),
  // Two windows filled and the sequence idle (back from Arrange with empty windows): the count, and SEE MY PHOTOGRAPHS beside the capture area.
  "guest-ui-camera-partial": photographs(signatureStrip, { filled: 2 }),
  // The last second of a countdown: the numeral shows "1" with the cue "Look at the camera.".
  "guest-ui-camera-last-second": photographs(signatureStrip, {
    filled: 1,
    sequence: { ...idleSequence, running: true, targetWindow: 1, countdownRemaining: 1, flashSequence: 1, totalInSequence: 4 },
    canTakePhotographs: false,
  }),
  // S1's "low" state: ready to start with 8 seconds left, the header pill filled.
  "guest-ui-camera-timer-low": withTimer(photographs(signatureStrip, {}), "photographsCapture", 8),
  // A Retake ready to start: the same tap to start, with KEEP ORIGINAL beside the capture area instead of the review link.
  "guest-ui-camera-retaking-ready": photographs(signatureStrip, {
    filled: 4,
    selection: { index: 1, mode: "retaking" },
    cropAspect: 600 / 450,
    canReview: false,
    canTakePhotographs: true,
  }),  "guest-ui-camera-retaking": photographs(signatureStrip, {
    filled: 4,
    selection: { index: 1, mode: "retaking" },
    sequence: { ...idleSequence, running: true, targetWindow: 1, countdownRemaining: 4, flashSequence: 4, totalInSequence: 1 },
    cropAspect: 600 / 450,
    canTakePhotographs: false,
    canReview: false,
  }),
  "guest-ui-arrange-gingham": arrange(butterGingham),
  "guest-ui-arrange-timer-low": withTimer(arrange(signatureStrip), "photographsReview", 9),
  "guest-ui-arrange-polka": arrange(polkaKeepsake),
  "guest-ui-arrange-partial": arrange(signatureStrip, {
    windows: slots(signatureStrip, 3),
    canTakePhotographs: true,
    canContinue: false,
  }),
  "guest-ui-arrange-moving-panorama": arrange(panoramaFour, { selection: { index: 1, mode: "moving" }, canContinue: false }),
  "guest-ui-arrange-retaking": arrange(signatureStrip, {
    selection: { index: 2, mode: "retaking" },
    retakeCandidate: { index: 2, imageUrl: fixtureCapture(11, 960, 720).src },
    canContinue: false,
  }),
  "guest-ui-arrange-retaking-panorama": arrange(panoramaFour, {
    selection: { index: 1, mode: "retaking" },
    retakeCandidate: { index: 1, imageUrl: fixtureCapture(12, 540, 720).src },
    canContinue: false,
  }),
};