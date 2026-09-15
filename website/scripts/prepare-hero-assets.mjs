import sharp from "sharp";
import { mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";
import sizes from "../event-photo-sizes.json" with { type: "json" };

const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const source = path.join(site, ".handoffs/hero-curtain-implementation");
const output = path.join(site, "public/images/hero");
await mkdir(output, { recursive: true });

for (const name of ["exterior", "interior"]) {
  const image = sharp(path.join(source, `hero-${name}-source.png`));
  await image
    .clone()
    .webp({ quality: 88 })
    .toFile(path.join(output, `${name}.webp`));
  for (const width of [...sizes.imageSizes, ...sizes.deviceSizes]) {
    await image
      .clone()
      .resize({ width, withoutEnlargement: true })
      .webp({ quality: 85 })
      .toFile(path.join(output, `${name}-${width}w.webp`));
  }
}
console.log("Prepared two hero images and fourteen responsive derivatives.");
