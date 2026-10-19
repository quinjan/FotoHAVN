import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import { RunningEventPanel, type RunningEventPanelProps } from "./RunningEventPanel.tsx";

const running: RunningEventPanelProps = {
  eventName: "Santos and Reyes wedding",
  guestDisplay: "connected",
  camera: "ready",
  step: "photographs",
  completedGuestCycles: 27,
  timeSinceStart: "1 h 12 min",
  onOpenGallery: noop,
  onRetry: noop,
  onEndEvent: noop,
};

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "running-event-panel",
    title: "Running-Event panel, a Guest Cycle on Photographs",
    surface: "light",
    role: "operator",
    render: () => <RunningEventPanel {...running} />,
  },
  {
    id: "running-event-panel-start-unavailable",
    title: "Running-Event panel, on Start with the camera not available",
    surface: "light",
    role: "operator",
    render: () => <RunningEventPanel {...running} step="start" camera="not-available" completedGuestCycles={0} timeSinceStart="4 min" />,
  },
  {
    id: "running-event-panel-camera-checking",
    title: "Running-Event panel, camera still being checked",
    surface: "light",
    role: "operator",
    render: () => <RunningEventPanel {...running} step="start" camera="checking" completedGuestCycles={0} timeSinceStart="Just started" />,
  },
  {
    id: "running-event-panel-assistance",
    title: "Running-Event panel, Operator Assistance with RETRY as the primary action",
    surface: "light",
    role: "operator",
    render: () => (
      <RunningEventPanel
        {...running}
        step="operator-assistance"
        guestDisplay="disconnected"
        assistance={{ reason: "The guest display isn't connected.", fix: "Reconnect it, then RETRY." }}
      />
    ),
  },
  {
    id: "running-event-panel-assistance-focus",
    title: "Running-Event panel, Operator Assistance, keyboard focus on RETRY",
    surface: "light",
    role: "operator",
    render: () => (
      <FocusFirst>
        <RunningEventPanel
          {...running}
          step="operator-assistance"
          camera="not-available"
          assistance={{ reason: "The camera stopped sending pictures.", fix: "Check its cable, then RETRY." }}
        />
      </FocusFirst>
    ),
  },
  {
    id: "running-event-panel-focus",
    title: "Running-Event panel, keyboard focus on OPEN GALLERY",
    surface: "light",
    role: "operator",
    render: () => (
      <FocusFirst>
        <RunningEventPanel {...running} step="keepsake" />
      </FocusFirst>
    ),
  },
  {
    id: "running-event-panel-long",
    title: "Running-Event panel, longest realistic name and facts",
    surface: "light",
    role: "operator",
    render: () => (
      <RunningEventPanel
        {...running}
        eventName="Alcantara-Villanueva and de los Santos fiftieth anniversary celebration at the Manila Hotel"
        step="guest-gallery"
        completedGuestCycles={1248}
        timeSinceStart="11 h 59 min"
      />
    ),
  },
];
