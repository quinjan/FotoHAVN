import { useEffect, useRef, useState, type ReactNode } from "react";
import { Eyebrow } from "../../components/Eyebrow/index.ts";
import { InlineStatus } from "../../components/InlineStatus/index.ts";
import { PrimaryAction } from "../../components/PrimaryAction/index.ts";
import { SecondaryAction } from "../../components/SecondaryAction/index.ts";
import { SegmentedChoice } from "../../components/SegmentedChoice/index.ts";
import { fieldStyles } from "../../components/shared/field.tsx";
import { StatementHeading } from "../../components/StatementHeading/index.ts";
import { Switch } from "../../components/Switch/index.ts";
import { TextField } from "../../components/TextField/index.ts";
import { TextLink } from "../../components/TextLink/index.ts";
import type { AssistanceReason, CommandPayloadMap, EventField, EventSetupFieldError, EventSetupView } from "../../protocol/generated.ts";
import { eventRunningCopy } from "../copy.ts";
import { OperatorAssistanceAlert } from "../OperatorAssistanceAlert.tsx";
import { useSendCommand } from "../OperatorCommands.tsx";
import { CameraField } from "./CameraField.tsx";
import { DisplayField } from "./DisplayField.tsx";
import { DraftDateField, DraftTextArea, DraftTextField, TimerField } from "./DraftFields.tsx";
import { ErrorSummary } from "./ErrorSummary.tsx";
import { PinSection } from "./PinSection.tsx";
import { LookPicker, TemplatePicker } from "./Pickers.tsx";
import {
  copy,
  fieldErrorText,
  fieldIds,
  fieldOrder,
  focusTargetId,
  limits,
  startFailureText,
  timingIntro,
} from "./setupCopy.ts";
import { SetupStepNav } from "./SetupStepNav.tsx";
import { setupSteps, stepForField, stepForStartFailure, type SetupStepId } from "./setupSteps.ts";
import { SetupSummary } from "./SetupSummary.tsx";
import styles from "./EventSetup.module.css";
import wizard from "./SetupWizard.module.css";

const countdownOptions = [3, 5, 10].map((seconds) => ({
  value: String(seconds),
  label: `${String(seconds)} s`,
  accessibleLabel: `${String(seconds)} seconds`,
}));

function Section({ id, title, children }: { readonly id: string; readonly title: string; readonly children: ReactNode }) {
  return (
    <section className={styles.section} aria-labelledby={id}>
      <Eyebrow id={id}>{title}</Eyebrow>
      <div className={styles.sectionBody}>{children}</div>
    </section>
  );
}

/** Focuses a field by id; for a group (a fieldset of options) it focuses the first control that takes the Tab key. */
function focusField(root: HTMLElement, id: string): void {
  const target = root.querySelector<HTMLElement>(`[id="${id}"]`);
  if (target === null) {
    return;
  }
  if (target instanceof HTMLFieldSetElement) {
    (
      target.querySelector<HTMLElement>('input:not(:disabled):not([tabindex="-1"])') ??
      target.querySelector<HTMLElement>("input:not(:disabled)")
    )?.focus();
    return;
  }
  target.focus();
}

/** The marked fields in the order the steps show them. */
function sortErrors(errors: readonly EventSetupFieldError[], view: EventSetupView): readonly EventSetupFieldError[] {
  const order = fieldOrder(view.phaseTimers);
  return [...errors].sort((a, b) => order.indexOf(a.field) - order.indexOf(b.field));
}

/** The first field the brain marked, in the order the steps show them. */
function firstError(errors: readonly EventSetupFieldError[], view: EventSetupView): EventSetupFieldError | undefined {
  return sortErrors(errors, view)[0];
}

/**
 * The step that opens first: the step that fixes a start failure the view opens with (START from the Event list), else the
 * one with the first marked field when the brain shows errors, otherwise the Event step.
 */
function openingStep(view: EventSetupView): SetupStepId {
  const failed = view.startFailure === null ? null : stepForStartFailure(view.startFailure);
  if (failed !== null) {
    return failed;
  }
  const first = view.showErrors ? firstError(view.fieldErrors, view) : undefined;
  return first === undefined ? "event" : stepForField(first.field);
}

export interface EventSetupProps {
  readonly view: EventSetupView;
  /** `IDENTIFY DISPLAYS` is showing its numbers on the displays. */
  readonly identifying: boolean;
  /** Set while the running Event is in Operator Assistance: the alert line shows on this view too. */
  readonly assistance: AssistanceReason | null;
}

/**
 * Event setup in five steps (DESIGN.md, Event setup): `01 Event`, `02 Hardware`, `03 Templates`, `04 Timing`, and
 * `05 Guests`, with a summary of the whole draft beside the step. Which step shows is the page's own view state; every
 * control sends its command and shows only what the brain's draft says, and the brain validates only at save, so any
 * step can be opened at any time. Every step stays mounted while hidden, so what is being typed survives a step change.
 * A refused save opens the step of the first invalid field and moves focus to the error summary, whose links open a
 * problem's step and focus its field.
 */
export function EventSetup({ view, identifying, assistance }: EventSetupProps) {
  const send = useSendCommand();
  const rootRef = useRef<HTMLDivElement>(null);
  const headRef = useRef<HTMLDivElement>(null);
  const summaryRef = useRef<HTMLDivElement>(null);
  /** The problem the operator chose in the error summary, until its step shows and its field takes focus. */
  const pendingFocus = useRef<EventSetupFieldError | null>(null);
  const [step, setStep] = useState<SetupStepId>(() => openingStep(view));
  /** How many times the operator has changed step; each change moves focus to the new step's content. */
  const [moves, setMoves] = useState(0);
  /** Counts refused saves; `jumpedFor` and `focusedFor` record which refusal has opened its step and taken focus. */
  const [refusals, setRefusals] = useState(0);
  const [jumpedFor, setJumpedFor] = useState(0);
  const focusedFor = useRef(0);

  const errors = view.showErrors ? sortErrors(view.fieldErrors, view) : [];
  const stepIndex = setupSteps.findIndex((candidate) => candidate.id === step);
  const nextStep = setupSteps[stepIndex + 1];
  const attention = new Set(errors.map((error) => stepForField(error.field)));
  const first = errors[0];

  // A refused save opens the step of the first invalid field, once the brain's errors are here (they can arrive after
  // the refusal). Adjusted while rendering, like useDraftText, so the step shows in the same commit.
  if (refusals !== jumpedFor && first !== undefined) {
    setJumpedFor(refusals);
    setStep(stepForField(first.field));
  }

  // A start that didn't go ahead opens the step where the operator can fix what the brain named.
  const [seenFailure, setSeenFailure] = useState(view.startFailure?.code ?? null);
  if ((view.startFailure?.code ?? null) !== seenFailure) {
    setSeenFailure(view.startFailure?.code ?? null);
    const target = view.startFailure === null ? null : stepForStartFailure(view.startFailure);
    if (target !== null) {
      setStep(target);
    }
  }

  useEffect(() => {
    if (refusals === focusedFor.current || refusals !== jumpedFor || first === undefined) {
      return;
    }
    if (stepForField(first.field) !== step) {
      return;
    }
    focusedFor.current = refusals;
    summaryRef.current?.focus();
  });

  // A problem chosen in the summary opens its step (when it isn't showing) and focuses its field once that step is shown.
  useEffect(() => {
    const target = pendingFocus.current;
    if (target === null || rootRef.current === null || stepForField(target.field) !== step) {
      return;
    }
    pendingFocus.current = null;
    focusField(rootRef.current, focusTargetId(target.field, target.code));
  });

  // A step change moves focus to the top of the view (the heading), which also scrolls it back into view below the bar,
  // so the keyboard continues from the status lines into the new step.
  useEffect(() => {
    if (moves > 0) {
      headRef.current?.focus();
    }
  }, [moves]);

  function goTo(target: SetupStepId): void {
    if (target === step) {
      return;
    }
    setStep(target);
    setMoves((count) => count + 1);
  }

  /** A problem in the error summary: open its step and focus the field, which isn't focusable until its step shows. */
  function showProblem(problem: EventSetupFieldError): void {
    const target = stepForField(problem.field);
    pendingFocus.current = problem;
    if (target === step) {
      if (rootRef.current !== null) {
        pendingFocus.current = null;
        focusField(rootRef.current, focusTargetId(problem.field, problem.code));
      }
      return;
    }
    setStep(target);
  }

  const errorText = (field: string): string | undefined => {
    const error = errors.find((candidate) => candidate.field === field);
    return error === undefined ? undefined : fieldErrorText(field, error.code, view.phaseTimers);
  };

  /** Sends a command; with a `key`, a second request is ignored until the first is answered. */
  function command<K extends keyof CommandPayloadMap>(name: K, payload: CommandPayloadMap[K], key?: string): void {
    void send(name, payload, key === undefined ? undefined : { key });
  }

  async function save(name: "SaveEvent" | "SaveAndStartEvent"): Promise<void> {
    const result = await send(name, {}, { key: "save-event", expected: ["validationFailed"] });
    if (result !== null && result.status === "rejected" && result.reason === "validationFailed") {
      setRefusals((count) => count + 1);
    }
  }

  const field = (name: EventField) => (next: string) => {
    command("ChangeEventField", { field: name, value: next });
  };
  const pinCode = errors.find((error) => error.field === "pin")?.code;
  const startReasonId = "setup-save-and-start-reason";
  const startBlocked = !view.saveAndStartAvailable;

  const stepContent: Record<SetupStepId, ReactNode> = {
    event: (
      <Section id="setup-section-event" title={copy.sections.event}>
        <DraftTextField id={fieldIds.name} label={copy.fields.name} required maxLength={limits.name} value={view.fields.name} error={errorText("name")} onCommit={field("name")} />
        <DraftDateField id={fieldIds.date} label={copy.fields.date} required value={view.fields.date} error={errorText("date")} onCommit={field("date")} />
        <DraftTextField id={fieldIds.venue} label={copy.fields.venue} maxLength={limits.venue} value={view.fields.venue} error={errorText("venue")} onCommit={field("venue")} />
        <DraftTextField id={fieldIds.customer} label={copy.fields.customer} maxLength={limits.customer} value={view.fields.customer} error={errorText("customer")} onCommit={field("customer")} />
        <DraftTextArea id={fieldIds.notes} label={copy.fields.notes} maxLength={limits.notes} value={view.fields.notes} error={errorText("notes")} onCommit={field("notes")} />
      </Section>
    ),
    hardware: (
      <>
        <Section id="setup-section-camera" title={copy.sections.camera}>
          <CameraField
            camera={view.camera}
            active={step === "hardware"}
            error={errorText("camera")}
            onSelect={(deviceId) => {
              command("SelectCamera", { deviceId });
            }}
          />
          <Switch
            label={copy.fields.mirror}
            help={copy.fields.mirrorHelp}
            checked={view.mirror}
            onCheckedChange={(enabled) => {
              command("SetMirror", { enabled });
            }}
          />
          <SegmentedChoice
            label={copy.fields.countdown}
            options={countdownOptions}
            value={String(view.countdownSeconds)}
            onChange={(seconds) => {
              command("SetCountdown", { seconds: Number(seconds) });
            }}
          />
        </Section>
        <Section id="setup-section-display" title={copy.sections.guestDisplay}>
          <DisplayField
            guestDisplay={view.guestDisplay}
            error={errorText("guestDisplay")}
            identifying={identifying}
            onSelect={(displayId) => {
              command("SelectGuestDisplay", { displayId });
            }}
            onIdentify={() => {
              command("IdentifyDisplays", {}, "identify-displays");
            }}
          />
        </Section>
        <Section id="setup-section-printer" title={copy.sections.printer}>
          <TextField label={copy.fields.printer} value={copy.fields.printerValue} readOnly showCount={false} />
        </Section>
      </>
    ),
    templates: (
      <>
        <Section id="setup-section-templates" title={copy.sections.templates}>
          <TemplatePicker
            templates={view.templates}
            error={errorText("templates") ?? errorText("defaultTemplate")}
            onOfferedChange={(templateIds) => {
              command("SetOfferedTemplates", { templateIds: [...templateIds] });
            }}
            onDefaultChange={(templateId) => {
              command("SetDefaultTemplate", { templateId });
            }}
          />
        </Section>
        <Section id="setup-section-filters" title={copy.sections.filters}>
          <LookPicker
            looks={view.looks}
            error={errorText("looks") ?? errorText("defaultLook")}
            onOfferedChange={(lookIds) => {
              command("SetOfferedLooks", { lookIds: [...lookIds] });
            }}
            onDefaultChange={(lookId) => {
              command("SetDefaultLook", { lookId });
            }}
          />
        </Section>
      </>
    ),
    timing: (
      <Section id="setup-section-timing" title={copy.sections.timing}>
        <p className={fieldStyles.help}>{timingIntro(view.phaseTimers)}</p>
        {view.phaseTimers.map((timer) => (
          <TimerField
            key={timer.phase}
            timer={timer}
            timers={view.phaseTimers}
            error={errorText(`timers.${timer.phase}`)}
            onCommit={(phase, seconds) => {
              command("SetPhaseTimer", { phase, seconds });
            }}
          />
        ))}
      </Section>
    ),
    guests: (
      <>
        <Section id="setup-section-guests" title={copy.sections.guests}>
          <Switch
            label={copy.fields.guestSounds}
            help={copy.fields.guestSoundsHelp}
            checked={view.guestSoundsEnabled}
            onCheckedChange={(enabled) => {
              command("SetGuestSounds", { enabled });
            }}
          />
          <Switch
            label={copy.fields.guestGallery}
            help={copy.fields.guestGalleryHelp}
            checked={view.guestGalleryEnabled}
            onCheckedChange={(enabled) => {
              command("SetGuestGallery", { enabled });
            }}
          />
        </Section>
        <Section id="setup-section-pin" title={copy.sections.pin}>
          <PinSection
            pin={view.pin}
            pinError={pinCode === "pinMismatch" ? undefined : errorText("pin")}
            confirmationError={pinCode === "pinMismatch" ? errorText("pin") : undefined}
            onChange={(pin, confirmation) => {
              command("SetPin", { pin, confirmation });
            }}
          />
        </Section>
      </>
    ),
  };

  return (
    <div ref={rootRef} className={styles.setup}>
      <header className={wizard.bar}>
        <div className={wizard.barBack}>
          <TextLink
            arrow="left"
            onClick={() => {
              command("LeaveEventSetup", {}, "leave-setup");
            }}
          >
            {copy.back}
          </TextLink>
        </div>
        <div className={wizard.barNav}>
          <SetupStepNav current={step} attention={attention} onSelect={goTo} />
        </div>
        <div className={wizard.barActions}>
          {nextStep === undefined ? (
            <>
              <SecondaryAction
                disabled={view.saving || startBlocked}
                aria-describedby={startBlocked ? startReasonId : undefined}
                onClick={() => {
                  void save("SaveAndStartEvent");
                }}
              >
                {copy.saveAndStart}
              </SecondaryAction>
              <PrimaryAction
                disabled={view.saving}
                onClick={() => {
                  void save("SaveEvent");
                }}
              >
                {copy.save}
              </PrimaryAction>
            </>
          ) : (
            <PrimaryAction
              arrow="right"
              onClick={() => {
                goTo(nextStep.id);
              }}
            >
              {copy.continueTo(nextStep.label)}
            </PrimaryAction>
          )}
        </div>
      </header>

      <div className={styles.body}>
        <div className={styles.main}>
          <div ref={headRef} className={styles.head} tabIndex={-1}>
            <StatementHeading level={1} lead={copy.heading.lead} turn={copy.heading.turn} />
          </div>

          <div className={styles.statuses}>
            <OperatorAssistanceAlert
              reason={assistance}
              onReturn={() => {
                command("LeaveEventSetup", {}, "leave-setup");
              }}
            />
            <InlineStatus tone="alert">{view.startFailure === null ? "" : startFailureText(view.startFailure)}</InlineStatus>
            <ErrorSummary errors={errors} timers={view.phaseTimers} summaryRef={summaryRef} onSelect={showProblem} />
            <InlineStatus>{view.saving ? copy.saving : ""}</InlineStatus>
          </div>

          <div className={styles.panel} data-moved={moves > 0}>
            {setupSteps.map((candidate) => (
              <div key={candidate.id} className={styles.form} data-setup-step={candidate.id} hidden={candidate.id !== step}>
                {stepContent[candidate.id]}
              </div>
            ))}
          </div>

          <div className={styles.stepFoot}>
            {stepIndex > 0 && (
              <TextLink
                arrow="left"
                onClick={() => {
                  goTo(setupSteps[stepIndex - 1]?.id ?? "event");
                }}
              >
                {copy.previousStep}
              </TextLink>
            )}
            {startBlocked && nextStep === undefined && (
              <p id={startReasonId} className={styles.reason}>
                {view.saveAndStartDisabledReason === "eventRunning" ? eventRunningCopy : ""}
              </p>
            )}
          </div>
        </div>

        <div className={styles.aside}>
          <SetupSummary view={view} current={step} onEdit={goTo} />
        </div>
      </div>
    </div>
  );
}
