import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { operatorGalleryFixtures } from "../../fixtures/operatorGalleryFixtures.ts";
import { runningEventPanel } from "../../fixtures/operatorSetupFixtures.ts";
import type { GuestCycleDetailView, RunningEventPanel } from "../../protocol/generated.ts";
import { renderOperator, sentCommands } from "../../test/operatorHarness.tsx";
import { GuestCycleDetailSurface } from "./GuestCycleDetailSurface.tsx";

function baseView(name = "operator-ui-gallery-detail"): GuestCycleDetailView {
  const view = operatorGalleryFixtures[name]?.presentation.guestCycleDetail;
  if (view === null || view === undefined) {
    throw new Error(`no detail in ${name}`);
  }
  return view;
}

function surface(view: GuestCycleDetailView, running: RunningEventPanel | null = null) {
  return <GuestCycleDetailSurface view={view} running={running} />;
}

afterEach(cleanup);

describe("the operator Guest Cycle detail surface", () => {
  it("shows the Event's name, 'Guest Cycle.', the full strip, the Captures in the brain's order, and the facts", () => {
    renderOperator(surface(baseView()));
    expect(screen.getByRole("heading", { level: 1, name: "Guest Cycle." })).toBeVisible();
    expect(screen.getByText("Event 5: Santos and Reyes celebration")).toBeVisible();
    expect(screen.getByAltText("Photo Strip")).toBeVisible();
    const captures = within(screen.getByRole("list")).getAllByAltText("Photograph from this Event");
    expect(captures).toHaveLength(4);
    expect(screen.getAllByRole("term").map((term) => term.textContent)).toEqual(["Time", "Outcome", "Template", "Look", "Captures"]);
    expect(screen.getAllByRole("definition").map((value) => value.textContent)).toEqual([expect.stringMatching(/^\d{1,2} [A-Z][a-z]{2}, \d{1,2}:\d{2} (AM|PM)$/), "Completed", "Signature Strip", "Naturale", "4"]);
  });

  it("starts focus on BACK TO GALLERY, and sends the way back, the folder, and the delete with this Guest Cycle's id", async () => {
    const { client } = renderOperator(surface(baseView()));
    const back = screen.getByRole("button", { name: "BACK TO GALLERY" });
    expect(back).toHaveFocus();
    fireEvent.click(back);
    fireEvent.click(screen.getByRole("button", { name: "OPEN FOLDER" }));
    fireEvent.click(screen.getByRole("button", { name: "DELETE" }));
    await waitFor(() => {
      expect(sentCommands(client)).toHaveLength(3);
    });
    expect(sentCommands(client)).toEqual([
      ["CloseGuestCycle", {}],
      ["OpenGuestCycleFolder", { guestCycleId: "cycle-9" }],
      ["DeleteGuestCycle", { guestCycleId: "cycle-9" }],
    ]);
  });

  it("keeps DELETE unavailable while the brain prepares the confirmation, and OPEN FOLDER when it can't open", async () => {
    const { client } = renderOperator(surface({ ...baseView(), canDelete: false, canOpenFolder: false }));
    fireEvent.click(screen.getByRole("button", { name: "DELETE" }));
    fireEvent.click(screen.getByRole("button", { name: "OPEN FOLDER" }));
    expect(screen.getByRole("button", { name: "DELETE" })).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByRole("button", { name: "OPEN FOLDER" })).toHaveAttribute("aria-disabled", "true");
    await Promise.resolve();
    expect(sentCommands(client)).toEqual([]);
  });

  it("shows a Guest Cycle with no Photo Strip: its kept Captures in sequence, no Template or Look", () => {
    renderOperator(surface(baseView("operator-ui-gallery-detail-no-strip")));
    expect(screen.getByText("No Photo Strip")).toBeVisible();
    expect(screen.getAllByAltText("Photograph from this Event")).toHaveLength(2);
    expect(screen.getAllByRole("term").map((term) => term.textContent)).toEqual(["Time", "Outcome", "Captures"]);
    expect(screen.getByText("Started over")).toBeVisible();
  });

  it("shows a Guest Cycle whose files are gone, with the reason once", () => {
    renderOperator(surface(baseView("operator-ui-gallery-detail-unavailable")));
    expect(screen.getAllByText("These photographs can't be found.")).toHaveLength(1);
    expect(screen.queryByRole("img")).toBeNull();
    expect(screen.getByText("App closed")).toBeVisible();
  });

  it("keeps a landscape strip in its native orientation", () => {
    renderOperator(surface(baseView("operator-ui-gallery-detail-landscape")));
    expect(screen.getByAltText("Photo Strip").parentElement).not.toHaveAttribute("data-rotated");
  });

  it("shows Operator Assistance, with a way back to the running Event that closes the gallery", async () => {
    const { client } = renderOperator(surface(baseView(), runningEventPanel({ assistance: { reason: "storage" } })));
    expect(screen.getByText("The Data folder needs attention.")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "GO TO THE RUNNING EVENT" }));
    await waitFor(() => {
      expect(sentCommands(client)).toEqual([["CloseGallery", {}]]);
    });
  });

  it("tabs through BACK TO GALLERY, OPEN FOLDER, and DELETE", () => {
    renderOperator(surface(baseView()));
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["BACK TO GALLERY", "OPEN FOLDER", "DELETE"]);
  });
});
