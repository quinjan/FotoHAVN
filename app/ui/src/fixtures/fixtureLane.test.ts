import { describe, expect, it } from "vitest";
import { ProtocolBrainClient } from "../brain/protocolClient.ts";
import { PROTOCOL_VERSION } from "../protocol/generated.ts";
import { createFixtureLaneTransport, fixtureNames, fixtureSnapshots, toFixtureSnapshot } from "./fixtureLane.ts";
import { guestStart, operatorEventListEmpty } from "./inlineFixtures.ts";

async function flush(): Promise<void> {
  for (let index = 0; index < 5; index += 1) {
    await Promise.resolve();
  }
}

describe("fixture lane", () => {
  it("serves every brain-generated file by name, plus the hand-written Event rows", () => {
    expect(fixtureNames).toEqual([
      "guest-fatal",
      "guest-start",
      "operator-event-list",
      "operator-event-list-empty",
      "operator-fatal",
      "protocol-mismatch",
    ]);
  });

  it("serves the generated welcomes unchanged", () => {
    const snapshots = fixtureSnapshots();
    expect(snapshots["operator-event-list-empty"]).toEqual(operatorEventListEmpty);
    expect(snapshots["guest-start"]).toEqual(guestStart);
  });

  it("turns a presentation message into the welcome at its revision, for either role", () => {
    const snapshots = fixtureSnapshots();
    expect(snapshots["operator-fatal"]).toMatchObject({
      type: "welcome",
      role: "operator",
      protocolVersion: PROTOCOL_VERSION,
      revision: 2,
      presentation: { surface: "fatal" },
    });
    expect(snapshots["guest-fatal"]).toMatchObject({ type: "welcome", role: "guest", revision: 2, presentation: { surface: "fatal" } });
  });

  it("keeps protocolMismatch and skips messages that aren't snapshots", () => {
    expect(toFixtureSnapshot({ type: "protocolMismatch", brainVersion: 1, uiVersion: 0 })).toEqual({
      type: "protocolMismatch",
      brainVersion: 1,
      uiVersion: 0,
    });
    expect(toFixtureSnapshot({ type: "error", id: null, reason: "invalidJson" })).toBeNull();
    expect(fixtureSnapshots({ broken: { type: "error", id: null, reason: "invalidJson" } })).toEqual({});
  });

  it("creates a fixture transport per name, and none for an unknown name", async () => {
    expect(createFixtureLaneTransport("nope")).toBeNull();
    expect(createFixtureLaneTransport("hasOwnProperty")).toBeNull();

    const client = new ProtocolBrainClient(createFixtureLaneTransport("operator-fatal"));
    client.connect();
    await flush();
    expect(client.getState()).toMatchObject({ status: "connected", session: { role: "operator", revision: 2 } });
  });

  it("answers hello with the mismatch for the protocol-mismatch fixture", async () => {
    const client = new ProtocolBrainClient(createFixtureLaneTransport("protocol-mismatch"));
    client.connect();
    await flush();
    expect(client.getState()).toEqual({
      status: "protocolMismatch",
      session: null,
      mismatch: { brainVersion: PROTOCOL_VERSION, uiVersion: PROTOCOL_VERSION - 1 },
    });
  });
});
