import type { TemplateWindowView } from "../protocol/generated.ts";

/** What a Template draws from: the brain's geometry. */
export interface TemplateGeometry {
  readonly width: number;
  readonly height: number;
  readonly windows: readonly TemplateWindowView[];
  readonly blankPreviewKey: string | null;
}

/**
 * The image of a blank Template: the composed preview when the brain has one (`blankPreviewKey`, served by the host),
 * otherwise a quiet schematic of its windows drawn from the geometry, so the print is never empty while the brain
 * composes it. The schematic is a placeholder, never a photograph and never a media URL.
 */
export function templateImageSource(
  template: TemplateGeometry,
  previewUrl: ((key: string) => string) | undefined,
): string {
  return template.blankPreviewKey !== null && previewUrl !== undefined
    ? previewUrl(template.blankPreviewKey)
    : templateSchematicUrl(template);
}

/** The Template as a Cream Paper sheet with its windows outlined, as an SVG data URL at the Template's native size. */
export function templateSchematicUrl(template: TemplateGeometry): string {
  const windows = template.windows
    .map(
      (window) =>
        `<rect x="${String(window.x)}" y="${String(window.y)}" width="${String(window.width)}" height="${String(window.height)}" ` +
        `fill="rgb(232 221 206)" stroke="rgb(186 172 154)" stroke-width="${String(Math.max(2, Math.round(template.width / 300)))}"/>`,
    )
    .join("");
  const markup =
    `<svg xmlns="http://www.w3.org/2000/svg" width="${String(template.width)}" height="${String(template.height)}" ` +
    `viewBox="0 0 ${String(template.width)} ${String(template.height)}">` +
    `<rect width="${String(template.width)}" height="${String(template.height)}" fill="rgb(243 235 221)"/>${windows}</svg>`;
  return `data:image/svg+xml,${encodeURIComponent(markup)}`;
}
