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
