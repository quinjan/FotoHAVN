import { act, fireEvent, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { draftIdleMs } from "./useDraftText.ts";
import { createPatternFrameSource } from "../../components/LivePreview/index.ts";
import { editingSetupView, phaseTimers, setupView } from "../../fixtures/operatorSetupFixtures.ts";
import type { BrainClient, CommandResult } from "../../brain/types.ts";
import type { AssistanceReason, EventSetupView } from "../../protocol/generated.ts";
import { renderOperator, sentCommands, stubClient, type StubClient } from "../../test/operatorHarness.tsx";
import { EventSetup } from "./EventSetup.tsx";

interface Options {
  readonly client?: StubClient;
  readonly identifying?: boolean;
  readonly assistance?: AssistanceReason | null;
}

function setup(view: EventSetupView, { client = stubClient(), identifying = false, assistance = null }: Options = {}) {
  const ui = (next: EventSetupView) => <EventSetup view={next} identifying={identifying} assistance={assistance} />;
  const result = renderOperator(ui(view), client);
  return { ...result, update: (next: EventSetupView) => { result.rerender(ui(next)); } };
}

const flush = () => act(async () => { await Promise.resolve(); });
const change = (label: string | RegExp, value: string) => {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
};
const sent = (client: BrainClient) => sentCommands(client);

describe("EventSetup", () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: false });
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  describe("the form", () => {
    it("has the plan's sections, in order, each opened by an eyebrow", () => {
      setup(editingSetupView());
      const sections = screen.getAllByRole("region").map((region) => region.getAttribute("aria-labelledby"));
      const names = sections.map((id) => document.getElementById(id ?? "")?.textContent);
      expect(names).toEqual(["EVENT", "CAMERA", "GUEST DISPLAY", "PRINTER", "TEMPLATES", "FILTERS", "TIMING", "GUESTS", "OPERATOR PIN"]);
      expect(screen.getByRole("heading", { level: 1, name: "Set up the Event." })).toBeVisible();
    });

    it("shows the brain's draft in every field", () => {
      setup(editingSetupView());
      expect(screen.getByLabelText(/^Name/)).toHaveValue("Harbour gala");
      expect(screen.getByLabelText(/^Date/)).toHaveValue("2026-06-12");
      expect(screen.getByLabelText(/^Venue/)).toHaveValue("Harbour Hall");
      expect(screen.getByLabelText(/^Customer or host/)).toHaveValue("Lindqvist & Co");
      expect(screen.getByLabelText(/^Notes/)).toHaveValue("Bring the props. The Customer would like the strips in warm tones.");
      expect(screen.getByLabelText(/^Camera/)).toHaveValue("camera-1");
      expect(screen.getByRole("radio", { name: "DELL P2419H" })).toBeChecked();
      expect(screen.getByRole("switch", { name: "Mirror" })).toBeChecked();
      expect(screen.getByRole("radio", { name: "10 seconds" })).toBeChecked();
      expect(screen.getByLabelText("Printer")).toHaveValue("No printer");
      expect(screen.getByLabelText("Printer")).toHaveAttribute("readonly");
      expect(screen.getByRole("switch", { name: "Guest gallery" })).toBeChecked();
      expect(screen.getByText("Turn this on only with the Customer's permission.")).toBeVisible();
    });

    it("shows a new Event's defaults: Naturale and the first Template as defaults, nothing chosen for the camera or display", () => {
      setup(setupView());
      expect(screen.getByLabelText(/^Name/)).toHaveValue("");
      expect(screen.getByLabelText(/^Camera/)).toHaveValue("");
      expect(screen.getAllByRole("radio", { checked: true }).map((radio) => radio.closest("li")?.querySelector("[id$='-title']")?.textContent ?? radio.getAttribute("aria-label"))).toContain("Signature Strip");
      expect(screen.getByRole("switch", { name: "Guest gallery" })).not.toBeChecked();
      expect(screen.getByRole("radio", { name: "5 seconds" })).toBeChecked();
    });

    it("shows the character counters and limits", () => {
      setup(editingSetupView());
      expect(screen.getByLabelText(/^Name/)).toHaveAttribute("maxlength", "80");
      expect(screen.getByLabelText(/^Venue/)).toHaveAttribute("maxlength", "120");
      expect(screen.getByLabelText(/^Customer or host/)).toHaveAttribute("maxlength", "120");
      expect(screen.getByLabelText(/^Notes/)).toHaveAttribute("maxlength", "1000");
    });
  });

  describe("text fields", () => {
    it("sends each text change as ChangeEventField after a pause, and on blur", () => {
      const { client } = setup(setupView());
      change(/^Name/, "Harbour");
      expect(sent(client)).toEqual([]);
      act(() => {
        vi.advanceTimersByTime(draftIdleMs);
      });
      expect(sent(client)).toEqual([["ChangeEventField", { field: "name", value: "Harbour" }]]);

      change(/^Venue/, "Harbour Hall");
      fireEvent.blur(screen.getByLabelText(/^Venue/));
      change(/^Customer or host/, "Lindqvist");
      fireEvent.blur(screen.getByLabelText(/^Customer or host/));
      change(/^Notes/, "Bring the props.");
      fireEvent.blur(screen.getByLabelText(/^Notes/));
      expect(sent(client).slice(1)).toEqual([
        ["ChangeEventField", { field: "venue", value: "Harbour Hall" }],
        ["ChangeEventField", { field: "customer", value: "Lindqvist" }],
        ["ChangeEventField", { field: "notes", value: "Bring the props." }],
      ]);
    });

    it("sends the date as soon as it is picked", () => {
      const { client } = setup(setupView());
      change(/^Date/, "2026-12-01");
      expect(sent(client)).toEqual([["ChangeEventField", { field: "date", value: "2026-12-01" }]]);
    });

    it("keeps what is typed while the brain's earlier echoes arrive, and shows a new draft's value after", () => {
      const { update } = setup(setupView());
      change(/^Name/, "Harb");
      change(/^Name/, "Harbo");
      update(setupView({ fields: { ...setupView().fields, name: "Harb" } }));
      expect(screen.getByLabelText(/^Name/)).toHaveValue("Harbo");
      fireEvent.blur(screen.getByLabelText(/^Name/));
      update(setupView({ fields: { ...setupView().fields, name: "Another" } }));
      expect(screen.getByLabelText(/^Name/)).toHaveValue("Another");
    });
  });

  describe("errors", () => {
    const errors = (view: Partial<EventSetupView>) =>
      editingSetupView({
        showErrors: true,
        dirty: true,
        fields: { name: "", date: "2026-06-12", venue: "", customer: "", notes: "" },
        ...view,
      });

    it("shows no error before a save attempt, though the brain has computed some", () => {
      setup(editingSetupView({ showErrors: false, fieldErrors: [{ field: "name", code: "required" }] }));
      expect(screen.queryByText("Name the Event.")).toBeNull();
      expect(screen.getByLabelText(/^Name/)).not.toHaveAttribute("aria-invalid");
    });

    it("shows each error in words beside its field, with aria-invalid and a description", () => {
      setup(
        errors({
          phaseTimers: phaseTimers({ keepsake: 2, template: 5 }),
          fieldErrors: [
            { field: "name", code: "required" },
            { field: "date", code: "invalidDate" },
            { field: "venue", code: "tooLong" },
            { field: "customer", code: "tooLong" },
            { field: "notes", code: "tooLong" },
            { field: "timers.keepsake", code: "timerOutOfRange" },
            { field: "timers.template", code: "timerOutOfRange" },
          ],
        }),
      );
      const name = screen.getByLabelText(/^Name/);
      expect(name).toHaveAttribute("aria-invalid", "true");
      expect(name).toHaveAccessibleDescription(expect.stringContaining("Name the Event."));
      expect(screen.getByLabelText(/^Date/)).toHaveAccessibleDescription(expect.stringContaining("That date isn't valid. Choose another."));
      expect(screen.getByLabelText(/^Venue/)).toHaveAccessibleDescription(expect.stringContaining("Keep the venue to 120 characters or fewer."));
      expect(screen.getByLabelText(/^Customer or host/)).toHaveAccessibleDescription(expect.stringContaining("Keep this to 120 characters or fewer."));
      expect(screen.getByLabelText(/^Notes/)).toHaveAccessibleDescription(expect.stringContaining("Keep the notes to 1,000 characters or fewer."));
      expect(screen.getByLabelText("Keepsake")).toHaveAccessibleDescription(expect.stringContaining("Enter 5 to 60 seconds."));
      expect(screen.getByLabelText("Template")).toHaveAccessibleDescription(expect.stringContaining("Enter 0 to turn it off, or 20 to 300 seconds."));
    });

    it("shows the camera, display, Template, Look, and PIN errors under their fields", () => {
      setup(
        errors({
          camera: { ...editingSetupView().camera, selectedDeviceId: null, selectedName: null, state: "none", previewActive: false },
          guestDisplay: { ...editingSetupView().guestDisplay, selectedId: null, selectedName: null, state: "none" },
          fieldErrors: [
            { field: "camera", code: "cameraRequired" },
            { field: "guestDisplay", code: "displayRequired" },
            { field: "templates", code: "atLeastOneTemplate" },
            { field: "defaultLook", code: "defaultLookNotOffered" },
            { field: "pin", code: "pinFormat" },
          ],
          pin: { isSet: false, changing: false },
        }),
      );
      expect(screen.getByLabelText(/^Camera/)).toHaveAccessibleDescription(expect.stringContaining("Choose a camera."));
      expect(screen.getByText("Choose the guest display.")).toBeVisible();
      expect(screen.getByText("Offer at least one Template.")).toBeVisible();
      expect(screen.getByText("Choose the default from the Looks you offer.")).toBeVisible();
      expect(screen.getByLabelText("PIN")).toHaveAccessibleDescription(expect.stringContaining("Use 4 to 6 digits, 0 to 9 only."));
      expect(screen.getByLabelText("Confirm PIN")).not.toHaveAttribute("aria-invalid");
    });

    it("puts a mismatch on the confirmation, and a default Template that isn't offered on the Templates", () => {
      setup(
        errors({
          pin: { isSet: false, changing: true },
          fieldErrors: [
            { field: "pin", code: "pinMismatch" },
            { field: "defaultTemplate", code: "defaultTemplateNotOffered" },
          ],
        }),
      );
      expect(screen.getByLabelText("Confirm PIN")).toHaveAccessibleDescription(expect.stringContaining("The two PINs don't match."));
      expect(screen.getByLabelText("PIN")).not.toHaveAttribute("aria-invalid");
      expect(screen.getByText("Choose the default from the Templates you offer.")).toBeVisible();
    });

    it("announces how many things need attention, in a live region that stays mounted", () => {
      const { update } = setup(editingSetupView());
      const live = screen.getAllByRole("status").find((status) => status.parentElement?.getAttribute("data-status-tone") === "alert" && status.closest("[data-empty]") !== null);
      expect(live).toBeDefined();
      update(errors({ fieldErrors: [{ field: "name", code: "required" }] }));
      expect(screen.getByText("One thing needs attention before this Event can be saved.")).toBeVisible();
      update(
        errors({
          fieldErrors: [
            { field: "name", code: "required" },
            { field: "camera", code: "cameraRequired" },
          ],
        }),
      );
      expect(screen.getByText("2 things need attention before this Event can be saved.")).toBeVisible();
      update(errors({ fieldErrors: [] }));
      expect(screen.queryByText(/need attention before this Event can be saved/)).toBeNull();
    });
  });

  describe("saving", () => {
    it("sends SaveEvent from the primary action and SaveAndStartEvent from the secondary one", async () => {
      const { client } = setup(editingSetupView());
      const save = screen.getByRole("button", { name: "SAVE" });
      const saveAndStart = screen.getByRole("button", { name: "SAVE AND START" });
      expect(save).toHaveAttribute("data-fotohavn-action", "primary");
      expect(saveAndStart).toHaveAttribute("data-fotohavn-action", "secondary");
      expect(document.querySelectorAll('[data-fotohavn-action="primary"]')).toHaveLength(1);
      fireEvent.click(save);
      await flush();
      fireEvent.click(saveAndStart);
      await flush();
      expect(sent(client)).toEqual([["SaveEvent", {}], ["SaveAndStartEvent", {}]]);
    });

    it("keeps SAVE AND START unavailable, with its reason in text, while another Event runs", async () => {
      const { client } = setup(editingSetupView({ saveAndStartAvailable: false, saveAndStartDisabledReason: "eventRunning" }));
      const saveAndStart = screen.getByRole("button", { name: "SAVE AND START" });
      expect(saveAndStart).toHaveAttribute("aria-disabled", "true");
      expect(saveAndStart).toHaveAccessibleDescription("End the running Event to start another.");
      expect(screen.getByText("End the running Event to start another.")).toBeVisible();
      fireEvent.click(saveAndStart);
      await flush();
      expect(sent(client)).toEqual([]);
      expect(screen.getByRole("button", { name: "SAVE" })).toHaveAttribute("aria-disabled", "false");
    });

    it("says it is saving and holds both actions while the brain writes the Event", async () => {
      const { client } = setup(editingSetupView({ saving: true }));
      expect(screen.getByText("Saving…")).toBeVisible();
      expect(screen.getByRole("button", { name: "SAVE" })).toHaveAttribute("aria-disabled", "true");
      expect(screen.getByRole("button", { name: "SAVE AND START" })).toHaveAttribute("aria-disabled", "true");
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      await flush();
      expect(sent(client)).toEqual([]);
    });

    it("sends one save at a time", async () => {
      let answer: (result: CommandResult) => void = () => undefined;
      const client = stubClient();
      client.sendCommand.mockReturnValueOnce(new Promise<CommandResult>((resolve) => { answer = resolve; }));
      setup(editingSetupView(), { client });
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      expect(client.sendCommand).toHaveBeenCalledOnce();
      answer({ status: "accepted" });
      await flush();
    });

    it("shows no notice for a save the brain refuses for its fields, since the fields say why", async () => {
      setup(editingSetupView(), { client: stubClient({ result: { status: "rejected", reason: "validationFailed" } }) });
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      await flush();
      expect(screen.queryByText("Some details need attention.")).toBeNull();
    });

    it("shows a polite notice for any other failure to save", async () => {
      setup(editingSetupView(), { client: stubClient({ result: { status: "failed", error: "timeout" } }) });
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      await flush();
      expect(screen.getByText("The booth didn't answer. Try again.")).toBeVisible();
    });
  });

  describe("focus to the first invalid field", () => {
    const refused: CommandResult = { status: "rejected", reason: "validationFailed" };
    const failing = (fieldErrors: EventSetupView["fieldErrors"], overrides: Partial<EventSetupView> = {}) =>
      editingSetupView({
        showErrors: true,
        dirty: true,
        fields: { name: "", date: "2026-06-12", venue: "", customer: "", notes: "" },
        fieldErrors,
        ...overrides,
      });

    it("moves focus to the first invalid control in the form's order when a save is refused", async () => {
      const view = failing([
        { field: "camera", code: "cameraRequired" },
        { field: "name", code: "required" },
        { field: "pin", code: "pinRequired" },
      ], { pin: { isSet: false, changing: false } });
      setup(view, { client: stubClient({ result: refused }) });
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      await flush();
      expect(screen.getByLabelText(/^Name/)).toHaveFocus();
    });

    it("works the same for SAVE AND START", async () => {
      setup(failing([{ field: "camera", code: "cameraRequired" }]), { client: stubClient({ result: refused }) });
      fireEvent.click(screen.getByRole("button", { name: "SAVE AND START" }));
      await flush();
      expect(screen.getByLabelText(/^Camera/)).toHaveFocus();
    });

    it("focuses a control of a group of options: the display, Templates, and Looks", async () => {
      for (const [field, code, group] of [
        ["guestDisplay", "displayRequired", "Guest display"],
        ["templates", "atLeastOneTemplate", "Offered Templates"],
        ["defaultLook", "defaultLookNotOffered", "Offered Looks"],
      ] as const) {
        const { unmount } = setup(failing([{ field, code }]), { client: stubClient({ result: refused }) });
        fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
        await flush();
        const inside = within(screen.getByRole("group", { name: group })).getAllByRole(field === "guestDisplay" ? "radio" : "checkbox");
        expect(inside).toContain(document.activeElement);
        unmount();
      }
    });

    it("focuses a timer, and the PIN inputs by what is wrong", async () => {
      const timer = setup(failing([{ field: "timers.keepsake", code: "timerOutOfRange" }]), { client: stubClient({ result: refused }) });
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      await flush();
      expect(screen.getByLabelText("Keepsake")).toHaveFocus();
      timer.unmount();

      const pin = setup(failing([{ field: "pin", code: "pinMismatch" }], { pin: { isSet: false, changing: true } }), { client: stubClient({ result: refused }) });
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      await flush();
      expect(screen.getByLabelText("Confirm PIN")).toHaveFocus();
      pin.unmount();

      setup(failing([{ field: "pin", code: "pinRequired" }], { pin: { isSet: false, changing: false } }), { client: stubClient({ result: refused }) });
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      await flush();
      expect(screen.getByLabelText("PIN")).toHaveFocus();
    });

    it("waits for the brain's errors when the refusal arrives before them", async () => {
      const { update } = setup(editingSetupView({ dirty: true }), { client: stubClient({ result: refused }) });
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      await flush();
      expect(screen.getByLabelText(/^Name/)).not.toHaveFocus();
      update(failing([{ field: "name", code: "required" }]));
      expect(screen.getByLabelText(/^Name/)).toHaveFocus();
    });

    it("leaves focus alone when the save is accepted, and doesn't steal it again when errors change later", async () => {
      const { update } = setup(editingSetupView({ dirty: true }));
      fireEvent.click(screen.getByRole("button", { name: "SAVE" }));
      await flush();
      update(failing([{ field: "name", code: "required" }]));
      expect(screen.getByLabelText(/^Name/)).not.toHaveFocus();
    });
  });

  describe("the camera field", () => {
    it("lists the connected cameras and sends SelectCamera for the one chosen", () => {
      const { client } = setup(setupView());
      const select = screen.getByLabelText(/^Camera/);
      expect(within(select).getAllByRole("option").map((option) => option.textContent)).toEqual(["Choose a camera", "Logitech BRIO", "Integrated Webcam", "USB camera"]);
      fireEvent.change(select, { target: { value: "camera-2" } });
      expect(sent(client)).toEqual([["SelectCamera", { deviceId: "camera-2" }]]);
    });

    it("sends nothing for the placeholder", () => {
      const { client } = setup(setupView());
      fireEvent.change(screen.getByLabelText(/^Camera/), { target: { value: "" } });
      expect(sent(client)).toEqual([]);
    });

    it.each([
      ["checking", { state: "checking" }, "Checking"],
      ["ready", { state: "ready" }, "Ready"],
      ["not available", { state: "notAvailable", reason: "stale" }, "Not available. The camera stopped sending pictures."],
      ["blocked by Windows privacy", { state: "notAvailable", reason: "privacyDenied" }, "Not available. Camera access is off in Windows privacy settings."],
      ["in use by another app", { state: "inUseByAnotherApp", reason: "inUseByAnotherApp" }, "In use by another app"],
      ["in use by the running Event", { state: "inUseByRunningEvent" }, "In use by the running Event. The preview waits until that Event ends."],
      ["not checked", { state: "notChecked" }, "Not checked while an Event is running. You can still save it."],
    ] as const)("says when it is %s", (_name, camera, text) => {
      setup(editingSetupView({ camera: { ...editingSetupView().camera, ...camera, previewActive: false } }));
      expect(screen.getByText(text)).toBeVisible();
      expect(screen.getByLabelText(/^Camera/)).toHaveValue("camera-1");
    });

    it("alerts for a camera that isn't available or in use, and stays neutral for the rest", () => {
      const { update } = setup(editingSetupView({ camera: { ...editingSetupView().camera, state: "notAvailable", reason: "failed" } }));
      expect(screen.getByText("Not available. The camera stopped working.").closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "alert");
      update(editingSetupView({ camera: { ...editingSetupView().camera, state: "ready" } }));
      expect(screen.getByText("Ready").closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "neutral");
    });

    it("keeps a bound camera that isn't connected, names it, and never swaps it for another", () => {
      const view = editingSetupView({
        camera: { ...editingSetupView().camera, cameras: editingSetupView().camera.cameras.slice(1), selectedDeviceId: "camera-1", selectedName: "Logitech BRIO", state: "missing", previewActive: false },
      });
      const { client } = setup(view);
      const select = screen.getByLabelText(/^Camera/);
      expect(select).toHaveValue("camera-1");
      expect(within(select).getByRole("option", { name: "Logitech BRIO (not connected)" })).toBeDisabled();
      expect(screen.getByText("This camera isn't connected. FOTOHAVN keeps it until you choose another.")).toBeVisible();
      expect(sent(client)).toEqual([]);
    });

    it("names a bound camera the brain sent no name for", () => {
      setup(editingSetupView({ camera: { ...editingSetupView().camera, cameras: [], selectedDeviceId: "gone", selectedName: null, state: "missing", previewActive: false } }));
      expect(screen.getByRole("option", { name: "Chosen camera (not connected)" })).toBeInTheDocument();
    });

    it("says when no camera is connected, and what to do", () => {
      setup(setupView({ camera: { ...setupView().camera, cameras: [] } }));
      expect(screen.getAllByText("No camera found.").length).toBeGreaterThan(0);
      expect(screen.getByText("Connect a camera and it will appear here.")).toBeVisible();
    });

    it("shows the live preview only while the brain sends this camera's frames, mirrored as the draft says", () => {
      const { update } = setup(editingSetupView(), { client: stubClient({ frames: createPatternFrameSource(64, 36) }) });
      const preview = screen.getByRole("img", { name: "Live camera preview" });
      expect(preview.querySelector("canvas")).toHaveAttribute("data-mirrored", "true");
      update(editingSetupView({ camera: { ...editingSetupView().camera, mirrored: false } }));
      expect(screen.getByRole("img", { name: "Live camera preview", hidden: true }).querySelector("canvas")).not.toHaveAttribute("data-mirrored");
      update(editingSetupView({ camera: { ...editingSetupView().camera, previewActive: false } }));
      expect(screen.queryByRole("img", { name: "Live camera preview", hidden: true })).toBeNull();
    });

    it("waits with words for the first frame, and shows no picture when the connection carries none", () => {
      setup(editingSetupView({ camera: { ...editingSetupView().camera, state: "checking" } }));
      expect(screen.getByText("Getting the camera ready.")).toBeVisible();
    });
  });

  describe("the Guest display field", () => {
    it("sends SelectGuestDisplay and IdentifyDisplays", async () => {
      const { client } = setup(setupView());
      fireEvent.click(screen.getByRole("radio", { name: "DELL P2419H" }));
      fireEvent.click(screen.getByRole("button", { name: "IDENTIFY DISPLAYS" }));
      await flush();
      expect(sent(client)).toEqual([["SelectGuestDisplay", { displayId: "display-2" }], ["IdentifyDisplays", {}]]);
    });

    it("holds IDENTIFY DISPLAYS while the numbers are showing", async () => {
      const { client } = setup(setupView(), { identifying: true });
      fireEvent.click(screen.getByRole("button", { name: "IDENTIFY DISPLAYS" }));
      await flush();
      expect(sent(client)).toEqual([]);
    });

    it("keeps a display that isn't connected, with its note", () => {
      setup(editingSetupView({ guestDisplay: { ...editingSetupView().guestDisplay, displays: editingSetupView().guestDisplay.displays.slice(0, 1), state: "missing" } }));
      expect(screen.getByText("The chosen guest display isn't connected.")).toBeVisible();
      expect(screen.getByRole("radio", { name: "DELL P2419H" })).toBeChecked();
    });

    it("warns when the Guest Screen will cover the Operator Screen", () => {
      setup(editingSetupView({ guestDisplay: { ...editingSetupView().guestDisplay, selectedId: "display-1", selectedName: "Built-in display", coversOperatorScreen: true } }));
      expect(screen.getByText("The Guest Screen will cover this screen. Use the hold and PIN to return.")).toBeVisible();
    });
  });

  describe("Mirror, Countdown, and Guest gallery", () => {
    it("send SetMirror, SetCountdown, and SetGuestGallery with the new value", async () => {
      const { client } = setup(editingSetupView());
      fireEvent.click(screen.getByRole("switch", { name: "Mirror" }));
      fireEvent.click(screen.getByRole("radio", { name: "3 seconds" }));
      fireEvent.click(screen.getByRole("switch", { name: "Guest gallery" }));
      await flush();
      expect(sent(client)).toEqual([["SetMirror", { enabled: false }], ["SetCountdown", { seconds: 3 }], ["SetGuestGallery", { enabled: false }]]);
    });

    it("offers 3, 5, and 10 seconds", () => {
      setup(setupView());
      expect(within(screen.getByRole("group", { name: "Countdown" })).getAllByRole("radio").map((radio) => radio.getAttribute("aria-label"))).toEqual(["3 seconds", "5 seconds", "10 seconds"]);
    });
  });

  describe("Templates and Looks", () => {
    it("show the offered ones checked, and the default marked", () => {
      setup(editingSetupView());
      const templates = within(screen.getByRole("group", { name: "Offered Templates" }));
      expect(templates.getByRole("checkbox", { name: "Signature Strip" })).toBeChecked();
      expect(templates.getByRole("checkbox", { name: "Butter Gingham" })).not.toBeChecked();
      expect(templates.getByRole("radio", { name: /Default Panorama Four/ })).toBeChecked();
      const looks = within(screen.getByRole("group", { name: "Offered Looks" }));
      expect(looks.getAllByRole("checkbox").filter((box) => (box as HTMLInputElement).checked)).toHaveLength(4);
      expect(looks.getByRole("radio", { name: /Default Naturale/ })).toBeChecked();
    });

    it("send SetOfferedTemplates and SetDefaultTemplate", async () => {
      const { client } = setup(editingSetupView());
      const templates = within(screen.getByRole("group", { name: "Offered Templates" }));
      fireEvent.click(templates.getByRole("checkbox", { name: "Butter Gingham" }));
      fireEvent.click(templates.getByRole("checkbox", { name: "Signature Strip" }));
      fireEvent.click(templates.getByRole("radio", { name: /Default Polka Keepsake/ }));
      await flush();
      expect(sent(client)).toEqual([
        ["SetOfferedTemplates", { templateIds: ["signature-strip", "panorama-four", "butter-gingham", "polka-keepsake"] }],
        ["SetOfferedTemplates", { templateIds: ["panorama-four", "polka-keepsake"] }],
        ["SetDefaultTemplate", { templateId: "polka-keepsake" }],
      ]);
    });

    it("send SetOfferedLooks and SetDefaultLook", async () => {
      const { client } = setup(editingSetupView());
      const looks = within(screen.getByRole("group", { name: "Offered Looks" }));
      fireEvent.click(looks.getByRole("checkbox", { name: "Soft Color" }));
      fireEvent.click(looks.getByRole("radio", { name: /Default Sepia/ }));
      await flush();
      expect(sent(client)).toEqual([
        ["SetOfferedLooks", { lookIds: ["monochrome-v1", "naturale-v1", "soft-color-v1", "sepia-v1", "classic-v1"] }],
        ["SetDefaultLook", { lookId: "sepia-v1" }],
      ]);
    });

    it("show each Template's blank preview from the brain's key, the landscape one turned a quarter", () => {
      setup(editingSetupView());
      const images = document.querySelectorAll<HTMLImageElement>("#setup-templates img");
      expect([...images].map((image) => image.getAttribute("src"))).toEqual([
        "https://preview.fotohavn/signature-strip.naturale-v1.r0.png",
        "https://preview.fotohavn/panorama-four.naturale-v1.r0.png",
        "https://preview.fotohavn/butter-gingham.naturale-v1.r0.png",
        "https://preview.fotohavn/polka-keepsake.naturale-v1.r0.png",
      ]);
      expect(document.querySelectorAll("#setup-templates [data-rotated='true']")).toHaveLength(1);
      expect(images[1]?.closest("[data-rotated='true']")).not.toBeNull();
    });

    it("show a Template without a preview until the brain has composed it, or when the connection serves none", () => {
      const view = editingSetupView();
      const first = view.templates[0];
      if (first === undefined) {
        throw new Error("no template");
      }
      const { unmount } = setup({ ...view, templates: [{ ...first, blankPreviewKey: null }, ...view.templates.slice(1)] });
      expect(document.querySelectorAll("#setup-templates img")).toHaveLength(3);
      unmount();
      const client = { ...stubClient(), previewUrl: undefined };
      setup(view, { client });
      expect(document.querySelectorAll("#setup-templates img")).toHaveLength(0);
    });
  });

  describe("Phase Timers", () => {
    it("shows one numeric control per phase, with its range, what happens at the end, and Off where allowed", () => {
      setup(editingSetupView());
      for (const [name, value] of [["Template", "45"], ["Photographs: capture", "30"], ["Photographs: review", "0"], ["Filters", "30"], ["Keepsake", "15"], ["Guest gallery view", "60"]] as const) {
        const field = screen.getByLabelText(name);
        expect(field).toHaveValue(value);
        expect(field).toHaveAttribute("inputmode", "numeric");
      }
      expect(screen.getByLabelText("Template")).toHaveAccessibleDescription(
        "20 to 300 seconds. Enter 0 for Off. When time runs out, the booth continues with the selected Template. If it is off, a guest stays on this step until someone continues or uses START OVER.",
      );
      expect(screen.getByLabelText("Photographs: review")).toHaveAccessibleDescription(expect.stringMatching(/^Off now\. 20 to 300 seconds\./));
      expect(screen.getByLabelText("Keepsake")).toHaveAccessibleDescription("5 to 60 seconds. This timer is always on. When time runs out, the booth returns to Start.");
      expect(screen.getByLabelText("Guest gallery view")).toHaveAccessibleDescription("20 to 300 seconds. This timer is always on. When time runs out, the booth returns to Start.");
    });

    it("sends SetPhaseTimer with the whole number typed", () => {
      const { client } = setup(editingSetupView());
      change("Filters", "45");
      fireEvent.blur(screen.getByLabelText("Filters"));
      change("Keepsake", "20");
      act(() => {
        vi.advanceTimersByTime(draftIdleMs);
      });
      expect(sent(client)).toEqual([["SetPhaseTimer", { phase: "filters", seconds: 45 }], ["SetPhaseTimer", { phase: "keepsake", seconds: 20 }]]);
    });

    it("sends 0 for Off, and takes digits only", () => {
      const { client } = setup(editingSetupView());
      change("Template", "0a");
      expect(screen.getByLabelText("Template")).toHaveValue("0");
      fireEvent.blur(screen.getByLabelText("Template"));
      expect(sent(client)).toEqual([["SetPhaseTimer", { phase: "template", seconds: 0 }]]);
    });

    it("sends a value out of range as typed, so the brain can say what is wrong", () => {
      const { client } = setup(editingSetupView());
      change("Keepsake", "2");
      fireEvent.blur(screen.getByLabelText("Keepsake"));
      expect(sent(client)).toEqual([["SetPhaseTimer", { phase: "keepsake", seconds: 2 }]]);
    });

    it("goes back to the brain's value when the field is left empty", () => {
      const { client } = setup(editingSetupView());
      change("Template", "");
      expect(screen.getByLabelText("Template")).toHaveValue("");
      fireEvent.blur(screen.getByLabelText("Template"));
      expect(screen.getByLabelText("Template")).toHaveValue("45");
      expect(sent(client)).toEqual([]);
    });
  });

  describe("the Operator PIN", () => {
    it("says the PIN is set and offers to change it, showing no input until then", () => {
      setup(editingSetupView());
      expect(screen.getByText("PIN set")).toBeVisible();
      expect(screen.queryByLabelText("PIN")).toBeNull();
      expect(screen.queryByLabelText("New PIN")).toBeNull();
      fireEvent.click(screen.getByRole("button", { name: "CHANGE PIN" }));
      expect(screen.getByLabelText("New PIN")).toBeVisible();
      expect(screen.getByLabelText("Confirm new PIN")).toBeVisible();
    });

    it("asks for the PIN twice when there is none, masked, with digits only, never shown again", () => {
      setup(editingSetupView({ pin: { isSet: false, changing: false } }));
      const pin = screen.getByLabelText("PIN");
      const confirmation = screen.getByLabelText("Confirm PIN");
      for (const input of [pin, confirmation]) {
        expect(input).toHaveAttribute("type", "password");
        expect(input).toHaveAttribute("inputmode", "numeric");
        expect(input).toHaveAttribute("autocomplete", "off");
        expect(input).toHaveAttribute("maxlength", "6");
      }
      expect(pin).toHaveAccessibleDescription("4 to 6 digits. Enter it twice. It is never shown again.");
      change("PIN", "12a34");
      expect(pin).toHaveValue("1234");
    });

    it("sends the pair with SetPin after a pause, and on blur", () => {
      const { client } = setup(editingSetupView({ pin: { isSet: false, changing: false } }));
      change("PIN", "4821");
      change("Confirm PIN", "4821");
      expect(sent(client)).toEqual([]);
      act(() => {
        vi.advanceTimersByTime(draftIdleMs);
      });
      expect(sent(client)).toEqual([["SetPin", { pin: "4821", confirmation: "4821" }]]);
      change("PIN", "48215");
      fireEvent.blur(screen.getByLabelText("PIN"));
      expect(sent(client)).toEqual([["SetPin", { pin: "4821", confirmation: "4821" }], ["SetPin", { pin: "48215", confirmation: "4821" }]]);
      fireEvent.blur(screen.getByLabelText("PIN"));
      expect(sent(client)).toHaveLength(2);
    });

    it("lets the operator keep the current PIN after starting to change it, and clears the pending change", () => {
      const { client } = setup(editingSetupView());
      fireEvent.click(screen.getByRole("button", { name: "CHANGE PIN" }));
      change("New PIN", "1234");
      fireEvent.click(screen.getByRole("button", { name: "KEEP CURRENT PIN" }));
      expect(sent(client)).toEqual([["SetPin", { pin: "", confirmation: "" }]]);
      expect(screen.getByText("PIN set")).toBeVisible();
      act(() => {
        vi.advanceTimersByTime(draftIdleMs * 2);
      });
      expect(sent(client)).toHaveLength(1);
    });

    it("shows the inputs at once while the brain holds a pending new PIN, and never anything typed in the page's text", () => {
      setup(editingSetupView({ pin: { isSet: true, changing: true } }));
      change("New PIN", "909090");
      change("Confirm new PIN", "909090");
      expect(document.body.textContent).not.toContain("909090");
      expect(screen.getByRole("button", { name: "KEEP CURRENT PIN" })).toBeVisible();
    });

    it("drops a pending send when the section goes away", () => {
      const { client, unmount } = setup(editingSetupView({ pin: { isSet: false, changing: false } }));
      change("PIN", "1234");
      unmount();
      act(() => {
        vi.advanceTimersByTime(draftIdleMs * 2);
      });
      expect(sent(client)).toEqual([]);
    });
  });

  describe("leaving, start failures, and Operator Assistance", () => {
    it("sends LeaveEventSetup from the back action", async () => {
      const { client } = setup(editingSetupView());
      fireEvent.click(screen.getByRole("button", { name: "BACK TO EVENTS" }));
      await flush();
      expect(sent(client)).toEqual([["LeaveEventSetup", {}]]);
    });

    it.each([
      [{ code: "eventRunning", cameraReason: null }, "End the running Event to start another."],
      [{ code: "guestDisplayMissing", cameraReason: null }, "The chosen guest display isn't connected."],
      [{ code: "cameraNotEligible", cameraReason: "privacyDenied" }, "The camera isn't ready to start. Camera access is off in Windows privacy settings."],
      [{ code: "storageNotWritable", cameraReason: null }, "The Data folder can't be written to, so the Event can't start. Check the folder, then try again."],
      [{ code: "pinMissing", cameraReason: null }, "Set an Operator PIN to start this Event."],
      [{ code: "invalidSetup", cameraReason: null }, "Some details need attention before this Event can start. Check the fields below."],
    ] as const)("shows the start failure %o at the top in words", (startFailure, text) => {
      setup(editingSetupView({ startFailure }));
      const line = screen.getByText(text, { selector: "span" });
      expect(line).toBeVisible();
      expect(line.closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "alert");
      expect(line.closest("section")).toBeNull();
    });

    it("shows the Operator Assistance alert, with a link back to the running Event", async () => {
      const { client } = setup(editingSetupView(), { assistance: "guestDisplay" });
      expect(screen.getByText("The guest display isn't connected.")).toBeVisible();
      fireEvent.click(screen.getByRole("button", { name: "GO TO THE RUNNING EVENT" }));
      await flush();
      expect(sent(client)).toEqual([["LeaveEventSetup", {}]]);
    });

    it("shows no alert while the running Event needs nothing", () => {
      setup(editingSetupView(), { assistance: null });
      expect(screen.queryByRole("button", { name: "GO TO THE RUNNING EVENT" })).toBeNull();
    });
  });
});
