import { afterEach, describe, expect, it, vi } from "vitest";
import { createWebAudioSink, sharedCaptureSounds } from "./captureSoundSink.ts";

type AudioState = "running" | "suspended";

interface Param {
  readonly setValueAtTime: ReturnType<typeof vi.fn>;
  readonly linearRampToValueAtTime: ReturnType<typeof vi.fn>;
  readonly exponentialRampToValueAtTime: ReturnType<typeof vi.fn>;
}

class FakeAudioContext {
  static instances: FakeAudioContext[] = [];
  static failToConstruct = false;
  static refuseResume = false;

  state: AudioState = "suspended";
  currentTime = 1;
  sampleRate = 1000;
  readonly destination = {};
  readonly resume = vi.fn(() => {
    if (FakeAudioContext.refuseResume) {
      return Promise.reject(new Error("not allowed"));
    }
    return Promise.resolve().then(() => {
      this.state = "running";
    });
  });
  readonly oscillators: { type: string; frequency: { value: number }; connect: ReturnType<typeof vi.fn>; start: ReturnType<typeof vi.fn>; stop: ReturnType<typeof vi.fn> }[] = [];
  readonly sources: { buffer: unknown; connect: ReturnType<typeof vi.fn>; start: ReturnType<typeof vi.fn> }[] = [];
  readonly gains: { gain: Param; connect: ReturnType<typeof vi.fn> }[] = [];
  readonly createdBuffers: { length: number; data: Float32Array }[] = [];

  constructor(readonly options?: unknown) {
    if (FakeAudioContext.failToConstruct) {
      throw new Error("no audio device");
    }
    FakeAudioContext.instances.push(this);
  }

  createOscillator() {
    const oscillator = { type: "", frequency: { value: 0 }, connect: vi.fn(), start: vi.fn(), stop: vi.fn() };
    this.oscillators.push(oscillator);
    return oscillator;
  }

  createGain() {
    const gain = {
      gain: { setValueAtTime: vi.fn(), linearRampToValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() },
      connect: vi.fn(),
    };
    this.gains.push(gain);
    return gain;
  }

  createBiquadFilter() {
    return { type: "", frequency: { value: 0 }, Q: { value: 0 }, connect: vi.fn() };
  }

  createBufferSource() {
    const source = { buffer: null as unknown, connect: vi.fn(), start: vi.fn() };
    this.sources.push(source);
    return source;
  }

  createBuffer(_channels: number, length: number) {
    const data = new Float32Array(length);
    const buffer = { length, getChannelData: () => data };
    this.createdBuffers.push({ length, data });
    return buffer;
  }
}

function install(): void {
  FakeAudioContext.instances = [];
  FakeAudioContext.failToConstruct = false;
  FakeAudioContext.refuseResume = false;
  vi.stubGlobal("AudioContext", FakeAudioContext);
}

async function settle(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

function only(): FakeAudioContext {
  const [context] = FakeAudioContext.instances;
  if (context === undefined) {
    throw new Error("No audio context was created");
  }
  return context;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("createWebAudioSink", () => {
  it("opens no audio context until it is primed or asked to play", () => {
    install();
    createWebAudioSink();

    expect(FakeAudioContext.instances).toHaveLength(0);
  });

  it("primes by creating one context and resuming it, however many times it is primed", async () => {
    install();
    const sink = createWebAudioSink();

    sink.prime();
    await settle();
    sink.prime();
    await settle();

    expect(FakeAudioContext.instances).toHaveLength(1);
    expect(only().options).toEqual({ latencyHint: "interactive" });
    expect(only().resume).toHaveBeenCalledTimes(1);
    expect(only().state).toBe("running");
  });

  it("plays a short soft sine tick once the context is running", async () => {
    install();
    const sink = createWebAudioSink();
    sink.prime();
    await settle();

    sink.tick();

    const context = only();
    const [oscillator] = context.oscillators;
    expect(context.oscillators).toHaveLength(1);
    expect(oscillator?.type).toBe("sine");
    expect(oscillator?.frequency.value).toBe(880);
    expect(oscillator?.start).toHaveBeenCalledWith(1);
    expect(oscillator?.stop).toHaveBeenCalledWith(expect.closeTo(1.1, 5));
    const [gain] = context.gains;
    expect(gain?.gain.linearRampToValueAtTime).toHaveBeenCalledWith(expect.any(Number), expect.any(Number));
    const peak = gain?.gain.linearRampToValueAtTime.mock.calls[0]?.[0] as number;
    expect(peak).toBeGreaterThan(0);
    expect(peak).toBeLessThanOrEqual(0.25);
  });

  it("plays a two-click shutter from one noise buffer that it keeps", async () => {
    install();
    const sink = createWebAudioSink();
    sink.prime();
    await settle();

    sink.shutter();
    sink.shutter();

    const context = only();
    expect(context.sources).toHaveLength(4);
    expect(context.createdBuffers).toHaveLength(1);
    expect(context.createdBuffers[0]?.length).toBe(50);
    expect(context.createdBuffers[0]?.data.some((sample) => sample !== 0)).toBe(true);
    expect(context.sources.slice(0, 2).map((source) => source.start.mock.calls[0]?.[0] as number)).toEqual([1, expect.closeTo(1.07, 5)]);
    const peaks = context.gains.map((gain) => gain.gain.linearRampToValueAtTime.mock.calls[0]?.[0] as number);
    expect(Math.max(...peaks)).toBeLessThanOrEqual(0.25);
  });

  it("skips a sound, and asks the context to resume, while the context is not running", async () => {
    install();
    const sink = createWebAudioSink();
    sink.prime();
    await settle();
    only().state = "suspended";

    sink.tick();
    sink.shutter();

    expect(only().oscillators).toHaveLength(0);
    expect(only().sources).toHaveLength(0);
    expect(only().resume).toHaveBeenCalledTimes(3);
  });

  it("tolerates a refused resume", async () => {
    install();
    FakeAudioContext.refuseResume = true;
    const sink = createWebAudioSink();

    sink.prime();
    sink.tick();
    await settle();

    expect(only().state).toBe("suspended");
    expect(only().oscillators).toHaveLength(0);
  });

  it("stays silent where there is no Web Audio, or no device to open", () => {
    vi.stubGlobal("AudioContext", undefined);
    const missing = createWebAudioSink();
    expect(() => {
      missing.prime();
      missing.tick();
      missing.shutter();
    }).not.toThrow();

    install();
    FakeAudioContext.failToConstruct = true;
    const broken = createWebAudioSink();
    expect(() => {
      broken.prime();
      broken.tick();
      broken.shutter();
    }).not.toThrow();
    expect(FakeAudioContext.instances).toHaveLength(0);
  });
});

describe("sharedCaptureSounds", () => {
  it("is one sink for the page", () => {
    expect(sharedCaptureSounds()).toBe(sharedCaptureSounds());
  });
});
