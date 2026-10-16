import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient } from "../brain/types.ts";
import { guestGalleryFixtures } from "../fixtures/guestGalleryFixtures.ts";
import { guestStart } from "../fixtures/inlineFixtures.ts";
import type { GuestPresentation } from "../protocol/generated.ts";
import { GuestRoot } from "./GuestRoot.tsx";

const client: BrainClient = {
  kind: "fixture",
  connect: vi.fn(),
  getState: () => ({ status: "connected", session: null, mismatch: null }),
  subscribe: () => () => undefined,
  sendCommand: () => Promise.resolve({ status: "accepted" }),
  sendTest: vi.fn(),
  dispose: vi.fn(),
};

function renderRoot(presentation: GuestPresentation) {
  return render(
    <BrainClientContext value={client}>
      <GuestRoot presentation={presentation} reconnecting={false} />
    </BrainClientContext>,
  );
}

function fixture(name: string): GuestPresentation {
  const message = guestGalleryFixtures[name];
  if (message === undefined) {
    throw new Error(`no fixture ${name}`);
  }
  return message.presentation;
}

describe("the guest gallery surfaces", () => {
  it("routes guestGallery to the guest gallery with its strips", () => {
    renderRoot(fixture("guest-ui-gallery-four-strips"));
    expect(document.querySelector("main")).toHaveAttribute("data-fotohavn-surface", "guestGallery");
    expect(screen.getByRole("heading", { level: 1, name: "Look at you." })).toBeVisible();
    expect(screen.getAllByRole("img", { name: "Photo Strip from this Event" })).toHaveLength(4);
  });

  it("routes guestStripDetail to the strip detail with its arranged Captures", () => {
    renderRoot(fixture("guest-ui-strip-detail"));
    expect(document.querySelector("main")).toHaveAttribute("data-fotohavn-surface", "guestStripDetail");
    expect(screen.getByRole("button", { name: "CLOSE" })).toBeVisible();
    expect(screen.getAllByRole("img", { name: "Photograph from this Event" })).toHaveLength(4);
  });

  it("keeps the exit hold on both surfaces", () => {
    for (const name of ["guest-ui-gallery-four-strips", "guest-ui-strip-detail"]) {
      const view = renderRoot(fixture(name));
      expect(screen.getByRole("button", { name: "Operator exit" })).toBeVisible();
      view.unmount();
    }
  });

  it.each([
    ["guestGallery", { guestGallery: null }],
    ["guestStripDetail", { guestStripDetail: null }],
  ] as const)("shows only the wordmark when %s carries no view", (surface, parts) => {
    renderRoot({ ...guestStart.presentation, surface, start: null, ...parts });
    expect(screen.getByRole("heading", { level: 1, name: "FOTOHAVN" })).toBeVisible();
  });
});
