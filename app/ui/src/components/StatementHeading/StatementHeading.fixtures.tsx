import type { ComponentFixture } from "../fixture.ts";
import { PrimaryAction } from "../PrimaryAction/index.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import { TextLink } from "../TextLink/index.ts";
import { StatementHeading } from "./StatementHeading.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "statement-heading-start",
    title: "Statement heading, guest Start moment",
    surface: "light",
    role: "guest",
    render: () => (
      <StatementHeading
        level={1}
        size="display"
        align="center"
        lead="Small room."
        turn="Whole other world."
        supporting="Come a little closer."
        action={
          <>
            <PrimaryAction arrow="right" onClick={noop}>
              STEP INSIDE
            </PrimaryAction>
            <TextLink arrow="right" onClick={noop}>
              SEE THE GOOD COMPANY
            </TextLink>
          </>
        }
      />
    ),
  },
  {
    id: "statement-heading-step",
    title: "Statement heading, guest step heading",
    surface: "light",
    role: "guest",
    render: () => (
      <StatementHeading level={1} lead="Make a little" turn="Moment" supporting="Choose your template." />
    ),
  },
  {
    id: "statement-heading-turn-own-line",
    title: "Statement heading, italic turn on its own line",
    surface: "light",
    role: "guest",
    render: () => (
      <StatementHeading
        level={1}
        size="heading-lg"
        align="center"
        lead="A little moment."
        turn="Yours."
        turnOnOwnLine
        supporting="Keep the moment close."
      />
    ),
  },
  {
    id: "statement-heading-dark",
    title: "Statement heading on dark, Operator Assistance",
    surface: "dark",
    role: "guest",
    render: () => (
      <StatementHeading
        level={1}
        size="heading-lg"
        align="center"
        lead="A little"
        turn="interruption."
        supporting="Please ask the FOTOHAVN team for help."
        action={<PrimaryAction onClick={noop}>RETRY</PrimaryAction>}
      />
    ),
  },
  {
    id: "statement-heading-operator",
    title: "Statement heading, operator section with eyebrow",
    surface: "light",
    role: "operator",
    render: () => (
      <StatementHeading
        eyebrow="SANTOS AND REYES WEDDING"
        lead="Every strip,"
        turn="kept."
        supporting={["Completed Guest Cycles appear here as they finish.", "Open one to see its photographs."]}
      />
    ),
  },
  {
    id: "statement-heading-plain",
    title: "Statement heading, operator, no italic turn",
    surface: "light",
    role: "operator",
    render: () => <StatementHeading level={1} lead="Events" />,
  },
];
