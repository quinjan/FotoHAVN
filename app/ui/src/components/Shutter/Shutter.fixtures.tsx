import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/fixtureLayout.module.css";
import { Shutter } from "./Shutter.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "shutter",
    title: "Shutter, idle, on the dark Photographs stage",
    surface: "dark",
    role: "guest",
    render: () => <Shutter onClick={noop} />,
  },
  {
    id: "shutter-recording",
    title: "Shutter, recording, on the dark stage",
    surface: "dark",
    role: "guest",
    render: () => <Shutter state="recording" onClick={noop} />,
  },
  {
    id: "shutter-disabled",
    title: "Shutter, disabled with its reason",
    surface: "dark",
    role: "guest",
    render: () => (
      <div className={styles.stack}>
        <Shutter disabled aria-describedby="shutter-reason" onClick={noop} />
        <p id="shutter-reason">The camera is getting ready.</p>
      </div>
    ),
  },
  {
    id: "shutter-focus",
    title: "Shutter, keyboard focus",
    surface: "dark",
    role: "guest",
    render: () => (
      <FocusFirst>
        <Shutter onClick={noop} />
      </FocusFirst>
    ),
  },
  {
    id: "shutter-light",
    title: "Shutter, idle and recording, on a light surface",
    surface: "light",
    role: "guest",
    render: () => (
      <div className={styles.row}>
        <Shutter onClick={noop} />
        <Shutter state="recording" onClick={noop} />
      </div>
    ),
  },
];
