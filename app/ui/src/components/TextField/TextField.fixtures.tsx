import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/formFixture.module.css";
import { TextField } from "./TextField.tsx";

const longestName = "The Santos and Villanueva wedding reception, Grand Ballroom, Manila Hotel, evening";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "text-field-empty",
    title: "Text field, empty and required, with help and counter",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextField label="Name" required maxLength={80} value="" onChange={noop} help="Shown in the Event list." />
      </div>
    ),
  },
  {
    id: "text-field-filled",
    title: "Text field, filled, with counters",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextField label="Name" required maxLength={80} value="Santos wedding" onChange={noop} />
        <TextField label="Venue" maxLength={120} value="Manila Hotel" onChange={noop} />
        <TextField label="Customer or host" maxLength={120} value="" placeholder="Who booked the booth" onChange={noop} />
      </div>
    ),
  },
  {
    id: "text-field-longest",
    title: "Text field, at its 80-character limit",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextField label="Name" required maxLength={80} value={longestName.slice(0, 80)} onChange={noop} />
      </div>
    ),
  },
  {
    id: "text-field-error",
    title: "Text field, error in text",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextField label="Name" required maxLength={80} value="" onChange={noop} error="Enter a name for the Event." />
      </div>
    ),
  },
  {
    id: "text-field-focus",
    title: "Text field, keyboard focus",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <FocusFirst>
          <TextField label="Venue" maxLength={120} value="Manila Hotel" onChange={noop} />
        </FocusFirst>
      </div>
    ),
  },
  {
    id: "text-field-read-only",
    title: "Text field, read-only fixed value",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextField label="Printer" readOnly value="No printer" />
      </div>
    ),
  },
  {
    id: "text-field-disabled",
    title: "Text field, disabled with its reason",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextField label="Name" disabled value="Santos wedding" help="End the running Event to change this." />
      </div>
    ),
  },
  {
    id: "text-field-dark",
    title: "Text field, on a dark band",
    surface: "dark",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <TextField label="Venue" maxLength={120} value="Manila Hotel" onChange={noop} help="Where the booth stands." />
      </div>
    ),
  },
];
