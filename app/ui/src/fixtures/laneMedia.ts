import { fixtureCapture, fixtureStrip } from "../components/shared/fixturePhotographs.ts";
import type { GuestPresentation, OperatorPresentation, PhotographsView, ServerMessage } from "../protocol/generated.ts";

/**
 * The brain-generated fixtures name Captures, Photo Strips, and gallery thumbnails by their media URLs
 * (`https://media.fotohavn/...`), which no host serves in the fixture lane. The lane swaps each for a placeholder photograph at
 * the same proportion, so those fixtures draw like the real thing; every other field is untouched. It covers the Guest Cycle's
 * Captures, Retake candidate, and Keepsake, and the Event Gallery's views on both sides (the operator list and detail, the
 * guest gallery and strip detail).
 */

const mediaHost = "https://media.fotohavn/";

function isMedia(url: string | null): url is string {
  return url?.startsWith(mediaHost) === true;
}

function capture(view: PhotographsView, index: number, seed: number): string {
  const rect = view.template.windows[index];
  const ratio = rect === undefined ? 4 / 3 : rect.width / rect.height;
  return fixtureCapture(seed, Math.round(ratio * 720), 720).src;
}

function placeholderStrip(landscape: boolean, seed: number): string {
  return fixtureStrip(landscape ? "panorama-four" : "signature-strip", seed).src;
}

/** A placeholder photograph at the proportion of the Capture it stands for, 720 px high. */
function placeholderCapture(width: number, height: number, seed: number): string {
  return fixtureCapture(seed, Math.round((width / height) * 720), 720).src;
}

function photographs(view: PhotographsView): PhotographsView {
  return {
    ...view,
    windows: view.windows.map((window) => (isMedia(window.imageUrl) ? { ...window, imageUrl: capture(view, window.index, window.index) } : window)),
    retakeCandidate:
      view.retakeCandidate !== null && isMedia(view.retakeCandidate.imageUrl)
        ? { ...view.retakeCandidate, imageUrl: capture(view, view.retakeCandidate.index, view.retakeCandidate.index + 11) }
        : view.retakeCandidate,
  };
}

function guestPresentation(presentation: GuestPresentation): GuestPresentation {
  const { keepsake, guestGallery, guestStripDetail } = presentation;
  return {
    ...presentation,
    photographs: presentation.photographs === null ? null : photographs(presentation.photographs),
    keepsake:
      keepsake !== null && isMedia(keepsake.stripUrl)
        ? { ...keepsake, stripUrl: fixtureStrip(keepsake.landscape ? "panorama-four" : "signature-strip", 4).src }
        : keepsake,
    guestGallery:
      guestGallery === null
        ? null
        : {
            ...guestGallery,
            strips: guestGallery.strips.map((strip, index) =>
              isMedia(strip.thumbnailUrl) ? { ...strip, thumbnailUrl: placeholderStrip(strip.landscape, index) } : strip,
            ),
          },
    guestStripDetail:
      guestStripDetail === null
        ? null
        : {
            strip: isMedia(guestStripDetail.strip.imageUrl)
              ? { ...guestStripDetail.strip, imageUrl: placeholderStrip(guestStripDetail.strip.landscape, 4) }
              : guestStripDetail.strip,
            captures: guestStripDetail.captures.map((photograph, index) =>
              isMedia(photograph.imageUrl) ? { ...photograph, imageUrl: placeholderCapture(photograph.width, photograph.height, index) } : photograph,
            ),
          },
  };
}

/** The Event Gallery's thumbnails and files, on the operator's side: each media URL becomes a placeholder at the item's own proportion. */
function operatorPresentation(presentation: OperatorPresentation): OperatorPresentation {
  const { eventGallery, guestCycleDetail } = presentation;
  return {
    ...presentation,
    eventGallery:
      eventGallery === null
        ? null
        : {
            ...eventGallery,
            items: eventGallery.items.map((item, itemIndex) => ({
              ...item,
              strip:
                item.strip !== null && isMedia(item.strip.thumbnailUrl)
                  ? { ...item.strip, thumbnailUrl: placeholderStrip(item.strip.landscape, itemIndex) }
                  : item.strip,
              captures: item.captures.map((photograph, index) =>
                isMedia(photograph.thumbnailUrl)
                  ? { ...photograph, thumbnailUrl: placeholderCapture(photograph.width, photograph.height, itemIndex * 4 + index) }
                  : photograph,
              ),
            })),
          },
    guestCycleDetail:
      guestCycleDetail === null
        ? null
        : {
            ...guestCycleDetail,
            strip:
              guestCycleDetail.strip !== null && isMedia(guestCycleDetail.strip.imageUrl)
                ? { ...guestCycleDetail.strip, imageUrl: placeholderStrip(guestCycleDetail.strip.landscape, 4) }
                : guestCycleDetail.strip,
            captures: guestCycleDetail.captures.map((photograph, index) =>
              isMedia(photograph.imageUrl) ? { ...photograph, imageUrl: placeholderCapture(photograph.width, photograph.height, index) } : photograph,
            ),
          },
  };
}

export function withLaneMedia(message: ServerMessage): ServerMessage {
  if (message.type !== "welcome") {
    return message;
  }
  return message.role === "guest"
    ? { ...message, presentation: guestPresentation(message.presentation) }
    : { ...message, presentation: operatorPresentation(message.presentation) };
}
