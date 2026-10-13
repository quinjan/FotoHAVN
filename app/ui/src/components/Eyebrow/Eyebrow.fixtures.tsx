import type { ComponentFixture } from "../fixture.ts";
import { Eyebrow } from "./Eyebrow.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "eyebrow-operator",
    title: "Eyebrow, operator",
    surface: "light",
    role: "operator",
    render: () => <Eyebrow>SANTOS AND REYES WEDDING</Eyebrow>,
  },
  {
    id: "eyebrow-guest",
    title: "Eyebrow, guest",
    surface: "light",
    role: "guest",
    render: () => <Eyebrow>THE FOTOHAVN EXPERIENCE</Eyebrow>,
  },
  {
    id: "eyebrow-dark",
    title: "Eyebrow on dark",
    surface: "dark",
    role: "guest",
    render: () => <Eyebrow>ON THE OTHER SIDE</Eyebrow>,
  },
];
