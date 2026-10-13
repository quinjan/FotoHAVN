import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/formFixture.module.css";
import { Select, type SelectOption } from "./Select.tsx";

const cameras: readonly SelectOption[] = [
  { value: "usb-1", label: "Logitech BRIO" },
  { value: "usb-2", label: "Integrated Camera" },
  { value: "usb-3", label: "Elgato Facecam Pro (in use by another app)", disabled: true },
];

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "select-placeholder",
    title: "Select, nothing chosen yet",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Select label="Camera" required options={cameras} placeholder="Choose a camera" value="" onChange={noop} />
      </div>
    ),
  },
  {
    id: "select-chosen",
    title: "Select, chosen, with help",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Select label="Camera" required options={cameras} value="usb-1" onChange={noop} help="Ready" />
      </div>
    ),
  },
  {
    id: "select-error",
    title: "Select, error in text",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Select
          label="Camera"
          required
          options={cameras}
          placeholder="Choose a camera"
          value=""
          onChange={noop}
          error="Choose a camera for the booth."
        />
      </div>
    ),
  },
  {
    id: "select-focus",
    title: "Select, keyboard focus",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <FocusFirst>
          <Select label="Camera" required options={cameras} value="usb-2" onChange={noop} />
        </FocusFirst>
      </div>
    ),
  },
  {
    id: "select-disabled",
    title: "Select, disabled with its reason",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Select label="Camera" disabled options={cameras} value="usb-1" help="End the running Event to change this." />
      </div>
    ),
  },
];
