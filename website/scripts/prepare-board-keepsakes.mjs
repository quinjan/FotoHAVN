import sharp from "sharp";
import { fileURLToPath } from "node:url";

for (const name of ["teddy-charm", "bunny-charm", "ribbon-keepsake", "ticket-keepsake"]) {
  const source = fileURLToPath(new URL(`../.handoffs/guest-board-keepsakes/${name}-source.png`, import.meta.url));
  const output = fileURLToPath(new URL(`../public/images/guest-board/${name}.webp`, import.meta.url));
  const metadata = await sharp(source).metadata();
  const stats = await sharp(source).stats();
  if (!metadata.hasAlpha || stats.isOpaque) throw new Error(`${name} must retain genuine transparency`);
  const result = await sharp(source).resize({ width: 480, withoutEnlargement: true })
    .webp({ quality: 88, alphaQuality: 100, effort: 6 }).toFile(output);
  console.log(JSON.stringify({ name, sourceWidth: metadata.width, sourceHeight: metadata.height,
    width: result.width, height: result.height, bytes: result.size, alpha: true }));
}
