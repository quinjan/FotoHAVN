import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient } from "../brain/types.ts";
import { eventRows, operatorFixtures } from "../fixtures/operatorFixtures.ts";
import { editingSetupView, operatorWelcome, overlays, runningEventPanel } from "../fixtures/operatorSetupFixtures.ts";
import type { OperatorNoticeCode, OperatorPresentation } from "../protocol/generated.ts";
import { sentCommands, stubClient } from "../test/operatorHarness.tsx";
import { OperatorRoot } from "./OperatorRoot.tsx";

function renderRoot(presentation: OperatorPresentation, client: BrainClient = stubClient(), reconnecting = false) {
  const view = (next: OperatorPresentation) => (
    <BrainClientContext value={client}>
      <OperatorRoot presentation={next} reconnecting={reconnecting} />
    </BrainClientContext>
  );
  const result = render(view(presentation));
  return { ...result, client, update: (next: OperatorPresentation) => { result.rerender(view(next)); } };
}

const fixture = (name: string): OperatorPresentation => {
  const message = operatorFixtures()[name];
  if (message === undefined) {
    throw new Error(`no fixture ${name}`);
  }
  return message.presentation;
};

const click = async (element: HTMLElement) => {
  await act(async () => {
    fireEvent.click(element);
    await Promise.resolve();
  });
};

describe("OperatorRoot", () => {
  describe("surfaces", () => {
    it("shows the Event list with the running-Event panel above the rows", () => {
      renderRoot(fixture("operator-list-running"));
      expect(document.querySelector("main")).toHaveAttribute("data-fotohavn-surface", "eventList");
      expect(screen.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
      expect(screen.getAllByRole("group")).toHaveLength(1);
    });

    it("shows Event setup from the brain's draft", () => {
      renderRoot(fixture("operator-setup-editing"));
      expect(screen.getByRole("heading", { level: 1, name: "Set up the Event." })).toBeVisible();
      expect(screen.getByLabelText(/^Name/)).toHaveValue("Harbour gala");
    });

    it("never builds Event setup without a draft", () => {
      renderRoot({ ...fixture("operator-setup-editing"), eventSetup: null });
      expect(screen.getByText("This view isn't ready yet.")).toBeVisible();
    });

    it("says the booth is running on this screen when the Guest Screen covers the window", () => {
      renderRoot(fixture("operator-single-screen"));
      expect(screen.getByRole("heading", { level: 1, name: "The booth is running on this screen." })).toBeVisible();
      expect(screen.queryByRole("button")).toBeNull();
    });

    it("keeps the screens of earlier milestones: starting, storage error, fatal, camera diagnostics, and views to come", () => {
      renderRoot(operatorWelcome("starting").presentation);
      expect(screen.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
      renderRoot(fixture("operator-storage-error-locked"));
      expect(screen.getByRole("button", { name: "TRY AGAIN" })).toBeVisible();
      renderRoot(operatorWelcome("fatal", { fatal: { reason: "The booth stopped." } }).presentation);
      expect(screen.getByText("The booth stopped.")).toBeVisible();
      renderRoot(operatorWelcome("eventGallery").presentation);
      expect(screen.getAllByText("This view isn't ready yet.").length).toBeGreaterThan(0);
      renderRoot(operatorWelcome("storageError").presentation);
      renderRoot(operatorWelcome("cameraDiagnostics").presentation);
      expect(screen.getAllByText("This view isn't ready yet.")).toHaveLength(3);
    });

    it("announces a lost connection", () => {
      renderRoot(fixture("operator-list-running"), stubClient(), true);
      expect(screen.getAllByRole("status").some((status) => status.textContent === "Reconnecting to the booth.")).toBe(true);
    });
  });

  describe("the brain's notices", () => {
    it.each([
      ["saveFailed", "The Event couldn't be saved. Check the Data folder, then try again."],
      ["deleteFailed", "The Event couldn't be deleted. Try again in a moment."],
      ["eventNotFound", "That Event isn't there any more."],
    ] as const satisfies readonly (readonly [OperatorNoticeCode, string])[])("writes %s in words", (code, text) => {
      renderRoot({ ...fixture("operator-list-notice"), overlays: overlays({ notice: code }) });
      expect(screen.getAllByRole("status").some((status) => status.textContent === text)).toBe(true);
    });

    it("sends DismissNotice when the operator closes it", async () => {
      const { client } = renderRoot(fixture("operator-list-notice"));
      await click(screen.getByRole("button", { name: "CLOSE" }));
      expect(sentCommands(client)).toEqual([["DismissNotice", {}]]);
    });

    it("keeps an empty live region mounted while there is no notice", () => {
      renderRoot(fixture("operator-list-running"));
      expect(screen.queryByRole("button", { name: "CLOSE" })).toBeNull();
    });
  });

  describe("confirmations", () => {
    it("opens the delete confirmation with the Event's name and what goes with it, and confirms or cancels", async () => {
      const presentation = fixture("operator-list-confirm-delete");
      const name = presentation.overlays.confirmation?.eventName ?? "";
      const { client } = renderRoot(presentation);
      const dialog = screen.getByRole("alertdialog", { name: "Delete this Event?" });
      expect(dialog).toHaveAccessibleDescription(`“${name}” will be removed with 12 Guest Cycles, 11 Photo Strips, and 41 photographs. This can't be undone.`);
      expect(screen.getByRole("button", { name: "KEEP EVENT" })).toHaveFocus();
      await click(within(dialog).getByRole("button", { name: "DELETE EVENT" }));
      await click(within(dialog).getByRole("button", { name: "KEEP EVENT" }));
      fireEvent.keyDown(dialog, { key: "Escape" });
      expect(sentCommands(client)).toEqual([["ConfirmDeleteEvent", {}], ["CancelDeleteEvent", {}], ["CancelDeleteEvent", {}]]);
    });

    it("says an Event that was never used has nothing in it, and falls back to a plain name if the brain sent none", () => {
      renderRoot({
        ...fixture("operator-list-confirm-delete"),
        overlays: overlays({ confirmation: { kind: "deleteEvent", eventName: null, counts: { guestCycles: 0, photoStrips: 0, captures: 0 }, guestCycle: null } }),
      });
      expect(screen.getByRole("alertdialog")).toHaveAccessibleDescription("“this Event” will be removed. It has no photographs yet. This can't be undone.");
    });

    it("opens the end confirmation with the plan's words, and confirms or keeps running", async () => {
      const { client } = renderRoot(fixture("operator-list-confirm-end"));
      const dialog = screen.getByRole("alertdialog", { name: "End this Event?" });
      expect(dialog).toHaveAccessibleDescription("Anyone using the booth will be stopped. Their photographs are kept.");
      expect(screen.getByRole("button", { name: "KEEP RUNNING" })).toHaveFocus();
      await click(within(dialog).getByRole("button", { name: "END EVENT" }));
      await click(within(dialog).getByRole("button", { name: "KEEP RUNNING" }));
      fireEvent.keyDown(dialog, { key: "Escape" });
      expect(sentCommands(client)).toEqual([["ConfirmEndEvent", {}], ["KeepRunning", {}], ["KeepRunning", {}]]);
    });

    it("asks to end the running Event before closing FOTOHAVN, and confirms or keeps it running", async () => {
      const { client } = renderRoot(fixture("operator-list-confirm-close"));
      const dialog = screen.getByRole("alertdialog", { name: "End the running Event and close FOTOHAVN?" });
      expect(dialog).toHaveAccessibleDescription("Anyone using the booth will be stopped. Their photographs are kept.");
      expect(within(dialog).getAllByRole("button").map((button) => button.textContent)).toEqual(["END EVENT AND CLOSE", "KEEP RUNNING"]);
      expect(screen.getByRole("button", { name: "KEEP RUNNING" })).toHaveFocus();
      await click(within(dialog).getByRole("button", { name: "END EVENT AND CLOSE" }));
      await click(within(dialog).getByRole("button", { name: "KEEP RUNNING" }));
      fireEvent.keyDown(dialog, { key: "Escape" });
      expect(sentCommands(client)).toEqual([["ConfirmCloseApp", {}], ["CancelCloseApp", {}], ["CancelCloseApp", {}]]);
    });

    it("opens the leave confirmation with SAVE, DISCARD CHANGES, and KEEP EDITING, focused on the one that loses nothing", async () => {
      const { client } = renderRoot(fixture("operator-setup-leave-confirmation"));
      const dialog = screen.getByRole("alertdialog", { name: "Save your changes?" });
      expect(within(dialog).getAllByRole("button").map((button) => button.textContent)).toEqual(["SAVE", "DISCARD CHANGES", "KEEP EDITING"]);
      expect(within(dialog).getByRole("button", { name: "SAVE" })).toHaveAttribute("data-fotohavn-action", "primary");
      expect(screen.getByRole("button", { name: "KEEP EDITING" })).toHaveFocus();
      await click(within(dialog).getByRole("button", { name: "SAVE" }));
      await click(within(dialog).getByRole("button", { name: "DISCARD CHANGES" }));
      await click(within(dialog).getByRole("button", { name: "KEEP EDITING" }));
      fireEvent.keyDown(dialog, { key: "Escape" });
      expect(sentCommands(client)).toEqual([
        ["ConfirmLeave", { choice: "save" }],
        ["ConfirmLeave", { choice: "discardChanges" }],
        ["ConfirmLeave", { choice: "keepEditing" }],
        ["ConfirmLeave", { choice: "keepEditing" }],
      ]);
    });

    it("makes the content behind a dialog inert, and only while one is open", () => {
      const open = renderRoot(fixture("operator-list-confirm-end"));
      const behind = document.querySelector("main > div[inert]");
      expect(behind).not.toBeNull();
      expect(behind).toContainElement(screen.getByRole("heading", { level: 1, name: "Events" }));
      expect(behind).not.toContainElement(screen.getByRole("alertdialog"));

      open.update(fixture("operator-list-running"));
      expect(document.querySelector("[inert]")).toBeNull();
      expect(screen.queryByRole("alertdialog")).toBeNull();
    });

    it("returns focus to where it was when the dialog closes", () => {
      const running = fixture("operator-list-running");
      const { update } = renderRoot(running);
      const endEvent = screen.getByRole("button", { name: "END EVENT" });
      endEvent.focus();
      update({ ...running, overlays: overlays({ confirmation: { kind: "endEvent", eventName: null, counts: null, guestCycle: null } }) });
      expect(screen.getByRole("button", { name: "KEEP RUNNING" })).toHaveFocus();
      update(running);
      expect(screen.getByRole("button", { name: "END EVENT" })).toHaveFocus();
    });
  });

  describe("Operator Assistance on views other than the Event list", () => {
    const assisting = (): OperatorPresentation => fixture("operator-setup-assistance");

    it("shows the reason as an alert line with a text link back to the running Event", async () => {
      const { client } = renderRoot(assisting());
      expect(screen.getByText("The camera needs attention.")).toBeVisible();
      await click(screen.getByRole("button", { name: "GO TO THE RUNNING EVENT" }));
      expect(sentCommands(client)).toEqual([["LeaveEventSetup", {}]]);
      expect(screen.getByRole("button", { name: "CONTINUE TO HARDWARE" })).toHaveAttribute("data-fotohavn-action", "primary");
      expect(document.querySelectorAll('[data-fotohavn-action="primary"]')).toHaveLength(1);
    });

    it("keeps the alert's live region empty, and offers no link, while the Event doesn't need help", () => {
      renderRoot({ ...assisting(), runningEvent: runningEventPanel() });
      expect(screen.queryByRole("button", { name: "GO TO THE RUNNING EVENT" })).toBeNull();
      expect(screen.queryByText("The camera needs attention.")).toBeNull();
    });

    it("says each reason in words", () => {
      for (const [reason, words] of [
        ["camera", "The camera needs attention."],
        ["storage", "The Data folder needs attention."],
        ["guestDisplay", "The guest display isn't connected."],
      ] as const) {
        const { unmount } = renderRoot({ ...assisting(), runningEvent: runningEventPanel({ assistance: { reason } }) });
        expect(screen.getByText(words)).toBeVisible();
        unmount();
      }
    });
  });

  it("disables IDENTIFY DISPLAYS while the numbers are on the displays", () => {
    const view = editingSetupView();
    renderRoot({
      ...operatorWelcome("eventSetup", { eventSetup: view }).presentation,
      overlays: { confirmation: null, identifyDisplays: { displays: [{ id: "display-1", number: 1, name: "Built-in display" }] }, notice: null },
    });
    expect(screen.getByRole("button", { name: "IDENTIFY DISPLAYS", hidden: true })).toHaveAttribute("aria-disabled", "true");
  });

  it("lists the rows it was given", () => {
    renderRoot({ ...fixture("operator-list-running"), eventList: { events: eventRows(2, false), newEventAvailable: true }, runningEvent: null });
    expect(within(screen.getByRole("list", { name: "Events" })).getAllByRole("listitem")).toHaveLength(2);
  });
});
