import { vi } from "vitest";
import type { ImageDataFactory } from "../preview/frameSupport.ts";

/** What jsdom lacks: an `ImageData` holding its size and pixels. */
export class FakeImageData {
  readonly data: Uint8ClampedArray<ArrayBuffer>;
  readonly width: number;
  readonly height: number;

  constructor(...args: [width: number, height: number] | [data: Uint8ClampedArray<ArrayBuffer>, width: number, height?: number]) {
    if (typeof args[0] === "number") {
      this.width = args[0];
      this.height = args[1];
      this.data = new Uint8ClampedArray(this.width * this.height * 4);
    } else {
      this.data = args[0];
      this.width = args[1];
      this.height = args[2] ?? this.data.length / 4 / this.width;
    }
  }
}

/** Makes `ImageData` the fake, for code that builds it from the browser global. */
export function stubImageData(): void {
  vi.stubGlobal("ImageData", FakeImageData);
}

/** A factory that records what it built. */
export function fakeImages(): ImageDataFactory & { readonly created: FakeImageData[]; readonly wrapped: FakeImageData[] } {
  const created: FakeImageData[] = [];
  const wrapped: FakeImageData[] = [];
  return {
    created,
    wrapped,
    create(width, height) {
      const image = new FakeImageData(width, height);
      created.push(image);
      return image as unknown as ImageData;
    },
    wrap(pixels, width, height) {
      const image = new FakeImageData(pixels, width, height);
      wrapped.push(image);
      return image as unknown as ImageData;
    },
  };
}
