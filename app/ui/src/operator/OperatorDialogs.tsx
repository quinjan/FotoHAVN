import { useRef } from "react";
import { ConfirmDialog } from "../components/ConfirmDialog/index.ts";
import { Dialog } from "../components/Dialog/index.ts";
import { PrimaryAction } from "../components/PrimaryAction/index.ts";
import { SecondaryAction } from "../components/SecondaryAction/index.ts";
import type { ConfirmationOverlay, LeaveChoice } from "../protocol/generated.ts";
import { deleteDialog, deleteDialogBody, deleteGuestCycleBody, deleteGuestCycleDialog, endEventDialog, leaveDialog } from "./copy.ts";
import { formatGuestCycleTime } from "./format.ts";
import { useSendCommand } from "./OperatorCommands.tsx";

function LeaveSetupDialog() {
  const send = useSendCommand();
  const keepEditingRef = useRef<HTMLButtonElement>(null);
  const choose = (choice: LeaveChoice) => {
    void send("ConfirmLeave", { choice }, { key: "confirm-leave" });
  };
  return (
    <Dialog
      role="alertdialog"
      title={leaveDialog.title}
      description={leaveDialog.body}
      initialFocus={keepEditingRef}
      onCancel={() => {
        choose("keepEditing");
      }}
      actions={
        <>
          <PrimaryAction
            onClick={() => {
              choose("save");
            }}
          >
            {leaveDialog.save}
          </PrimaryAction>
          <SecondaryAction
            onClick={() => {
              choose("discardChanges");
            }}
          >
            {leaveDialog.discard}
          </SecondaryAction>
          <SecondaryAction
            ref={keepEditingRef}
            onClick={() => {
              choose("keepEditing");
            }}
          >
            {leaveDialog.keepEditing}
          </SecondaryAction>
        </>
      }
    />
  );
}

/**
 * The confirmation the brain has open, if any: delete an Event (its name and what goes with it), end the running Event
 * (the plan's sentence), delete a Guest Cycle (its time and how many files go), or leave Event setup with changes. Each
 * opens with focus on the choice that loses nothing, and
 * the brain closes it: the dialog goes away when the presentation no longer carries it.
 */
export function OperatorDialogs({ confirmation }: { readonly confirmation: ConfirmationOverlay | null }) {
  const send = useSendCommand();
  if (confirmation === null) {
    return null;
  }
  switch (confirmation.kind) {
    case "deleteEvent":
      return (
        <ConfirmDialog
          title={deleteDialog.title}
          body={deleteDialogBody(confirmation.eventName ?? "this Event", confirmation.counts)}
          confirmLabel={deleteDialog.confirmLabel}
          cancelLabel={deleteDialog.cancelLabel}
          onConfirm={() => {
            void send("ConfirmDeleteEvent", {}, { key: "confirm-delete" });
          }}
          onCancel={() => {
            void send("CancelDeleteEvent", {}, { key: "cancel-delete" });
          }}
        />
      );
    case "endEvent":
      return (
        <ConfirmDialog
          title={endEventDialog.title}
          body={endEventDialog.body}
          confirmLabel={endEventDialog.confirmLabel}
          cancelLabel={endEventDialog.cancelLabel}
          onConfirm={() => {
            void send("ConfirmEndEvent", {}, { key: "confirm-end" });
          }}
          onCancel={() => {
            void send("KeepRunning", {}, { key: "keep-running" });
          }}
        />
      );
    case "leaveSetup":
      return <LeaveSetupDialog />;
    case "deleteGuestCycle": {
      const { guestCycle } = confirmation;
      return (
        <ConfirmDialog
          title={deleteGuestCycleDialog.title}
          body={deleteGuestCycleBody(
            guestCycle === null ? null : formatGuestCycleTime(guestCycle.startedAt),
            guestCycle === null ? null : guestCycle.fileCount,
          )}
          confirmLabel={deleteGuestCycleDialog.confirmLabel}
          cancelLabel={deleteGuestCycleDialog.cancelLabel}
          onConfirm={() => {
            void send("ConfirmDeleteGuestCycle", {}, { key: "confirm-delete-guest-cycle" });
          }}
          onCancel={() => {
            void send("CancelDeleteGuestCycle", {}, { key: "cancel-delete-guest-cycle" });
          }}
        />
      );
    }
  }
}
