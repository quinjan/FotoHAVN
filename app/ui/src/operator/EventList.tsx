import { useState } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { PrimaryAction } from "../components/PrimaryAction/index.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { EventListRow } from "../protocol/generated.ts";
import { commandNotice, formatEventDate, formatGuestCycles, formatLastUsed } from "./format.ts";
import styles from "./Operator.module.css";

function EventRow({ row }: { readonly row: EventListRow }) {
  return (
    <li className={styles.row} data-event-id={row.id}>
      <h2 className={styles.name}>{row.name}</h2>
      {row.running && <p className={styles.running}>RUNNING</p>}
      <dl className={styles.facts}>
        <div className={styles.fact}>
          <dt>Date</dt>
          <dd>{formatEventDate(row.date)}</dd>
        </div>
        <div className={styles.fact}>
          <dt>Venue</dt>
          <dd>{row.venue ?? "No venue"}</dd>
        </div>
        <div className={styles.fact}>
          <dt>Completed</dt>
          <dd>{formatGuestCycles(row.completedGuestCycles)}</dd>
        </div>
        <div className={styles.fact}>
          <dt>Last used</dt>
          <dd>{formatLastUsed(row.lastUsed)}</dd>
        </div>
      </dl>
    </li>
  );
}

/** The launch screen: every Event, in the brain's order, with `NEW EVENT →` as the one primary action. */
export function EventList({ events }: { readonly events: readonly EventListRow[] }) {
  const client = useBrainClient();
  const [pending, setPending] = useState(false);
  const [notice, setNotice] = useState("");

  async function openNewEvent(): Promise<void> {
    if (pending) {
      return;
    }
    setPending(true);
    setNotice("");
    const result = await client.sendCommand("OpenNewEvent", {});
    setPending(false);
    setNotice(commandNotice(result) ?? "");
  }

  return (
    <div className={styles.view}>
      <header className={styles.header}>
        <StatementHeading level={1} lead="Events" />
        <PrimaryAction
          arrow="right"
          disabled={pending}
          onClick={() => {
            void openNewEvent();
          }}
        >
          NEW EVENT
        </PrimaryAction>
      </header>
      {events.length === 0 ? (
        <p className={styles.empty}>No Events yet.</p>
      ) : (
        <ul className={styles.rows} aria-label="Events">
          {events.map((row) => (
            <EventRow key={row.id} row={row} />
          ))}
        </ul>
      )}
      <p className={styles.notice} role="status">
        {notice}
      </p>
    </div>
  );
}
