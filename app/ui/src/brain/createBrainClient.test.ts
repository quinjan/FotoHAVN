import { describe, expect, it, vi } from "vitest";
import type { AppEnv } from "../env.ts";
import { createFixtureLaneTransport } from "../fixtures/fixtureLane.ts";
import { PROTOCOL_VERSION } from "../protocol/generated.ts";
import { createBrainClient, exposeE2EHandle, readStartupParams, type E2EHandle } from "./createBrainClient.ts";
import { ProtocolBrainClient } from "./protocolClient.ts";
import type { SocketLike } from "./webSocketTransport.ts";

const field: AppEnv = { e2e: false, dev: false };
const e2e: AppEnv = { e2e: true, dev: false };
const dev: AppEnv = { e2e: false, dev: true };

function target(search: string, webview?: WebView2Bridge): Pick<Window, "chrome" | "location"> {
  const location = { search, protocol: "http:", host: "127.0.0.1:5123" } as Location;
  return webview === undefined ? { location } : { chrome: { webview }, location };
}

function fakeWebView() {
  const postMessage = vi.fn<(message: unknown) => void>();
  const webview: WebView2Bridge = { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() };
  return { webview, postMessage };
}

function recordingSocket(urls: string[]) {
  return (url: string): SocketLike => {
    urls.push(url);
    return { readyState: 0, onopen: null, onmessage: null, onclose: null, onerror: null, send: vi.fn(), close: vi.fn() };
  };
}

describe("readStartupParams", () => {
  it("reads the token and brain override in every build, treating empty values as absent", () => {
    expect(readStartupParams("?token=abc&brain=ws://h/bridge", field)).toEqual({
      token: "abc",
      brain: "ws://h/bridge",
      fixture: null,
      protocolVersion: null,
    });
    expect(readStartupParams("?token=&brain=", field)).toMatchObject({ token: null, brain: null });
  });

  it("honors ?fixture= only in development and E2E builds", () => {
    expect(readStartupParams("?fixture=guest-start", field).fixture).toBeNull();
    expect(readStartupParams("?fixture=guest-start", dev).fixture).toBe("guest-start");
    expect(readStartupParams("?fixture=guest-start", e2e).fixture).toBe("guest-start");
  });

  it("honors an integer ?e2eProtocolVersion= only in E2E builds", () => {
    expect(readStartupParams("?e2eProtocolVersion=0", e2e).protocolVersion).toBe(0);
    expect(readStartupParams("?e2eProtocolVersion=-3", e2e).protocolVersion).toBe(-3);
    expect(readStartupParams("?e2eProtocolVersion=2", dev).protocolVersion).toBeNull();
    expect(readStartupParams("?e2eProtocolVersion=0", field).protocolVersion).toBeNull();
    expect(readStartupParams("?e2eProtocolVersion=1.5", e2e).protocolVersion).toBeNull();
    expect(readStartupParams("?e2eProtocolVersion=abc", e2e).protocolVersion).toBeNull();
  });
});

describe("createBrainClient", () => {
  it("prefers the WebView2 bridge over a token or fixture", () => {
    const { webview, postMessage } = fakeWebView();
    const client = createBrainClient({ target: target("?token=abc&fixture=guest-start", webview), env: dev });
    expect(client.kind).toBe("webview2");
    client.connect();
    expect(postMessage).toHaveBeenCalledWith({ type: "hello", protocolVersion: PROTOCOL_VERSION });
  });

  it("uses the WebSocket bridge when the page has a token", () => {
    const urls: string[] = [];
    const client = createBrainClient({ target: target("?token=abc"), env: field, createSocket: recordingSocket(urls) });
    expect(client.kind).toBe("websocket");
    client.connect();
    expect(urls).toEqual(["ws://127.0.0.1:5123/bridge?token=abc"]);
  });

  it("uses the ?brain= override for the WebSocket bridge", () => {
    const urls: string[] = [];
    const client = createBrainClient({
      target: target("?token=abc&brain=ws%3A%2F%2F127.0.0.1%3A6000%2Fbridge"),
      env: dev,
      createSocket: recordingSocket(urls),
    });
    client.connect();
    expect(urls).toEqual(["ws://127.0.0.1:6000/bridge?token=abc"]);
  });

  it("is notConnected when the ?brain= override isn't a URL", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const client = createBrainClient({ target: target("?token=abc&brain=nope"), env: dev });
    expect(client.kind).toBe("none");
    expect(warn).toHaveBeenCalledOnce();
  });

  it("uses the browser WebSocket when no factory is given", () => {
    const created: string[] = [];
    vi.stubGlobal(
      "WebSocket",
      class {
        readonly url: string;
        constructor(url: string) {
          this.url = url;
          created.push(url);
        }
      },
    );
    createBrainClient({ target: target("?token=abc"), env: field }).connect();
    expect(created).toEqual(["ws://127.0.0.1:5123/bridge?token=abc"]);
  });

  it("uses a known fixture from the fixture lane in development and E2E builds", () => {
    const fixtures = createFixtureLaneTransport;
    expect(createBrainClient({ target: target("?fixture=operator-event-list"), env: e2e, fixtures }).kind).toBe("fixture");
    expect(createBrainClient({ target: target("?fixture=guest-start"), env: dev, fixtures }).kind).toBe("fixture");
  });

  it("is notConnected for an unknown fixture, a field build without a bridge, or no parameters", () => {
    for (const client of [
      createBrainClient({ target: target("?fixture=nope"), env: dev, fixtures: createFixtureLaneTransport }),
      createBrainClient({ target: target("?fixture="), env: dev, fixtures: createFixtureLaneTransport }),
      createBrainClient({ target: target("?fixture=hasOwnProperty"), env: dev, fixtures: createFixtureLaneTransport }),
      createBrainClient({ target: target("?fixture=guest-start"), env: field, fixtures: createFixtureLaneTransport }),
      createBrainClient({ target: target("?fixture=guest-start"), env: dev }),
      createBrainClient({ target: target(""), env: e2e }),
    ]) {
      expect(client.kind).toBe("none");
      expect(client.getState().status).toBe("notConnected");
    }
  });

  it("sends the E2E version override in hello and passes client options through", () => {
    const { webview, postMessage } = fakeWebView();
    const client = createBrainClient({
      target: target("?e2eProtocolVersion=0", webview),
      env: e2e,
      clientOptions: { createId: () => "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b" },
    });
    expect(client).toBeInstanceOf(ProtocolBrainClient);
    client.connect();
    expect(postMessage).toHaveBeenCalledWith({ type: "hello", protocolVersion: 0 });
    void client.sendCommand("OpenNewEvent", {});
    expect(postMessage).toHaveBeenLastCalledWith(expect.objectContaining({ id: "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b" }));
    client.dispose();
  });
});

describe("exposeE2EHandle", () => {
  it("exposes the client's requests only in E2E builds", async () => {
    const client = new ProtocolBrainClient(null);
    const fieldTarget = {};
    expect(exposeE2EHandle(fieldTarget, client, field)).toBe(false);
    expect(fieldTarget).not.toHaveProperty("fotohavnE2E");

    const e2eTarget = {} as { fotohavnE2E?: E2EHandle };
    expect(exposeE2EHandle(e2eTarget, client, e2e)).toBe(true);
    const handle = e2eTarget.fotohavnE2E;
    expect(handle?.kind).toBe("none");
    expect(handle?.getState().status).toBe("notConnected");
    await expect(handle?.sendCommand("OpenNewEvent", {})).resolves.toEqual({ status: "failed", error: "notConnected" });
    await expect(handle?.sendTest("test.getDiagnostics", null)).resolves.toEqual({ ok: false, error: "notConnected" });
  });
});
