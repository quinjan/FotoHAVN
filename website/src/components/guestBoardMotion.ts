export type Rect = { left: number; top: number; width: number; height: number };

export function liftTransform(from: Rect, to: Rect, tilt = 0) {
  const x = from.left + from.width / 2 - to.left - to.width / 2;
  const y = from.top + from.height / 2 - to.top - to.height / 2;
  const sx = Math.max(0.05, Math.min(1, from.width / Math.max(1, to.width)));
  const sy = Math.max(0.05, Math.min(1, from.height / Math.max(1, to.height)));
  return `translate(${x}px, ${y}px) scale(${sx}, ${sy}) rotate(${tilt}deg)`;
}

export function animateLift(element: HTMLElement, from: Rect | null, tilt: number, reduced: boolean) {
  if (reduced || !element.animate) return null;
  const transform = from ? liftTransform(from, element.getBoundingClientRect(), tilt) : "translateY(10px) scale(0.98)";
  return element.animate([
    { transform, opacity: 0.65 }, { transform: "none", opacity: 1 },
  ], { duration: from ? 620 : 280, easing: "cubic-bezier(.2,.8,.2,1)" });
}
