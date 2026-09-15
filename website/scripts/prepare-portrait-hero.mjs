import sharp from "sharp";
import path from "node:path";
import { fileURLToPath } from "node:url";
import sizes from "../event-photo-sizes.json" with { type: "json" };

const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const source = path.join(site, ".handoffs/responsive-editorial-lift/portrait-source.png");
const output = path.join(site, "public/images/hero");
await sharp(source).webp({ quality: 88 }).toFile(path.join(output, "exterior-portrait.webp"));
for (const width of [...sizes.imageSizes, ...sizes.deviceSizes]) {
  await sharp(source).resize({ width, withoutEnlargement: true }).webp({ quality: 85 })
    .toFile(path.join(output, `exterior-portrait-${width}w.webp`));
}
console.log("Prepared portrait exterior and responsive derivatives; desktop artwork unchanged.");
