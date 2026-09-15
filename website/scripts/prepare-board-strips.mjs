import sharp from "sharp";
import path from "node:path";
import { fileURLToPath } from "node:url";
import sizes from "../event-photo-sizes.json" with { type: "json" };

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
for (const name of ["classic", "sepia", "monochrome", "naturale"]) {
  const source = path.join(root, ".handoffs/guest-board-implementation", `strip-${name}-source.jpg`);
  const metadata = await sharp(source).metadata();
  const target = path.join(root, "public/images/guest-board", `strip-${name}`);
  await sharp(source).webp({ quality: 90 }).toFile(`${target}.webp`);
  for (const width of [...sizes.imageSizes, ...sizes.deviceSizes]) {
    await sharp(source).resize({ width, withoutEnlargement: true }).webp({ quality: 88 })
      .toFile(`${target}-${width}w.webp`);
  }
  console.log(`${name}: ${metadata.width}x${metadata.height}; preserved complete strip.`);
}
