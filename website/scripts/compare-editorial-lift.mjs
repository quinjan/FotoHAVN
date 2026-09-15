import sharp from "sharp";
import path from "node:path";
import { fileURLToPath } from "node:url";

const folder = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../.handoffs/editorial-lift");
const source = await sharp(path.join(folder, "approved-storyboard.png")).metadata();
// The selected board illustrates sequential moments, not three alternate pages.
const panels = [
  { name: "rest", x: 0.019, y: 0.267, w: 0.315, h: 0.438 },
  { name: "mid", x: 0.346, y: 0.267, w: 0.309, h: 0.438 },
  { name: "arrived", x: 0.669, y: 0.267, w: 0.313, h: 0.438 },
];
for (const panel of panels) {
  const crop = {
    left: Math.round(source.width * panel.x), top: Math.round(source.height * panel.y),
    width: Math.round(source.width * panel.w), height: Math.round(source.height * panel.h),
  };
  const reference = await sharp(path.join(folder, "approved-storyboard.png"))
    .extract(crop).resize(720, 560, { fit: "contain", background: "#f3ebdd" }).png().toBuffer();
  const screenshot = path.join(folder, `desktop-${panel.name}.png`);
  const rendered = await sharp(screenshot)
    .resize(720, 560, { fit: "contain", background: "#f3ebdd" }).png().toBuffer();
  await sharp({ create: { width: 1464, height: 560, channels: 3, background: "#fbf8f2" } })
    .composite([{ input: reference, left: 0, top: 0 }, { input: rendered, left: 744, top: 0 }])
    .png().toFile(path.join(folder, `comparison-${panel.name}.png`));
  console.log(`${panel.name}: source LEFT / implementation RIGHT; aspect preserved with paper letterboxing.`);
}
