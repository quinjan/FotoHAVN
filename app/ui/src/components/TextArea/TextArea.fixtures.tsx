import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/formFixture.module.css";
import { TextArea } from "./TextArea.tsx";

const notes =
  "Booth stands left of the stage, beside the dessert table. The couple asked for Signature Strip and Naturale as defaults. " +
  "Power is on the wall behind the curtain; tape the cable down along the skirting board. The host's coordinator is Ana at the " +
  "registration desk, and the reception runs until eleven. Pack down only after the last dance.";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "text-area-empty",
    title: "Text area, empty, with its 1,000-character counter",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextArea label="Notes" maxLength={1000} value="" onChange={noop} help="For the team only." />
      </div>
    ),
  },
  {
    id: "text-area-filled",
    title: "Text area, long notes",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextArea label="Notes" maxLength={1000} rows={6} value={notes} onChange={noop} />
      </div>
    ),
  },
  {
    id: "text-area-error",
    title: "Text area, error in text",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextArea label="Notes" maxLength={1000} value={notes} onChange={noop} error="Notes can be up to 1,000 characters." />
      </div>
    ),
  },
  {
    id: "text-area-focus",
    title: "Text area, keyboard focus",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <FocusFirst>
          <TextArea label="Notes" maxLength={1000} value="" onChange={noop} />
        </FocusFirst>
      </div>
    ),
  },
  {
    id: "text-area-disabled",
    title: "Text area, disabled with its reason",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextArea label="Notes" disabled value="Booth stands left of the stage." help="End the running Event to change this." />
      </div>
    ),
  },
];
