import type { ReactNode } from "react";
import styles from "./EditorialLift.module.css";

export default function EditorialLift({ hero, children }: { hero: ReactNode; children: ReactNode }) {
  return (
    <div className={styles.root}>
      <div className={styles.stage}>
        <div className={styles.heroLayer}>{hero}</div>
        <div className={styles.experienceLayer}>{children}</div>
      </div>
    </div>
  );
}
