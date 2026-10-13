import { readFileSync } from "node:fs";
import path from "node:path";
import { expect, type Page } from "@playwright/test";
import { appOrigin } from "./launch";
import { paths } from "./paths";

/** The fixture gallery, served by the E2E UI build the host loads from https://app.fotohavn/. */
export const galleryUrl = `${appOrigin}index.html`;

/** One entry of `?gallery=manifest` (app/ui/src/gallery/discover.ts). */
export interface ManifestFixture {
  readonly id: string;
  readonly title: string;
  readonly surface: "light" | "dark";
  readonly role: "guest" | "operator";
  readonly layout: "centered" | "fill";
  readonly source: string;
}

export const resolutions = [
  { width: 1280, height: 720 },
  { width: 1366, height: 768 },
  { width: 1920, height: 1080 },
  { width: 2560, height: 1440 },
] as const;

export function resolutionName({ width, height }: { width: number; height: number }): string {
  return `${String(width)}x${String(height)}`;
}

/** The Chromium fixture lane's screenshot for one fixture and resolution (app/ui `npm run test:fixtures`). */
export function chromiumBaselinePath(fixtureId: string, resolution: { width: number; height: number }): string {
  return path.join(paths.appDir, "output", "fixtures", "components", fixtureId, `${resolutionName(resolution)}.png`);
}

/**
 * Navigates the page back to the app itself (not a gallery URL) and waits for its brain handshake, so the test hooks
 * (`window.fotohavnE2E`) work again. The gallery mounts without a brain connection, so a test that visited it calls this
 * before it ends and before it needs a hook.
 */
export async function returnToApp(page: Page): Promise<void> {
  await page.goto(galleryUrl);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-status", "connected");
}

export async function readManifest(page: Page): Promise<readonly ManifestFixture[]> {
  await page.goto(`${galleryUrl}?gallery=manifest`);
  await expect(page.locator('[data-fotohavn-gallery-error="true"]')).toHaveCount(0);
  const text = await page.locator("#fotohavn-gallery-manifest").textContent();
  return (JSON.parse(text ?? "{}") as { fixtures: ManifestFixture[] }).fixtures;
}

/** Opens one fixture alone and waits as the Chromium fixture lane does: gallery ready (fonts loaded), role set, fixture visible. */
export async function openFixture(page: Page, fixtureId: string, role: ManifestFixture["role"]): Promise<void> {
  await page.goto(`${galleryUrl}?gallery=${encodeURIComponent(fixtureId)}`);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-gallery-ready", "true");
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", role);
  await expect(page.locator(`main[data-fotohavn-fixture="${fixtureId}"]`)).toBeVisible();
}

interface TokenFile {
  color: Record<string, { $value: string }>;
}

/** The brand color tokens from brand/tokens/tokens.json, keyed by token name (`off-white`), as `rgb(r, g, b)` strings. */
export function brandColors(): Record<string, string> {
  const file = path.resolve(paths.appDir, "..", "brand", "tokens", "tokens.json");
  const tokens = JSON.parse(readFileSync(file, "utf8")) as TokenFile;
  return Object.fromEntries(
    Object.entries(tokens.color).map(([name, token]) => {
      const hex = /^#([0-9a-f]{6})$/i.exec(token.$value);
      if (hex?.[1] === undefined) {
        throw new Error(`brand/tokens/tokens.json color ${name} is not a #RRGGBB value: ${token.$value}`);
      }
      const value = Number.parseInt(hex[1], 16);
      return [name, `rgb(${String((value >> 16) & 255)}, ${String((value >> 8) & 255)}, ${String(value & 255)})`];
    }),
  );
}
