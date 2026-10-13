import type { ProtocolMismatch } from "../brain/types.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import { TextLink } from "../components/TextLink/index.ts";
import styles from "./Screen.module.css";

/** Before the first `welcome`. */
export function ConnectingScreen() {
  return (
    <main className={styles.screen} aria-busy="true" data-fotohavn-screen="connecting">
      <h1 className={styles.wordmark}>FOTOHAVN</h1>
      <p className={styles.line}>The booth is getting ready.</p>
    </main>
  );
}

/** No bridge exists: a field build opened outside the host, or an unknown fixture. */
export function NotConnectedScreen({ fixtureNames }: { readonly fixtureNames: readonly string[] }) {
  return (
    <main className={styles.screen} data-fotohavn-screen="notConnected">
      <StatementHeading
        level={1}
        size="heading-lg"
        align="center"
        lead="Not"
        turn="connected."
        supporting="This screen can't reach the booth. Open FOTOHAVN to use it."
      />
      {fixtureNames.length > 0 && (
        <nav aria-label="Fixtures">
          <ul className={styles.links}>
            {fixtureNames.map((name) => (
              <li key={name}>
                <TextLink size="meta" href={`?fixture=${encodeURIComponent(name)}`}>
                  {name}
                </TextLink>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </main>
  );
}

/** The fixed screen for a `protocolMismatch` answer; the UI and brain always ship together, so it means a broken install. */
export function ProtocolMismatchScreen({ mismatch }: { readonly mismatch: ProtocolMismatch | null }) {
  return (
    <main className={styles.screen} data-fotohavn-screen="protocolMismatch">
      <StatementHeading
        level={1}
        size="heading-lg"
        align="center"
        lead="A little"
        turn="mix-up."
        supporting="This screen and the booth are out of step. Restart FOTOHAVN to fix it."
      />
      {mismatch !== null && (
        <p className={styles.line}>
          Booth version {mismatch.brainVersion} · Screen version {mismatch.uiVersion}
        </p>
      )}
    </main>
  );
}

/** Announces a lost connection on whichever surface is showing; the last snapshot stays visible. */
export function ConnectionNotice({ reconnecting }: { readonly reconnecting: boolean }) {
  return (
    <p className={styles.notice} role="status">
      {reconnecting ? "Reconnecting to the booth." : ""}
    </p>
  );
}
