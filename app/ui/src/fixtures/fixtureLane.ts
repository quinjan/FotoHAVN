import { createFixtureTransport, type FixtureSnapshot } from "../brain/fixtureTransport.ts";
import type { Transport } from "../brain/types.ts";
import { PROTOCOL_VERSION, type ServerMessage } from "../protocol/generated.ts";
import { operatorEventListWithRows } from "./inlineFixtures.ts";
import { operatorFixtures } from "./operatorFixtures.ts";
import { fixturePreviewUrl } from "./previewImages.ts";

/**
 * The fixture lane: `?fixture=<name>` in development and E2E builds. `main.tsx` imports this module only in those
 * builds (a dynamic import behind a build-time guard), so field builds contain no fixture code or data.
 *
 * The fixtures are the brain-generated messages in `presentations/` (written by FotoHavn.Core tests, contract §14),
 * by file name. A `presentation` message is served as the `welcome` a page would get at that revision; the
 * `protocol-mismatch` message makes the stand-in brain answer every `hello` with it. The operator Event lists with rows
 * and the storage error for every kind are hand-written (`operatorFixtures.ts`), for states the brain tests don't write.
 */

export type FixtureMessages = Readonly<Record<string, ServerMessage>>;

const generated: FixtureMessages = Object.fromEntries(
  Object.entries(
    import.meta.glob<ServerMessage>("./presentations/*.json", { eager: true, import: "default" }),
  ).map(([file, message]) => [fileStem(file), message]),
);

const handWritten: FixtureMessages = {
  "operator-event-list": operatorEventListWithRows,
  ...operatorFixtures(),
};

function fileStem(file: string): string {
  return file.replace(/^.*\//, "").replace(/\.json$/, "");
}

/** The snapshot a fixture brain answers `hello` with, or null for a message that isn't a snapshot. */
export function toFixtureSnapshot(message: ServerMessage): FixtureSnapshot | null {
  switch (message.type) {
    case "welcome":
    case "protocolMismatch":
      return message;
    case "presentation":
      return message.role === "operator"
        ? { type: "welcome", role: "operator", protocolVersion: PROTOCOL_VERSION, revision: message.revision, presentation: message.presentation }
        : { type: "welcome", role: "guest", protocolVersion: PROTOCOL_VERSION, revision: message.revision, presentation: message.presentation };
    default:
      return null;
  }
}

/** Every fixture snapshot by name: the generated ones, then the hand-written ones the brain can't generate yet. */
export function fixtureSnapshots(messages: FixtureMessages = { ...generated, ...handWritten }): Record<string, FixtureSnapshot> {
  const snapshots: Record<string, FixtureSnapshot> = {};
  for (const [name, message] of Object.entries(messages)) {
    const snapshot = toFixtureSnapshot(message);
    if (snapshot !== null) {
      snapshots[name] = snapshot;
    }
  }
  return snapshots;
}

const snapshots = fixtureSnapshots();

/** The fixture names, sorted, for the not-connected screen's links. */
export const fixtureNames: readonly string[] = Object.keys(snapshots).sort();

/** A fixture transport for `name`, or null when there is no such fixture. */
export function createFixtureLaneTransport(name: string): Transport | null {
  const snapshot = Object.hasOwn(snapshots, name) ? snapshots[name] : undefined;
  return snapshot === undefined ? null : createFixtureTransport(snapshot, { previewUrl: fixturePreviewUrl });
}
