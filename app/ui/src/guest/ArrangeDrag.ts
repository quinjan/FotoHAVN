/** How far (in pixels) a pointer travels before a press on a photograph becomes a drag instead of a tap. */
export const dragThreshold = 10;

/** The box a window occupies on the page; a `DOMRect` satisfies it, a test passes plain numbers. */
export interface WindowRect {
  readonly left: number;
  readonly top: number;
  readonly right: number;
  readonly bottom: number;
}

/** True once the pointer has moved far enough from where it went down to count as a drag. */
export function hasDragged(startX: number, startY: number, x: number, y: number): boolean {
  return Math.hypot(x - startX, y - startY) >= dragThreshold;
}

/**
 * The index of the window under a point, or null. The strip may be turned a quarter (Panorama Four), so this works on
 * the boxes the browser reports for each window, never on the Template's own geometry.
 */
export function windowAtPoint(rects: readonly (WindowRect | null)[], x: number, y: number): number | null {
  const index = rects.findIndex((rect) => rect !== null && x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom);
  return index < 0 ? null : index;
}
