import { renderHook } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import { usePhotographsCommands } from "./PhotographsCommands.ts";
import { stubClient } from "./PhotographsTestKit.tsx";

describe("usePhotographsCommands", () => {
  it("sends each step command with exactly the payload the protocol names", () => {
    const stub = stubClient();
    const wrapper = ({ children }: { readonly children: ReactNode }) => <BrainClientContext value={stub.client}>{children}</BrainClientContext>;
    const { result } = renderHook(() => usePhotographsCommands(), { wrapper });
    const commands = result.current;

    commands.selectTemplate("panorama-four");
    commands.goToStep("filters");
    commands.showCameraTab();
    commands.showArrangeTab();
    commands.takePhotographs();
    commands.selectPosition(2);
    commands.beginRetake(1);
    commands.acceptRetake();
    commands.tryAgain();
    commands.keepOriginal();
    commands.beginMove(3);
    commands.moveTo(0);
    commands.cancelMove();
    commands.dragPlace(0, "capture-1", 6, 2);

    expect(stub.sent).toEqual([
      { name: "SelectTemplate", payload: { templateId: "panorama-four" } },
      { name: "GoToStep", payload: { step: "filters" } },
      { name: "ShowCameraTab", payload: {} },
      { name: "ShowArrangeTab", payload: {} },
      { name: "TakePhotographs", payload: {} },
      { name: "SelectPosition", payload: { index: 2 } },
      { name: "BeginRetake", payload: { index: 1 } },
      { name: "AcceptRetake", payload: {} },
      { name: "TryAgain", payload: {} },
      { name: "KeepOriginal", payload: {} },
      { name: "BeginMove", payload: { index: 3 } },
      { name: "MoveTo", payload: { index: 0 } },
      { name: "CancelMove", payload: {} },
      { name: "DragPlace", payload: { sourceIndex: 0, captureId: "capture-1", revision: 6, targetIndex: 2 } },
    ]);
  });

  it("keeps the same commands while the client stays the same", () => {
    const stub = stubClient();
    const wrapper = ({ children }: { readonly children: ReactNode }) => <BrainClientContext value={stub.client}>{children}</BrainClientContext>;
    const { result, rerender } = renderHook(() => usePhotographsCommands(), { wrapper });
    const first = result.current;

    rerender();

    expect(result.current).toBe(first);
  });
});
