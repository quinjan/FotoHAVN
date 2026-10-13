import { act, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ProtocolBrainClient } from "../brain/protocolClient.ts";
import { guestStart } from "../fixtures/inlineFixtures.ts";
import type { GuestPresentation } from "../protocol/generated.ts";
import { ManualTransport } from "../test/manualTransport.ts";
import { App } from "../app/App.tsx";

function guest(presentation: Partial<GuestPresentation>) {
  const transport = new ManualTransport();
  const client = new ProtocolBrainClient(transport);
  client.connect();
  render(<App client={client} />);
  act(() => {
    transport.open();
    transport.receive({ ...guestStart, presentation: { ...guestStart.presentation, ...presentation } });
  });
}

describe("Guest surfaces", () => {
  it("renders the start placeholder: wordmark, statement, and supporting line, with no operator controls", () => {
    guest({});
    const main = screen.getByRole("main");
    expect(main).toHaveAttribute("data-fotohavn-surface", "start");
    expect(screen.getByText("FOTOHAVN")).toBeVisible();
    expect(screen.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
    expect(screen.getByText("Come a little closer.")).toBeVisible();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(main.querySelector("[data-guest-gallery-available]")).toHaveAttribute("data-guest-gallery-available", "false");
  });

  it("renders start even without its view", () => {
    guest({ start: null });
    expect(screen.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
  });

  it("renders the fatal surface", () => {
    guest({ surface: "fatal", start: null, fatal: { reason: "camera" } });
    expect(screen.getByRole("heading", { level: 1, name: "A little interruption." })).toBeVisible();
    expect(screen.getByText("Please ask the FOTOHAVN team for help.")).toBeVisible();
    expect(screen.queryByText("camera")).not.toBeInTheDocument();
  });

  it("renders only the wordmark for surfaces later milestones build", () => {
    guest({ surface: "template", start: null });
    expect(screen.getByRole("main")).toHaveAttribute("data-fotohavn-surface", "template");
    expect(screen.getByRole("heading", { level: 1, name: "FOTOHAVN" })).toBeVisible();
  });

  it("shows the brain's notice", () => {
    guest({
      overlays: { exitHold: null, pinDialog: null, phaseTimerWarning: null, startOverConfirmation: null, notice: { message: "One moment." } },
    });
    expect(screen.getAllByRole("status").map((status) => status.textContent)).toContain("One moment.");
  });
});
