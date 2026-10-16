import type { GalleryImage, GuestCycleOutcome, OperatorGuestCycle } from "../../components/GuestCycleList/index.ts";
import type { GalleryItemView, GalleryOutcome, GuestCycleDetailView } from "../../protocol/generated.ts";
import { formatGuestCycleTime } from "../format.ts";
import { captureAlt, stripAlt } from "./galleryCopy.ts";

/** The wire's outcomes (camelCase) in the components' words (kebab-case); the labels come from the component. */
const outcomes: Readonly<Record<GalleryOutcome, GuestCycleOutcome>> = {
  completed: "completed",
  startedOver: "started-over",
  endedByOperator: "ended-by-operator",
  appClosed: "app-closed",
  unrecoverable: "unrecoverable",
};

function image(src: string, alt: string, width: number, height: number): GalleryImage {
  return { src, alt, width, height };
}

/** One list item of the Event Gallery as the component takes it. Thumbnails are the brain's URLs, used as they are. */
export function operatorItem(item: GalleryItemView): OperatorGuestCycle {
  return {
    id: item.guestCycleId,
    time: formatGuestCycleTime(item.startedAt),
    outcome: outcomes[item.outcome],
    ...(item.templateName === null ? {} : { templateName: item.templateName }),
    ...(item.lookName === null ? {} : { lookName: item.lookName }),
    strip: item.strip === null ? null : image(item.strip.thumbnailUrl, stripAlt, item.strip.width, item.strip.height),
    captures: item.captures.map((capture) => image(capture.thumbnailUrl, captureAlt, capture.width, capture.height)),
    ...(item.unavailable ? { unavailable: true } : {}),
  };
}

/** The Guest Cycle detail as the component takes it: the full files, the Captures in the order the brain sends. */
export function operatorDetail(view: GuestCycleDetailView): OperatorGuestCycle {
  return {
    id: view.guestCycleId,
    time: formatGuestCycleTime(view.startedAt),
    outcome: outcomes[view.outcome],
    ...(view.templateName === null ? {} : { templateName: view.templateName }),
    ...(view.lookName === null ? {} : { lookName: view.lookName }),
    strip: view.strip === null ? null : image(view.strip.imageUrl, stripAlt, view.strip.width, view.strip.height),
    captures: view.captures.map((capture) => image(capture.imageUrl, captureAlt, capture.width, capture.height)),
    ...(view.unavailable ? { unavailable: true } : {}),
  };
}
