import { expect, test } from "../../src/fixtures";
import { size, viewport } from "../../src/displays";
import {
  createEvent,
  endEventFromOperator,
  findDigitsInLogs,
  findDigitsInStore,
  guestPage,
  readStoredEvent,
  useVirtualScreens,
} from "../../src/operator";
import { EventListScreen, GuestStartScreen } from "../../src/screens/screens";

const pin = "739104";

test(
  "an Event is created with every field, started with SAVE AND START on the second display, ended with END EVENT, and the store holds a PIN hash but no PIN",
  { tag: ["@m6"] },
  async ({ app, operatorPage, camera, hooks, dataDir }) => {
    const screens = await useVirtualScreens(app, 2);
    const guestDisplay = screens.guest;
    if (guestDisplay === undefined) {
      throw new Error("Two virtual displays were expected.");
    }
    const list = new EventListScreen(operatorPage);
    await expect(list.empty).toBeVisible();

    const name = "Harbour gala";
    const eventId = await test.step("create the Event with every metadata field, the real camera, the second display, and a PIN; SAVE AND START", () =>
      createEvent(
        { app, page: operatorPage, camera },
        {
          name,
          date: "2026-06-12",
          venue: "Harbour Hall",
          customer: "Lindqvist & Co",
          notes: "Bring the props.",
          display: guestDisplay.name,
          pin,
        },
        "saveAndStart",
      ),
    );

    const guest = await test.step("the Guest Screen opens on the second display with Start; the Operator Screen shows the running-Event panel", async () => {
      const guest = await guestPage(app);
      const start = new GuestStartScreen(guest);
      await expect(start.heading).toBeVisible();
      await expect(start.stepInside).toBeVisible();
      await expect(start.stepInside).toHaveAttribute("aria-disabled", "false").catch(() => undefined);

      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.guestScreenOpen).toBe(true);
      expect(diagnostics.windows.find((window) => window.role === "guest")?.bounds).toEqual(guestDisplay.bounds);
      expect(await viewport(guest)).toEqual({ ...size(guestDisplay.bounds), devicePixelRatio: 1 });
      expect(diagnostics.camera.state).toBe("ready");
      expect(diagnostics.wakeLock.held).toBe(true);

      await expect(list.runningPanel(name)).toBeVisible();
      await expect(list.row(name)).toContainText("RUNNING");
      await expect(list.runningFact("display")).toHaveText("Connected");
      await expect(list.runningFact("camera")).toHaveText("Ready");
      await expect(list.runningFact("step")).toHaveText("Start");
      return guest;
    });

    await test.step("END EVENT asks first, with the plan's words; KEEP RUNNING keeps it", async () => {
      await list.endEvent.click();
      const dialog = operatorPage.getByRole("alertdialog");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole("heading", { name: "End this Event?" })).toBeVisible();
      await expect(dialog).toContainText("Anyone using the booth will be stopped. Their photographs are kept.");
      await dialog.getByRole("button", { name: "KEEP RUNNING", exact: true }).click();
      await expect(dialog).toHaveCount(0);
      expect((await hooks.getDiagnostics()).guestScreenOpen).toBe(true);
    });

    await test.step("END EVENT and confirm: the Guest Screen closes, the camera and wake lock are released", async () => {
      await endEventFromOperator(app, operatorPage);
      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.guestScreenOpen).toBe(false);
      expect(diagnostics.camera.state).toBe("idle");
      expect(diagnostics.wakeLock.held).toBe(false);
      await expect.poll(() => guest.isClosed()).toBe(true);
      await expect(list.row(name)).toBeVisible();
      await expect(list.runningPanel()).toHaveCount(0);
    });

    await test.step("the store has the Event's metadata, offered Templates and Looks, timers, display binding, and a PIN hash, but no PIN", () => {
      const stored = readStoredEvent(dataDir, eventId);
      expect(stored).toBeDefined();
      const row = stored?.row ?? {};
      expect(row).toMatchObject({
        name,
        event_date: "2026-06-12",
        venue: "Harbour Hall",
        customer_name: "Lindqvist & Co",
        notes: "Bring the props.",
        camera_name: camera.name,
        guest_display_id: guestDisplay.id,
        guest_display_name: guestDisplay.name,
        mirror: 1,
        countdown_seconds: 5,
        default_template_id: "signature-strip",
        default_look_id: "naturale-v1",
        guest_gallery_enabled: 0,
        pin_iterations: expect.any(Number),
      });
      expect((row.pin_hash as Uint8Array).byteLength).toBeGreaterThan(0);
      expect((row.pin_salt as Uint8Array).byteLength).toBeGreaterThan(0);
      expect(stored?.templates).toEqual(["butter-gingham", "panorama-four", "polka-keepsake", "signature-strip"]);
      expect(stored?.looks).toEqual([
        "classic-v1",
        "monochrome-v1",
        "naturale-v1",
        "sepia-v1",
        "soft-color-v1",
        "soft-monochrome-v1",
        "warm-monochrome-v1",
      ]);
      expect(Object.keys(stored?.timers ?? {}).sort()).toEqual([
        "filters",
        "guest_gallery",
        "keepsake",
        "photographs_capture",
        "photographs_review",
        "template",
      ]);
      expect(stored?.timers.keepsake).toBe(12);
      expect(stored?.timers.template).toBe(30);
      expect(findDigitsInStore(dataDir, pin)).toEqual([]);
      expect(findDigitsInLogs(dataDir, pin)).toEqual([]);
    });
  },
);
