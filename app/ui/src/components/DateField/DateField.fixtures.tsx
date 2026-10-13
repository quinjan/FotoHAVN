import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/formFixture.module.css";
import { DateField } from "./DateField.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "date-field-default",
    title: "Date field, required, filled",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <DateField label="Date" required value="2026-10-17" onChange={noop} help="The day the booth runs." />
      </div>
    ),
  },
  {
    id: "date-field-error",
    title: "Date field, empty with an error in text",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <DateField label="Date" required value="" onChange={noop} error="Choose the Event's date." />
      </div>
    ),
  },
  {
    id: "date-field-focus",
    title: "Date field, keyboard focus",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <FocusFirst>
          <DateField label="Date" required value="2026-10-17" onChange={noop} />
        </FocusFirst>
      </div>
    ),
  },
  {
    id: "date-field-disabled",
    title: "Date field, disabled with its reason",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <DateField label="Date" disabled value="2026-10-17" help="End the running Event to change this." />
      </div>
    ),
  },
];
