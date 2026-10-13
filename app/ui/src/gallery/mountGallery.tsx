import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { discoverFixtures, type GalleryEntry } from "./discover.ts";
import { Gallery, GalleryError } from "./Gallery.tsx";

/**
 * Every `*.fixtures.tsx` under src/components, found by glob: adding a component's fixtures file adds it to the
 * gallery and to the fixture lane, with no registry to edit. `main.tsx` imports this module only in development and
 * E2E builds, behind a build-time guard, so field builds contain no gallery or fixture code.
 */
const modules: Record<string, unknown> = import.meta.glob("../components/**/*.fixtures.tsx", { eager: true });

export function loadEntries(fixtureModules: Readonly<Record<string, unknown>> = modules): GalleryEntry[] | Error {
  try {
    return discoverFixtures(fixtureModules);
  } catch (error) {
    return error instanceof Error ? error : new Error(String(error));
  }
}

/** Renders the gallery into `root` for a `?gallery=<selection>` URL. */
export function mountGallery(root: HTMLElement, selection: string, fixtureModules?: Readonly<Record<string, unknown>>): void {
  const entries = loadEntries(fixtureModules);
  document.title = "FOTOHAVN fixture gallery";
  createRoot(root).render(
    <StrictMode>
      {entries instanceof Error ? <GalleryError error={entries} /> : <Gallery entries={entries} selection={selection} />}
    </StrictMode>,
  );
}
