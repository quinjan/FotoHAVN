import { describe, expect, it } from "vitest";
import { secondsText } from "./secondsText.ts";

describe("secondsText", () => {
  it("uses the singular for exactly one second", () => {
    expect(secondsText(1)).toBe("1 second");
  });

  it.each([0, 2, 30])("uses the plural for %s", (n) => {
    expect(secondsText(n)).toBe(`${String(n)} seconds`);
  });
});
