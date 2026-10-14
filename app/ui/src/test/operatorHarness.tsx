import { render, type RenderResult } from "@testing-library/react";
import type { ReactNode } from "react";
import { vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient, CommandResult } from "../brain/types.ts";
import type { FrameSource } from "../components/LivePreview/index.ts";
import { LocalNotice, OperatorNoticeProvider } from "../operator/OperatorCommands.tsx";

export type SendCommandMock = ReturnType<typeof vi.fn<BrainClient["sendCommand"]>>;

export interface StubClient extends BrainClient {
  readonly sendCommand: SendCommandMock;
}

const accepted: CommandResult = { status: "accepted" };

/** A brain client whose commands are a mock (accepted unless told otherwise), for operator surface tests. */
export function stubClient(options: { readonly result?: CommandResult; readonly previewUrl?: (key: string) => string; readonly frames?: FrameSource } = {}): StubClient {
  return {
    kind: "fixture",
    connect: vi.fn(),
    getState: () => ({ status: "connected", session: null, mismatch: null }),
    subscribe: () => () => undefined,
    sendCommand: vi.fn<BrainClient["sendCommand"]>().mockResolvedValue(options.result ?? accepted),
    sendTest: vi.fn(),
    dispose: vi.fn(),
    previewUrl: options.previewUrl ?? ((key) => `https://preview.fotohavn/${key}.png`),
    frames: options.frames,
  };
}

/** Renders `ui` where an operator surface lives: a brain client, and the local notice's provider and live region. */
export function renderOperator(ui: ReactNode, client: BrainClient = stubClient()): RenderResult & { readonly client: BrainClient } {
  const wrap = (children: ReactNode) => (
    <BrainClientContext value={client}>
      <OperatorNoticeProvider>
        <LocalNotice />
        {children}
      </OperatorNoticeProvider>
    </BrainClientContext>
  );
  const result = render(wrap(ui));
  return {
    ...result,
    rerender: (next: ReactNode) => {
      result.rerender(wrap(next));
    },
    client,
  };
}

/** The `[name, payload]` pairs a stub client was sent, in order. */
export function sentCommands(client: BrainClient): readonly (readonly [string, unknown])[] {
  return (client.sendCommand as SendCommandMock).mock.calls.map(([name, payload]) => [name, payload] as const);
}
