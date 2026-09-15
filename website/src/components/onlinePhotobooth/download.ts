import type { LayoutId } from "./presets";

let resultSerial = 0;

function createResultId() {
  const crypto = globalThis.crypto;
  if (typeof crypto?.randomUUID === "function") return crypto.randomUUID();
  // This is local filename bookkeeping, not a security token. Keep the serial in
  // the eight-character suffix so same-timestamp results remain distinct.
  return (++resultSerial).toString(16).padStart(8, "0");
}

export function compositionFilename(layoutId: LayoutId, createdAt = new Date(), resultId = createResultId()) {
  const layout = layoutId.replace(/-v\d+$/, "");
  const timestamp = createdAt.toISOString().replace(/[:.]/g, "-");
  return `FOTOHAVN-${layout}-${timestamp}-${resultId.slice(0, 8)}.png`;
}
