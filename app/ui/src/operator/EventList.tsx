import { useId } from "react";
import { PrimaryAction } from "../components/PrimaryAction/index.ts";
import { SecondaryAction } from "../components/SecondaryAction/index.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import { TextLink } from "../components/TextLink/index.ts";
import type { EventListRow, RunningEventPanel } from "../protocol/generated.ts";
import { eventRunningCopy } from "./copy.ts";
import { formatEventDate, formatGuestCycles, formatLastUsed } from "./format.ts";
import { useSendCommand, type SendCommand } from "./OperatorCommands.tsx";
import styles from "./Operator.module.css";
import { RunningEventSection } from "./RunningEventSection.tsx";

type RowCommand = "StartEvent" | "OpenGallery" | "EditEvent" | "OpenEventFolder" | "DeleteEvent";

function sendRowCommand(command: SendCommand, name: RowCommand, row: EventListRow): void {
  void command(name, { eventId: row.id }, { key: `${name}:${row.id}` });
}

/** The row's actions as the brain's availability allows: the running Event offers Gallery and Open folder only. */
function RowActions({ row }: { readonly row: EventListRow }) {
  const command = useSendCommand();
  const reasonId = useId();
  const showStart = !row.running;
  const startDisabled = !row.actions.canStart;
  const startReason = showStart && startDisabled && row.startDisabledReason === "eventRunning" ? eventRunningCopy : null;
  return (
    <div className={styles.actions}>
      <div className={styles.actionRow}>
        {showStart && (
          <TextLink
            size="meta"
            disabled={startDisabled}
            aria-label={`Start ${row.name}`}
            aria-describedby={startReason === null ? undefined : reasonId}
            onClick={() => {
              sendRowCommand(command, "StartEvent", row);
            }}
          >
            START
          </TextLink>
        )}
        {row.actions.canOpenGallery && (
          <TextLink
            size="meta"
            aria-label={`Open the gallery of ${row.name}`}
            onClick={() => {
              sendRowCommand(command, "OpenGallery", row);
            }}
          >
            GALLERY
          </TextLink>
        )}
        {row.actions.canEdit && (
          <TextLink
            size="meta"
            aria-label={`Edit ${row.name}`}
            onClick={() => {
              sendRowCommand(command, "EditEvent", row);
            }}
          >
            EDIT
          </TextLink>
        )}
        {row.actions.canOpenFolder && (
          <TextLink
            size="meta"
            aria-label={`Open the folder of ${row.name}`}
            onClick={() => {
              sendRowCommand(command, "OpenEventFolder", row);
            }}
          >
            OPEN FOLDER
          </TextLink>
        )}
        {row.actions.canDelete && (
          <TextLink
            size="meta"
            aria-label={`Delete ${row.name}`}
            onClick={() => {
              sendRowCommand(command, "DeleteEvent", row);
            }}
          >
            DELETE
          </TextLink>
        )}
      </div>
      {startReason !== null && (
        <p id={reasonId} className={styles.reason}>
          {startReason}
        </p>
      )}
    </div>
  );
}

function EventRow({ row }: { readonly row: EventListRow }) {
  return (
    <li className={styles.row} data-event-id={row.id}>
      <div className={styles.body}>
        <div className={styles.title}>
          <h2 className={styles.name}>{row.name}</h2>
          {row.running && <p className={styles.running}>RUNNING</p>}
        </div>
        {/* The running Event's count and use are live in the panel above, so its row keeps only what the panel doesn't carry. */}
        <p className={styles.facts}>
          <span>{formatEventDate(row.date)}</span>
          {row.venue !== null && <span className={styles.venue}>{row.venue}</span>}
          {!row.running && <span>{formatGuestCycles(row.completedGuestCycles)}</span>}
          {!row.running && <span>{formatLastUsed(row.lastUsedAt)}</span>}
        </p>
      </div>
      <RowActions row={row} />
    </li>
  );
}

export interface EventListProps {
  readonly events: readonly EventListRow[];
  /** The running Event's facts, pinned above the list; null when no Event runs. */
  readonly running?: RunningEventPanel | null;
}

/**
 * The launch screen: every Event in the brain's order, each with the actions the brain allows, and `NEW EVENT →` as the
 * view's one primary action. While an Event runs, its panel sits above the list; during Operator Assistance the panel
 * takes the primary action (`RETRY`) and `NEW EVENT` becomes secondary.
 */
export function EventList({ events, running = null }: EventListProps) {
  const send = useSendCommand();
  const inAssistance = running !== null && running.assistance !== null;
  const openNewEvent = () => {
    void send("OpenNewEvent", {}, { key: "open-new-event" });
  };
  return (
    <div className={styles.view}>
      <header className={styles.header}>
        <StatementHeading level={1} lead="Events" />
        {inAssistance ? (
          <SecondaryAction arrow="right" onClick={openNewEvent}>
            NEW EVENT
          </SecondaryAction>
        ) : (
          <PrimaryAction arrow="right" onClick={openNewEvent}>
            NEW EVENT
          </PrimaryAction>
        )}
      </header>
      {running !== null && <RunningEventSection panel={running} />}
      {events.length === 0 ? (
        <p className={styles.empty}>No Events yet.</p>
      ) : (
        <ul className={styles.rows} aria-label="Events">
          {events.map((row) => (
            <EventRow key={row.id} row={row} />
          ))}
        </ul>
      )}
    </div>
  );
}
