/**
 * The catalog's names for the operator's diagnostics view. The brain's `CompositionView` carries ids; these are the
 * Template and Look names exactly as `FotoHavn.Catalog` has them (M8's screens will read the catalog from the brain).
 * `catalogNames.test.ts` pins the lists against the ids the brain fixtures use.
 */

export interface CatalogChoice {
  readonly id: string;
  readonly name: string;
}

export const templateChoices: readonly CatalogChoice[] = [
  { id: "signature-strip", name: "Signature Strip" },
  { id: "panorama-four", name: "Panorama Four" },
  { id: "butter-gingham", name: "Butter Gingham" },
  { id: "polka-keepsake", name: "Polka Keepsake" },
];

/** The Look a Photo Strip has when nobody picked one (the catalog's default: Naturale, no filter). */
export const defaultLookId = "naturale-v1";

export const lookChoices: readonly CatalogChoice[] = [
  { id: "monochrome-v1", name: "Monochrome" },
  { id: "naturale-v1", name: "Naturale" },
  { id: "soft-monochrome-v1", name: "Soft Monochrome" },
  { id: "soft-color-v1", name: "Soft Color" },
  { id: "sepia-v1", name: "Sepia" },
  { id: "warm-monochrome-v1", name: "Warm Monochrome" },
  { id: "classic-v1", name: "Classic" },
];

function nameOf(choices: readonly CatalogChoice[], id: string): string {
  return choices.find((choice) => choice.id === id)?.name ?? id;
}

/** The Template's name, or the id itself for one this list doesn't know. */
export function templateName(id: string): string {
  return nameOf(templateChoices, id);
}

/** The Look's name, or the id itself for one this list doesn't know. */
export function lookName(id: string): string {
  return nameOf(lookChoices, id);
}
