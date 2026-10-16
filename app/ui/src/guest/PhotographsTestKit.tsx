import { render, type RenderResult } from "@testing-library/react";
import { vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient } from "../brain/types.ts";
import type { FrameSource } from "../components/LivePreview/index.ts";
import { guestCaptureFixtures } from "../fixtures/guestCaptureFixtures.ts";
import type {
  CommandPayloadMap,
  GuestCycleView,
  GuestPresentation,
  PhotographsView,
  TemplateView,
} from "../protocol/generated.ts";
import { PhotographsStep } from "./PhotographsStep.tsx";
import { TemplateStep } from "./TemplateStep.tsx";

/** What the Template and Photographs tests share: a stub brain client that records commands, and the hand-written fixtures' views. */

export interface SentCommand {
  readonly name: keyof CommandPayloadMap;
  readonly payload: unknown;
}

export interface StubClient {
  readonly client: BrainClient;
  readonly sent: SentCommand[];
}

export function stubClient(options: { readonly frames?: FrameSource; readonly previewUrl?: (key: string) => string } = {}): StubClient {
  const sent: SentCommand[] = [];
  const client: BrainClient = {
    kind: "fixture",
    ...options,
    connect: vi.fn(),
    getState: () => ({ status: "connected", session: null, mismatch: null }),
    subscribe: () => () => undefined,
    sendCommand: (name, payload) => {
      sent.push({ name, payload });
      return Promise.resolve({ status: "accepted" });
    },
    sendTest: vi.fn(),
    dispose: vi.fn(),
  };
  return { client, sent };
}

function presentation(name: string): GuestPresentation {
  const fixture = guestCaptureFixtures[name];
  if (fixture === undefined) {
    throw new Error(`No guest capture fixture ${name}`);
  }
  return fixture.presentation;
}

export function cycleOf(name: string): GuestCycleView {
  const cycle = presentation(name).guestCycle;
  if (cycle === null) {
    throw new Error(`Fixture ${name} has no cycle`);
  }
  return cycle;
}

export function photographsOf(name: string, changes: Partial<PhotographsView> = {}): PhotographsView {
  const view = presentation(name).photographs;
  if (view === null) {
    throw new Error(`Fixture ${name} has no photographs view`);
  }
  return { ...view, ...changes };
}

export function templateOf(name: string, changes: Partial<TemplateView> = {}): TemplateView {
  const view = presentation(name).template;
  if (view === null) {
    throw new Error(`Fixture ${name} has no template view`);
  }
  return { ...view, ...changes };
}

export function names(sent: readonly SentCommand[]): string[] {
  return sent.map((command) => command.name);
}

export function renderPhotographs(
  view: PhotographsView,
  stub: StubClient,
  cycle: GuestCycleView = cycleOf("guest-ui-camera-idle"),
): RenderResult & { readonly show: (next: PhotographsView) => void } {
  const ui = (next: PhotographsView) => (
    <BrainClientContext value={stub.client}>
      <PhotographsStep cycle={cycle} view={next} />
    </BrainClientContext>
  );
  const result = render(ui(view));
  return {
    ...result,
    show: (next) => {
      result.rerender(ui(next));
    },
  };
}

export function renderTemplate(view: TemplateView, stub: StubClient): RenderResult {
  return render(
    <BrainClientContext value={stub.client}>
      <TemplateStep cycle={cycleOf("guest-ui-template-signature")} view={view} />
    </BrainClientContext>,
  );
}
