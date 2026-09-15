import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import vm from "node:vm";
import test from "node:test";
import ts from "typescript";
import sharp from "sharp";
import React from "react";
import * as JSXRuntime from "react/jsx-runtime";
import { renderToStaticMarkup } from "react-dom/server";
import imageSizes from "../event-photo-sizes.json" with { type: "json" };

function compile(name, dependencies = {}) {
  const code = readFileSync(new URL(`../src/components/${name}`, import.meta.url), "utf8");
  const result = ts.transpileModule(code, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020, jsx: ts.JsxEmit.ReactJSX,
  } });
  const exports = {};
  vm.runInNewContext(result.outputText, { exports, ...dependencies });
  return exports;
}
const data = compile("guestBoardData.ts");
const motion = compile("guestBoardMotion.ts");

test("all posted prints have unique identities, real assets, alt text and editorial notes", () => {
  assert.equal(data.guestPhotographs.length, 18);
  assert.equal(new Set(data.guestPhotographs.map(photo => photo.id)).size, 18);
  for (const photo of data.guestPhotographs) {
    assert.ok(existsSync(new URL(`../public${photo.image}`, import.meta.url)));
    for (const width of [...imageSizes.imageSizes, ...imageSizes.deviceSizes]) {
      assert.ok(existsSync(new URL(`../public${photo.image.replace(/\.webp$/, `-${width}w.webp`)}`, import.meta.url)),
        `Missing responsive photograph: ${photo.id} at ${width}px`);
    }
    assert.ok(photo.alt.length > 20 && photo.title && photo.note.length > 40);
    assert.equal(photo.testimonial, undefined, "No unapproved guest quotes may be published");
  }
});
test("every index opens its own photograph, and invalid selections are rejected", () => {
  for (let index = 0; index < data.guestPhotographs.length; index++) {
    const state = data.guestBoardReducer(data.initialBoardState, { type: "open", index });
    assert.equal(state.index, index); assert.equal(state.side, "photo");
  }
  for (const index of [-1, data.guestPhotographs.length, NaN, 0.5]) {
    assert.equal(data.guestBoardReducer(data.initialBoardState, { type: "open", index }), data.initialBoardState);
  }
});
test("flip and next/previous preserve collection identity and reset the photo face", () => {
  let state = data.guestBoardReducer(data.initialBoardState, { type: "open", index: 0 });
  state = data.guestBoardReducer(state, { type: "flip" }); assert.equal(state.side, "note");
  state = data.guestBoardReducer(state, { type: "next", direction: -1 });
  assert.equal(state.index, data.guestPhotographs.length - 1); assert.equal(state.side, "photo");
  state = data.guestBoardReducer(state, { type: "next", direction: 1 }); assert.equal(state.index, 0);
});
test("returning a print preserves fullscreen; exiting fullscreen clears the viewer", () => {
  let state = data.guestBoardReducer(data.initialBoardState, { type: "board" });
  state = data.guestBoardReducer(state, { type: "open", index: 7 });
  state = data.guestBoardReducer(state, { type: "close-photo" });
  assert.equal(state.fullscreen, true); assert.equal(state.index, null);
  const exited = data.guestBoardReducer(state, { type: "exit" });
  assert.equal(exited.index, null); assert.equal(exited.fullscreen, false);
  assert.equal(exited.selectedIndex, 7, "Closing fullscreen remembers the selected keepsake");
});
test("portrait navigation wraps, resets its note, and never opens a dialog", () => {
  let state = data.initialBoardState;
  assert.equal(data.guestPhotographs[state.selectedIndex].id, "guest-trio");
  assert.equal(state.selectedIndex + 1, 12);
  state = data.guestBoardReducer(state, { type: "flip" });
  assert.equal(state.side, "note");
  state = data.guestBoardReducer(state, { type: "portrait-next", direction: 6 });
  assert.equal(state.selectedIndex, 17); assert.equal(state.side, "photo");
  state = data.guestBoardReducer(state, { type: "portrait-next", direction: 1 });
  assert.equal(state.selectedIndex, 0);
  state = data.guestBoardReducer(state, { type: "portrait-next", direction: -1 });
  assert.equal(state.selectedIndex, 17); assert.equal(state.index, null); assert.equal(state.fullscreen, false);
});
test("opening and navigating the full board updates the portrait keepsake, and closing retains its note", () => {
  let state = data.guestBoardReducer(data.initialBoardState, { type: "flip" });
  state = data.guestBoardReducer(state, { type: "board" });
  assert.equal(state.selectedIndex, 11); assert.equal(state.side, "note");
  state = data.guestBoardReducer(state, { type: "open", index: 0 });
  state = data.guestBoardReducer(state, { type: "next", direction: -1 });
  assert.equal(state.selectedIndex, 17);
  state = data.guestBoardReducer(state, { type: "flip" });
  state = data.guestBoardReducer(state, { type: "close-photo" });
  state = data.guestBoardReducer(state, { type: "exit" });
  assert.equal(state.selectedIndex, 17); assert.equal(state.side, "note"); assert.equal(state.index, null);
});
test("only horizontal deliberate swipes navigate; vertical and small gestures scroll normally", () => {
  assert.equal(data.photographSwipe(-80, 10), 1);
  assert.equal(data.photographSwipe(40, 0), -1);
  assert.equal(data.photographSwipe(39, 0), 0);
  assert.equal(data.photographSwipe(-50, 90), 0);
  assert.equal(data.photographSwipe(60, 50), 0);
});
test("lift travels from the clicked print's geometry without invalid transforms", () => {
  assert.equal(motion.liftTransform({ left: 0, top: 0, width: 100, height: 200 },
    { left: 400, top: 100, width: 400, height: 500 }, -4), "translate(-550px, -250px) scale(0.25, 0.4) rotate(-4deg)");
  assert.ok(!motion.liftTransform({ left: 0, top: 0, width: 0, height: 0 },
    { left: 0, top: 0, width: 0, height: 0 }).includes("NaN"));
});
test("reduced motion skips the lift and regular motion is finite and cancelable", () => {
  let calls = 0;
  const handle = { cancel() {} };
  const element = { getBoundingClientRect: () => ({ left: 0, top: 0, width: 400, height: 500 }),
    animate(frames, options) { calls++; assert.equal(frames.length, 2); assert.ok(options.duration <= 620); return handle; } };
  assert.equal(motion.animateLift(element, null, 0, true), null); assert.equal(calls, 0);
  assert.equal(motion.animateLift(element, null, 0, false), handle); assert.equal(calls, 1);
});
test("published strips retain their full near-2x6 aspect ratio in every responsive asset", async () => {
  for (const name of ["classic", "sepia", "monochrome", "naturale"]) {
    for (const suffix of ["", ...[...imageSizes.imageSizes, ...imageSizes.deviceSizes].map(width => `-${width}w`)]) {
      const bytes = readFileSync(new URL(`../public/images/guest-board/strip-${name}${suffix}.webp`, import.meta.url));
      const metadata = await sharp(bytes).metadata();
      assert.ok(metadata.width / metadata.height > 0.31 && metadata.width / metadata.height < 0.325);
    }
  }
});
test("server markup exposes every posted image as a named native button", () => {
  const { default: GuestAlbum } = compile("GuestAlbum.tsx", { require(name) {
    if (name === "react") return React;
    if (name === "react/jsx-runtime") return JSXRuntime;
    if (name === "./guestBoardData") return data;
    if (name === "./guestBoardMotion") return motion;
    if (name === "../../site.config") return { withSiteBasePath: value => `/FOTOHAVN${value}` };
    if (name.endsWith(".module.css")) return { default: new Proxy({}, { get: (_object, key) => key }) };
    if (name === "next/image") return { default: () => null };
    if (name === "./GuestBoardPhoto") return { default: ({ photo }) => React.createElement("img", { src: photo.image, alt: photo.alt }) };
    if (name.startsWith("@phosphor-icons/")) return new Proxy({}, { get: () => () => null });
    throw new Error(`Unexpected dependency ${name}`);
  } });
  const html = renderToStaticMarkup(React.createElement(GuestAlbum));
  assert.equal((html.match(/data-photo-id=/g) || []).length, 18);
  assert.equal((html.match(/aria-label="Open photograph:/g) || []).length, 18);
  assert.ok(html.includes("View board fullscreen"));
  assert.ok(!html.includes("SAMPLE TESTIMONIAL"));
});
