import { describe, expect, it } from "vitest";
import { dragThreshold, hasDragged, windowAtPoint } from "./ArrangeDrag.ts";

describe("arrange drag geometry", () => {
  it("counts a press as a drag only once it travels the threshold in any direction", () => {
    expect(hasDragged(10, 10, 10, 10)).toBe(false);
    expect(hasDragged(10, 10, 10 + dragThreshold - 1, 10)).toBe(false);
    expect(hasDragged(10, 10, 10 + dragThreshold, 10)).toBe(true);
    expect(hasDragged(10, 10, 10, 10 - dragThreshold)).toBe(true);
    expect(hasDragged(0, 0, 8, 8)).toBe(true);
  });

  const rects = [
    { left: 0, top: 0, right: 100, bottom: 50 },
    null,
    { left: 0, top: 60, right: 100, bottom: 110 },
  ];

  it("finds the window under a point from the boxes the browser reports", () => {
    expect(windowAtPoint(rects, 50, 25)).toBe(0);
    expect(windowAtPoint(rects, 50, 80)).toBe(2);
    expect(windowAtPoint(rects, 100, 110)).toBe(2);
  });

  it("finds none in the gaps, outside the strip, or over a window with no box", () => {
    expect(windowAtPoint(rects, 50, 55)).toBeNull();
    expect(windowAtPoint(rects, 150, 25)).toBeNull();
    expect(windowAtPoint(rects, 50, -1)).toBeNull();
    expect(windowAtPoint([], 1, 1)).toBeNull();
    expect(windowAtPoint([null], 1, 1)).toBeNull();
  });
});
