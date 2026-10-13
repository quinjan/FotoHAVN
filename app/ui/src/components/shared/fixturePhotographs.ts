/**
 * Fixture-only placeholder photographs: deterministic SVG data URLs standing in for Photo Strips and Captures, so
 * fixtures need no network and no binary files. Import from `*.fixtures.tsx` files (and tests) only.
 *
 * The colors here are photograph content, not interface color, so they don't come from the tokens. They are written as
 * `rgb()` inside SVG markup; the paper is Warm Ivory, as the compositor fills it (implementation plan, Compositor).
 */

/** A placeholder image and its native size in pixels. */
export interface FixtureImage {
  readonly src: string;
  readonly width: number;
  readonly height: number;
}

/** Warm, quiet scene tones that vary with the index: [background top, background bottom, figures]. */
function sceneAt(index: number): readonly [string, string, string] {
  const a = (index * 13) % 40;
  const b = (index * 23) % 44;
  const c = (index * 17) % 36;
  const warm = (shift: number, red: number, green: number, blue: number) =>
    `rgb(${String(Math.round(red + shift))} ${String(Math.round(green + shift * 0.8))} ${String(Math.round(blue + shift * 0.6))})`;
  return [warm(a, 196, 182, 160), warm(b, 112, 92, 76), warm(c, 48, 38, 32)];
}

/** One photograph: a soft graded backdrop, a window of light, and two figures, drawn in a w × h box at x, y. */
function photograph(x: number, y: number, w: number, h: number, index: number): string {
  const [top, bottom, figure] = sceneAt(index);
  const id = `g${String(index)}-${String(Math.round(x))}-${String(Math.round(y))}`;
  const unit = Math.min(w, h);
  const headRadius = unit * 0.11;
  const shift = ((index % 3) - 1) * w * 0.06;
  const figures = [0.36, 0.64]
    .map((at, n) => {
      const cx = x + w * at + shift;
      const cy = y + h * (0.5 + n * 0.04);
      return (
        `<circle cx="${cx.toFixed(1)}" cy="${cy.toFixed(1)}" r="${headRadius.toFixed(1)}" fill="${figure}"/>` +
        `<rect x="${(cx - headRadius * 1.9).toFixed(1)}" y="${(cy + headRadius * 1.3).toFixed(1)}" width="${(headRadius * 3.8).toFixed(1)}" ` +
        `height="${(h * 0.5).toFixed(1)}" rx="${(headRadius * 1.4).toFixed(1)}" fill="${figure}"/>`
      );
    })
    .join("");
  return (
    `<defs><linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${top}"/>` +
    `<stop offset="1" stop-color="${bottom}"/></linearGradient>` +
    `<clipPath id="c${id}"><rect x="${String(x)}" y="${String(y)}" width="${String(w)}" height="${String(h)}"/></clipPath></defs>` +
    `<g clip-path="url(#c${id})"><rect x="${String(x)}" y="${String(y)}" width="${String(w)}" height="${String(h)}" fill="url(#${id})"/>` +
    `<ellipse cx="${(x + w * 0.78).toFixed(1)}" cy="${(y + h * 0.22).toFixed(1)}" rx="${(w * 0.24).toFixed(1)}" ry="${(h * 0.3).toFixed(1)}" ` +
    `fill="rgb(255 250 240)" opacity="0.28"/>${figures}</g>`
  );
}

function svg(width: number, height: number, body: string): FixtureImage {
  const markup = `<svg xmlns="http://www.w3.org/2000/svg" width="${String(width)}" height="${String(height)}" viewBox="0 0 ${String(width)} ${String(height)}">${body}</svg>`;
  return { src: `data:image/svg+xml,${encodeURIComponent(markup)}`, width, height };
}

const paper = (width: number, height: number) => `<rect width="${String(width)}" height="${String(height)}" fill="rgb(243 235 221)"/>`;

/** The catalog's Templates, by id, as placeholder Photo Strips (implementation plan, Catalog). */
export type FixtureTemplate = "signature-strip" | "panorama-four" | "butter-gingham" | "polka-keepsake";

/**
 * A placeholder Photo Strip at the Template's native size; `seed` varies the scenes so neighbouring strips differ.
 */
export function fixtureStrip(template: FixtureTemplate, seed = 0): FixtureImage {
  switch (template) {
    case "signature-strip":
      return svg(600, 1800, [0, 1, 2, 3].map((n) => photograph(0, n * 450, 600, 450, seed + n)).join(""));
    case "panorama-four":
      return svg(1800, 600, [0, 1, 2, 3].map((n) => photograph(n * 450, 0, 450, 600, seed + n)).join(""));
    case "butter-gingham":
      return svg(
        600,
        1800,
        paper(600, 1800) +
          `<rect width="600" height="1800" fill="rgb(240 222 160)" opacity="0.55"/>` +
          [0, 1, 2].map((n) => photograph(26.5, 96 + n * 562, 547, 522, seed + n)).join(""),
      );
    case "polka-keepsake":
      return svg(600, 1800, paper(600, 1800) + [0, 1, 2, 3].map((n) => photograph(43.5, 80 + n * 420, 513, 390, seed + n)).join(""));
  }
}

/** A placeholder Capture: a full camera frame, 1920 × 1080 by default. */
export function fixtureCapture(seed = 0, width = 1920, height = 1080): FixtureImage {
  return svg(width, height, photograph(0, 0, width, height, seed));
}
