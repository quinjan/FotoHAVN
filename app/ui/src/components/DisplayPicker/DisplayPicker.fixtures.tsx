import { useEffect, useRef, type ReactNode } from "react";
import type { ComponentFixture } from "../fixture.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/formFixture.module.css";
import { DisplayPicker, type DisplayPickerDisplay } from "./DisplayPicker.tsx";

const operator: DisplayPickerDisplay = {
  id: "\\\\?\\DISPLAY#LEN40A9#1",
  name: "Lenovo ThinkPad display",
  width: 1920,
  height: 1080,
  position: "Primary",
  isOperatorScreen: true,
};

const guest: DisplayPickerDisplay = {
  id: "\\\\?\\DISPLAY#DELA1F3#2",
  name: "DELL P2422H",
  width: 1920,
  height: 1080,
  position: "Right of the Operator Screen",
};

const touch: DisplayPickerDisplay = {
  id: "\\\\?\\DISPLAY#IIY6603#3",
  name: "iiyama ProLite T2455MSC touch",
  width: 2560,
  height: 1440,
  position: "Left of the Operator Screen",
};

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
    id: "display-picker-two-displays",
    title: "Display picker, guest display chosen",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <DisplayPicker displays={[operator, guest]} value={guest.id} onChange={noop} onIdentify={noop} />
      </div>
    ),
  },
  {
    id: "display-picker-unchosen",
    title: "Display picker, three displays, none chosen, with an error",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <DisplayPicker
          displays={[operator, guest, touch]}
          value={null}
          onChange={noop}
          onIdentify={noop}
          error="Choose the display guests will use."
        />
      </div>
    ),
  },
  {
    id: "display-picker-operator-screen",
    title: "Display picker, the Operator Screen's display chosen, with the D38 note",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <DisplayPicker displays={[operator]} value={operator.id} onChange={noop} onIdentify={noop} />
      </div>
    ),
  },
  {
    id: "display-picker-not-connected",
    title: "Display picker, the bound display isn't connected",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <DisplayPicker
          displays={[operator, touch]}
          value={guest.id}
          boundDisplayName={guest.name}
          onChange={noop}
          onIdentify={noop}
        />
      </div>
    ),
  },
  {
    id: "display-picker-focus",
    title: "Display picker, keyboard focus on the chosen row",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <FocusTabStop>
          <DisplayPicker displays={[operator, guest]} value={guest.id} onChange={noop} onIdentify={noop} />
        </FocusTabStop>
      </div>
    ),
  },
  {
    id: "display-picker-identifying",
    title: "Display picker, IDENTIFY DISPLAYS unavailable while the numbers show",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <DisplayPicker displays={[operator, guest]} value={guest.id} onChange={noop} onIdentify={noop} identifyDisabled />
      </div>
    ),
  },
];
