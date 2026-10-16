import type { GalleryImage, GuestStrip } from "../components/GuestCycleList/index.ts";
import type { GuestStripDetailView, GuestStripView } from "../protocol/generated.ts";

/** One strip of the guest row as the component takes it: the image, its proportion, its alternative text, and an opaque id. */
export function guestStrip(strip: GuestStripView): GuestStrip {
  return { id: strip.guestCycleId, src: strip.thumbnailUrl, alt: strip.alt, width: strip.width, height: strip.height };
}

/** The strip detail as the component takes it: the full strip and its arranged Captures, images and alternative text only. */
export function guestDetail(view: GuestStripDetailView): { readonly strip: GalleryImage; readonly captures: readonly GalleryImage[] } {
  return {
    strip: { src: view.strip.imageUrl, alt: view.strip.alt, width: view.strip.width, height: view.strip.height },
    captures: view.captures.map((capture) => ({ src: capture.imageUrl, alt: capture.alt, width: capture.width, height: capture.height })),
  };
}
