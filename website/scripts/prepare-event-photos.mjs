import sharp from "sharp";
import { readdir, mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";
import photoSizes from "../event-photo-sizes.json" with { type: "json" };

const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const source = path.resolve(
  site,
  "../marketing/reels/FOTOHAVN-evia-event/public/assets/Pictures",
);
const output = path.join(site, ".handoffs/real-booth-redesign");
await mkdir(output, { recursive: true });
const files = (await readdir(source))
  .filter((file) => /\.jpg$/i.test(file))
  .sort();
const tiles = await Promise.all(
  files.map(async (file, index) => {
    const image = await sharp(path.join(source, file))
      .rotate()
      .resize(230, 280, { fit: "contain", background: "#e8ddce" })
      .toBuffer();
    const label = Buffer.from(
      `<svg width="230" height="38"><rect width="230" height="38" fill="#fbf8f2"/><text x="8" y="24" font-size="13" font-family="Arial" fill="#1e1a17">${index + 1}. ${file.slice(0, 18)}</text></svg>`,
    );
    const tile = await sharp({
      create: { width: 230, height: 318, channels: 3, background: "#fbf8f2" },
    })
      .composite([{ input: image }, { input: label, top: 280, left: 0 }])
      .jpeg()
      .toBuffer();
    console.log(`${index + 1}: ${file}`);
    return {
      input: tile,
      left: (index % 6) * 242,
      top: Math.floor(index / 6) * 330,
    };
  }),
);
await sharp({
  create: {
    width: 1452,
    height: Math.ceil(files.length / 6) * 330,
    channels: 3,
    background: "#f3ebdd",
  },
})
  .composite(tiles)
  .jpeg({ quality: 85 })
  .toFile(path.join(output, "source-contact-sheet.jpg"));

// Production derivatives only. The original event photographs stay untouched.
const web = path.join(site, "public/images/evia");
await mkdir(web, { recursive: true });
const selection = {
  "booth-close": "3f970799-a9ca-4a6e-966a-3a804a2ebc8a.jpg",
  "booth-at-evia": "1000018870.jpg",
  couple: "1000018878.jpg",
  friends: "1000018879.jpg",
  sisters: "1000018877.jpg",
  weekend: "ff29714a-97be-4401-aa2c-72c51677425c.jpg",
  family: "1000018876.jpg",
  keepsakes: "d0602d5c-cf98-47a5-8c1c-08e852fb131e.jpg",
  smiles: "5ea64c75-d9ff-4aab-ac2e-99ee31f944bc.jpg",
  "evia-event": "1000018867.jpg",
};
const widths = [...photoSizes.imageSizes, ...photoSizes.deviceSizes];

async function preparePhotograph(name, sourceImage, quality = 84) {
  await sourceImage
    .clone()
    .resize({ width: 1400, withoutEnlargement: true })
    .webp({ quality })
    .toFile(path.join(web, `${name}.webp`));
  for (const width of widths) {
    await sourceImage
      .clone()
      .resize({ width, withoutEnlargement: true })
      .webp({ quality: 80 })
      .toFile(path.join(web, `${name}-${width}w.webp`));
  }
}

for (const [name, file] of Object.entries(selection)) {
  await preparePhotograph(name, sharp(path.join(source, file)).rotate());
}
await preparePhotograph(
  "prints-detail",
  sharp(path.join(source, "1000018866.jpg"))
    .rotate()
    .extract({ left: 0, top: 1000, width: 1700, height: 1720 }),
  86,
);
await sharp(path.join(source, selection["booth-close"]))
  .rotate()
  .extract({ left: 210, top: 240, width: 180, height: 240 })
  .resize(384)
  .webp({ quality: 88 })
  .toFile(path.join(web, "booth-photo-board.webp"));
await sharp(path.join(source, selection["booth-close"]))
  .rotate()
  .extract({ left: 242, top: 955, width: 140, height: 205 })
  .resize(256, 512, { fit: "fill" })
  .webp({ quality: 88 })
  .toFile(path.join(web, "booth-wood.webp"));
console.log(
  `Prepared ${Object.keys(selection).length + 3} web photographs and reference textures, plus ${(Object.keys(selection).length + 1) * widths.length} responsive variants.`,
);
