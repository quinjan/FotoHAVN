import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { guestStart, operatorEventListEmpty, operatorEventListWithRows } from "../fixtures/inlineFixtures.ts";
import { PROTOCOL_VERSION, type OperatorPresentation } from "../protocol/generated.ts";
import { ManualTransport, sequentialIds } from "../test/manualTransport.ts";
import { ProtocolBrainClient, defaultCommandTimeoutMs, defaultTestTimeoutMs } from "./protocolClient.ts";

function setup(options: ConstructorParameters<typeof ProtocolBrainClient>[1] = {}) {
  const transport = new ManualTransport();
  const client = new ProtocolBrainClient(transport, { createId: sequentialIds(), ...options });
  client.connect();
  return { transport, client };
}

function handshaken(options: ConstructorParameters<typeof ProtocolBrainClient>[1] = {}) {
  const context = setup(options);
  context.transport.open();
  context.transport.receive(operatorEventListEmpty);
  return context;
}

const rowsPresentation: OperatorPresentation = operatorEventListWithRows.presentation;

describe("ProtocolBrainClient handshake", () => {
  it("exposes its transport's live preview frames, and none without one", () => {
    const frames = { subscribe: () => () => undefined };
    const withFrames = new ProtocolBrainClient(Object.assign(new ManualTransport(), { frames }));
    expect(withFrames.frames).toBe(frames);
    expect(new ProtocolBrainClient(new ManualTransport()).frames).toBeUndefined();
    expect(new ProtocolBrainClient(null).frames).toBeUndefined();
  });

  it("starts connecting, sends hello with the generated version when the transport opens, and takes the role from welcome", () => {
    const { transport, client } = setup();
    expect(client.kind).toBe("fixture");
    expect(client.getState()).toEqual({ status: "connecting", session: null, mismatch: null });

    transport.open();
    expect(transport.sent).toEqual([{ type: "hello", protocolVersion: PROTOCOL_VERSION }]);

    transport.receive(guestStart);
    expect(client.getState()).toEqual({
      status: "connected",
      session: { role: "guest", revision: 1, presentation: guestStart.presentation },
      mismatch: null,
    });
  });

  it("sends the overridden version in hello", () => {
    const { transport } = setup({ protocolVersion: 0 });
    transport.open();
    expect(transport.sent).toEqual([{ type: "hello", protocolVersion: 0 }]);
  });

  it("connects only once", () => {
    const { transport, client } = setup();
    const start = vi.spyOn(transport, "start");
    client.connect();
    expect(start).not.toHaveBeenCalled();
  });

  it("shows a protocol mismatch, drops the session, and recovers on a later welcome", () => {
    const { transport, client } = handshaken();
    transport.receive({ type: "protocolMismatch", brainVersion: 2, uiVersion: 1 });
    expect(client.getState()).toEqual({
      status: "protocolMismatch",
      session: null,
      mismatch: { brainVersion: 2, uiVersion: 1 },
    });

    // Presentations aren't accepted while the connection isn't handshaken.
    transport.receive({ type: "presentation", role: "operator", revision: 5, presentation: rowsPresentation });
    expect(client.getState().session).toBeNull();

    transport.receive(operatorEventListEmpty);
    expect(client.getState().status).toBe("connected");
    expect(client.getState().mismatch).toBeNull();
  });

  it("ignores values that aren't brain messages and unknown message types", () => {
    const { transport, client } = handshaken();
    const before = client.getState();
    transport.receive(null);
    transport.receive("welcome");
    transport.receive({ kind: "welcome" });
    transport.receive({ type: "somethingElse" });
    expect(client.getState()).toBe(before);
  });

  it("stays notConnected without a transport and fails every request locally", async () => {
    const client = new ProtocolBrainClient(null);
    client.connect();
    expect(client.kind).toBe("none");
    expect(client.getState()).toEqual({ status: "notConnected", session: null, mismatch: null });
    await expect(client.sendCommand("OpenNewEvent", {})).resolves.toEqual({ status: "failed", error: "notConnected" });
    await expect(client.sendTest("test.getDiagnostics", null)).resolves.toEqual({ ok: false, error: "notConnected" });
  });

  it("uses crypto.randomUUID for ids by default", () => {
    const transport = new ManualTransport();
    const client = new ProtocolBrainClient(transport);
    client.connect();
    transport.open();
    void client.sendCommand("OpenNewEvent", {});
    expect(transport.lastId()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
    client.dispose();
  });
});

describe("ProtocolBrainClient revisions", () => {
  it("accepts a newer presentation of its own role", () => {
    const { transport, client } = handshaken();
    transport.receive({ type: "presentation", role: "operator", revision: 2, presentation: rowsPresentation });
    expect(client.getState().session).toEqual({ role: "operator", revision: 2, presentation: rowsPresentation });
  });

  it("drops presentations with the same or an older revision", () => {
    const { transport, client } = handshaken();
    transport.receive({ type: "presentation", role: "operator", revision: 3, presentation: rowsPresentation });
    const accepted = client.getState();

    transport.receive({ type: "presentation", role: "operator", revision: 3, presentation: operatorEventListEmpty.presentation });
    transport.receive({ type: "presentation", role: "operator", revision: 2, presentation: operatorEventListEmpty.presentation });
    expect(client.getState()).toBe(accepted);
  });

  it("drops presentations for the other role", () => {
    const { transport, client } = handshaken();
    const before = client.getState();
    transport.receive({ type: "presentation", role: "guest", revision: 9, presentation: guestStart.presentation });
    expect(client.getState()).toBe(before);
  });

  it("drops presentations before the first welcome", () => {
    const { transport, client } = setup();
    transport.open();
    transport.receive({ type: "presentation", role: "operator", revision: 2, presentation: rowsPresentation });
    expect(client.getState().session).toBeNull();
  });

  it("lets a welcome reset the baseline, even to a lower revision", () => {
    const { transport, client } = handshaken();
    transport.receive({ type: "presentation", role: "operator", revision: 7, presentation: rowsPresentation });
    transport.receive({ ...operatorEventListEmpty, revision: 2 });
    expect(client.getState().session?.revision).toBe(2);

    transport.receive({ type: "presentation", role: "operator", revision: 3, presentation: rowsPresentation });
    expect(client.getState().session?.revision).toBe(3);
  });

  it("notifies subscribers on each accepted change, and not after unsubscribing", () => {
    const { transport, client } = handshaken();
    const listener = vi.fn();
    const unsubscribe = client.subscribe(listener);

    transport.receive({ type: "presentation", role: "operator", revision: 2, presentation: rowsPresentation });
    transport.receive({ type: "presentation", role: "operator", revision: 2, presentation: rowsPresentation });
    expect(listener).toHaveBeenCalledTimes(1);

    unsubscribe();
    transport.receive({ type: "presentation", role: "operator", revision: 3, presentation: rowsPresentation });
    expect(listener).toHaveBeenCalledTimes(1);
  });
});

describe("ProtocolBrainClient reconnect", () => {
  it("keeps the last snapshot while reconnecting, re-handshakes on reopen, and accepts the new welcome", () => {
    const { transport, client } = handshaken();
    transport.receive({ type: "presentation", role: "operator", revision: 4, presentation: rowsPresentation });

    transport.close();
    expect(client.getState()).toMatchObject({ status: "reconnecting", session: { revision: 4 } });

    transport.open();
    expect(transport.sent.filter((message) => message.type === "hello")).toHaveLength(2);
    // Until the new welcome, presentations on the new connection are dropped.
    transport.receive({ type: "presentation", role: "operator", revision: 5, presentation: operatorEventListEmpty.presentation });
    expect(client.getState()).toMatchObject({ status: "reconnecting", session: { revision: 4 } });

    transport.receive({ ...operatorEventListWithRows, revision: 4 });
    expect(client.getState()).toMatchObject({ status: "connected", session: { revision: 4 } });
  });

  it("stays connecting when the transport closes before any welcome", () => {
    const { transport, client } = setup();
    transport.open();
    transport.close();
    expect(client.getState().status).toBe("connecting");
  });

  it("fails pending requests with disconnected when the transport closes", async () => {
    const { transport, client } = handshaken();
    const command = client.sendCommand("OpenNewEvent", {});
    const test = client.sendTest("test.getDiagnostics", null);
    transport.close();
    await expect(command).resolves.toEqual({ status: "failed", error: "disconnected" });
    await expect(test).resolves.toEqual({ ok: false, error: "disconnected" });
  });
});

describe("ProtocolBrainClient commands", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("sends a command envelope with a UUID id and payload", () => {
    const { transport, client } = handshaken();
    void client.sendCommand("EditEvent", { eventId: "event-1" });
    expect(transport.last()).toEqual({
      type: "command",
      id: "00000000-0000-4000-8000-000000000001",
      name: "EditEvent",
      payload: { eventId: "event-1" },
    });
  });

  it("correlates results by id, in any order", async () => {
    const { transport, client } = handshaken();
    const first = client.sendCommand("OpenNewEvent", {});
    const firstId = transport.lastId();
    const second = client.sendCommand("EditEvent", { eventId: "event-1" });
    const secondId = transport.lastId();

    transport.receive({ type: "commandResult", id: secondId, status: "rejected", reason: "notAvailable" });
    transport.receive({ type: "commandResult", id: firstId, status: "accepted", reason: null });

    await expect(first).resolves.toEqual({ status: "accepted" });
    await expect(second).resolves.toEqual({ status: "rejected", reason: "notAvailable" });
  });

  it("ignores results for unknown or already settled ids", async () => {
    const { transport, client } = handshaken();
    const command = client.sendCommand("OpenNewEvent", {});
    const id = transport.lastId();
    transport.receive({ type: "commandResult", id: "00000000-0000-4000-8000-0000000000ff", status: "accepted", reason: null });
    transport.receive({ type: "commandResult", id, status: "rejected", reason: "wrongRole" });
    transport.receive({ type: "commandResult", id, status: "accepted", reason: null });
    await expect(command).resolves.toEqual({ status: "rejected", reason: "wrongRole" });
  });

  it("fails with timeout when no result arrives in time, and ignores a late result", async () => {
    const { transport, client } = handshaken();
    const command = client.sendCommand("OpenNewEvent", {});
    const id = transport.lastId();

    vi.advanceTimersByTime(defaultCommandTimeoutMs - 1);
    let settled = false;
    void command.then(() => {
      settled = true;
    });
    await Promise.resolve();
    expect(settled).toBe(false);

    vi.advanceTimersByTime(1);
    await expect(command).resolves.toEqual({ status: "failed", error: "timeout" });
    transport.receive({ type: "commandResult", id, status: "accepted", reason: null });
    await expect(command).resolves.toEqual({ status: "failed", error: "timeout" });
  });

  it("honors a custom timeout", async () => {
    const { client } = handshaken({ commandTimeoutMs: 50 });
    const command = client.sendCommand("OpenNewEvent", {});
    vi.advanceTimersByTime(50);
    await expect(command).resolves.toEqual({ status: "failed", error: "timeout" });
  });

  it("fails with the envelope error the brain reports for that id", async () => {
    const { transport, client } = handshaken();
    const command = client.sendCommand("OpenNewEvent", {});
    transport.receive({ type: "error", id: transport.lastId(), reason: "invalidEnvelope" });
    await expect(command).resolves.toEqual({ status: "failed", error: "invalidEnvelope" });
  });

  it("logs envelope errors without a pending request", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const { transport } = handshaken();
    transport.receive({ type: "error", id: null, reason: "payloadTooLarge" });
    transport.receive({ type: "error", id: "00000000-0000-4000-8000-0000000000ff", reason: "invalidId" });
    expect(warn).toHaveBeenCalledTimes(2);
    expect(warn).toHaveBeenCalledWith("FOTOHAVN: the brain reported a protocol error (payloadTooLarge).");
  });

  it("does not settle a command with a test result or a test with a command result", async () => {
    const { transport, client } = handshaken();
    const command = client.sendCommand("OpenNewEvent", {});
    const commandId = transport.lastId();
    const test = client.sendTest("test.exit", null);
    const testId = transport.lastId();

    transport.receive({ type: "testResult", id: commandId, ok: true, result: {}, error: null });
    transport.receive({ type: "commandResult", id: testId, status: "accepted", reason: null });
    transport.receive({ type: "commandResult", id: commandId, status: "accepted", reason: null });
    transport.receive({ type: "testResult", id: testId, ok: true, result: {}, error: null });

    await expect(command).resolves.toEqual({ status: "accepted" });
    await expect(test).resolves.toEqual({ ok: true, result: {} });
  });
});

describe("ProtocolBrainClient test hooks", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("omits args for hooks without arguments and sends them otherwise", () => {
    const { transport, client } = setup();
    transport.open();
    void client.sendTest("test.getDiagnostics", null);
    expect(transport.last()).toEqual({ type: "test", id: "00000000-0000-4000-8000-000000000001", name: "test.getDiagnostics" });

    void client.sendTest("test.openGuestScreen", { displayId: "virtual-guest" });
    expect(transport.last()).toEqual({
      type: "test",
      id: "00000000-0000-4000-8000-000000000002",
      name: "test.openGuestScreen",
      args: { displayId: "virtual-guest" },
    });
  });

  it("works before the handshake and resolves results and hook errors", async () => {
    const { transport, client } = setup();
    transport.open();
    const ok = client.sendTest("test.listCameras", null);
    transport.receive({ type: "testResult", id: transport.lastId(), ok: true, result: { cameras: [] }, error: null });
    await expect(ok).resolves.toEqual({ ok: true, result: { cameras: [] } });

    const failed = client.sendTest("test.exit", null);
    transport.receive({ type: "testResult", id: transport.lastId(), ok: false, result: null, error: "hooksUnavailable" });
    await expect(failed).resolves.toEqual({ ok: false, error: "hooksUnavailable" });

    const noReason = client.sendTest("test.exit", null);
    transport.receive({ type: "testResult", id: transport.lastId(), ok: false, result: null, error: null });
    await expect(noReason).resolves.toEqual({ ok: false, error: "hookFailed" });

    const envelope = client.sendTest("test.exit", null);
    transport.receive({ type: "error", id: transport.lastId(), reason: "invalidId" });
    await expect(envelope).resolves.toEqual({ ok: false, error: "invalidId" });
  });

  it("times out after the test timeout", async () => {
    const { transport, client } = setup();
    transport.open();
    const test = client.sendTest("test.exit", null);
    vi.advanceTimersByTime(defaultTestTimeoutMs);
    await expect(test).resolves.toEqual({ ok: false, error: "timeout" });
  });

  it("fails locally when the transport isn't open", async () => {
    const { client } = setup();
    await expect(client.sendTest("test.exit", null)).resolves.toEqual({ ok: false, error: "notConnected" });
    await expect(client.sendCommand("OpenNewEvent", {})).resolves.toEqual({ status: "failed", error: "notConnected" });
  });
});

describe("ProtocolBrainClient dispose", () => {
  it("stops the transport, fails pending requests, refuses new ones, and ignores later messages", async () => {
    const { transport, client } = handshaken();
    const listener = vi.fn();
    client.subscribe(listener);
    const command = client.sendCommand("OpenNewEvent", {});
    const test = client.sendTest("test.exit", null);

    client.dispose();
    client.dispose();
    expect(transport.stopped).toBe(true);
    await expect(command).resolves.toEqual({ status: "failed", error: "disposed" });
    await expect(test).resolves.toEqual({ ok: false, error: "disposed" });
    await expect(client.sendCommand("OpenNewEvent", {})).resolves.toEqual({ status: "failed", error: "disposed" });

    transport.receive({ type: "presentation", role: "operator", revision: 2, presentation: rowsPresentation });
    expect(listener).not.toHaveBeenCalled();
  });

  it("can't connect after dispose", () => {
    const transport = new ManualTransport();
    const client = new ProtocolBrainClient(transport);
    client.dispose();
    client.connect();
    expect(transport.listener).toBeNull();
  });
});
