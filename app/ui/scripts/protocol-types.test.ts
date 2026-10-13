// @vitest-environment node
import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";
import { generateFromCommittedSchema, generatedPath } from "./protocol-types.ts";

describe("generated protocol types", () => {
  it("match the committed protocol schema", async () => {
    const committed = (await readFile(generatedPath, "utf8")).replaceAll("\r\n", "\n");
    const regenerated = await generateFromCommittedSchema();

    // On failure, run app/scripts/generate-protocol.ps1 and commit src/protocol/generated.ts.
    expect(committed).toBe(regenerated);
  }, 30_000);
});
