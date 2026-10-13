import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/formFixture.module.css";
import { Checkbox } from "./Checkbox.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "checkbox-states",
    title: "Checkbox, unchecked and checked",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Checkbox label="Offer Monochrome" checked={false} onChange={noop} />
        <Checkbox label="Offer Naturale" checked onChange={noop} description="Original colors. No filter." />
      </div>
    ),
  },
  {
    id: "checkbox-help",
    title: "Checkbox, with help under it",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Checkbox label="Show the guest gallery" checked={false} onChange={noop} help="Turn this on only with the Customer's permission." />
      </div>
    ),
  },
  {
    id: "checkbox-error",
    title: "Checkbox, error in text",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Checkbox label="Offer Signature Strip" checked={false} onChange={noop} error="Offer at least one Template." />
      </div>
    ),
  },
  {
    id: "checkbox-focus",
    title: "Checkbox, keyboard focus",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <FocusFirst>
          <Checkbox label="Offer Sepia" checked onChange={noop} />
        </FocusFirst>
      </div>
    ),
  },
  {
    id: "checkbox-disabled",
    title: "Checkbox, disabled unchecked and checked",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Checkbox label="Offer Classic" disabled checked={false} onChange={noop} />
        <Checkbox label="Offer Naturale" disabled checked onChange={noop} help="End the running Event to change this." />
      </div>
    ),
  },
];
