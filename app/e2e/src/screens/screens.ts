import type { Locator, Page } from "@playwright/test";

/** The Operator Screen's Event list (M2: the empty state and `NEW EVENT`). */
export class EventListScreen {
  readonly main: Locator;
  readonly heading: Locator;
  readonly empty: Locator;
  readonly newEvent: Locator;
  readonly notice: Locator;

  constructor(readonly page: Page) {
    this.main = page.locator('main[data-fotohavn-surface="eventList"]');
    this.heading = this.main.getByRole("heading", { level: 1, name: "Events" });
    this.empty = this.main.getByText("No Events yet.");
    this.newEvent = this.main.getByRole("button", { name: "NEW EVENT" });
    this.notice = this.main.getByRole("status").filter({ hasText: /\S/ });
  }
}

/** The Guest Screen's Start placeholder (M2). */
export class GuestStartScreen {
  readonly main: Locator;
  readonly heading: Locator;

  constructor(readonly page: Page) {
    this.main = page.locator('main[data-fotohavn-surface="start"]');
    this.heading = this.main.getByRole("heading", { level: 1, name: "Small room. Whole other world." });
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