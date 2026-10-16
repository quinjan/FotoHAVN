import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { BrainClientContext } from "../../brain/BrainClientContext.ts";
import { operatorGalleryFixtures } from "../../fixtures/operatorGalleryFixtures.ts";
import { overlays } from "../../fixtures/operatorSetupFixtures.ts";
import type { OperatorPresentation } from "../../protocol/generated.ts";
import { sentCommands, stubClient } from "../../test/operatorHarness.tsx";
import { deleteGuestCycleBody } from "../copy.ts";
import { formatGuestCycleTime } from "../format.ts";
import { OperatorRoot } from "../OperatorRoot.tsx";
import { forgetOpened } from "./galleryFocus.ts";
function nth<T>(items: readonly T[], index: number): T {
  const item = items[index];
  if (item === undefined) {
    throw new Error(`No item ${String(index)}.`);
  }
  return item;
}

function fixture(name: string): OperatorPresentation {
  const message = operatorGalleryFixtures[name];
  if (message === undefined) {
    throw new Error(`no fixture ${name}`);
  }
  return message.presentation;
}

function renderRoot(presentation: OperatorPresentation) {
  const client = stubClient();
  const view = (next: OperatorPresentation) => (
    <BrainClientContext value={client}>
      <OperatorRoot presentation={next} reconnecting={false} />
    </BrainClientContext>
  );
  const result = render(view(presentation));
  return {
    client,
    update: (next: OperatorPresentation) => {
      result.rerender(view(next));
    },
  };
}

const click = async (element: HTMLElement) => {
  await act(async () => {
    fireEvent.click(element);
    await Promise.resolve();
  });
};

afterEach(() => {
  cleanup();
  forgetOpened();
});

describe("the Guest Cycle delete confirmation", () => {
  it("names the time and counts the files, opens on the choice that loses nothing, and confirms or cancels", async () => {
    const presentation = fixture("operator-ui-gallery-detail-delete");
    const startedAt = presentation.overlays.confirmation?.guestCycle?.startedAt ?? "";
    const { client } = renderRoot(presentation);
    const dialog = screen.getByRole("alertdialog", { name: "Delete this Guest Cycle?" });
    expect(dialog).toHaveAccessibleDescription(`The Guest Cycle from ${formatGuestCycleTime(startedAt)} and its 5 files will be removed from this computer. This can't be undone.`);
    expect(within(dialog).getAllByRole("button").map((button) => button.textContent)).toEqual(["DELETE GUEST CYCLE", "KEEP GUEST CYCLE"]);
    expect(within(dialog).getByRole("button", { name: "DELETE GUEST CYCLE" })).toHaveAttribute("data-fotohavn-action", "primary");
    expect(screen.getByRole("button", { name: "KEEP GUEST CYCLE" })).toHaveFocus();
    await click(within(dialog).getByRole("button", { name: "DELETE GUEST CYCLE" }));
    await click(within(dialog).getByRole("button", { name: "KEEP GUEST CYCLE" }));
    fireEvent.keyDown(dialog, { key: "Escape" });
    expect(sentCommands(client)).toEqual([["ConfirmDeleteGuestCycle", {}], ["CancelDeleteGuestCycle", {}], ["CancelDeleteGuestCycle", {}]]);
  });

  it("opens over the list as well as the detail, and makes what is behind it inert", () => {
    renderRoot(fixture("operator-ui-gallery-delete-list"));
    expect(screen.getByRole("alertdialog", { name: "Delete this Guest Cycle?" })).toBeVisible();
    const behind = document.querySelector("main > div[inert]");
    expect(behind).toContainElement(screen.getByRole("heading", { level: 1, name: "Event Gallery." }));
  });

  it("says 'its file' for one file, and leaves the files out when there are none or the brain sent no Guest Cycle", () => {
    expect(deleteGuestCycleBody("13 Oct, 8:42 PM", 1)).toBe("The Guest Cycle from 13 Oct, 8:42 PM and its file will be removed from this computer. This can't be undone.");
    expect(deleteGuestCycleBody("13 Oct, 8:42 PM", 0)).toBe("The Guest Cycle from 13 Oct, 8:42 PM will be removed from this computer. This can't be undone.");
    expect(deleteGuestCycleBody(null, null)).toBe("This Guest Cycle and its files will be removed from this computer. This can't be undone.");
    renderRoot({
      ...fixture("operator-ui-gallery-detail-delete"),
      overlays: overlays({ confirmation: { kind: "deleteGuestCycle", eventName: null, counts: null, guestCycle: null } }),
    });
    expect(screen.getByRole("alertdialog")).toHaveAccessibleDescription("This Guest Cycle and its files will be removed from this computer. This can't be undone.");
  });

  it("returns focus to DELETE on the detail when the operator keeps the Guest Cycle", () => {
    const withDialog = fixture("operator-ui-gallery-detail-delete");
    const detail = fixture("operator-ui-gallery-detail");
    const { update } = renderRoot(detail);
    const remove = screen.getByRole("button", { name: "DELETE" });
    remove.focus();
    update(withDialog);
    expect(screen.getByRole("button", { name: "KEEP GUEST CYCLE" })).toHaveFocus();
    update(detail);
    expect(screen.getByRole("button", { name: "DELETE" })).toHaveFocus();
  });

  it("takes focus to the Guest Cycle that takes the deleted one's place when it leaves the list with the dialog", () => {
    const list = fixture("operator-ui-gallery-mixed");
    const view = list.eventGallery;
    if (view === null) {
      throw new Error("no gallery");
    }
    const { update } = renderRoot(list);
    screen.getAllByRole("button", { name: "DELETE" })[1]?.focus();
    fireEvent.click(nth(screen.getAllByRole("button", { name: "DELETE" }), 1));
    update({
      ...list,
      eventGallery: view,
      overlays: overlays({
        confirmation: { kind: "deleteGuestCycle", eventName: null, counts: null, guestCycle: { guestCycleId: "cycle-8", startedAt: view.items[1]?.startedAt ?? "", fileCount: 2 } },
      }),
    });
    expect(screen.getByRole("button", { name: "KEEP GUEST CYCLE" })).toHaveFocus();
    update({ ...list, eventGallery: { ...view, items: view.items.filter((item) => item.guestCycleId !== "cycle-8") } });
    expect(screen.getAllByRole("button", { name: "OPEN" })[1]).toHaveFocus();
    expect(screen.queryByRole("alertdialog")).toBeNull();
  });

  it("shows the brain's notice when the delete didn't go through", () => {
    renderRoot(fixture("operator-ui-gallery-detail-delete-failed"));
    expect(screen.getByText("The Guest Cycle couldn't be deleted. Try again in a moment.")).toBeVisible();
  });
});

describe("returning from a Guest Cycle's detail", () => {
  it("goes back to the list with focus on the Guest Cycle that was open", () => {
    const list = fixture("operator-ui-gallery-mixed");
    const detail = fixture("operator-ui-gallery-detail");
    const { update } = renderRoot(list);
    fireEvent.click(nth(screen.getAllByRole("button", { name: "OPEN" }), 2));
    update(detail);
    expect(screen.getByRole("button", { name: "BACK TO GALLERY" })).toHaveFocus();
    update(list);
    expect(screen.getAllByRole("button", { name: "OPEN" })[2]).toHaveFocus();
  });
});
