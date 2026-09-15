import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import sharp from "sharp";
import photoSizes from "../event-photo-sizes.json" with { type: "json" };

const wrapperSource = readFileSync(
  new URL("../src/components/EditorialLift.tsx", import.meta.url),
  "utf8",
);
const wrapperStyles = readFileSync(
  new URL("../src/components/EditorialLift.module.css", import.meta.url),
  "utf8",
);
const heroSource = readFileSync(
  new URL("../src/components/HeroExterior.tsx", import.meta.url),
  "utf8",
);

test("hero and Experience render in static document flow", () => {
  assert.doesNotMatch(wrapperSource, /useEffect|mountEditorialLift|data-lift-/);
  assert.doesNotMatch(wrapperStyles, /position:\s*sticky|transform:|data-lift-mode/);
});

test("Explore the Experience remains a native section anchor", () => {
  assert.match(heroSource, /href=["']#experience["']/);
  assert.doesNotMatch(heroSource, /onClick=.*experience|preventDefault/);
});

test("all portrait image candidates exist and preserve the source aspect ratio", async () => {
  const source = await sharp(
    readFileSync(
      new URL("../public/images/hero/exterior-portrait.webp", import.meta.url),
    ),
  ).metadata();
  assert.equal(source.width / source.height, 2 / 3);
  for (const width of [...photoSizes.imageSizes, ...photoSizes.deviceSizes]) {
    const file = new URL(
      `../public/images/hero/exterior-portrait-${width}w.webp`,
      import.meta.url,
    );
    const metadata = await sharp(readFileSync(file)).metadata();
    assert.equal(metadata.width, Math.min(width, source.width));
    assert.ok(Math.abs(metadata.width / metadata.height - 2 / 3) < 0.005);
  }
});
