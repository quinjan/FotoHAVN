import { applyLook, blankPhotoColor, coverCrop, footerRegion, getFrame, getLayout, getTemplate, type FrameId, type LayoutId, type LookId } from "./presets";
import { withSiteBasePath } from "../../../site.config";
import { canvasBlob, checkAbort, decodeImage } from "./media";
import type { Photo } from "./session";

export type Composition = { layoutId: LayoutId; frameId: FrameId; lookId: LookId; photos: (Photo | null)[] };
export type FontFamilies = { display: string; sans: string };

export async function loadCompositionFonts(): Promise<FontFamilies> {
  const style = getComputedStyle(document.documentElement);
  const display = style.getPropertyValue("--font-cormorant").trim().split(",")[0] || '"Cormorant Garamond"';
  const sans = style.getPropertyValue("--font-manrope").trim().split(",")[0] || '"Manrope"';
  const loaded = await Promise.all([document.fonts.load(`500 64px ${display}`), document.fonts.load(`400 24px ${sans}`)]);
  if (loaded.some((fonts) => !fonts.length)) throw new Error("The frame lettering is still loading. Please try preparing your preview again.");
  return { display, sans };
}

/** The same full-resolution opaque PNG is used for selection previews and the final download. */
export async function renderComposition(composition: Composition, fonts: FontFamilies, signal: AbortSignal) {
  checkAbort(signal);
  const layout = getLayout(composition.layoutId), frame = getFrame(composition.frameId);
  const canvas = document.createElement("canvas"); canvas.width = layout.width; canvas.height = layout.height;
  const context = canvas.getContext("2d", { alpha: false });
  if (!context) throw new Error("The print preview couldn’t be prepared. Please try again.");
  context.fillStyle = frame.paper; context.fillRect(0, 0, layout.width, layout.height);
  const template = getTemplate(composition.layoutId);
  if (template?.artwork) {
    const artwork = await decodeImage(withSiteBasePath(template.artwork as `/${string}`), signal);
    context.drawImage(artwork, 0, 0, layout.width, layout.height);
  }

  for (let index = 0; index < layout.slots.length; index++) {
    checkAbort(signal);
    const rect = layout.slots[index];
    if (frame.id === "gallery-v1") {
      context.fillStyle = "#FBF8F2"; context.fillRect(rect.x - 12, rect.y - 12, rect.width + 24, rect.height + 24);
      context.strokeStyle = "#D1C4B4"; context.lineWidth = 2; context.strokeRect(rect.x - 13, rect.y - 13, rect.width + 26, rect.height + 26);
    }
    const photo = composition.photos[index];
    if (!photo) {
      context.fillStyle = template ? blankPhotoColor : frame.id === "walnut-v1" ? "#67584B" : "#DDD2C2";
      context.fillRect(rect.x, rect.y, rect.width, rect.height);
      // Empty photo windows are functional layout positions; numbers belong to the UI overlay.
      continue;
    }
    const image = await decodeImage(photo.url, signal);
    const crop = coverCrop(image.naturalWidth, image.naturalHeight, rect.width / rect.height);
    context.drawImage(image, crop.x, crop.y, crop.width, crop.height, rect.x, rect.y, rect.width, rect.height);
    if (composition.lookId !== "naturale-v1") {
      const pixels = context.getImageData(rect.x, rect.y, rect.width, rect.height);
      applyLook(pixels.data, composition.lookId);
      context.putImageData(pixels, rect.x, rect.y);
    }
    // Yield between photographs so a newer selection can cancel obsolete rendering.
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
  }
  checkAbort(signal);
  if (template) {
    if (!template.artwork) {
      // In the empty state, hairlines distinguish touching photo windows.
      // They are never burned into captured photographs.
      context.strokeStyle = "#EEEAE3"; context.lineWidth = 1;
      layout.slots.forEach((rect, index) => {
        if (index === 0 || composition.photos[index] || composition.photos[index - 1]) return;
        context.beginPath(); context.moveTo(rect.x, rect.y);
        context.lineTo(layout.width > layout.height ? rect.x : layout.width, layout.width > layout.height ? layout.height : rect.y); context.stroke();
      });
      context.fillStyle = "#FFFFFF"; context.textBaseline = "middle";
      const panorama = template.id === "panorama-four";
      context.font = `500 ${panorama ? 48 : 58}px ${fonts.display}`;
      context.textAlign = panorama ? "right" : "center";
      context.fillText("FOTOHAVN", panorama ? layout.width - 24 : layout.width / 2, panorama ? layout.height - 35 : layout.height / 2, panorama ? 300 : 390);
    }
    const blob = await canvasBlob(canvas); checkAbort(signal); return blob;
  }
  const footer = footerRegion(layout);
  if (footer.height < 60) throw new Error("This frame has insufficient room for its lettering.");
  const centerY = footer.y + footer.height / 2;
  const wordSize = Math.min(70, footer.height * 0.46);
  context.fillStyle = frame.ink; context.textBaseline = "middle";
  context.font = `500 ${wordSize}px ${fonts.display}`;
  if (frame.id === "archive-v1") {
    context.strokeStyle = "#D1C4B4"; context.lineWidth = 2;
    context.beginPath(); context.moveTo(footer.x, footer.y); context.lineTo(layout.width - footer.x, footer.y); context.stroke();
    context.textAlign = "left"; context.fillText("FOTOHAVN", footer.x, centerY, footer.width * 0.64);
    context.font = `400 ${Math.min(23, footer.height * 0.2)}px ${fonts.sans}`;
    context.textAlign = "right"; context.fillText("ONLINE", layout.width - footer.x, centerY, footer.width * 0.25);
  } else {
    context.textAlign = "center";
    if (frame.id === "walnut-v1") {
      context.strokeStyle = "#B39A78"; context.lineWidth = 2;
      context.beginPath(); context.moveTo(layout.width * 0.38, footer.y + 4); context.lineTo(layout.width * 0.62, footer.y + 4); context.stroke();
    }
    const hasLine = frame.id === "ivory-v1";
    context.fillText("FOTOHAVN", layout.width / 2, centerY - (hasLine ? footer.height * 0.11 : 0), footer.width);
    if (hasLine) {
      context.font = `400 ${Math.min(21, footer.height * 0.15)}px ${fonts.sans}`;
      context.fillText("A LITTLE MOMENT, YOURS.", layout.width / 2, centerY + footer.height * 0.28, footer.width);
    }
  }
  const blob = await canvasBlob(canvas); checkAbort(signal);
  return blob;
}
