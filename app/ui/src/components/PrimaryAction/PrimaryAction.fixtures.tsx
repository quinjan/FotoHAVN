import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import { PrimaryAction } from "./PrimaryAction.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "primary-action-operator",
    title: "Primary action, operator, with arrow",
    surface: "light",
    role: "operator",
    render: () => (
      <PrimaryAction arrow="right" onClick={noop}>
        NEW EVENT
      </PrimaryAction>
    ),
  },
  {
    id: "primary-action-operator-no-arrow",
    title: "Primary action, operator, no arrow",
    surface: "light",
    role: "operator",
    render: () => <PrimaryAction onClick={noop}>SAVE</PrimaryAction>,
  },
  {
    id: "primary-action-guest",
    title: "Primary action, guest",
    surface: "light",
    role: "guest",
    render: () => (
      <PrimaryAction arrow="right" onClick={noop}>
        STEP INSIDE
      </PrimaryAction>
    ),
  },
  {
    id: "primary-action-guest-long",
    title: "Primary action, guest, long label",
    surface: "light",
    role: "guest",
    render: () => (
      <PrimaryAction arrow="right" onClick={noop}>
        CONTINUE TO PHOTOGRAPHS
      </PrimaryAction>
    ),
  },
  {
    id: "primary-action-disabled",
    title: "Primary action, disabled",
    surface: "light",
    role: "operator",
    render: () => (
      <PrimaryAction arrow="right" disabled onClick={noop}>
        NEW EVENT
      </PrimaryAction>
    ),
  },
  {
    id: "primary-action-focus",
    title: "Primary action, keyboard focus",
    surface: "light",
    role: "guest",
    render: () => (
      <FocusFirst>
        <PrimaryAction arrow="right" onClick={noop}>
          FINISH MY STRIP
        </PrimaryAction>
      </FocusFirst>
    ),
  },
  {
    id: "primary-action-inverted",
    title: "Primary action, inverted on dark",
    surface: "dark",
    role: "guest",
    render: () => <PrimaryAction onClick={noop}>RETRY</PrimaryAction>,
  },
  {
    id: "primary-action-inverted-focus",
    title: "Primary action, inverted on dark, keyboard focus",
    surface: "dark",
    role: "guest",
    render: () => (
      <FocusFirst>
        <PrimaryAction onClick={noop}>RETRY</PrimaryAction>
      </FocusFirst>
    ),
  },
  {
    id: "primary-action-inverted-disabled",
    title: "Primary action, inverted on dark, disabled",
    surface: "dark",
    role: "operator",
    render: () => (
      <PrimaryAction disabled onClick={noop}>
        RETRY
      </PrimaryAction>
    ),
  },
];
