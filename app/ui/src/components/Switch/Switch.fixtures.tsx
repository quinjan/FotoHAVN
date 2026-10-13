import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/formFixture.module.css";
import { Switch } from "./Switch.tsx";

const permission = "Turn this on only with the Customer's permission.";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "switch-off",
    title: "Switch, off, with help",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Switch label="Guest gallery" checked={false} onCheckedChange={noop} help={permission} />
      </div>
    ),
  },
  {
    id: "switch-on",
    title: "Switch, on",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Switch label="Mirror" checked onCheckedChange={noop} help="Applies to the preview and saved Captures." />
      </div>
    ),
  },
  {
    id: "switch-focus",
    title: "Switch, keyboard focus",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <FocusFirst>
          <Switch label="Mirror" checked onCheckedChange={noop} />
        </FocusFirst>
      </div>
    ),
  },
  {
    id: "switch-error",
    title: "Switch, error in text",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Switch label="Guest gallery" checked onCheckedChange={noop} help={permission} error="Confirm the Customer's permission first." />
      </div>
    ),
  },
  {
    id: "switch-disabled",
    title: "Switch, disabled on and off, with the reason",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Switch label="Mirror" checked disabled onCheckedChange={noop} />
        <Switch label="Guest gallery" checked={false} disabled onCheckedChange={noop} help="End the running Event to change this." />
      </div>
    ),
  },
  {
    id: "switch-dark",
    title: "Switch, on and off on a dark band",
    surface: "dark",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Switch label="Mirror" checked onCheckedChange={noop} />
        <Switch label="Guest gallery" checked={false} onCheckedChange={noop} help={permission} />
      </div>
    ),
  },
];
