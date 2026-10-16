import { useMemo } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import type { GuestStep } from "../protocol/generated.ts";

/** Every command the Template and Photographs steps send. The brain decides each; none of them answers the guest with an error. */
export interface PhotographsCommands {
  readonly selectTemplate: (templateId: string) => void;
  readonly goToStep: (step: GuestStep) => void;
  readonly showCameraTab: () => void;
  readonly showArrangeTab: () => void;
  readonly takePhotographs: () => void;
  readonly stopCapture: () => void;
  readonly selectPosition: (index: number) => void;
  readonly beginRetake: (index: number) => void;
  readonly acceptRetake: () => void;
  readonly tryAgain: () => void;
  readonly keepOriginal: () => void;
  readonly beginMove: (index: number) => void;
  readonly moveTo: (index: number) => void;
  readonly cancelMove: () => void;
  readonly dragPlace: (sourceIndex: number, captureId: string, revision: number, targetIndex: number) => void;
}

/** The step commands, bound to the page's brain client. A command's result is the presentation that follows, never read here. */
export function usePhotographsCommands(): PhotographsCommands {
  const client = useBrainClient();
  return useMemo<PhotographsCommands>(
    () => ({
      selectTemplate: (templateId) => void client.sendCommand("SelectTemplate", { templateId }),
      goToStep: (step) => void client.sendCommand("GoToStep", { step }),
      showCameraTab: () => void client.sendCommand("ShowCameraTab", {}),
      showArrangeTab: () => void client.sendCommand("ShowArrangeTab", {}),
      takePhotographs: () => void client.sendCommand("TakePhotographs", {}),
      stopCapture: () => void client.sendCommand("StopCapture", {}),
      selectPosition: (index) => void client.sendCommand("SelectPosition", { index }),
      beginRetake: (index) => void client.sendCommand("BeginRetake", { index }),
      acceptRetake: () => void client.sendCommand("AcceptRetake", {}),
      tryAgain: () => void client.sendCommand("TryAgain", {}),
      keepOriginal: () => void client.sendCommand("KeepOriginal", {}),
      beginMove: (index) => void client.sendCommand("BeginMove", { index }),
      moveTo: (index) => void client.sendCommand("MoveTo", { index }),
      cancelMove: () => void client.sendCommand("CancelMove", {}),
      dragPlace: (sourceIndex, captureId, revision, targetIndex) =>
        void client.sendCommand("DragPlace", { sourceIndex, captureId, revision, targetIndex }),
    }),
    [client],
  );
}
