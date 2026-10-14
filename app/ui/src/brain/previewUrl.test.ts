import { describe, expect, it } from "vitest";
import { headlessPreviewBase, headlessPreviewUrl, webViewPreviewUrl } from "./previewUrl.ts";

const key = "signature-strip.naturale-v1.r42";

describe("preview URLs", () => {
  it("is the WebView2 virtual host's address for the key", () => {
    expect(webViewPreviewUrl(key)).toBe("https://preview.fotohavn/signature-strip.naturale-v1.r42.png");
  });

  it("is /preview/<key>.png on the page's own origin for the headless host, by default", () => {
    expect(headlessPreviewUrl(key)).toBe("/preview/signature-strip.naturale-v1.r42.png");
    expect(headlessPreviewUrl(key, "http://127.0.0.1:6000/preview/")).toBe("http://127.0.0.1:6000/preview/signature-strip.naturale-v1.r42.png");
  });

  it("keeps a key that isn't a plain segment from changing the path", () => {
    expect(webViewPreviewUrl("a/b?c#d")).toBe("https://preview.fotohavn/a%2Fb%3Fc%23d.png");
    expect(headlessPreviewUrl("a/b?c#d")).toBe("/preview/a%2Fb%3Fc%23d.png");
  });

  it("finds the headless preview route from the ?brain= bridge, else the page's own origin", () => {
    expect(headlessPreviewBase(null)).toBe("/preview/");
    expect(headlessPreviewBase("ws://127.0.0.1:6000/bridge")).toBe("http://127.0.0.1:6000/preview/");
    expect(headlessPreviewBase("wss://booth.example:7443/bridge")).toBe("https://booth.example:7443/preview/");
  });

  it("rejects a ?brain= value that isn't a URL", () => {
    expect(() => headlessPreviewBase("nope")).toThrow(TypeError);
  });
});
