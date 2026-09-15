export type PhotoRect = { x: number; y: number; width: number; height: number };
export type TemplateId = "signature-strip" | "panorama-four" | "butter-gingham" | "polka-keepsake";
export type LayoutId = "long-strip-v1" | "story-strip-v1" | "pair-v1" | "contact-sheet-v1" | TemplateId;
export type FrameId = "ivory-v1" | "archive-v1" | "walnut-v1" | "gallery-v1";
export type LookId = "naturale-v1" | "classic-v1" | "monochrome-v1" | "sepia-v1" | "soft-monochrome-v1" | "soft-color-v1" | "warm-monochrome-v1";

export type Layout = { id: LayoutId; name: string; description: string; width: number; height: number; slots: PhotoRect[] };
const stripSlots = Array.from({ length: 4 }, (_, index) => ({ x: 60, y: 60 + index * 615, width: 780, height: 585 }));

export const layouts: Layout[] = [
  { id: "long-strip-v1", name: "Long Strip", description: "Four little moments", width: 900, height: 2700, slots: stripSlots },
  { id: "story-strip-v1", name: "Story Strip", description: "A story in three", width: 900, height: 2100, slots: stripSlots.slice(0, 3) },
  { id: "pair-v1", name: "Pair", description: "Better together", width: 900, height: 1500, slots: stripSlots.slice(0, 2) },
  { id: "contact-sheet-v1", name: "Contact Sheet", description: "A whole little world", width: 1800, height: 1500, slots: [
    { x: 60, y: 60, width: 820, height: 615 }, { x: 920, y: 60, width: 820, height: 615 },
    { x: 60, y: 715, width: 820, height: 615 }, { x: 920, y: 715, width: 820, height: 615 },
  ] },
];

export const frames: { id: FrameId; name: string; paper: string; ink: string; description: string }[] = [
  { id: "ivory-v1", name: "Ivory", paper: "#F3EBDD", ink: "#1E1A17", description: "Warm paper. A quiet signature." },
  { id: "archive-v1", name: "Archive", paper: "#FBF8F2", ink: "#1E1A17", description: "Clean lines. Kept for the future." },
  { id: "walnut-v1", name: "Walnut", paper: "#2D211B", ink: "#FBF8F2", description: "Deep wood tones. Fine brass detail." },
  { id: "gallery-v1", name: "Gallery", paper: "#E8DDCE", ink: "#1E1A17", description: "A paper mount for every moment." },
];

// One visual approximation per published Output sample, in carousel order.
// These are not recovered booth LUTs; lighting and backdrops also affect the samples.
export const looks: { id: LookId; name: string; description: string; sourceOutput: number }[] = [
  { id: "monochrome-v1", name: "Monochrome", description: "Black and white with deep blacks and strong contrast.", sourceOutput: 1 },
  { id: "naturale-v1", name: "Naturale", description: "Original colors. No filter.", sourceOutput: 2 },
  { id: "soft-monochrome-v1", name: "Soft Monochrome", description: "Black and white with softer contrast.", sourceOutput: 3 },
  { id: "soft-color-v1", name: "Soft Color", description: "Slightly muted colors and softer highlights.", sourceOutput: 4 },
  { id: "sepia-v1", name: "Sepia", description: "Soft brown tones with faded contrast.", sourceOutput: 5 },
  { id: "warm-monochrome-v1", name: "Warm Monochrome", description: "Black and white with a light warm tint.", sourceOutput: 6 },
  { id: "classic-v1", name: "Classic", description: "Warm colors with a golden tint.", sourceOutput: 7 },
];

export const blankPhotoColor = "#E5E0D7";
export type Template = Layout & { id: TemplateId; artwork?: string };
// Geometry measured from the published 600 × 1800 FOTOHAVN outputs.
export const templates: Template[] = [
  { id: "signature-strip", name: "Signature Strip", description: "Four edge-to-edge photographs with a signature across the middle.", width: 600, height: 1800, slots: Array.from({ length: 4 }, (_, i) => ({ x: 0, y: i * 450, width: 600, height: 450 })) },
  { id: "panorama-four", name: "Panorama Four", description: "Four portraits side by side, with a quiet corner signature.", width: 1800, height: 600, slots: Array.from({ length: 4 }, (_, i) => ({ x: i * 450, y: 0, width: 450, height: 600 })) },
  { id: "butter-gingham", name: "Butter Gingham", description: "Three photographs framed in soft yellow checks.", width: 600, height: 1800, artwork: "/images/online-templates/butter-gingham.png", slots: [32, 573, 1113].map((y) => ({ x: 27, y, width: 547, height: 522 })) },
  { id: "polka-keepsake", name: "Polka Keepsake", description: "Four photographs bordered by playful black dots.", width: 600, height: 1800, artwork: "/images/online-templates/polka-keepsake.png", slots: [39, 441, 846, 1251].map((y) => ({ x: 42, y, width: 513, height: 390 })) },
];
export const getTemplate = (id: LayoutId) => templates.find((item) => item.id === id);
export const getLayout = (id: LayoutId) => getTemplate(id) ?? layouts.find((item) => item.id === id)!;
export const getFrame = (id: FrameId) => frames.find((item) => item.id === id)!;

export function footerRegion(layout: Layout) {
  const top = Math.max(...layout.slots.map((slot) => slot.y + slot.height)) + 30;
  return { x: 60, y: top, width: layout.width - 120, height: layout.height - 45 - top };
}

/** Center-cover in source coordinates, using the selected photo window's aspect ratio. */
export function coverCrop(width: number, height: number, targetRatio = 4 / 3): PhotoRect {
  if (width <= 0 || height <= 0 || !Number.isFinite(width + height)) throw new Error("The photograph has no usable dimensions.");
  const cropWidth = Math.min(width, height * targetRatio);
  const cropHeight = cropWidth / targetRatio;
  return { x: (width - cropWidth) / 2, y: (height - cropHeight) / 2, width: cropWidth, height: cropHeight };
}

/** Operates on encoded sRGB, in this order: gains, luma/saturation, contrast, offsets, clamp.
 * Naturale never rewrites pixels. Alpha is untouched. The compositor alone owns this kernel. */
export function applyLook(pixels: Uint8ClampedArray, look: LookId) {
  if (look === "naturale-v1") return;
  for (let i = 0; i < pixels.length; i += 4) {
    let r = pixels[i] / 255, g = pixels[i + 1] / 255, b = pixels[i + 2] / 255;
    if (look === "classic-v1") { r *= 1.055; b *= 0.91; }
    const luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
    if (look === "monochrome-v1") {
      r = g = b = (luma - 0.5) * 1.1 + 0.51;
    } else if (look === "soft-monochrome-v1") {
      r = g = b = (luma - 0.5) * 0.94 + 0.52;
    } else if (look === "warm-monochrome-v1") {
      const tone = (luma - 0.5) * 1.02 + 0.505;
      r = tone + 0.02; g = tone + 0.015; b = tone - 0.012;
    } else if (look === "sepia-v1") {
      // The published Sepia samples are subtly brown monochrome, not faded color.
      // This is a visual approximation; original booth LUTs are not available.
      const tone = (luma - 0.5) * 0.94 + 0.52;
      r = tone + 0.045; g = tone + 0.025; b = tone - 0.025;
    } else {
      const saturation = look === "soft-color-v1" ? 0.88 : 0.94;
      const contrast = look === "soft-color-v1" ? 0.94 : 0.98;
      const lift = look === "soft-color-v1" ? 0.02 : 0.015;
      r = ((luma + (r - luma) * saturation) - 0.5) * contrast + 0.5 + lift;
      g = ((luma + (g - luma) * saturation) - 0.5) * contrast + 0.5 + lift;
      b = ((luma + (b - luma) * saturation) - 0.5) * contrast + 0.5 + lift;
    }
    pixels[i] = Math.round(Math.max(0, Math.min(1, r)) * 255);
    pixels[i + 1] = Math.round(Math.max(0, Math.min(1, g)) * 255);
    pixels[i + 2] = Math.round(Math.max(0, Math.min(1, b)) * 255);
  }
}

/** Preview-only quarter-turn: original leftmost slot becomes the bottom slot. */
export function previewLayout(layout: Layout) {
  if (layout.width <= layout.height) return layout;
  return { ...layout, width: layout.height, height: layout.width,
    slots: layout.slots.map((slot) => ({ x: slot.y, y: layout.width - slot.x - slot.width, width: slot.height, height: slot.width })) };
}

export function previewArtworkStyle(layout: Layout) {
  return layout.width > layout.height ? {
    width: `${layout.width / layout.height * 100}%`, height: `${layout.height / layout.width * 100}%`,
    transform: "rotate(-90deg) translateX(-100%)", transformOrigin: "0 0",
  } : { width: "100%", height: "100%" };
}
