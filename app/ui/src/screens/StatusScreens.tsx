import type { ProtocolMismatch } from "../brain/types.ts";
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
      <h1 className={styles.statement}>
        Not <em>connected.</em>
      </h1>
      <p className={styles.line}>This screen can&apos;t reach the booth. Open FOTOHAVN to use it.</p>
      {fixtureNames.length > 0 && (
        <nav aria-label="Fixtures">
          <ul className={styles.links}>
            {fixtureNames.map((name) => (
              <li key={name}>
                <a href={`?fixture=${encodeURIComponent(name)}`}>{name}</a>
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
      <h1 className={styles.statement}>
        A little <em>mix-up.</em>
      </h1>
      <p className={styles.line}>This screen and the booth are out of step. Restart FOTOHAVN to fix it.</p>
      {mismatch !== null && (
        <p className={styles.meta}>
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
