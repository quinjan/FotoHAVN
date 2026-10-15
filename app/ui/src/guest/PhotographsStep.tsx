import type { GuestCycleView, PhotographsView } from "../protocol/generated.ts";
import { ArrangeTab } from "./ArrangeTab.tsx";
import { CameraTab } from "./CameraTab.tsx";

/** The Photographs step (M8 UI-1): the Camera tab or the Arrange tab, as the brain says. */
export function PhotographsStep({ cycle, view }: { readonly cycle: GuestCycleView; readonly view: PhotographsView }) {
  return view.tab === "camera" ? <CameraTab cycle={cycle} view={view} /> : <ArrangeTab cycle={cycle} view={view} />;
}