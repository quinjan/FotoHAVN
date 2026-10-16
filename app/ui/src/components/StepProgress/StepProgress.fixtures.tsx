import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import { StepProgress } from "./StepProgress.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "step-progress-template",
    title: "Step progress, on Template",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="template" onSelectStep={noop} />,
  },
  {
    id: "step-progress-photographs",
    title: "Step progress, on Photographs, Template completed",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="photographs" onSelectStep={noop} />,
  },
  {
    id: "step-progress-filters",
    title: "Step progress, on Filters, two completed steps to go back to",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="filters" onSelectStep={noop} />,
  },
  {
    id: "step-progress-skipped-template",
    title: "Step progress, on Filters, Template skipped by a one-Template Event",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="filters" onSelectStep={noop} skipped={["template"]} />,
  },
  {
    id: "step-progress-omitted-steps",
    title: "Step progress, on Photographs, Template and Filters skipped and left out, renumbered",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="photographs" steps={["photographs", "keepsake"]} />,
  },
  {
    id: "step-progress-keepsake",
    title: "Step progress, on Keepsake, no going back",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="keepsake" />,
  },
  {
    id: "step-progress-focus",
    title: "Step progress, keyboard focus on a completed step",
    surface: "light",
    role: "guest",
    render: () => (
      <FocusFirst>
        <StepProgress current="filters" onSelectStep={noop} />
      </FocusFirst>
    ),
  },
  {
    id: "step-progress-dark",
    title: "Step progress on dark",
    surface: "dark",
    role: "guest",
    render: () => <StepProgress current="photographs" onSelectStep={noop} />,
  },
];
