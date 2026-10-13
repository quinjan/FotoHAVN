import { useSignalReady } from "../host/useSignalReady.ts";
import styles from "./App.module.css";

/** M1 placeholder page; the real role-based surfaces arrive in M2 and M3. */
export function App() {
  useSignalReady();
  return (
    <main className={styles.main}>
      <h1 className={styles.title}>FOTOHAVN</h1>
      <p className={styles.status}>The booth is getting ready.</p>
    </main>
  );
}
