import { useEffect, useRef, type ReactNode } from "react";
import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import { fixtureCapture, fixtureStrip, type FixtureTemplate } from "../shared/fixturePhotographs.ts";
import type { GuestStrip, OperatorGuestCycle } from "./galleryModel.ts";
import { GuestCycleList } from "./GuestCycleList.tsx";

const stripAlt = "Photo Strip from this Event";

function strip(template: FixtureTemplate, seed: number) {
  return { ...fixtureStrip(template, seed), alt: stripAlt };
}

function captures(count: number, seed: number) {
  return Array.from({ length: count }, (_, n) => ({ ...fixtureCapture(seed + n), alt: "Capture from this Guest Cycle" }));
}

const cycles: readonly OperatorGuestCycle[] = [
  {
    id: "c5",
    time: "9:14 PM",
    outcome: "completed",
    templateName: "Signature Strip",
    lookName: "Soft Monochrome",
    strip: strip("signature-strip", 0),
    captures: captures(4, 0),
  },
  {
    id: "c4",
    time: "9:06 PM",
    outcome: "completed",
    templateName: "Panorama Four",
    lookName: "Naturale",
    strip: strip("panorama-four", 2),
    captures: captures(4, 2),
  },
  {
    id: "c3",
    time: "8:57 PM",
    outcome: "started-over",
    templateName: "Butter Gingham",
    strip: null,
    captures: captures(2, 4),
  },
  {
    id: "c2",
    time: "8:49 PM",
    outcome: "completed",
    templateName: "Polka Keepsake",
    lookName: "Sepia",
    strip: strip("polka-keepsake", 3),
    captures: captures(4, 3),
  },
  {
    id: "c1",
    time: "8:42 PM",
    outcome: "app-closed",
    templateName: "Signature Strip",
    strip: null,
    captures: [],
  },
];

const gone: OperatorGuestCycle = {
  id: "gone",
  time: "9:14 PM",
  outcome: "app-closed",
  templateName: "Signature Strip",
  lookName: "Sepia",
  strip: null,
  captures: [],
  unavailable: true,
};

const endings: readonly OperatorGuestCycle[] = [
  { id: "e1", time: "10:02 PM", outcome: "ended-by-operator", templateName: "Signature Strip", strip: null, captures: captures(3, 1) },
  { id: "e2", time: "9:58 PM", outcome: "unrecoverable", templateName: "Butter Gingham", lookName: "Classic", strip: null, captures: captures(1, 5) },
  {
    id: "e3",
    time: "9:51 PM",
    outcome: "completed",
    templateName: "Butter Gingham",
    lookName: "Warm Monochrome",
    strip: strip("butter-gingham", 1),
    captures: captures(3, 1),
  },
];

const strips: readonly GuestStrip[] = [
  { id: "s1", ...strip("signature-strip", 0) },
  { id: "s2", ...strip("panorama-four", 2) },
  { id: "s3", ...strip("polka-keepsake", 3) },
  { id: "s4", ...strip("butter-gingham", 1) },
  { id: "s5", ...strip("signature-strip", 4) },
  { id: "s6", ...strip("signature-strip", 5) },
  { id: "s7", ...strip("polka-keepsake", 1) },
  { id: "s8", ...strip("panorama-four", 0) },
  { id: "s9", ...strip("signature-strip", 2) },
];

/** Focuses the first text action, for the operator focus fixture (the prints themselves are not in the tab order). */
function FocusFirstAction({ children }: { readonly children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    ref.current?.querySelector<HTMLElement>("[data-fotohavn-action]")?.focus();
  }, []);
  return <div ref={ref}>{children}</div>;
}

const operator = { mode: "operator", onOpen: noop, onDelete: noop, onLoadMore: noop } as const;
const guest = { mode: "guest", onOpen: noop, onBack: noop } as const;

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "guest-cycle-list-operator",
    title: "Guest Cycle list, operator, with more to load",
    surface: "light",
    role: "operator",
    render: () => <GuestCycleList {...operator} cycles={cycles} hasMore />,
  },
  {
    id: "guest-cycle-list-operator-endings",
    title: "Guest Cycle list, operator, every way a Guest Cycle ends without a Photo Strip, all loaded",
    surface: "light",
    role: "operator",
    render: () => <GuestCycleList {...operator} cycles={endings} hasMore={false} />,
  },
  {
    id: "guest-cycle-list-operator-loading",
    title: "Guest Cycle list, operator, loading the next page",
    surface: "light",
    role: "operator",
    render: () => <GuestCycleList {...operator} cycles={cycles.slice(0, 3)} hasMore loading />,
  },
  {
    id: "guest-cycle-list-operator-focus",
    title: "Guest Cycle list, operator, keyboard focus on OPEN",
    surface: "light",
    role: "operator",
    render: () => (
      <FocusFirstAction>
        <GuestCycleList {...operator} cycles={cycles.slice(0, 2)} hasMore={false} />
      </FocusFirstAction>
    ),
  },
  {
    id: "guest-cycle-list-operator-empty",
    title: "Guest Cycle list, operator, empty",
    surface: "light",
    role: "operator",
    render: () => <GuestCycleList {...operator} cycles={[]} hasMore={false} />,
  },
  {
    id: "guest-cycle-list-operator-unavailable",
    title: "Guest Cycle list, operator, a Guest Cycle whose files are gone",
    surface: "light",
    role: "operator",
    render: () => (
      <GuestCycleList
        {...operator}
        cycles={[gone, ...cycles.slice(1, 3)]}
        hasMore={false}
      />
    ),
  },
  {
    id: "guest-cycle-list-guest",
    title: "Guest gallery, a row of Photo Strips that scrolls by touch",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => <GuestCycleList {...guest} strips={strips} />,
  },
  {
    id: "guest-cycle-list-guest-few",
    title: "Guest gallery, two Photo Strips, centered",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => <GuestCycleList {...guest} strips={strips.slice(0, 2)} />,
  },
  {
    id: "guest-cycle-list-guest-loading",
    title: "Guest gallery, loading the first page",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => <GuestCycleList {...guest} strips={[]} loading />,
  },
  {
    id: "guest-cycle-list-guest-focus",
    title: "Guest gallery, keyboard focus on a Photo Strip",
    surface: "light",
    role: "guest",
    layout: "fill",
    render: () => (
      <FocusFirst>
        <GuestCycleList {...guest} strips={strips.slice(0, 4)} />
      </FocusFirst>
    ),
  },
];
