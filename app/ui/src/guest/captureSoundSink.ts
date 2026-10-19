/**
 * The sounds the Guest Screen plays during a capture sequence (app/DESIGN.md, Sound). Playback is rendering: the brain
 * decides whether sounds are on and when a numeral or a Capture happens; this module only makes the noise.
 */
export interface CaptureSoundSink {
  /** Readies audio output. Call it from a guest's tap or key press, where the browser allows audio to start; it is safe to repeat. */
  prime(): void;
  /** One soft tick for a countdown numeral. */
  tick(): void;
  /** One soft shutter sound for a Capture. */
  shutter(): void;
}

/** Loudest point of any sound, as a fraction of full scale: soft enough to sit under a group's chatter without startling anyone. */
const tickPeak = 0.12;
const shutterPeak = 0.22;
const silence = 0.0001;

/**
 * Synthesizes the sounds with the Web Audio API, so the booth ships no audio files. The context is created on first use and
 * resumed by `prime()`. A sound that finds the context not running is skipped rather than queued, because a late tick would
 * mislead the guests who are counting on it; the countdown numerals and the flash already carry the same cues.
 */
export function createWebAudioSink(): CaptureSoundSink {
  let context: AudioContext | null = null;
  let noise: AudioBuffer | null = null;

  function open(): AudioContext | null {
    if (context !== null) {
      return context;
    }
    const AudioContextType = (globalThis as { AudioContext?: typeof AudioContext }).AudioContext;
    if (AudioContextType === undefined) {
      return null;
    }
    try {
      context = new AudioContextType({ latencyHint: "interactive" });
    } catch {
      return null;
    }
    return context;
  }

  function resume(audio: AudioContext): void {
    if (audio.state === "suspended") {
      audio.resume().catch(() => undefined);
    }
  }

  function running(): AudioContext | null {
    const audio = open();
    if (audio === null) {
      return null;
    }
    resume(audio);
    return audio.state === "running" ? audio : null;
  }

  function noiseOf(audio: AudioContext): AudioBuffer {
    if (noise === null) {
      const length = Math.ceil(audio.sampleRate * 0.05);
      noise = audio.createBuffer(1, length, audio.sampleRate);
      const samples = noise.getChannelData(0);
      for (let index = 0; index < length; index += 1) {
        samples[index] = Math.random() * 2 - 1;
      }
    }
    return noise;
  }

  function click(audio: AudioContext, at: number, peak: number): void {
    const source = audio.createBufferSource();
    source.buffer = noiseOf(audio);
    const band = audio.createBiquadFilter();
    band.type = "bandpass";
    band.frequency.value = 2400;
    band.Q.value = 0.9;
    const gain = audio.createGain();
    gain.gain.setValueAtTime(silence, at);
    gain.gain.linearRampToValueAtTime(peak, at + 0.002);
    gain.gain.exponentialRampToValueAtTime(silence, at + 0.045);
    source.connect(band);
    band.connect(gain);
    gain.connect(audio.destination);
    source.start(at);
  }

  return {
    prime() {
      const audio = open();
      if (audio !== null) {
        resume(audio);
      }
    },
    tick() {
      const audio = running();
      if (audio === null) {
        return;
      }
      const at = audio.currentTime;
      const oscillator = audio.createOscillator();
      oscillator.type = "sine";
      oscillator.frequency.value = 880;
      const gain = audio.createGain();
      gain.gain.setValueAtTime(silence, at);
      gain.gain.linearRampToValueAtTime(tickPeak, at + 0.006);
      gain.gain.exponentialRampToValueAtTime(silence, at + 0.09);
      oscillator.connect(gain);
      gain.connect(audio.destination);
      oscillator.start(at);
      oscillator.stop(at + 0.1);
    },
    shutter() {
      const audio = running();
      if (audio === null) {
        return;
      }
      const at = audio.currentTime;
      click(audio, at, shutterPeak);
      click(audio, at + 0.07, shutterPeak * 0.7);
    },
  };
}

let shared: CaptureSoundSink | null = null;

/** The one sink the Guest Screen uses, created on first use so a page that never plays a sound never opens an audio context. */
export function sharedCaptureSounds(): CaptureSoundSink {
  shared ??= createWebAudioSink();
  return shared;
}
