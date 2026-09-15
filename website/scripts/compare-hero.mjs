import sharp from "sharp";
import { fileURLToPath } from "node:url";
import path from "node:path";

const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const folder = path.join(site, ".handoffs/hero-curtain-implementation");
const filename = process.argv[2] ?? "desktop-closed-v1.png";
if (path.basename(filename) !== filename || !filename.endsWith(".png")) {
  throw new Error(
    "Pass a screenshot filename from the hero handoff directory.",
  );
}
const implementation = path.join(folder, filename);
const source = path.join(folder, "approved-hero.png");
const { width, height } = await sharp(implementation).metadata();
const sourcePixels = await sharp(source).metadata();
const reference = await sharp(source)
  .resize(width, height, { fit: "fill" })
  .png()
  .toBuffer();
const rendered = await sharp(implementation).png().toBuffer();

async function compare(label, region) {
  const left = region
    ? await sharp(reference).extract(region).toBuffer()
    : reference;
  const right = region
    ? await sharp(rendered).extract(region).toBuffer()
    : rendered;
  const w = region?.width ?? width;
  const h = region?.height ?? height;
  const output = path.join(
    folder,
    `comparison-${filename.slice(0, -4)}-${label}.png`,
  );
  await sharp({
    create: {
      width: w * 2 + 24,
      height: h,
      channels: 3,
      background: "#fbf8f2",
    },
  })
    .composite([
      { input: left, left: 0, top: 0 },
      { input: right, left: w + 24, top: 0 },
    ])
    .png()
    .toFile(output);
  console.log(`${label}: source LEFT, implementation RIGHT — ${output}`);
}

await compare("full");
await compare("sign", {
  left: 0,
  top: 0,
  width,
  height: Math.round(height * 0.26),
});
await compare("copy", {
  left: Math.round(width * 0.25),
  top: Math.round(height * 0.32),
  width: Math.round(width * 0.54),
  height: Math.round(height * 0.52),
});
console.log(
  JSON.stringify({
    source: [sourcePixels.width, sourcePixels.height],
    implementation: [width, height],
    normalization:
      "Source resized to matching viewport pixels; negligible aspect-ratio difference.",
  }),
);
