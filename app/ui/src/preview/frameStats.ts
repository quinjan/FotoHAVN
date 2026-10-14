/** What the page measured about the frames it received. `null` means not known yet. */
export interface FrameRate {
  /** Frames per second over the trailing window; 0 once the window holds none; null before the second frame. */
  readonly fps: number | null;
  /** Milliseconds since the newest frame arrived; null before the first frame. */
  readonly ageMs: number | null;
}

export const noFrameRate: FrameRate = { fps: null, ageMs: null };

/** The rate window in milliseconds; two seconds matches the host's `PreviewBackPressure.RateWindow`. */
export const frameRateWindowMs = 2_000;

/**
 * Frame arrival times, turned into a rate and an age. It holds only the arrivals inside the window, so a camera that
 * stops reads 0 fps and an age that keeps growing. The clock is injected so tests need no timers.
 */
export class FrameStats {
  private arrivals: number[] = [];
  private newest: number | null = null;

  constructor(
    private readonly now: () => number = () => performance.now(),
    private readonly windowMs: number = frameRateWindowMs,
  ) {}

  /** Notes that a frame arrived now. */
  record(): void {
    const at = this.now();
    this.newest = at;
    this.arrivals.push(at);
    this.prune(at);
  }

  read(): FrameRate {
    const at = this.now();
    this.prune(at);
    if (this.newest === null) {
      return noFrameRate;
    }
    return { fps: this.rate(), ageMs: Math.max(0, at - this.newest) };
  }

  private rate(): number | null {
    const count = this.arrivals.length;
    if (count === 0) {
      return 0;
    }
    // Arrivals are in order, so the span is the newest minus the oldest.
    const span = Math.max(...this.arrivals) - Math.min(...this.arrivals);
    return count < 2 || span <= 0 ? null : ((count - 1) * 1_000) / span;
  }

  private prune(at: number): void {
    const cutoff = at - this.windowMs;
    this.arrivals = this.arrivals.filter((arrival) => arrival >= cutoff);
  }
}

/** Whether two readings would show the same text, so the screen re-renders only when its words would change. */
export function sameFrameRate(a: FrameRate, b: FrameRate): boolean {
  const seconds = (rate: FrameRate) => (rate.ageMs === null ? null : Math.floor(rate.ageMs / 1_000));
  const rounded = (rate: FrameRate) => (rate.fps === null ? null : Math.round(rate.fps));
  return rounded(a) === rounded(b) && seconds(a) === seconds(b);
}
