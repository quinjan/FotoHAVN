import { useEffect, useRef, type ReactNode } from "react";
import type { ComponentFixture } from "../fixture.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/formFixture.module.css";
import { SegmentedChoice, type SegmentedChoiceOption } from "./SegmentedChoice.tsx";

const countdown: readonly SegmentedChoiceOption[] = [
  { value: "3", label: "3 s", accessibleLabel: "3 seconds" },
  { value: "5", label: "5 s", accessibleLabel: "5 seconds" },
  { value: "10", label: "10 s", accessibleLabel: "10 seconds" },
];

/** Focuses the radio group's tab stop (the chosen value), so the fixture shows where Tab lands. */
function FocusTabStop({ children }: { readonly children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    ref.current?.querySelector<HTMLElement>('input[tabindex="0"]')?.focus();
  }, []);
  return <div ref={ref}>{children}</div>;
}

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "segmented-choice-countdown",
    title: "Segmented choice, Countdown 5 s chosen",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <SegmentedChoice label="Countdown" options={countdown} value="5" onChange={noop} help="Before each Capture." />
      </div>
    ),
  },
  {
    id: "segmented-choice-unset",
    title: "Segmented choice, nothing chosen, with an error",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <SegmentedChoice label="Countdown" options={countdown} value={null} onChange={noop} error="Choose a countdown." />
      </div>
    ),
  },
  {
    id: "segmented-choice-focus",
    title: "Segmented choice, keyboard focus on the chosen value",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <FocusTabStop>
          <SegmentedChoice label="Countdown" options={countdown} value="10" onChange={noop} />
        </FocusTabStop>
      </div>
    ),
  },
  {
    id: "segmented-choice-disabled",
    title: "Segmented choice, disabled group and a disabled value",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <SegmentedChoice label="Countdown" options={countdown} value="5" disabled help="End the running Event to change this." />
        <SegmentedChoice
          label="Countdown"
          options={countdown.map((option) => (option.value === "10" ? { ...option, disabled: true } : option))}
          value="3"
          onChange={noop}
        />
      </div>
    ),
  },
  {
    id: "segmented-choice-dark",
    title: "Segmented choice, on a dark band",
    surface: "dark",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <SegmentedChoice label="Countdown" options={countdown} value="5" onChange={noop} />
      </div>
    ),
  },
];
