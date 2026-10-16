import { fixtureCapture, fixtureStrip } from "../components/shared/fixturePhotographs.ts";
import type { GuestPresentation, PhotographsView, ServerMessage } from "../protocol/generated.ts";

/**
 * The brain-generated Guest fixtures name Captures and the committed Photo Strip by their media URLs
 * (`https://media.fotohavn/...`), which no host serves in the fixture lane. The lane swaps each for a placeholder photograph at
 * the same proportion, so those fixtures draw like the real thing; every other field is untouched.
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
  const { keepsake } = presentation;
  return {
    ...presentation,
    photographs: presentation.photographs === null ? null : photographs(presentation.photographs),
    keepsake:
      keepsake !== null && isMedia(keepsake.stripUrl)
        ? { ...keepsake, stripUrl: fixtureStrip(keepsake.landscape ? "panorama-four" : "signature-strip", 4).src }
        : keepsake,
  };
}

export function withLaneMedia(message: ServerMessage): ServerMessage {
  return message.type === "welcome" && message.role === "guest" ? { ...message, presentation: guestPresentation(message.presentation) } : message;
}
