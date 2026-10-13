import type { ComponentFixture } from "../fixture.ts";
import type { GuestStrip, OperatorGuestCycle } from "../GuestCycleList/index.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import { fixtureCapture, fixtureStrip, type FixtureTemplate } from "../shared/fixturePhotographs.ts";
import { GuestCycleDetail } from "./GuestCycleDetail.tsx";

const stripAlt = "Photo Strip from this Event";

function strip(template: FixtureTemplate, seed: number): GuestStrip {
  return { id: `${template}-${String(seed)}`, ...fixtureStrip(template, seed), alt: stripAlt };
}

function captures(count: number, seed: number, alt = "Photograph from this Event") {
  return Array.from({ length: count }, (_, n) => ({ ...fixtureCapture(seed + n), alt }));
}

const completed: OperatorGuestCycle = {
  id: "c5",
  time: "9:14 PM",
  outcome: "completed",
  templateName: "Signature Strip",
  lookName: "Soft Monochrome",
  strip: strip("signature-strip", 0),
  captures: captures(4, 0, "Capture 1 of 4"),
};

const panorama: OperatorGuestCycle = {
  id: "c4",
  time: "9:06 PM",
  outcome: "completed",
  templateName: "Panorama Four",
  lookName: "Naturale",
  strip: strip("panorama-four", 2),
  captures: captures(4, 2, "Capture"),
};

const startedOver: OperatorGuestCycle = {
  id: "c3",
  time: "8:57 PM",
  outcome: "started-over",
  templateName: "Butter Gingham",
  strip: null,
  captures: captures(2, 4, "Capture"),
};

const appClosed: OperatorGuestCycle = {
  id: "c1",
  time: "8:42 PM",
  outcome: "app-closed",
  templateName: "Signature Strip",
  strip: null,
  captures: [],
};

const operator = { mode: "operator", onOpenFolder: noop, onDelete: noop } as const;

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "guest-cycle-detail-operator",
    title: "Guest Cycle detail, operator, completed",
    surface: "light",
    role: "operator",
    render: () => <GuestCycleDetail {...operator} cycle={completed} />,
  },
  {
    id: "guest-cycle-detail-operator-panorama",
    title: "Guest Cycle detail, operator, Panorama Four, with CLOSE",
    surface: "light",
    role: "operator",
    render: () => <GuestCycleDetail {...operator} cycle={panorama} onClose={noop} />,
  },
  {
    id: "guest-cycle-detail-operator-no-strip",
    title: "Guest Cycle detail, operator, started over: no Photo Strip, kept Captures",
    surface: "light",
    role: "operator",
    render: () => <GuestCycleDetail {...operator} cycle={startedOver} />,
  },
  {
    id: "guest-cycle-detail-operator-nothing-kept",
    title: "Guest Cycle detail, operator, app closed with nothing kept",
    surface: "light",
    role: "operator",
    render: () => <GuestCycleDetail {...operator} cycle={appClosed} />,
  },
  {
    id: "guest-cycle-detail-operator-focus",
    title: "Guest Cycle detail, operator, keyboard focus on OPEN FOLDER",
    surface: "light",
    role: "operator",
    render: () => (
      <FocusFirst>
        <GuestCycleDetail {...operator} cycle={completed} />
      </FocusFirst>
    ),
  },
  {
    id: "guest-cycle-detail-guest",
    title: "Guest strip detail, lifted, with its arranged Captures",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => <GuestCycleDetail mode="guest" strip={strip("signature-strip", 0)} captures={captures(4, 0)} onClose={noop} />,
  },
  {
    id: "guest-cycle-detail-guest-panorama",
    title: "Guest strip detail, Panorama Four turned a quarter counterclockwise",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => <GuestCycleDetail mode="guest" strip={strip("panorama-four", 2)} captures={captures(4, 2)} onClose={noop} />,
  },
  {
    id: "guest-cycle-detail-guest-three",
    title: "Guest strip detail, three Captures (Butter Gingham), keyboard focus on CLOSE",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <FocusFirst>
        <GuestCycleDetail mode="guest" strip={strip("butter-gingham", 1)} captures={captures(3, 1)} onClose={noop} />
      </FocusFirst>
    ),
  },
];
