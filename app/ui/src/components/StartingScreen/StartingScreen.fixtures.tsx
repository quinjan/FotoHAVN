import type { ComponentFixture } from "../fixture.ts";
import { StartingScreen } from "./StartingScreen.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "starting-screen",
    title: "Starting, opening the Data folder",
    surface: "light",
    role: "operator",
    layout: "fill",
    render: () => <StartingScreen />,
  },
];
