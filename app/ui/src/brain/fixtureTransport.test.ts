import { describe, expect, it } from "vitest";
import { guestStart, operatorEventListEmpty } from "../fixtures/inlineFixtures.ts";
import { PROTOCOL_VERSION } from "../protocol/generated.ts";
import { createFixtureTransport } from "./fixtureTransport.ts";
import { ProtocolBrainClient } from "./protocolClient.ts";

async function flush(): Promise<void> {
  for (let index = 0; index < 5; index += 1) {
    await Promise.resolve();
  }
}

describe("createFixtureTransport", () => {
  it("offers the pattern as its live preview, and takes frame acks without answering", async () => {
    const transport = createFixtureTransport(guestStart);
    const client = new ProtocolBrainClient(transport);
    expect(client.frames).toBe(transport.frames);
    const frames: unknown[] = [];
    transport.frames?.subscribe((frame) => frames.push(frame));
    expect(frames).toHaveLength(1);

    client.connect();
    await flush();
    transport.send({ type: "frameAck", seq: 3 });
    await flush();
    expect(transport.sent.at(-1)).toEqual({ type: "frameAck", seq: 3 });
    expect(client.getState().status).toBe("connected");
  });

  it("answers hello with its welcome, asynchronously", async () => {
    const transport = createFixtureTransport(guestStart);
    const client = new ProtocolBrainClient(transport);
    expect(client.kind).toBe("fixture");
    client.connect();
    expect(client.getState().status).toBe("connecting");

    await flush();
    expect(transport.sent).toEqual([{ type: "hello", protocolVersion: PROTOCOL_VERSION }]);
    expect(client.getState()).toMatchObject({ status: "connected", session: { role: "guest", revision: 1 } });
  });

  it("answers another version with protocolMismatch", async () => {
    const client = new ProtocolBrainClient(createFixtureTransport(guestStart), { protocolVersion: 0 });
    client.connect();
    await flush();
    expect(client.getState()).toEqual({
      status: "protocolMismatch",
      session: null,
      mismatch: { brainVersion: PROTOCOL_VERSION, uiVersion: 0 },
    });
  });

  it("rejects every command with notAvailable by default, like the M2 brain", async () => {
    const client = new ProtocolBrainClient(createFixtureTransport(operatorEventListEmpty));
    client.connect();
    await flush();
    await expect(client.sendCommand("OpenNewEvent", {})).resolves.toEqual({ status: "rejected", reason: "notAvailable" });
  });

  it("answers commands with a custom answer", async () => {
    const client = new ProtocolBrainClient(
      createFixtureTransport(operatorEventListEmpty, {
        answerCommand: (command) =>
          command.name === "OpenNewEvent" ? { status: "accepted", reason: null } : { status: "rejected", reason: "eventRunning" },
      }),
    );
    client.connect();
    await flush();
    await expect(client.sendCommand("OpenNewEvent", {})).resolves.toEqual({ status: "accepted" });
    await expect(client.sendCommand("DeleteEvent", { eventId: "e" })).resolves.toEqual({ status: "rejected", reason: "eventRunning" });
  });

  it("answers test hooks with hooksUnavailable", async () => {
    const client = new ProtocolBrainClient(createFixtureTransport(operatorEventListEmpty));
    client.connect();
    await flush();
    await expect(client.sendTest("test.getDiagnostics", null)).resolves.toEqual({ ok: false, error: "hooksUnavailable" });
  });

  it("records a frame ack and answers it with nothing", async () => {
    const transport = createFixtureTransport(operatorEventListEmpty);
    const client = new ProtocolBrainClient(transport);
    client.connect();
    await flush();
    const revision = client.getState().session?.revision;

    expect(transport.send({ type: "frameAck", seq: 7 })).toBe(true);
    await flush();

    expect(transport.sent.at(-1)).toEqual({ type: "frameAck", seq: 7 });
    expect(client.getState().session?.revision).toBe(revision);
  });

  it("delivers pushed messages and nothing after stop", async () => {
    const transport = createFixtureTransport(operatorEventListEmpty);
    const client = new ProtocolBrainClient(transport);
    client.connect();
    await flush();

    transport.push({ type: "presentation", role: "operator", revision: 2, presentation: operatorEventListEmpty.presentation });
    await flush();
    expect(client.getState().session?.revision).toBe(2);

    transport.start({ onOpen: () => undefined, onMessage: () => undefined, onClose: () => undefined });
    client.dispose();
    expect(transport.send({ type: "hello", protocolVersion: 1 })).toBe(false);
    transport.push({ type: "presentation", role: "operator", revision: 3, presentation: operatorEventListEmpty.presentation });
    await flush();
    expect(client.getState().session?.revision).toBe(2);
  });
});
