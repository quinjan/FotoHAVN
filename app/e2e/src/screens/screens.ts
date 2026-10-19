import { expect, type Locator, type Page } from "@playwright/test";

/** A row action of the Event list, named as the visible link (the accessible name adds the Event's name). */
export type RowAction = "START" | "GALLERY" | "EDIT" | "OPEN FOLDER" | "DELETE";

const rowActionLabel: Readonly<Record<RowAction, (name: string) => string>> = {
  START: (name) => `Start ${name}`,
  GALLERY: (name) => `Open the gallery of ${name}`,
  EDIT: (name) => `Edit ${name}`,
  "OPEN FOLDER": (name) => `Open the folder of ${name}`,
  DELETE: (name) => `Delete ${name}`,
};

/** The Operator Screen's Event list: the rows with their actions, the running-Event panel, `NEW EVENT`, and notices. */
export class EventListScreen {
  readonly main: Locator;
  readonly heading: Locator;
  readonly empty: Locator;
  readonly newEvent: Locator;
  readonly notice: Locator;
  readonly rows: Locator;

  constructor(readonly page: Page) {
    this.main = page.locator('main[data-fotohavn-surface="eventList"]');
    this.heading = this.main.getByRole("heading", { level: 1, name: "Events" });
    this.empty = this.main.getByText("No Events yet.");
    this.newEvent = this.main.getByRole("button", { name: "NEW EVENT" });
    this.notice = this.main.getByRole("status").filter({ hasText: /\S/ });
    this.rows = this.main.getByRole("list", { name: "Events" }).getByRole("listitem");
  }

  /** The row of the Event with this exact name. */
  row(name: string): Locator {
    return this.rows.filter({ has: this.page.getByRole("heading", { level: 2, name, exact: true }) });
  }

  /** One of the row's actions; disabled ones stay in the page with `aria-disabled="true"`. */
  rowAction(name: string, action: RowAction): Locator {
    return this.row(name).getByRole("button", { name: rowActionLabel[action](name), exact: true });
  }

  /** The reason line under a row's actions ("End the running Event to start another."). */
  rowReason(name: string): Locator {
    return this.row(name).getByText("End the running Event to start another.", { exact: true });
  }

  /** The running-Event panel (a group named by the Event), or its absence. */
  runningPanel(name?: string): Locator {
    const panels = this.main.getByRole("group").filter({ has: this.page.getByRole("button", { name: "END EVENT", exact: true }) });
    return name === undefined ? panels : panels.filter({ has: this.page.getByRole("heading", { name, exact: true }) });
  }

  /** One fact of the running-Event panel's list: `display`, `camera`, `step`, `cycles`, or `time`. */
  runningFact(name: "display" | "camera" | "step" | "cycles" | "time"): Locator {
    return this.runningPanel().locator(`[data-fact="${name}"] dd`);
  }

  get endEvent(): Locator {
    return this.runningPanel().getByRole("button", { name: "END EVENT", exact: true });
  }

  get openGallery(): Locator {
    return this.runningPanel().getByRole("button", { name: "OPEN GALLERY", exact: true });
  }
}

/** The operator's single-screen surface while the Guest Screen covers the Operator window. */
export class SingleScreenOperator {
  readonly main: Locator;
  readonly statement: Locator;

  constructor(readonly page: Page) {
    this.main = page.locator('main[data-fotohavn-surface="guestScreenActive"]');
    this.statement = this.main.getByText("The booth is running on this screen.", { exact: true });
  }
}

/** The Operator Screen's confirmation dialogs (end, delete, leave setup). */
export class ConfirmDialogScreen {
  readonly dialog: Locator;

  constructor(readonly page: Page) {
    this.dialog = page.getByRole("alertdialog");
  }

  get title(): Locator {
    return this.dialog.getByRole("heading").first();
  }

  button(name: string): Locator {
    return this.dialog.getByRole("button", { name, exact: true });
  }
}

/** The Guest Screen's Start surface (STEP INSIDE, the gallery link) and its exit hold. */
export class GuestStartScreen {
  readonly main: Locator;
  readonly heading: Locator;
  readonly stepInside: Locator;
  readonly exitHold: Locator;

  constructor(readonly page: Page) {
    this.main = page.locator('main[data-fotohavn-surface="start"]');
    this.heading = this.main.getByRole("heading", { level: 1, name: "Small room. Whole other world." });
    this.stepInside = this.main.getByRole("button", { name: "STEP INSIDE" });
    this.exitHold = page.getByRole("button", { name: "Operator exit", exact: true });
  }
}

/** The Operator PIN dialog on the Guest Screen: the on-screen pad, its messages, and the lockout countdown. */
export class PinDialogScreen {
  readonly dialog: Locator;
  readonly submit: Locator;
  readonly keepRunning: Locator;
  readonly wrongPin: Locator;
  readonly locked: Locator;
  readonly countdown: Locator;
  readonly triesLeft: Locator;

  constructor(readonly page: Page) {
    this.dialog = page.getByRole("dialog", { name: "Operator PIN" });
    this.submit = this.dialog.getByRole("button", { name: "END EVENT", exact: true });
    this.keepRunning = this.dialog.getByRole("button", { name: "KEEP RUNNING", exact: true });
    this.wrongPin = this.dialog.getByText("That PIN isn't right. Try again.", { exact: true });
    this.locked = this.dialog.getByText("Too many tries. The pad is locked for now.", { exact: true });
    this.countdown = this.dialog.getByText(/^Try again in \d+ seconds?\.$/);
    this.triesLeft = this.dialog.getByText(/^\d+ (tries|try) left\.$/);
  }

  key(digit: string): Locator {
    return this.dialog.getByRole("group", { name: "PIN digits" }).getByRole("button", { name: digit, exact: true });
  }

  /** Presses the on-screen digits in order. */
  async enter(digits: string): Promise<void> {
    for (const digit of digits) {
      await this.key(digit).click();
    }
  }

  /** Enters `digits` and presses the submit action. */
  async submitPin(digits: string): Promise<void> {
    await this.enter(digits);
    await this.submit.click();
  }
}

/** Event setup's steps, by their labels in the step row. */
export type SetupStep = "Event" | "Hardware" | "Templates" | "Timing" | "Guests";

/** Event setup's fields, pickers, and actions, each found by its label. Each lives on one step; open it with `goTo`. */
export class EventSetupScreen {
  readonly main: Locator;
  readonly heading: Locator;
  readonly name: Locator;
  readonly date: Locator;
  readonly venue: Locator;
  readonly customer: Locator;
  readonly notes: Locator;
  readonly camera: Locator;
  readonly cameraStatus: Locator;
  readonly mirror: Locator;
  readonly displayGroup: Locator;
  readonly identifyDisplays: Locator;
  readonly templates: Locator;
  readonly looks: Locator;
  readonly guestGallery: Locator;
  readonly pin: Locator;
  readonly pinConfirmation: Locator;
  readonly save: Locator;
  readonly saveAndStart: Locator;
  readonly back: Locator;

  constructor(readonly page: Page) {
    this.main = page.locator('main[data-fotohavn-surface="eventSetup"]');
    this.heading = this.main.getByRole("heading", { level: 1, name: /Set up the\s+Event\./ });
    this.name = this.main.getByLabel("Name", { exact: true });
    this.date = this.main.getByLabel("Date", { exact: true });
    this.venue = this.main.getByLabel("Venue", { exact: true });
    this.customer = this.main.getByLabel("Customer or host", { exact: true });
    this.notes = this.main.getByLabel("Notes", { exact: true });
    this.camera = this.main.getByLabel("Camera", { exact: true });
    this.cameraStatus = this.main.locator('section[aria-labelledby="setup-section-camera"]').getByRole("status").first();
    this.mirror = this.main.getByRole("switch", { name: "Mirror", exact: true });
    this.displayGroup = this.main.locator("#setup-guest-display");
    this.identifyDisplays = this.main.getByRole("button", { name: "IDENTIFY DISPLAYS", exact: true });
    this.templates = this.main.locator("#setup-templates");
    this.looks = this.main.locator("#setup-looks");
    this.guestGallery = this.main.getByRole("switch", { name: "Guest gallery", exact: true });
    this.pin = this.main.getByLabel(/^(New )?PIN$/);
    this.pinConfirmation = this.main.getByLabel(/^Confirm (new )?PIN$/);
    this.save = this.main.getByRole("button", { name: "SAVE", exact: true });
    this.saveAndStart = this.main.getByRole("button", { name: "SAVE AND START", exact: true });
    this.back = this.main.getByRole("button", { name: "BACK TO EVENTS" });
  }

  /** A display row of the picker, by the display's name; its radio is the control. */
  display(name: string): Locator {
    return this.displayGroup.getByRole("radio", { name, exact: true });
  }

  /** The text of the display rows in order: `Virtual operator`, `Virtual guest`, and so on. */
  async displayRows(): Promise<{ name: string; detail: string; checked: boolean; disabled: boolean }[]> {
    return this.displayGroup.locator("li").evaluateAll((items) =>
      items.map((item) => {
        const input = item.querySelector<HTMLInputElement>("input");
        const label = (id: string) => (id === "" ? "" : (document.getElementById(id)?.textContent ?? ""));
        const describedBy = (input?.getAttribute("aria-describedby") ?? "").split(" ").map(label).join(" ").trim();
        return {
          name: label(input?.getAttribute("aria-labelledby") ?? ""),
          detail: describedBy,
          checked: input?.checked ?? false,
          disabled: input?.disabled ?? false,
        };
      }),
    );
  }

  /** The offered-Template checkbox for a Template's name. */
  template(name: string): Locator {
    return this.templates.getByRole("checkbox", { name, exact: true });
  }

  /** The "Default" radio on a Template's row. */
  defaultTemplate(name: string): Locator {
    return this.templates.getByRole("radio", { name: `Default ${name}`, exact: true });
  }

  look(name: string): Locator {
    return this.looks.getByRole("checkbox", { name, exact: true });
  }

  defaultLook(name: string): Locator {
    return this.looks.getByRole("radio", { name: `Default ${name}`, exact: true });
  }

  /** The Template rows' preview images, by Template name. */
  templatePreview(name: string): Locator {
    return this.templates.locator("li").filter({ has: this.page.getByRole("checkbox", { name, exact: true }) }).locator("img");
  }

  /** A Phase Timer's input by its label (Template, Photographs: capture, …, Keepsake, Guest gallery view). */
  timer(label: string): Locator {
    return this.main.getByLabel(label, { exact: true });
  }

  countdown(seconds: 3 | 5 | 10): Locator {
    return this.main.getByRole("radio", { name: `${String(seconds)} seconds`, exact: true });
  }

  /** A step in the step row (`01 Event` to `05 Guests`); its name ends ", needs attention" while a field on it is marked. */
  step(label: SetupStep): Locator {
    return this.main.getByRole("navigation", { name: "Event setup steps" }).getByRole("button", { name: new RegExp(`^\\d\\d ${label}`) });
  }

  /** Opens a step from the step row and waits until it is the current one. SAVE and SAVE AND START are on the Guests step. */
  async goTo(label: SetupStep): Promise<void> {
    await this.step(label).click();
    await expect(this.step(label)).toHaveAttribute("aria-current", "step");
  }

  /** The alert line that explains a failed save or start (error summary, start failure). */
  statusLine(text: string | RegExp): Locator {
    return this.main.getByRole("status").filter({ hasText: text });
  }
}

/** The fixed screen a page shows when the brain answers its `hello` with `protocolMismatch`. */
export class ProtocolMismatchScreen {
  readonly main: Locator;
  readonly heading: Locator;
  readonly versions: Locator;

  constructor(readonly page: Page) {
    this.main = page.locator('main[data-fotohavn-screen="protocolMismatch"]');
    this.heading = this.main.getByRole("heading", { level: 1, name: "A little mix-up." });
    this.versions = this.main.getByText(/^Booth version \d+ · Screen version -?\d+$/);
  }
}
/** The Operator Screen's storage error view (M4): what is wrong with the Data folder, its path, and `TRY AGAIN`. */
export class StorageErrorScreen {
  readonly main: Locator;
  readonly heading: Locator;
  readonly dataFolderLabel: Locator;
  readonly dataPath: Locator;
  readonly tryAgain: Locator;

  constructor(readonly page: Page) {
    this.main = page.locator('main[data-fotohavn-surface="storageError"]');
    this.heading = this.main.getByRole("heading", { level: 1, name: "The Data folder needs attention." });
    this.dataFolderLabel = this.main.locator("dt");
    this.dataPath = this.main.locator("dd");
    this.tryAgain = this.main.getByRole("button", { name: "TRY AGAIN" });
  }

  /** The line explaining the problem, by its exact text. */
  line(text: string): Locator {
    return this.main.getByText(text, { exact: true });
  }
}

/** The Event list's rows, by Event. */
export class EventRows {
  readonly rows: Locator;

  constructor(readonly page: Page) {
    this.rows = page.locator('main[data-fotohavn-surface="eventList"]').getByRole("list", { name: "Events" }).getByRole("listitem");
  }

  /** Each row's heading and its facts line (date, venue, Guest Cycles, last use), in display order. */
  async read(): Promise<{ name: string; facts: string }[]> {
    return this.rows.evaluateAll((items) =>
      items.map((item) => ({
        name: item.querySelector("h2")?.textContent ?? "",
        facts: Array.from(item.querySelectorAll("p span"))
          .map((span) => span.textContent ?? "")
          .join(" | "),
      })),
    );
  }
}
/** The Operator Screen's camera diagnostics view (M5): the live preview, the camera list, the state line, and the measures. */
export class CameraDiagnosticsScreen {
  readonly root: Locator;
  readonly heading: Locator;
  readonly preview: Locator;
  readonly canvas: Locator;
  readonly cameraRows: Locator;
  readonly capture: Locator;
  readonly mirror: Locator;
  readonly statusLine: Locator;
  readonly composition: Locator;
  readonly compositionImage: Locator;
  readonly templateSelect: Locator;
  readonly lookSelect: Locator;
  readonly compose: Locator;

  constructor(readonly page: Page) {
    this.root = page.locator("[data-camera-state]");
    this.heading = page.getByRole("heading", { level: 1 });
    this.preview = page.locator("[data-state]").filter({ has: page.locator("canvas") });
    this.canvas = page.locator("canvas");
    this.cameraRows = page.getByRole("radio");
    this.capture = page.getByRole("button", { name: "CAPTURE" });
    this.mirror = page.getByRole("switch", { name: "Mirror" });
    this.statusLine = page.locator("[data-status-tone]").filter({ hasText: /\S/ });
    this.composition = page.locator("[data-composition-key]");
    this.compositionImage = this.composition.locator("img");
    this.templateSelect = page.getByLabel("Template", { exact: true });
    this.lookSelect = page.getByLabel("Look", { exact: true });
    this.compose = page.getByRole("button", { name: "COMPOSE", exact: true });
  }

  /** The camera rows as rendered: the radio's value is the device id, its accessible name the camera name. */
  async cameraList(): Promise<{ id: string; name: string }[]> {
    return await this.cameraRows.evaluateAll((inputs) =>
      inputs.map((input) => {
        const radio = input as HTMLInputElement;
        const title = (radio.getAttribute("aria-labelledby") ?? "").split(" ").map((id) => document.getElementById(id)?.textContent ?? "").join(" ");
        return { id: radio.value, name: title.trim() };
      }),
    );
  }

  measure(name: "fps" | "age" | "host" | "capture"): Locator {
    return this.page.locator(`[data-camera-measure="${name}"]`);
  }
}
