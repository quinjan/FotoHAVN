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
      <div className={styles.body}>
        <div className={styles.title}>
          <h2 className={styles.name}>{row.name}</h2>
          {row.running && <p className={styles.running}>RUNNING</p>}
        </div>
        <p className={styles.facts}>
          <span>{formatEventDate(row.date)}</span>
          {row.venue !== null && <span className={styles.venue}>{row.venue}</span>}
          <span>{formatGuestCycles(row.completedGuestCycles)}</span>
          <span>{formatLastUsed(row.lastUsedAt)}</span>
        </p>
      </div>
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
