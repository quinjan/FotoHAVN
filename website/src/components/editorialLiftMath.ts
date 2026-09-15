export const clampUnit = (value: number) => Math.max(0, Math.min(1, value));
export function smoothRange(value: number, from: number, to: number) {
  const progress = clampUnit((value - from) / (to - from));
  return progress * progress * (3 - 2 * progress);
}
export function liftAt(progress: number) {
  return {
    heroScale: 1 - 0.035 * smoothRange(progress, 0, 1),
    copy: 1 - smoothRange(progress, 0, 0.85),
    paperOffset: 1 - smoothRange(progress, 0, 1),
    details: smoothRange(progress, 0.55, 0.95),
  };
}
export function canOverlap(width: number, height: number, heroHeight: number, reduced: boolean) {
  // Small screens animate too. Only abandon pinning when enlarged content or a
  // short landscape viewport would hide controls before the reader reaches them.
  return !reduced && width > 0 && heroHeight >= height * 0.6 && heroHeight <= height * 1.08;
}

export function liftTravel(width: number, height: number) {
  return width < 768
    ? Math.min(300, Math.max(180, height * 0.32))
    : width <= 1100 && height > width
      ? Math.min(420, Math.max(280, height * 0.35))
      : Math.min(540, Math.max(320, height * 0.55));
}
