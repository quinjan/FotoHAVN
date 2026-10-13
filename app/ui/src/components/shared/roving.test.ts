import { describe, expect, it } from "vitest";
import { arrowStep, nextEnabledIndex, tabStopValue } from "./roving.ts";

const items = [{ value: "a" }, { value: "b", disabled: true }, { value: "c" }];

describe("roving radio helpers", () => {
  it("puts the tab stop on the checked enabled item, else the first enabled one, else nowhere", () => {
    expect(tabStopValue(items, "c")).toBe("c");
    expect(tabStopValue(items, "b")).toBe("a");
    expect(tabStopValue(items, null)).toBe("a");
    expect(tabStopValue([{ value: "x", disabled: true }], "x")).toBeUndefined();
  });

  it("maps the arrow keys to a step, vertical-only on request", () => {
    expect(arrowStep("ArrowDown")).toBe(1);
    expect(arrowStep("ArrowRight")).toBe(1);
    expect(arrowStep("ArrowUp")).toBe(-1);
    expect(arrowStep("ArrowLeft")).toBe(-1);
    expect(arrowStep("ArrowRight", "vertical")).toBeUndefined();
    expect(arrowStep("ArrowLeft", "vertical")).toBeUndefined();
    expect(arrowStep("Enter")).toBeUndefined();
  });

  it("finds the next enabled item, skipping disabled ones, wrapping or stopping at the ends", () => {
    expect(nextEnabledIndex(items, 0, 1, false)).toBe(2);
    expect(nextEnabledIndex(items, 2, 1, false)).toBeUndefined();
    expect(nextEnabledIndex(items, 2, 1, true)).toBe(0);
    expect(nextEnabledIndex(items, 0, -1, true)).toBe(2);
    expect(nextEnabledIndex([{ value: "x", disabled: true }], 0, 1, true)).toBeUndefined();
  });
});
