import { CaretDownIcon, WarningCircleIcon } from "@phosphor-icons/react";
import { useState, type ReactNode } from "react";
import { useBrainClient } from "../../brain/BrainClientContext.ts";
import { Eyebrow } from "../../components/Eyebrow/index.ts";
import { PrintObject } from "../../components/PrintObject/index.ts";
import { TextLink } from "../../components/TextLink/index.ts";
import type { EventSetupView } from "../../protocol/generated.ts";
import { copy, timerNames } from "./setupCopy.ts";
import { stepForField, type SetupStepId } from "./setupSteps.ts";
import styles from "./SetupWizard.module.css";

type GroupId = "event" | "hardware" | "templates" | "looks" | "timing" | "guests";

interface Group {
  readonly id: GroupId;
  readonly step: SetupStepId;
  /** The fields of the brain's `fieldErrors` this group covers. */
  readonly covers: (field: string) => boolean;
  readonly title: string;
  /** The group's name for its EDIT action ("Edit Looks"). */
  readonly name: string;
  readonly value: string;
  readonly detail: ReactNode;
}

export interface SetupSummaryProps {
  readonly view: EventSetupView;
  readonly current: SetupStepId;
  readonly onEdit: (step: SetupStepId) => void;
}

/** The Event's date in words (`Saturday 14 November 2026`); an empty or unreadable date is left as it is. */
export function dateInWords(date: string): string {
  const parsed = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date);
  if (parsed === null) {
    return date;
  }
  const day = new Date(Date.UTC(Number(parsed[1]), Number(parsed[2]) - 1, Number(parsed[3])));
  const parts = new Intl.DateTimeFormat("en-GB", { weekday: "long", day: "numeric", month: "long", year: "numeric", timeZone: "UTC" }).formatToParts(day);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((candidate) => candidate.type === type)?.value ?? "";
  return `${part("weekday")} ${part("day")} ${part("month")} ${part("year")}`;
}

function Facts({ rows }: { readonly rows: readonly (readonly [string, ReactNode])[] }) {
  return (
    <dl className={styles.facts}>
      {rows.map(([term, value]) => (
        <div key={term} className={styles.fact}>
          <dt>{term}</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  );
}

function groups(view: EventSetupView, previewUrl: ((key: string) => string) | undefined): Group[] {
  const s = copy.summary;
  const { fields } = view;
  const offeredTemplates = view.templates.filter((template) => template.offered);
  const offeredLooks = view.looks.filter((look) => look.offered);
  const defaultTemplate = offeredTemplates.find((template) => template.isDefault);
  const defaultLook = offeredLooks.find((look) => look.isDefault);
  const timersOff = view.phaseTimers.filter((timer) => timer.canBeOff && timer.seconds === 0).length;
  const pinText = view.pin.changing ? s.pinEntered : view.pin.isSet ? s.pinSet : s.pinNotSet;
  const eventRows: [string, ReactNode][] = [
    [s.rows.name, fields.name],
    [s.rows.date, dateInWords(fields.date)],
    [s.rows.venue, fields.venue],
    [s.rows.customer, fields.customer],
  ];
  return [
    {
      id: "event",
      step: "event",
      covers: (field) => stepForField(field) === "event",
      title: s.event,
      name: s.event,
      value: dateInWords(fields.date),
      detail: <Facts rows={eventRows.filter(([, value]) => value !== "")} />,
    },
    {
      id: "hardware",
      step: "hardware",
      covers: (field) => stepForField(field) === "hardware",
      title: s.hardware,
      name: s.hardware,
      value: view.camera.selectedName ?? s.noCamera,
      detail: (
        <Facts
          rows={[
            [s.rows.camera, view.camera.selectedName ?? s.noCamera],
            [s.rows.mirror, view.mirror ? s.on : s.off],
            [s.rows.countdown, s.countdown(view.countdownSeconds)],
            [s.rows.guestDisplay, view.guestDisplay.selectedName ?? s.noDisplay],
            [s.rows.printer, copy.fields.printerValue],
          ]}
        />
      ),
    },
    {
      id: "templates",
      step: "templates",
      covers: (field) => field === "templates" || field === "defaultTemplate",
      title: s.templates(offeredTemplates.length),
      name: s.templatesName,
      value: offeredTemplates.length === 0 ? s.noneOffered : defaultTemplate === undefined ? "" : s.byDefault(defaultTemplate.name),
      detail: (
        <ul className={styles.prints}>
          {offeredTemplates.map((template) => (
            <li key={template.id} className={styles.printItem}>
              <span className={styles.printFrame}>
                <PrintObject
                  src={template.blankPreviewKey === null || previewUrl === undefined ? null : previewUrl(template.blankPreviewKey)}
                  alt=""
                  width={template.width}
                  height={template.height}
                  rotated={template.landscape}
                />
              </span>
              <span className={styles.printCaption}>{template.name}</span>
              {template.isDefault && <span className={styles.defaultMark}>{s.isDefault}</span>}
            </li>
          ))}
        </ul>
      ),
    },
    {
      id: "looks",
      step: "templates",
      covers: (field) => field === "looks" || field === "defaultLook",
      title: s.looks(offeredLooks.length),
      name: s.looksName,
      value: offeredLooks.length === 0 ? s.noneOffered : defaultLook === undefined ? "" : s.byDefault(defaultLook.name),
      detail: (
        <ul className={styles.names}>
          {offeredLooks.map((look) => (
            <li key={look.id}>
              {look.name}
              {look.isDefault && <span className={styles.defaultMark}>{s.isDefault}</span>}
            </li>
          ))}
        </ul>
      ),
    },
    {
      id: "timing",
      step: "timing",
      covers: (field) => stepForField(field) === "timing",
      title: s.timing,
      name: s.timing,
      value: timersOff === 0 ? s.allOn : s.someOff(timersOff),
      detail: (
        <Facts
          rows={view.phaseTimers.map((timer) => [timerNames[timer.phase], timer.seconds === 0 && timer.canBeOff ? s.off : s.seconds(timer.seconds)] as const)}
        />
      ),
    },
    {
      id: "guests",
      step: "guests",
      covers: (field) => stepForField(field) === "guests",
      title: s.guests,
      name: s.guests,
      value: pinText,
      detail: (
        <Facts
          rows={[
            [s.rows.guestSounds, view.guestSoundsEnabled ? s.on : s.off],
            [s.rows.guestGallery, view.guestGalleryEnabled ? s.on : s.off],
            [s.rows.pin, pinText],
          ]}
        />
      ),
    },
  ];
}

/**
 * The Event at a glance beside the step (DESIGN.md, Event setup): the brain's draft in words, one collapsible group
 * per part, the current step's groups open and marked with the Muted Brass rule. `EDIT` opens a group's step. After a
 * save attempt, a group holding a field the brain marked says "Needs attention" with an icon, never color alone.
 */
export function SetupSummary({ view, current, onEdit }: SetupSummaryProps) {
  const client = useBrainClient();
  const [opened, setOpened] = useState<ReadonlySet<GroupId>>(new Set());
  const errors = view.showErrors ? view.fieldErrors : [];
  const name = view.fields.name.trim();
  const facts = [dateInWords(view.fields.date), view.fields.venue].filter((part) => part !== "").join(", ");

  return (
    <aside className={styles.summary} aria-labelledby="setup-summary-title">
      <div className={styles.summaryHead}>
        <Eyebrow>{copy.summary.eyebrow}</Eyebrow>
        <h2 id="setup-summary-title" className={styles.summaryTitle}>
          {name === "" ? copy.summary.untitled : name}
        </h2>
        {facts !== "" && <p className={styles.summaryFacts}>{facts}</p>}
      </div>
      <div className={styles.summaryBody}>
        {groups(view, client.previewUrl).map((group) => {
          const isCurrent = group.step === current;
          const attention = errors.some((error) => group.covers(error.field));
          return (
            <details
              key={group.id}
              className={styles.group}
              data-group={group.id}
              data-current={isCurrent || undefined}
              open={isCurrent || opened.has(group.id)}
              onToggle={(event) => {
                if (isCurrent) {
                  return;
                }
                const open = event.currentTarget.open;
                setOpened((previous) => {
                  const next = new Set(previous);
                  if (open) {
                    next.add(group.id);
                  } else {
                    next.delete(group.id);
                  }
                  return next;
                });
              }}
            >
              <summary className={styles.groupSummary}>
                <span className={styles.groupTitle}>{group.title}</span>
                <span className={styles.groupValue} data-attention={attention || undefined}>
                  {attention ? (
                    <>
                      <span className={styles.groupIcon} aria-hidden="true">
                        <WarningCircleIcon weight="light" size="1em" />
                      </span>
                      {copy.summary.needsAttention}
                    </>
                  ) : (
                    group.value
                  )}
                </span>
                <span className={styles.caret} aria-hidden="true">
                  <CaretDownIcon weight="light" size="1em" />
                </span>
              </summary>
              <div className={styles.groupDetail}>
                {group.detail}
                {!isCurrent && (
                  <TextLink
                    size="meta"
                    aria-label={copy.summary.editLabel(group.name)}
                    onClick={() => {
                      onEdit(group.step);
                    }}
                  >
                    {copy.summary.edit}
                  </TextLink>
                )}
              </div>
            </details>
          );
        })}
      </div>
    </aside>
  );
}
