import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";

interface FetchResult {
  status: number;
  contentType: string | null;
  body: number[];
}

/** Fetches a URL from inside the page (the app origin), as the UI would. */
function pageFetch(page: Page, url: string, method = "GET"): Promise<FetchResult> {
  return page.evaluate(
    async ({ url, method }) => {
      const response = await fetch(url, { method, cache: "no-store" });
      const body = method === "HEAD" ? [] : Array.from(new Uint8Array(await response.arrayBuffer()));
      return { status: response.status, contentType: response.headers.get("content-type"), body };
    },
    { url, method },
  );
}

const media = "https://media.fotohavn/";

test.describe("media hosts", { tag: ["@m2"] }, () => {
  test("media.fotohavn serves files only inside Data/Events and Data/Cache/thumbnails and refuses traversal", async ({
    operatorPage,
    dataDir,
  }) => {
    const strip = Buffer.from([0xff, 0xd8, 0xff, 0xe0, 1, 2, 3, 4, 0xff, 0xd9]);
    const thumbnail = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 9, 8, 7]);

    await test.step("create files in the run's Data folder", () => {
      const cycle = path.join(dataDir, "Events", "event-1", "GuestCycles", "cycle-1");
      mkdirSync(cycle, { recursive: true });
      writeFileSync(path.join(cycle, "strip.jpg"), strip);
      writeFileSync(path.join(dataDir, "Events", "event-1", "event.json"), "{}");
      const thumbnails = path.join(dataDir, "Cache", "thumbnails");
      mkdirSync(thumbnails, { recursive: true });
      writeFileSync(path.join(thumbnails, "cycle-1.png"), thumbnail);
      writeFileSync(path.join(dataDir, "Cache", "other.png"), thumbnail);
      writeFileSync(path.join(dataDir, "fotohavn.db"), "SQLite format 3\0 (a stand-in, not a database)");
      mkdirSync(path.join(dataDir, "logs"), { recursive: true });
      writeFileSync(path.join(dataDir, "logs", "secret.txt"), "not media");
    });

    await test.step("files inside Data/Events and Data/Cache/thumbnails are served read-only", async () => {
      const served = await pageFetch(operatorPage, `${media}events/event-1/guest-cycles/cycle-1/strip.jpg`);
      expect(served).toEqual({ status: 200, contentType: "image/jpeg", body: [...strip] });

      const withVersion = await pageFetch(operatorPage, `${media}events/event-1/guest-cycles/cycle-1/strip.jpg?v=abc123`);
      expect(withVersion.status).toBe(200);

      const head = await pageFetch(operatorPage, `${media}thumbnails/cycle-1.png`, "HEAD");
      expect(head.status).toBe(200);
      const thumb = await pageFetch(operatorPage, `${media}thumbnails/cycle-1.png`);
      expect(thumb).toEqual({ status: 200, contentType: "image/png", body: [...thumbnail] });

      expect((await pageFetch(operatorPage, `${media}thumbnails/cycle-1.png`, "POST")).status).not.toBe(200);
      expect((await pageFetch(operatorPage, `${media}events/event-1/guest-cycles/cycle-1/missing.jpg`)).status).toBe(404);
    });

    await test.step("other Data files are 404", async () => {
      for (const url of [
        `${media}fotohavn.db`,
        `${media}logs/fotohavn.jsonl`,
        `${media}logs/secret.txt`,
        `${media}events/event-1/event.json`,
        `${media}Events/event-1/GuestCycles/cycle-1/strip.jpg`,
        `${media}cache/other.png`,
        `${media}thumbnails/`,
      ]) {
        expect((await pageFetch(operatorPage, url)).status, url).toBe(404);
      }
    });

    await test.step("traversal is refused, raw and percent-encoded", async () => {
      for (const url of [
        // Raw: the page's URL parser removes these dot segments before the request, so they reach the host as plain
        // paths outside the allowed roots.
        `${media}thumbnails/../fotohavn.db`,
        `${media}events/event-1/guest-cycles/cycle-1/../../../../fotohavn.db`,
        `${media}thumbnails/..\\..\\fotohavn.db`,
        // Encoded: separators and dots that only the host could decode.
        `${media}thumbnails/..%2F..%2Ffotohavn.db`,
        `${media}thumbnails/..%5C..%5Cfotohavn.db`,
        `${media}thumbnails/%2e%2e%2f%2e%2e%2ffotohavn.db`,
        `${media}events/event-1/guest-cycles/cycle-1/..%2F..%2F..%2F..%2Ffotohavn.db`,
        `${media}events/event-1/guest-cycles/cycle-1/%2E%2E%5C%2E%2E%5Cevent.json`,
        `${media}events/event-1/guest-cycles/..%252F..%252Ffotohavn.db/strip.jpg`,
        `${media}thumbnails/C:%5CWindows%5Cwin.ini`,
      ]) {
        expect((await pageFetch(operatorPage, url)).status, url).toBe(404);
      }
    });
  });
});
