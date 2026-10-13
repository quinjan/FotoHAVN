import { describe, expect, it } from "vitest";
import { discoverFixtures, toManifest } from "./discover.ts";

const render = () => null;

describe("discoverFixtures", () => {
  it("collects fixtures sorted by source with their source paths", () => {
    const entries = discoverFixtures({
      "../components/B/B.fixtures.tsx": { fixtures: [{ id: "b-one", title: "B one", surface: "dark", role: "guest", render }] },
      "../components/A/A.fixtures.tsx": {
        fixtures: [
          { id: "a-two", title: "A two", surface: "light", role: "operator", layout: "fill", render },
          { id: "a-one", title: "A one", surface: "light", role: "operator", render },
        ],
      },
    });
    expect(entries.map(({ fixture, source }) => `${source}#${fixture.id}`)).toEqual([
      "components/A/A.fixtures.tsx#a-two",
      "components/A/A.fixtures.tsx#a-one",
      "components/B/B.fixtures.tsx#b-one",
    ]);
    expect(toManifest(entries).fixtures).toEqual([
      { id: "a-two", title: "A two", surface: "light", role: "operator", layout: "fill", source: "components/A/A.fixtures.tsx" },
      { id: "a-one", title: "A one", surface: "light", role: "operator", layout: "centered", source: "components/A/A.fixtures.tsx" },
      { id: "b-one", title: "B one", surface: "dark", role: "guest", layout: "centered", source: "components/B/B.fixtures.tsx" },
    ]);
  });

  const valid = { id: "x-one", title: "X", surface: "light", role: "guest", render };

  it.each([
    ["a module without fixtures", {}, "components/X/X.fixtures.tsx must export a non-empty `fixtures` array."],
    ["an empty fixtures array", { fixtures: [] }, "must export a non-empty `fixtures` array."],
    ["a non-object fixture", { fixtures: [42] }, "fixtures[0] is not an object."],
    ["a bad id", { fixtures: [{ ...valid, id: "X One" }] }, 'needs a kebab-case id, like "primary-action-disabled".'],
    ["a missing title", { fixtures: [{ ...valid, title: " " }] }, "(x-one) needs a title."],
    ["a bad surface", { fixtures: [{ ...valid, surface: "paper" }] }, 'needs a surface: "light" or "dark".'],
    ["a bad role", { fixtures: [{ ...valid, role: "both" }] }, 'needs a role: "guest" or "operator".'],
    ["a bad layout", { fixtures: [{ ...valid, layout: "wide" }] }, 'has an unknown layout; use "centered" or "fill".'],
    ["a missing render", { fixtures: [{ ...valid, render: "no" }] }, "(x-one) needs a render function."],
  ])("rejects %s", (_name, module, message) => {
    expect(() => discoverFixtures({ "../components/X/X.fixtures.tsx": module })).toThrow(message);
  });

  it("rejects an id used twice", () => {
    expect(() =>
      discoverFixtures({
        "../components/X/X.fixtures.tsx": { fixtures: [valid] },
        "../components/Y/Y.fixtures.tsx": { fixtures: [valid] },
      }),
    ).toThrow('Fixture id "x-one" is used by both components/X/X.fixtures.tsx and components/Y/Y.fixtures.tsx.');
  });
});
