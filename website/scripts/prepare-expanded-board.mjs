import sharp from "sharp";
import path from "node:path";
import { fileURLToPath } from "node:url";
import sizes from "../event-photo-sizes.json" with { type: "json" };

const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const source = path.join(site, "../marketing/reels/FOTOHAVN-evia-event/public/assets/Pictures");
const selection = {
  "guest-trio": "1000018874.jpg",
  "guest-solo": "1000018875.jpg",
  "guest-duo": "1000018880.jpg",
  "guest-flowers": "1000018881.jpg",
  "guest-three": "1000018882.jpg",
  "guest-close": "0010dffb-cbe9-4499-b885-83a6c171c9d5.jpg",
  "guest-together": "64b9e7d1-c76b-44e8-b699-b69e466680d2.jpg",
};
for (const [name, file] of Object.entries(selection)) {
  for (const width of [null, ...sizes.imageSizes, ...sizes.deviceSizes]) {
    const output = path.join(site, "public/images/guest-board", `${name}${width ? `-${width}w` : ""}.webp`);
    await sharp(path.join(source, file)).rotate().resize({ width: width ?? 1400, withoutEnlargement: true })
      .webp({ quality: width ? 80 : 84 }).toFile(output);
  }
  console.log(`${name}: ${file}`);
}
// The eighth added photograph reuses the prepared evia/sisters assets.
