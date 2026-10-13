import type { ComponentFixture } from "../components/fixture.ts";

/** A discovered fixture and the file it came from, relative to `src/`, e.g. `components/Eyebrow/Eyebrow.fixtures.tsx`. */
export interface GalleryEntry {
  readonly fixture: ComponentFixture;
  readonly source: string;
}

/** What `?gallery=manifest` publishes for the fixture lane: every fixture without its render function. */
export interface GalleryManifest {
  readonly fixtures: readonly {
    readonly id: string;
    readonly title: string;
    readonly surface: ComponentFixture["surface"];
    readonly role: ComponentFixture["role"];
    readonly layout: NonNullable<ComponentFixture["layout"]>;
    readonly source: string;
  }[];
}

const idPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const surfaces: readonly string[] = ["light", "dark"];
const roles: readonly string[] = ["guest", "operator"];
const layouts: readonly string[] = ["centered", "fill"];

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function checkFixture(value: unknown, source: string, index: number): ComponentFixture {
  const where = `${source} fixtures[${String(index)}]`;
  if (!isRecord(value)) {
    throw new Error(`${where} is not an object.`);
  }
  const { id, title, surface, role, layout, render } = value;
  if (typeof id !== "string" || !idPattern.test(id)) {
    throw new Error(`${where} needs a kebab-case id, like "primary-action-disabled".`);
  }
  if (typeof title !== "string" || title.trim() === "") {
    throw new Error(`${where} (${id}) needs a title.`);
  }
  if (typeof surface !== "string" || !surfaces.includes(surface)) {
    throw new Error(`${where} (${id}) needs a surface: "light" or "dark".`);
  }
  if (typeof role !== "string" || !roles.includes(role)) {
    throw new Error(`${where} (${id}) needs a role: "guest" or "operator".`);
  }
  if (layout !== undefined && (typeof layout !== "string" || !layouts.includes(layout))) {
    throw new Error(`${where} (${id}) has an unknown layout; use "centered" or "fill".`);
  }
  if (typeof render !== "function") {
    throw new Error(`${where} (${id}) needs a render function.`);
  }
  return value as unknown as ComponentFixture;
}

/**
 * Turns the modules matched by `import.meta.glob("../components/**\/*.fixtures.tsx", { eager: true })` into gallery
 * entries, sorted by source then declaration order. Throws on a module without a non-empty `fixtures` array, on an
 * invalid fixture, or on an id used twice, so a broken fixture fails the gallery and the fixture lane loudly.
 */
export function discoverFixtures(modules: Readonly<Record<string, unknown>>): GalleryEntry[] {
  const entries: GalleryEntry[] = [];
  const seen = new Map<string, string>();
  for (const path of Object.keys(modules).sort()) {
    const source = path.replace(/^(?:\.\.\/|\.\/|\/src\/)+/, "");
    const module = modules[path];
    const list = isRecord(module) ? module.fixtures : undefined;
    if (!Array.isArray(list) || list.length === 0) {
      throw new Error(`${source} must export a non-empty \`fixtures\` array.`);
    }
    list.forEach((value: unknown, index) => {
      const fixture = checkFixture(value, source, index);
      const earlier = seen.get(fixture.id);
      if (earlier !== undefined) {
        throw new Error(`Fixture id "${fixture.id}" is used by both ${earlier} and ${source}.`);
      }
      seen.set(fixture.id, source);
      entries.push({ fixture, source });
    });
  }
  return entries;
}

export function toManifest(entries: readonly GalleryEntry[]): GalleryManifest {
  return {
    fixtures: entries.map(({ fixture, source }) => ({
      id: fixture.id,
      title: fixture.title,
      surface: fixture.surface,
      role: fixture.role,
      layout: fixture.layout ?? "centered",
      source,
    })),
  };
}
