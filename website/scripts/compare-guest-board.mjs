import sharp from "sharp";
import path from "node:path";
import { fileURLToPath } from "node:url";

const folder = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../.handoffs/guest-board-implementation");
const reference = await sharp(path.join(folder, "approved-design.png"))
  .extract({ left: 0, top: 0, width: 1536, height: 900 })
  .resize(760, 640, { fit: "contain", background: "#f3ebdd" }).png().toBuffer();
for (const [name, file, top, height] of [
  ["initial", "desktop-note-v2.png", 88, 912],
  ["final", "desktop-note-final.png", 88, 1061],
  ["production", "production-desktop-note.png", 88, 1061],
]) {
  const metadata = await sharp(path.join(folder, file)).metadata();
  const implementation = await sharp(path.join(folder, file))
    .extract({ left: 0, top, width: metadata.width, height: Math.min(height, metadata.height - top) })
    .resize(760, 640, { fit: "contain", background: "#f3ebdd" }).png().toBuffer();
  await sharp({ create: { width: 1540, height: 640, channels: 3, background: "#fbf8f2" } })
    .composite([{ input: reference, left: 0, top: 0 }, { input: implementation, left: 780, top: 0 }])
    .png().toFile(path.join(folder, `comparison-${name}.png`));
}
const referenceDetail = await sharp(path.join(folder, "approved-design.png"))
  .extract({ left: 525, top: 250, width: 435, height: 555 }).resize(420, 560, { fit: "contain", background: "#f3ebdd" }).png().toBuffer();
const implementationDetail = await sharp(path.join(folder, "desktop-note-final.png"))
  .extract({ left: 513, top: 332, width: 400, height: 501 }).resize(420, 560, { fit: "contain", background: "#f3ebdd" }).png().toBuffer();
await sharp({ create: { width: 860, height: 560, channels: 3, background: "#fbf8f2" } })
  .composite([{ input: referenceDetail, left: 0, top: 0 }, { input: implementationDetail, left: 440, top: 0 }])
  .png().toFile(path.join(folder, "comparison-note-detail.png"));
console.log("Paired source LEFT / implementation RIGHT; aspect ratios preserved, proposal workflow rail excluded.");
