import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { operatorEventListEmpty, operatorEventListWithRows } from "../fixtures/inlineFixtures.ts";
import { PROTOCOL_VERSION } from "../protocol/generated.ts";
import { stubImageData } from "../test/fakeImageData.ts";
import { ProtocolBrainClient } from "./protocolClient.ts";
import { bridgeUrl, createWebSocketTransport, defaultBackoffMs, type SocketLike } from "./webSocketTransport.ts";

class FakeSocket implements SocketLike {
  readyState = 0;
  binaryType?: "arraybuffer" | "blob";
  onopen: ((event: Event) => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onclose: ((event: CloseEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  readonly sent: unknown[] = [];
  closed = false;

  constructor(readonly url: string) {}

  send(data: string): void {
    this.sent.push(JSON.parse(data));
  }

  close(): void {
    this.closed = true;
    this.readyState = 3;
  }

  open(): void {
    this.readyState = 1;
    this.onopen?.(new Event("open"));
  }

  message(data: unknown): void {
    this.onmessage?.(new MessageEvent("message", { data }));
  }

  /** The server or network dropped the socket. */
  drop(): void {
    this.readyState = 3;
    this.onerror?.(new Event("error"));
    this.onclose?.(new CloseEvent("close"));
  }
}

function setup(backoffMs?: readonly number[]) {
  const sockets: FakeSocket[] = [];
  const transport = createWebSocketTransport({
    url: "ws://127.0.0.1:5000/bridge?token=abc",
    createSocket: (url) => {
      const socket = new FakeSocket(url);
      sockets.push(socket);
      return socket;
    },
    ...(backoffMs === undefined ? {} : { backoffMs }),
  });
  const client = new ProtocolBrainClient(transport);
  client.connect();
  const latest = () => {
    const socket = sockets.at(-1);
    if (socket === undefined) {
      throw new Error("No socket was created.");
    }
    return socket;
  };
  return { sockets, transport, client, latest };
}

describe("createWebSocketTransport", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("opens the bridge URL, sends hello as a JSON text frame, and decodes frames", () => {
    const { client, latest } = setup();
    expect(client.kind).toBe("websocket");
    expect(latest().url).toBe("ws://127.0.0.1:5000/bridge?token=abc");

    latest().open();
    expect(latest().sent).toEqual([{ type: "hello", protocolVersion: PROTOCOL_VERSION }]);

    latest().message(JSON.stringify(operatorEventListEmpty));
    expect(client.getState()).toMatchObject({ status: "connected", session: { role: "operator" } });
  });

  it("ignores frames that aren't JSON text", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const { client, latest } = setup();
    latest().open();
    latest().message("{not json");
    latest().message(new Blob(["x"]));
    expect(client.getState().status).toBe("connecting");
    expect(warn).toHaveBeenCalledOnce();
  });

  it("reconnects with backoff and re-handshakes on the new socket", () => {
    const { sockets, client, latest } = setup();
    latest().open();
    latest().message(JSON.stringify({ ...operatorEventListWithRows, revision: 3 }));

    latest().drop();
    expect(client.getState()).toMatchObject({ status: "reconnecting", session: { revision: 3 } });
    expect(sockets).toHaveLength(1);

    vi.advanceTimersByTime(defaultBackoffMs[0] ?? 0);
    expect(sockets).toHaveLength(2);
    latest().open();
    expect(latest().sent).toEqual([{ type: "hello", protocolVersion: PROTOCOL_VERSION }]);

    latest().message(JSON.stringify({ ...operatorEventListEmpty, revision: 3 }));
    expect(client.getState()).toMatchObject({ status: "connected", session: { revision: 3 } });
    expect(client.getState().session?.presentation).toEqual(operatorEventListEmpty.presentation);
  });

  it("backs off longer after each failed attempt, caps at the last delay, and starts over after an open", () => {
    const { sockets, latest } = setup([100, 200, 400]);
    latest().drop();
    vi.advanceTimersByTime(99);
    expect(sockets).toHaveLength(1);
    vi.advanceTimersByTime(1);
    expect(sockets).toHaveLength(2);

    latest().drop();
    vi.advanceTimersByTime(199);
    expect(sockets).toHaveLength(2);
    vi.advanceTimersByTime(1);
    expect(sockets).toHaveLength(3);

    latest().drop();
    vi.advanceTimersByTime(400);
    expect(sockets).toHaveLength(4);
    latest().drop();
    vi.advanceTimersByTime(400);
    expect(sockets).toHaveLength(5);

    latest().open();
    latest().drop();
    vi.advanceTimersByTime(100);
    expect(sockets).toHaveLength(6);
  });

  it("refuses to send while the socket isn't open", async () => {
    const { client, latest } = setup();
    await expect(client.sendCommand("OpenNewEvent", {})).resolves.toEqual({ status: "failed", error: "notConnected" });
    expect(latest().sent).toEqual([]);
  });

  describe("live preview frames", () => {
    beforeEach(() => {
      stubImageData();
    });

    function frameMessage(seq: number, width: number, height: number): ArrayBuffer {
      const message = new ArrayBuffer(24 + width * height * 4);
      const view = new DataView(message);
      new Uint8Array(message).set([0x46, 0x48, 0x50, 0x46]);
      view.setUint16(4, 1, true);
      view.setUint32(8, seq, true);
      view.setUint16(12, width, true);
      view.setUint16(14, height, true);
      return message;
    }

    it("asks for binary messages as ArrayBuffers on every new socket", () => {
      const { latest } = setup();
      expect(latest().binaryType).toBe("arraybuffer");
    });

    it("hands binary messages to the frame source and acknowledges each as a JSON text message", () => {
      const { transport, latest } = setup();
      const frames = vi.fn();
      transport.frames?.subscribe(frames);
      latest().open();

      latest().message(frameMessage(5, 2, 1));

      expect(frames).toHaveBeenCalledOnce();
      expect(latest().sent).toEqual([
        { type: "hello", protocolVersion: PROTOCOL_VERSION },
        { type: "frameAck", seq: 5 },
      ]);
    });

    it("does not acknowledge while the socket isn't open", () => {
      const { latest } = setup();
      latest().message(frameMessage(5, 2, 1));
      expect(latest().sent).toEqual([]);
    });
  });

  it("stops without reconnecting and ignores the old socket's late close", () => {
    const { sockets, transport, latest } = setup();
    const socket = latest();
    socket.open();
    transport.stop();
    expect(socket.closed).toBe(true);
    socket.drop();
    vi.advanceTimersByTime(10_000);
    expect(sockets).toHaveLength(1);

    transport.start({ onOpen: vi.fn(), onMessage: vi.fn(), onClose: vi.fn() });
    expect(sockets).toHaveLength(1);
  });

  it("cancels a scheduled reconnect on stop", () => {
    const { sockets, transport, latest } = setup();
    latest().drop();
    transport.stop();
    vi.advanceTimersByTime(10_000);
    expect(sockets).toHaveLength(1);
  });

  it("starts only once", () => {
    const { sockets, transport } = setup();
    transport.start({ onOpen: vi.fn(), onMessage: vi.fn(), onClose: vi.fn() });
    expect(sockets).toHaveLength(1);
  });

  it("uses the browser WebSocket by default", () => {
    const created: string[] = [];
    class BrowserSocket extends FakeSocket {
      constructor(url: string) {
        super(url);
        created.push(url);
      }
    }
    vi.stubGlobal("WebSocket", BrowserSocket);
    const transport = createWebSocketTransport({ url: "ws://127.0.0.1:1/bridge?token=t" });
    transport.start({ onOpen: vi.fn(), onMessage: vi.fn(), onClose: vi.fn() });
    expect(created).toEqual(["ws://127.0.0.1:1/bridge?token=t"]);
    transport.stop();
  });
});

describe("bridgeUrl", () => {
  it("uses /bridge on the page's host with the page's token", () => {
    expect(bridgeUrl({ protocol: "http:", host: "127.0.0.1:5123" }, "a1b2", null)).toBe("ws://127.0.0.1:5123/bridge?token=a1b2");
    expect(bridgeUrl({ protocol: "https:", host: "example.test" }, "a1b2", null)).toBe("wss://example.test/bridge?token=a1b2");
  });

  it("uses the ?brain= override and replaces any token in it", () => {
    expect(bridgeUrl({ protocol: "http:", host: "localhost:5173" }, "t 1", "ws://127.0.0.1:6000/bridge?token=old")).toBe(
      "ws://127.0.0.1:6000/bridge?token=t+1",
    );
  });
});
