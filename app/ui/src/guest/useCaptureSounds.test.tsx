import { act, fireEvent, render, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient } from "../brain/types.ts";
import { fixtureSnapshots } from "../fixtures/fixtureLane.ts";
import type { CaptureSequenceView, GuestPresentation } from "../protocol/generated.ts";
import type { CaptureSoundSink } from "./captureSoundSink.ts";
import { GuestRoot } from "./GuestRoot.tsx";
import { useCaptureSounds } from "./useCaptureSounds.ts";

function fixture(name: string): GuestPresentation {
  const message = fixtureSnapshots()[name];
  if (message?.type !== "welcome" || message.role !== "guest") {
    throw new Error(`No guest fixture ${name}`);
  }
  return message.presentation;
}

const camera = fixture("guest-cycle-photographs-camera");
const arrange = fixture("guest-cycle-photographs-arrange");

function withSequence(base: GuestPresentation, sequence: Partial<CaptureSequenceView>): GuestPresentation {
  const photographs = base.photographs;
  if (photographs === null) {
    throw new Error("The fixture has no Photographs view");
  }
  return { ...base, photographs: { ...photographs, sequence: { ...photographs.sequence, ...sequence } } };
}

function fakeSink() {
  return { prime: vi.fn(), tick: vi.fn(), shutter: vi.fn() } satisfies CaptureSoundSink;
}

function mount(initial: GuestPresentation, sink: CaptureSoundSink) {
  const view = renderHook((presentation: GuestPresentation) => {
    useCaptureSounds(presentation, sink);
  }, { initialProps: initial });
  return (next: GuestPresentation) => {
    view.rerender(next);
  };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("useCaptureSounds", () => {
  it("is silent for what is already showing when it mounts", () => {
    const sink = fakeSink();

    mount(withSequence(camera, { running: true, countdownRemaining: 4, flashSequence: 2 }), sink);

    expect(sink.tick).not.toHaveBeenCalled();
    expect(sink.shutter).not.toHaveBeenCalled();
  });

  it("ticks once for each new countdown numeral and not for a repeated presentation", () => {
    const sink = fakeSink();
    const show = mount(withSequence(camera, { soundsOn: true }), sink);

    for (const numeral of [5, 5, 4, 3, 3, 2, 1]) {
      show(withSequence(camera, { running: true, countdownRemaining: numeral }));
    }

    expect(sink.tick).toHaveBeenCalledTimes(5);
    expect(sink.shutter).not.toHaveBeenCalled();
  });

  it("plays one shutter sound for each increment of the flash and ticks again after a pause", () => {
    const sink = fakeSink();
    const show = mount(withSequence(camera, { flashSequence: 0 }), sink);

    show(withSequence(camera, { running: true, countdownRemaining: 1, flashSequence: 0 }));
    show(withSequence(camera, { running: true, countdownRemaining: null, flashSequence: 1 }));
    show(withSequence(camera, { running: true, countdownRemaining: null, flashSequence: 1, pausing: true }));
    show(withSequence(camera, { running: true, countdownRemaining: 5, flashSequence: 1 }));

    expect(sink.shutter).toHaveBeenCalledTimes(1);
    expect(sink.tick).toHaveBeenCalledTimes(2);
  });

  it("plays nothing when the Event's Guest sounds are off", () => {
    const sink = fakeSink();
    const show = mount(withSequence(camera, { soundsOn: false }), sink);

    show(withSequence(camera, { soundsOn: false, running: true, countdownRemaining: 5 }));
    show(withSequence(camera, { soundsOn: false, running: true, countdownRemaining: null, flashSequence: 1 }));
    fireEvent.pointerDown(document.body);

    expect(sink.tick).not.toHaveBeenCalled();
    expect(sink.shutter).not.toHaveBeenCalled();
    expect(sink.prime).not.toHaveBeenCalled();
  });

  it("stays silent when the Photographs step returns, and for a step that has no sequence", () => {
    const sink = fakeSink();
    const show = mount(withSequence(camera, { flashSequence: 1 }), sink);

    show(fixture("guest-cycle-template"));
    show(withSequence(camera, { flashSequence: 4, running: true, countdownRemaining: 3 }));

    expect(sink.shutter).not.toHaveBeenCalled();
    expect(sink.tick).not.toHaveBeenCalled();
  });

  it("plays on the Arrange tab too, where a Retake runs", () => {
    const sink = fakeSink();
    const show = mount(arrange, sink);

    show(withSequence(arrange, { running: true, countdownRemaining: 5 }));

    expect(sink.tick).toHaveBeenCalledTimes(1);
  });

  it("primes the sink from a tap or a key press, and stops listening when sounds go off or the page leaves", () => {
    const sink = fakeSink();
    const view = renderHook((presentation: GuestPresentation) => {
      useCaptureSounds(presentation, sink);
    }, { initialProps: camera });

    fireEvent.pointerDown(document.body);
    fireEvent.keyDown(document.body, { key: "Enter" });
    expect(sink.prime).toHaveBeenCalledTimes(2);

    view.rerender(withSequence(camera, { soundsOn: false }));
    fireEvent.pointerDown(document.body);
    view.rerender(camera);
    view.unmount();
    fireEvent.pointerDown(document.body);

    expect(sink.prime).toHaveBeenCalledTimes(2);
  });
});

describe("GuestRoot sounds", () => {
  it("plays the countdown tick through Web Audio once a guest has tapped", async () => {
    const started: number[] = [];
    class FakeContext {
      state = "running";
      currentTime = 0;
      readonly destination = {};
      resume = () => Promise.resolve();
      createOscillator = () => ({ type: "", frequency: { value: 0 }, connect: vi.fn(), start: (at: number) => started.push(at), stop: vi.fn() });
      createGain = () => ({ gain: { setValueAtTime: vi.fn(), linearRampToValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() }, connect: vi.fn() });
    }
    vi.stubGlobal("AudioContext", FakeContext);
    const client: BrainClient = {
      kind: "fixture",
      connect: vi.fn(),
      getState: () => ({ status: "connected", session: null, mismatch: null }),
      subscribe: () => () => undefined,
      sendCommand: () => Promise.resolve({ status: "accepted" }),
      sendTest: vi.fn(),
      dispose: vi.fn(),
    };
    const ui = (presentation: GuestPresentation) => (
      <BrainClientContext value={client}>
        <GuestRoot presentation={presentation} reconnecting={false} />
      </BrainClientContext>
    );
    const view = render(ui(camera));

    fireEvent.pointerDown(document.body);
    await act(async () => {
      await Promise.resolve();
    });
    view.rerender(ui(withSequence(camera, { running: true, countdownRemaining: 5 })));

    expect(started).toHaveLength(1);
  });
});
