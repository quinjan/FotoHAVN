import { act, render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { loadEntries, mountGallery } from "./mountGallery.tsx";

// Every real component fixture, through the same glob the gallery uses: each renders without throwing, and none
// shows more than one primary action (one per view, DESIGN.md). The fixture lane checks them rendered in Chromium.
const entries = loadEntries();

describe("component fixtures", () => {
  it("are discovered without errors", () => {
    expect(entries).toBeInstanceOf(Array);
    expect(Array.isArray(entries) ? entries.length : 0).toBeGreaterThan(0);
  });

  const list = Array.isArray(entries) ? entries : [];
  it.each(list.map((entry) => [entry.fixture.id, entry] as const))("%s renders with at most one primary action", (_id, entry) => {
    const { container } = render(<div data-tone={entry.fixture.surface}>{entry.fixture.render()}</div>);
    expect(container.firstElementChild?.childElementCount).toBeGreaterThan(0);
    expect(container.querySelectorAll('[data-fotohavn-action="primary"]').length).toBeLessThanOrEqual(1);
  });
});

describe("mountGallery", () => {
  it("renders the selected view into the root", async () => {
    const root = document.createElement("div");
    document.body.append(root);
    await act(async () => {
      mountGallery(root, "manifest");
      await Promise.resolve();
    });
    expect(document.title).toBe("FOTOHAVN fixture gallery");
    expect(root.querySelector("#fotohavn-gallery-manifest")?.textContent).toContain('"fixtures"');
    root.remove();
  });

  it("renders the discovery error instead of the gallery", async () => {
    const root = document.createElement("div");
    document.body.append(root);
    await act(async () => {
      mountGallery(root, "", { "../components/X/X.fixtures.tsx": {} });
      await Promise.resolve();
    });
    expect(root.querySelector('[data-fotohavn-gallery-error="true"]')?.textContent).toBe(
      "components/X/X.fixtures.tsx must export a non-empty `fixtures` array.",
    );
    root.remove();
  });

  it("turns a thrown non-error into an error", () => {
    const throwing = {
      get "../components/X/X.fixtures.tsx"(): never {
        // eslint-disable-next-line @typescript-eslint/only-throw-error -- the case under test
        throw "broken";
      },
    };
    const result = loadEntries(throwing);
    expect(result).toBeInstanceOf(Error);
    expect(result instanceof Error ? result.message : "").toBe("broken");
  });
});
