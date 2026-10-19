import type { ComponentFixture } from "../fixture.ts";
import { PrimaryAction } from "../PrimaryAction/index.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import { SecondaryAction } from "./SecondaryAction.tsx";
import styles from "../shared/fixtureLayout.module.css";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "secondary-action-guest",
    title: "Secondary action, guest",
    surface: "light",
    role: "guest",
    render: () => <SecondaryAction onClick={noop}>TRY AGAIN</SecondaryAction>,
  },
  {
    id: "secondary-action-operator",
    title: "Secondary action, operator",
    surface: "light",
    role: "operator",
    render: () => <SecondaryAction onClick={noop}>SAVE AND START</SecondaryAction>,
  },
  {
    id: "secondary-action-disabled",
    title: "Secondary action, disabled",
    surface: "light",
    role: "operator",
    render: () => (
      <SecondaryAction disabled onClick={noop}>
        SAVE AND START
      </SecondaryAction>
    ),
  },
  {
    id: "secondary-action-paired",
    title: "Secondary action paired with the primary action",
    surface: "light",
    role: "guest",
    render: () => (
      <div className={styles.row}>
        <PrimaryAction onClick={noop}>ACCEPT</PrimaryAction>
        <SecondaryAction onClick={noop}>TRY AGAIN</SecondaryAction>
      </div>
    ),
  },
  {
    id: "secondary-action-inverted",
    title: "Secondary action, inverted on dark",
    surface: "dark",
    role: "operator",
    render: () => (
      <SecondaryAction arrow="right" onClick={noop}>
        NEW EVENT
      </SecondaryAction>
    ),
  },
];
