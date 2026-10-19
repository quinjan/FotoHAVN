import {
  PROTOCOL_VERSION,
  type ConfirmationOverlay,
  type EventSetupCameraView,
  type EventSetupGuestDisplayView,
  type EventSetupLookOption,
  type EventSetupTemplateOption,
  type EventSetupView,
  type GuestDisplayOption,
  type OperatorNoticeCode,
  type OperatorPresentation,
  type OperatorWelcomeMessage,
  type PhaseTimerOption,
  type RunningEventPanel,
} from "../protocol/generated.ts";

/**
 * Typed operator fixtures for the states the brain's own fixtures don't cover (or don't cover yet): the running-Event
 * panel in each state, confirmations and notices over the Event list, and Event setup in every state of its fields.
 * They are built here, typed against the generated protocol, so the fixture lane shows exactly what the brain's
 * presentations would, deterministic apart from the running Event's start time (always about an hour and a quarter ago).
 */

/** One operator snapshot: every part null unless given. */
export function operatorWelcome(surface: OperatorPresentation["surface"], parts: Partial<OperatorPresentation> = {}): OperatorWelcomeMessage {
  return {
    type: "welcome",
    role: "operator",
    protocolVersion: PROTOCOL_VERSION,
    revision: 1,
    presentation: {
      surface,
      eventList: null,
      eventSetup: null,
      runningEvent: null,
      overlays: { confirmation: null, identifyDisplays: null, notice: null },
      fatal: null,
      storageError: null,
      cameraDiagnostics: null,
      eventGallery: null,
      guestCycleDetail: null,
      ...parts,
    },
  };
}

const minute = 60_000;

/** The running-Event panel's facts, in the "normal" state: a connected display, a ready camera, 27 Guest Cycles. */
export function runningEventPanel(overrides: Partial<RunningEventPanel> = {}): RunningEventPanel {
  return {
    eventId: "event-5",
    name: "Event 5: Santos and Reyes celebration",
    guestDisplay: { name: "DELL P2419H", state: "connected" },
    camera: { state: "ready", reason: null },
    currentStep: "start",
    completedGuestCycles: 27,
    startedAt: new Date(Date.now() - 72 * minute).toISOString(),
    assistance: null,
    singleScreen: false,
    ...overrides,
  };
}

export function overlays(parts: { confirmation?: ConfirmationOverlay; notice?: OperatorNoticeCode } = {}): OperatorPresentation["overlays"] {
  return {
    confirmation: parts.confirmation ?? null,
    identifyDisplays: null,
    notice: parts.notice === undefined ? null : { code: parts.notice },
  };
}

const displays: readonly GuestDisplayOption[] = [
  { id: "display-1", name: "Built-in display", x: 0, y: 0, width: 1920, height: 1080, number: 1, isOperatorScreen: true },
  { id: "display-2", name: "DELL P2419H", x: 1920, y: 0, width: 1920, height: 1080, number: 2, isOperatorScreen: false },
];

const cameras = [
  { deviceId: "camera-1", name: "Logitech BRIO" },
  { deviceId: "camera-2", name: "Integrated Webcam" },
  { deviceId: "camera-3", name: "USB camera" },
];

const templateDefinitions = [
  { id: "signature-strip", name: "Signature Strip", description: "Four edge-to-edge photographs with a signature across the middle.", width: 600, height: 1800, landscape: false },
  { id: "panorama-four", name: "Panorama Four", description: "Four portraits side by side, with a quiet corner signature.", width: 1800, height: 600, landscape: true },
  { id: "butter-gingham", name: "Butter Gingham", description: "Three photographs framed in soft yellow checks.", width: 600, height: 1800, landscape: false },
  { id: "polka-keepsake", name: "Polka Keepsake", description: "Four photographs bordered by playful black dots.", width: 600, height: 1800, landscape: false },
] as const;

const lookDefinitions = [
  { id: "monochrome-v1", name: "Monochrome", description: "Black and white with deep blacks and strong contrast." },
  { id: "naturale-v1", name: "Naturale", description: "Original colors. No filter." },
  { id: "soft-monochrome-v1", name: "Soft Monochrome", description: "Black and white with softer contrast." },
  { id: "soft-color-v1", name: "Soft Color", description: "Slightly muted colors and softer highlights." },
  { id: "sepia-v1", name: "Sepia", description: "Soft brown tones with faded contrast." },
  { id: "warm-monochrome-v1", name: "Warm Monochrome", description: "Black and white with a light warm tint." },
  { id: "classic-v1", name: "Classic", description: "Warm colors with a golden tint." },
] as const;

function templates(offered: readonly string[], defaultId: string | null): EventSetupTemplateOption[] {
  return templateDefinitions.map((template) => ({
    ...template,
    offered: offered.includes(template.id),
    isDefault: template.id === defaultId,
    blankPreviewKey: `${template.id}.naturale-v1.r0`,
  }));
}

function looks(offered: readonly string[], defaultId: string | null): EventSetupLookOption[] {
  return lookDefinitions.map((look) => ({ ...look, offered: offered.includes(look.id), isDefault: look.id === defaultId }));
}

/** The six Phase Timers at their defaults (the plan's table), with `overrides` by phase. */
export function phaseTimers(overrides: Partial<Record<PhaseTimerOption["phase"], number>> = {}): PhaseTimerOption[] {
  const base: readonly Omit<PhaseTimerOption, "seconds">[] = [
    { phase: "template", defaultSeconds: 30, min: 20, max: 300, canBeOff: true },
    { phase: "photographsCapture", defaultSeconds: 30, min: 20, max: 300, canBeOff: true },
    { phase: "photographsReview", defaultSeconds: 45, min: 20, max: 300, canBeOff: true },
    { phase: "filters", defaultSeconds: 30, min: 20, max: 300, canBeOff: true },
    { phase: "keepsake", defaultSeconds: 12, min: 5, max: 60, canBeOff: false },
    { phase: "guestGallery", defaultSeconds: 60, min: 20, max: 300, canBeOff: false },
  ];
  return base.map((timer) => ({ ...timer, seconds: overrides[timer.phase] ?? timer.defaultSeconds }));
}

const allTemplateIds = templateDefinitions.map((template) => template.id);
const allLookIds = lookDefinitions.map((look) => look.id);

function cameraView(overrides: Partial<EventSetupCameraView> = {}): EventSetupCameraView {
  return {
    cameras,
    selectedDeviceId: null,
    selectedName: null,
    state: "none",
    reason: null,
    previewActive: false,
    mirrored: true,
    ...overrides,
  };
}

function displayView(overrides: Partial<EventSetupGuestDisplayView> = {}): EventSetupGuestDisplayView {
  return { displays: [...displays], selectedId: null, selectedName: null, state: "none", coversOperatorScreen: false, ...overrides };
}

/** A new Event's draft as the brain opens it (no camera, no display, all Templates and Looks, default timers), with `overrides`. */
export function setupView(overrides: Partial<EventSetupView> = {}): EventSetupView {
  return {
    eventId: null,
    isNew: true,
    dirty: false,
    fields: { name: "", date: "2026-11-14", venue: "", customer: "", notes: "" },
    fieldErrors: [],
    showErrors: false,
    camera: cameraView(),
    guestDisplay: displayView(),
    mirror: true,
    countdownSeconds: 5,
    printer: "none",
    templates: templates(allTemplateIds, "signature-strip"),
    looks: looks(allLookIds, "naturale-v1"),
    phaseTimers: phaseTimers(),
    guestGalleryEnabled: false,
    guestSoundsEnabled: true,
    pin: { isSet: false, changing: false },
    saveAndStartAvailable: true,
    saveAndStartDisabledReason: null,
    startFailure: null,
    saving: false,
    ...overrides,
  };
}

/** A saved Event being edited: everything chosen, the camera ready with its preview, the PIN set. */
export function editingSetupView(overrides: Partial<EventSetupView> = {}): EventSetupView {
  return setupView({
    eventId: "event-3",
    isNew: false,
    fields: {
      name: "Harbour gala",
      date: "2026-06-12",
      venue: "Harbour Hall",
      customer: "Lindqvist & Co",
      notes: "Bring the props. The Customer would like the strips in warm tones.",
    },
    camera: cameraView({ selectedDeviceId: "camera-1", selectedName: "Logitech BRIO", state: "ready", previewActive: true }),
    guestDisplay: displayView({ selectedId: "display-2", selectedName: "DELL P2419H", state: "connected" }),
    countdownSeconds: 10,
    templates: templates(["signature-strip", "panorama-four", "polka-keepsake"], "panorama-four"),
    looks: looks(["naturale-v1", "monochrome-v1", "sepia-v1", "classic-v1"], "naturale-v1"),
    phaseTimers: phaseTimers({ template: 45, photographsReview: 0, keepsake: 15 }),
    guestGalleryEnabled: true,
    pin: { isSet: true, changing: false },
    ...overrides,
  });
}

function setupMessage(view: EventSetupView, parts: Partial<OperatorPresentation> = {}): OperatorWelcomeMessage {
  return operatorWelcome("eventSetup", { eventSetup: view, ...parts });
}

const runningEventDisabled: Partial<EventSetupView> = {
  saveAndStartAvailable: false,
  saveAndStartDisabledReason: "eventRunning",
};

/** Every hand-written Event setup fixture, by name. */
export function operatorSetupFixtures(): Record<string, OperatorWelcomeMessage> {
  const chosenCamera = { selectedDeviceId: "camera-1", selectedName: "Logitech BRIO" } as const;
  return {
    "operator-setup-new": setupMessage(setupView({ camera: cameraView({ state: "none" }) })),
    "operator-setup-new-errors": setupMessage(
      setupView({
        dirty: true,
        showErrors: true,
        phaseTimers: phaseTimers({ keepsake: 2 }),
        fieldErrors: [
          { field: "name", code: "required" },
          { field: "camera", code: "cameraRequired" },
          { field: "guestDisplay", code: "displayRequired" },
          { field: "timers.keepsake", code: "timerOutOfRange" },
          { field: "pin", code: "pinRequired" },
        ],
      }),
    ),
    "operator-setup-errors-offered": setupMessage(
      editingSetupView({
        dirty: true,
        showErrors: true,
        fields: { name: "x".repeat(81), date: "", venue: "", customer: "", notes: "" },
        templates: templates([], null),
        looks: looks(["sepia-v1"], "classic-v1"),
        pin: { isSet: false, changing: true },
        fieldErrors: [
          { field: "name", code: "tooLong" },
          { field: "date", code: "required" },
          { field: "templates", code: "atLeastOneTemplate" },
          { field: "defaultLook", code: "defaultLookNotOffered" },
          { field: "pin", code: "pinMismatch" },
        ],
      }),
    ),
    "operator-setup-errors-start-only": setupMessage(
      editingSetupView({
        dirty: true,
        showErrors: true,
        pin: { isSet: false, changing: false },
        fieldErrors: [{ field: "pin", code: "pinRequired" }],
      }),
    ),
    "operator-setup-editing": setupMessage(editingSetupView()),
    "operator-setup-camera-checking": setupMessage(
      editingSetupView({ camera: cameraView({ ...chosenCamera, state: "checking", previewActive: true }) }),
    ),
    "operator-setup-camera-not-available": setupMessage(
      editingSetupView({ camera: cameraView({ ...chosenCamera, state: "notAvailable", reason: "privacyDenied" }) }),
    ),
    "operator-setup-camera-in-use": setupMessage(
      editingSetupView({ camera: cameraView({ ...chosenCamera, state: "inUseByAnotherApp", reason: "inUseByAnotherApp" }) }),
    ),
    "operator-setup-camera-in-use-by-running-event": setupMessage(
      editingSetupView({ ...runningEventDisabled, camera: cameraView({ ...chosenCamera, state: "inUseByRunningEvent" }) }),
      { runningEvent: runningEventPanel() },
    ),
    "operator-setup-camera-not-checked": setupMessage(
      editingSetupView({ ...runningEventDisabled, camera: cameraView({ selectedDeviceId: "camera-2", selectedName: "Integrated Webcam", state: "notChecked" }) }),
      { runningEvent: runningEventPanel() },
    ),
    "operator-setup-camera-missing": setupMessage(
      editingSetupView({
        camera: cameraView({ cameras: cameras.slice(1), selectedDeviceId: "camera-1", selectedName: "Logitech BRIO", state: "missing" }),
      }),
    ),
    "operator-setup-camera-none-found": setupMessage(setupView({ camera: cameraView({ cameras: [], state: "none" }) })),
    "operator-setup-display-missing": setupMessage(
      editingSetupView({ guestDisplay: displayView({ displays: displays.slice(0, 1), selectedId: "display-2", selectedName: "DELL P2419H", state: "missing" }) }),
    ),
    "operator-setup-covers-operator-screen": setupMessage(
      editingSetupView({
        guestDisplay: displayView({ displays: displays.slice(0, 1), selectedId: "display-1", selectedName: "Built-in display", state: "connected", coversOperatorScreen: true }),
      }),
    ),
    "operator-setup-leave-confirmation": setupMessage(editingSetupView({ dirty: true }), {
      overlays: overlays({ confirmation: { kind: "leaveSetup", eventName: "Harbour gala", counts: null, guestCycle: null } }),
    }),
    "operator-setup-start-failure-display": setupMessage(
      editingSetupView({ startFailure: { code: "guestDisplayMissing", cameraReason: null } }),
    ),
    "operator-setup-start-failure-camera": setupMessage(
      editingSetupView({
        camera: cameraView({ ...chosenCamera, state: "notAvailable", reason: "stale" }),
        startFailure: { code: "cameraNotEligible", cameraReason: "stale" },
      }),
    ),
    "operator-setup-start-failure-pin": setupMessage(
      editingSetupView({ pin: { isSet: false, changing: false }, startFailure: { code: "pinMissing", cameraReason: null } }),
    ),
    "operator-setup-start-failure-guest-screen": setupMessage(
      editingSetupView({ startFailure: { code: "guestScreenNotReady", cameraReason: null } }),
    ),
    "operator-setup-assistance": setupMessage(editingSetupView(runningEventDisabled), {
      runningEvent: runningEventPanel({ currentStep: "operatorAssistance", assistance: { reason: "camera" }, camera: { state: "notAvailable", reason: "stale" } }),
    }),
    "operator-setup-saving": setupMessage(editingSetupView({ dirty: true, saving: true })),
    "operator-setup-pin-changing": setupMessage(editingSetupView({ dirty: true, pin: { isSet: true, changing: true } })),
  };
}
