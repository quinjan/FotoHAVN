import sharp from "sharp";
import { mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";
import sizes from "../event-photo-sizes.json" with { type: "json" };

const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const source = path.join(site, ".handoffs/online-photobooth-20260914");
const output = path.join(site, "public/images/experience-online");
await mkdir(output, { recursive: true });

for (const name of ["curtain", "lens", "keepsake", "keepsake-reunion", "candid", "candid-v2", "look-choice", "look-prints"]) {
  const original = sharp(path.join(source, `asset-${name}.png`));
  await original.clone().webp({ quality: 88 }).toFile(path.join(output, `${name}.webp`));
  for (const width of [...sizes.imageSizes, ...sizes.deviceSizes]) {
    await original.clone()
      .resize({ width, withoutEnlargement: true })
      .webp({ quality: 85 })
      .toFile(path.join(output, `${name}-${width}w.webp`));
  }
}

console.log("Prepared eight Experience images and their responsive WebP derivatives.");
