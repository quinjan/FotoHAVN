import type { Locator, Page } from "@playwright/test";

/** The M1 placeholder page: `<main>` with the FOTOHAVN heading and a status line. */
export class PlaceholderScreen {
  readonly main: Locator;
  readonly heading: Locator;
  readonly status: Locator;

  constructor(readonly page: Page) {
    this.main = page.getByRole("main");
    this.heading = this.main.getByRole("heading", { level: 1, name: "FOTOHAVN" });
    this.status = this.main.getByText("The booth is getting ready.");
  }
}
