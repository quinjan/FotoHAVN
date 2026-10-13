// @vitest-environment node
import { readdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

// The folder convention in src/components/README.md, so parallel component work stays uniform: one folder per
// component, named in PascalCase, holding exactly its five files (plus any extra *.module.css or helpers it needs).
const componentsDir = resolve(dirname(fileURLToPath(import.meta.url)), "../src/components");
const notComponents = new Set(["shared"]);

const folders = (await readdir(componentsDir, { withFileTypes: true }))
  .filter((entry) => entry.isDirectory() && !notComponents.has(entry.name))
  .map((entry) => entry.name)
  .sort();

describe("component folders", () => {
  it("exist", () => {
    expect(folders.length).toBeGreaterThan(0);
  });

  it.each(folders)("%s follows the convention", async (name) => {
    expect(name).toMatch(/^[A-Z][A-Za-z0-9]*$/);
    const files = await readdir(`${componentsDir}/${name}`);
    expect(files).toEqual(
      expect.arrayContaining([
        `${name}.tsx`,
        `${name}.module.css`,
        `${name}.test.tsx`,
        `${name}.fixtures.tsx`,
        "index.ts",
      ]),
    );
  });
});
