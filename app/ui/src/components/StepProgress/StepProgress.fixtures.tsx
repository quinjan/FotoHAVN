import type { ComponentFixture } from "../fixture.ts";
import { StepProgress } from "./StepProgress.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "step-progress-template",
    title: "Step progress, on Template",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="template" />,
  },
  {
    id: "step-progress-photographs",
    title: "Step progress, on Photographs, Template completed",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="photographs" />,
  },
  {
    id: "step-progress-filters",
    title: "Step progress, on Filters, two completed steps",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="filters" />,
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
    title: "Step progress, on Keepsake",
    surface: "light",
    role: "guest",
    render: () => <StepProgress current="keepsake" />,
  },
  {
    id: "step-progress-dark",
    title: "Step progress on dark",
    surface: "dark",
    role: "guest",
    render: () => <StepProgress current="photographs" />,
  },
];