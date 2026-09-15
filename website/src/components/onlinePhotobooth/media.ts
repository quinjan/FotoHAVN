import { coverCrop } from "./presets";
import type { Photo } from "./session";

let sourceSerial = 0;

export function abortError() { return new DOMException("Operation cancelled", "AbortError"); }
export function checkAbort(signal?: AbortSignal) { if (signal?.aborted) throw abortError(); }

export function waitFor(milliseconds: number, signal: AbortSignal) {
  return new Promise<void>((resolve, reject) => {
    checkAbort(signal);
    const abort = () => { clearTimeout(timer); reject(abortError()); };
    const timer = setTimeout(() => { signal.removeEventListener("abort", abort); resolve(); }, milliseconds);
    signal.addEventListener("abort", abort, { once: true });
  });
}

export function canvasBlob(canvas: HTMLCanvasElement) {
  return new Promise<Blob>((resolve, reject) => canvas.toBlob((blob) => blob ? resolve(blob) : reject(new Error("The photograph could not be prepared. Please try again.")), "image/png"));
}

export function decodeImage(url: string, signal?: AbortSignal) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    checkAbort(signal);
    const image = new Image();
    const cleanup = () => { image.onload = null; image.onerror = null; signal?.removeEventListener("abort", abort); };
    const abort = () => { cleanup(); image.src = ""; reject(abortError()); };
    image.onload = () => { cleanup(); resolve(image); };
    image.onerror = () => { cleanup(); reject(new Error("This image could not be opened. Try a JPEG, PNG, or WebP photograph.")); };
    signal?.addEventListener("abort", abort, { once: true });
    image.src = url;
  });
}

async function normalize(source: CanvasImageSource, width: number, height: number, kind: Photo["kind"], mirror: boolean, signal: AbortSignal): Promise<Photo> {
  checkAbort(signal);
  const crop = coverCrop(width, height);
  const canvas = document.createElement("canvas");
  canvas.width = Math.min(1640, Math.max(4, Math.floor(crop.width / 4) * 4));
  canvas.height = canvas.width * 3 / 4;
  const context = canvas.getContext("2d", { alpha: false });
  if (!context) throw new Error("Your browser could not prepare a photograph. Please try again.");
  context.fillStyle = "#FBF8F2"; context.fillRect(0, 0, canvas.width, canvas.height);
  if (mirror) { context.translate(canvas.width, 0); context.scale(-1, 1); }
  context.drawImage(source, crop.x, crop.y, crop.width, crop.height, 0, 0, canvas.width, canvas.height);
  const blob = await canvasBlob(canvas);
  checkAbort(signal);
  // Photo identity is local bookkeeping. Device-photo fallback also works on origins
  // where camera access and crypto.randomUUID are unavailable.
  const id = globalThis.crypto?.randomUUID?.() ?? `photograph-${Date.now()}-${++sourceSerial}`;
  return { id, blob, url: URL.createObjectURL(blob), width: canvas.width, height: canvas.height, kind };
}

export async function photoFromFile(file: File, signal: AbortSignal) {
  if (!/^image\/(jpeg|png|webp|avif|heic|heif)$/i.test(file.type)) throw new Error(`${file.name}: choose a JPEG, PNG, WebP, or supported phone photograph.`);
  if (file.size > 25 * 1024 * 1024) throw new Error(`${file.name}: choose a photograph smaller than 25 MB.`);
  const url = URL.createObjectURL(file);
  try {
    // Browser image decoding honors EXIF orientation. Normalization applies it once,
    // bounds retained sources to 1640 × 1230, and uses the viewfinder's center crop.
    const image = await decodeImage(url, signal);
    if (image.naturalWidth * image.naturalHeight > 64_000_000) throw new Error(`${file.name}: this image is too large. Choose a version below 64 megapixels.`);
    return await normalize(image, image.naturalWidth, image.naturalHeight, "device", false, signal);
  } finally { URL.revokeObjectURL(url); }
}

export async function photoFromVideo(video: HTMLVideoElement, mirror: boolean, signal: AbortSignal) {
  if (video.readyState < 2 || !video.videoWidth || !video.videoHeight) throw new Error("The camera is not ready yet. Resume it and try again.");
  return normalize(video, video.videoWidth, video.videoHeight, "camera", mirror, signal);
}

export function disposePhoto(photo: Photo) { URL.revokeObjectURL(photo.url); }
