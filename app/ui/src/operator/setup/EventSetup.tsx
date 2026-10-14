import { useEffect, useRef, useState, type ReactNode } from "react";
import { Eyebrow } from "../../components/Eyebrow/index.ts";
import { InlineStatus } from "../../components/InlineStatus/index.ts";
import { PrimaryAction } from "../../components/PrimaryAction/index.ts";
import { SecondaryAction } from "../../components/SecondaryAction/index.ts";
import { SegmentedChoice } from "../../components/SegmentedChoice/index.ts";
import { StatementHeading } from "../../components/StatementHeading/index.ts";
import { Switch } from "../../components/Switch/index.ts";
import { TextField } from "../../components/TextField/index.ts";
import { TextLink } from "../../components/TextLink/index.ts";
import type { AssistanceReason, CommandPayloadMap, EventField, EventSetupView } from "../../protocol/generated.ts";
import { eventRunningCopy } from "../copy.ts";
import { OperatorAssistanceAlert } from "../OperatorAssistanceAlert.tsx";
import { useSendCommand } from "../OperatorCommands.tsx";
import { CameraField } from "./CameraField.tsx";
import { DisplayField } from "./DisplayField.tsx";
import { DraftDateField, DraftTextArea, DraftTextField, TimerField } from "./DraftFields.tsx";
import { PinSection } from "./PinSection.tsx";
import { LookPicker, TemplatePicker } from "./Pickers.tsx";
import {
  copy,
  errorSummary,
  fieldErrorText,
  fieldIds,
  fieldOrder,
  focusTargetId,
  limits,
  startFailureText,
} from "./setupCopy.ts";
import styles from "./EventSetup.module.css";

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

export interface EventSetupProps {
  readonly view: EventSetupView;
  /** `IDENTIFY DISPLAYS` is showing its numbers on the displays. */
  readonly identifying: boolean;
  /** Set while the running Event is in Operator Assistance: the alert line shows on this view too. */
  readonly assistance: AssistanceReason | null;
}

/**
 * Event setup: one screen of sections in the plan's order, every control sending its command and showing only what the
 * brain's draft says. Errors appear in words under their fields after a save attempt; a failed save moves focus to the
 * first invalid field and announces how many things need attention.
 */
export function EventSetup({ view, identifying, assistance }: EventSetupProps) {
  const send = useSendCommand();
  const rootRef = useRef<HTMLDivElement>(null);
  const focusPending = useRef(false);
  const [, requestRender] = useState(0);

  const errors = view.showErrors ? view.fieldErrors : [];

  useEffect(() => {
    if (!focusPending.current || errors.length === 0 || rootRef.current === null) {
      return;
    }
    const order = fieldOrder(view.phaseTimers);
    const first = [...errors].sort((a, b) => order.indexOf(a.field) - order.indexOf(b.field))[0];
    focusPending.current = false;
    if (first !== undefined) {
      focusField(rootRef.current, focusTargetId(first.field, first.code));
    }
  });

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
      focusPending.current = true;
      requestRender((count) => count + 1);
    }
  }

  const field = (name: EventField) => (next: string) => {
    command("ChangeEventField", { field: name, value: next });
  };
  const pinCode = errors.find((error) => error.field === "pin")?.code;
  const startReasonId = "setup-save-and-start-reason";
  const startBlocked = !view.saveAndStartAvailable;

  return (
    <div ref={rootRef} className={styles.setup}>
      <header className={styles.header}>
        <TextLink
          arrow="left"
          onClick={() => {
            command("LeaveEventSetup", {}, "leave-setup");
          }}
        >
          {copy.back}
        </TextLink>
        <StatementHeading level={1} lead={copy.heading.lead} turn={copy.heading.turn} />
      </header>

      <div className={styles.statuses}>
        <OperatorAssistanceAlert
          reason={assistance}
          onReturn={() => {
            command("LeaveEventSetup", {}, "leave-setup");
          }}
        />
        <InlineStatus tone="alert">{view.startFailure === null ? "" : startFailureText(view.startFailure)}</InlineStatus>
        <InlineStatus tone="alert">{errors.length === 0 ? "" : errorSummary(errors.length)}</InlineStatus>
        <InlineStatus>{view.saving ? copy.saving : ""}</InlineStatus>
      </div>

      <div className={styles.form}>
        <Section id="setup-section-event" title={copy.sections.event}>
          <DraftTextField id={fieldIds.name} label={copy.fields.name} required maxLength={limits.name} value={view.fields.name} error={errorText("name")} onCommit={field("name")} />
          <DraftDateField id={fieldIds.date} label={copy.fields.date} required value={view.fields.date} error={errorText("date")} onCommit={field("date")} />
          <DraftTextField id={fieldIds.venue} label={copy.fields.venue} maxLength={limits.venue} value={view.fields.venue} error={errorText("venue")} onCommit={field("venue")} />
          <DraftTextField id={fieldIds.customer} label={copy.fields.customer} maxLength={limits.customer} value={view.fields.customer} error={errorText("customer")} onCommit={field("customer")} />
          <DraftTextArea id={fieldIds.notes} label={copy.fields.notes} maxLength={limits.notes} value={view.fields.notes} error={errorText("notes")} onCommit={field("notes")} />
        </Section>

        <Section id="setup-section-camera" title={copy.sections.camera}>
          <CameraField
            camera={view.camera}
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

        <Section id="setup-section-timing" title={copy.sections.timing}>
          {view.phaseTimers.map((timer) => (
            <TimerField
              key={timer.phase}
              timer={timer}
              error={errorText(`timers.${timer.phase}`)}
              onCommit={(phase, seconds) => {
                command("SetPhaseTimer", { phase, seconds });
              }}
            />
          ))}
        </Section>

        <Section id="setup-section-guests" title={copy.sections.guests}>
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
      </div>

      <div className={styles.actions}>
        <PrimaryAction
          disabled={view.saving}
          onClick={() => {
            void save("SaveEvent");
          }}
        >
          {copy.save}
        </PrimaryAction>
        <SecondaryAction
          disabled={view.saving || startBlocked}
          aria-describedby={startBlocked ? startReasonId : undefined}
          onClick={() => {
            void save("SaveAndStartEvent");
          }}
        >
          {copy.saveAndStart}
        </SecondaryAction>
        {startBlocked && (
          <p id={startReasonId} className={styles.reason}>
            {view.saveAndStartDisabledReason === "eventRunning" ? eventRunningCopy : ""}
          </p>
        )}
      </div>
    </div>
  );
}
