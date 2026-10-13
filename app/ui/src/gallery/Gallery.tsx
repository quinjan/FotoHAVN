import { useLayoutEffect, useState } from "react";
import { TextLink } from "../components/TextLink/index.ts";
import { toManifest, type GalleryEntry } from "./discover.ts";
import styles from "./Gallery.module.css";

/** The `?gallery=` value that publishes the manifest JSON for the fixture lane. */
export const manifestSelection = "manifest";

/** The id of the `<pre>` holding the manifest JSON. */
export const manifestElementId = "fotohavn-gallery-manifest";

/**
 * Marks the document for screenshots: the fixture's window role on <html> (so the guest rules and kiosk scale apply
 * exactly as on the Guest Screen), and `data-fotohavn-gallery-ready` once the fonts have loaded.
 */
function useFixtureDocument(role: string): void {
  useLayoutEffect(() => {
    const root = document.documentElement;
    root.dataset.fotohavnRole = role;
    let active = true;
    const fonts = (document as Partial<Document>).fonts;
    void (fonts?.ready ?? Promise.resolve()).then(() => {
      if (active) {
        root.dataset.fotohavnGalleryReady = "true";
      }
    });
    return () => {
      active = false;
      delete root.dataset.fotohavnRole;
      delete root.dataset.fotohavnGalleryReady;
    };
  }, [role]);
}

function GalleryFixture({ entry }: { readonly entry: GalleryEntry }) {
  const { fixture } = entry;
  useFixtureDocument(fixture.role);
  return (
    <main
      className={styles.single}
      data-tone={fixture.surface}
      data-layout={fixture.layout ?? "centered"}
      data-fotohavn-fixture={fixture.id}
      aria-label={fixture.title}
    >
      {fixture.render()}
    </main>
  );
}

function GalleryManifestView({ entries }: { readonly entries: readonly GalleryEntry[] }) {
  return (
    <main className={styles.index} aria-label="Fixture manifest">
      <pre id={manifestElementId} className={styles.manifest}>
        {JSON.stringify(toManifest(entries), null, 2)}
      </pre>
    </main>
  );
}

function groupBySource(entries: readonly GalleryEntry[]): Map<string, GalleryEntry[]> {
  const groups = new Map<string, GalleryEntry[]>();
  for (const entry of entries) {
    groups.set(entry.source, [...(groups.get(entry.source) ?? []), entry]);
  }
  return groups;
}

function componentName(source: string): string {
  return source.replace(/^.*\//, "").replace(/\.fixtures\.tsx$/, "");
}

function GalleryIndex({ entries }: { readonly entries: readonly GalleryEntry[] }) {
  return (
    <main className={styles.index}>
      <h1 className={styles.title}>Fixture gallery</h1>
      <p>
        {entries.length} fixtures. Open one alone with <code>?gallery=&lt;id&gt;</code>; the fixture lane reads{" "}
        <code>?gallery=manifest</code>.
      </p>
      {[...groupBySource(entries)].map(([source, group]) => (
        <section key={source} className={styles.group} aria-labelledby={`gallery-${componentName(source)}`}>
          <h2 id={`gallery-${componentName(source)}`} className={styles.component}>
            {componentName(source)}
          </h2>
          <ul className={styles.list}>
            {group.map(({ fixture }) => (
              <li key={fixture.id} className={styles.item}>
                <TextLink size="meta" arrow="up-right" href={`?gallery=${encodeURIComponent(fixture.id)}`}>
                  {fixture.id}
                </TextLink>
                <p className={styles.caption}>
                  {fixture.title} · {fixture.role} · {fixture.surface}
                </p>
                <div className={styles.preview} data-tone={fixture.surface} data-fotohavn-role={fixture.role}>
                  {fixture.render()}
                </div>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </main>
  );
}

function GalleryNotFound({ selection }: { readonly selection: string }) {
  return (
    <main className={styles.index}>
      <h1 className={styles.title}>No fixture named “{selection}”</h1>
      <TextLink href="?gallery" arrow="left">
        ALL FIXTURES
      </TextLink>
    </main>
  );
}

/**
 * The fixture gallery (development and E2E builds only): `?gallery` lists and renders every component fixture,
 * `?gallery=<id>` renders one alone in the full viewport, and `?gallery=manifest` publishes the manifest.
 */
export function Gallery({ entries, selection }: { readonly entries: readonly GalleryEntry[]; readonly selection: string }) {
  const [entry] = useState(() => entries.find(({ fixture }) => fixture.id === selection));
  if (selection === manifestSelection) {
    return <GalleryManifestView entries={entries} />;
  }
  if (selection === "") {
    return <GalleryIndex entries={entries} />;
  }
  return entry === undefined ? <GalleryNotFound selection={selection} /> : <GalleryFixture entry={entry} />;
}

/** Shown when discovery fails, so the gallery and the fixture lane fail with the reason. */
export function GalleryError({ error }: { readonly error: unknown }) {
  return (
    <main className={styles.index}>
      <h1 className={styles.title}>The fixture gallery couldn&apos;t load</h1>
      <p role="alert" data-fotohavn-gallery-error="true">
        {error instanceof Error ? error.message : String(error)}
      </p>
    </main>
  );
}
