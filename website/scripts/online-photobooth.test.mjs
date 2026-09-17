import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";
import ts from "typescript";

function compile(name, dependencies = {}, globals = {}) {
  const source = readFileSync(new URL(`../src/components/onlinePhotobooth/${name}.ts`, import.meta.url), "utf8");
  const result = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 } });
  const exports = {};
  dependencies = { "../../../site.config": { withSiteBasePath: (path) => path }, ...dependencies };
  vm.runInNewContext(result.outputText, { exports, require: (id) => {
    assert.ok(id in dependencies, `Unmapped import ${id}`); return dependencies[id];
  }, Blob, DOMException, AbortController, Uint8ClampedArray, setTimeout, clearTimeout, ...globals });
  return exports;
}
const presets = compile("presets");
const session = compile("session", { "./presets": presets });
const { sessionReducer: reduce, initialSession, canContinue } = session;
const serial = (value) => JSON.parse(JSON.stringify(value));
const photo = (id) => ({ id, url: `blob:${id}`, blob: new Blob([id]), width: 1640, height: 1230, kind: "device" });
function loaded(ids = ["A", "B", "C", "D"]) {
  let state = reduce(initialSession, { type: "stage", stage: "capture" });
  state = reduce(state, { type: "begin", mode: "import", id: 1 });
  ids.forEach((id, index) => { state = reduce(state, { type: "photo", operationId: 1, photo: photo(id), index }); });
  return reduce(state, { type: "finish", id: 1 });
}
function move(state, from, to) {
  return reduce(reduce(state, { type: "move-start", source: { kind: "slot", index: from } }), { type: "move-to", index: to });
}

test("layout changes preserve all source identities, order, and retained photographs", () => {
  const first = loaded();
  let state = reduce(first, { type: "layout", id: "pair-v1" });
  assert.deepEqual(serial(state.slots), ["A", "B"]); assert.deepEqual(serial(state.tray), ["C", "D"]);
  state = move(state, 0, 1);
  state = reduce(state, { type: "layout", id: "long-strip-v1" });
  assert.deepEqual(serial(state.slots), ["B", "A", "C", "D"]); assert.equal(state.tray.length, 0);
  for (const id of ["A", "B", "C", "D"]) assert.equal(state.photos[id], first.photos[id]);
  assert.deepEqual(serial(first.slots), ["A", "B", "C", "D"], "prior snapshots are immutable");
});

test("a larger layout cannot continue until its newly empty positions are filled", () => {
  let state = reduce(loaded(["A", "B"]), { type: "layout", id: "pair-v1" });
  assert.equal(canContinue(state), true);
  state = reduce(state, { type: "layout", id: "long-strip-v1" });
  assert.deepEqual(serial(state.slots), ["A", "B", null, null]); assert.equal(canContinue(state), false);
  assert.equal(reduce(state, { type: "stage", stage: "look" }), state);
  assert.equal(reduce(state, { type: "stage", stage: "download" }), state);
});

test("a retake is transactional: cancel, retry, and accept affect only its selected position", () => {
  const first = loaded();
  const retake = reduce(first, { type: "begin", id: 2, mode: "retake", target: 1 });
  assert.equal(retake.selected, 1, "replacement target is selected even when initiated by file drop");
  const candidate = reduce(retake, { type: "photo", operationId: 2, photo: photo("E"), index: 1 });
  assert.deepEqual(serial(candidate.slots), ["A", "B", "C", "D"]);
  assert.equal(canContinue(candidate), false);
  const cancelled = reduce(candidate, { type: "cancel" });
  assert.deepEqual(serial(cancelled.slots), ["A", "B", "C", "D"]); assert.equal(cancelled.photos.E, undefined);
  assert.equal(cancelled.photos.B, first.photos.B);
  const retry = reduce(candidate, { type: "try-again" });
  assert.equal(retry.operation.candidate, null); assert.equal(retry.photos.E, undefined); assert.equal(retry.photos.B, first.photos.B);
  const accepted = reduce(candidate, { type: "accept" });
  assert.deepEqual(serial(accepted.slots), ["A", "E", "C", "D"]); assert.equal(accepted.photos.B, undefined); assert.equal(accepted.selected, 1);
  for (const id of ["A", "C", "D"]) assert.equal(accepted.photos[id], first.photos[id]);
});

test("cancelled and stale operations cannot commit a late photograph or finish a newer operation", () => {
  let state = reduce(initialSession, { type: "stage", stage: "capture" });
  state = reduce(state, { type: "begin", id: 8, mode: "sequence" });
  state = reduce(state, { type: "photo", operationId: 8, photo: photo("A"), index: 0 });
  state = reduce(state, { type: "cancel" });
  const cancelled = state;
  assert.equal(reduce(state, { type: "photo", operationId: 8, photo: photo("B"), index: 1 }), state);
  state = reduce(state, { type: "begin", id: 9, mode: "sequence" });
  assert.equal(reduce(state, { type: "photo", operationId: 8, photo: photo("C"), index: 2 }), state);
  assert.equal(reduce(state, { type: "finish", id: 8 }), state);
  assert.deepEqual(serial(cancelled.slots), ["A", null, null, null]);
  const departed = reduce(state, { type: "stage", stage: "layout" });
  assert.equal(reduce(departed, { type: "photo", operationId: 9, photo: photo("D"), index: 3 }), departed);
});

test("retake targets cannot move under capture, and duplicate/wrong-slot candidates are ignored", () => {
  const first = loaded();
  let state = reduce(first, { type: "begin", id: 3, mode: "retake", target: 1 });
  assert.equal(reduce(state, { type: "layout", id: "pair-v1" }), state);
  assert.equal(reduce(state, { type: "frame", id: "walnut-v1" }), state);
  assert.equal(reduce(state, { type: "select", index: 3 }), state);
  assert.equal(move(state, 0, 3), state);
  assert.equal(reduce(state, { type: "photo", operationId: 3, photo: photo("E"), index: 2 }), state);
  state = reduce(state, { type: "photo", operationId: 3, photo: photo("E"), index: 1 });
  assert.equal(reduce(state, { type: "photo", operationId: 3, photo: photo("F"), index: 1 }), state);
  assert.equal(reduce(state, { type: "photo", operationId: 3, photo: photo("E"), index: 1 }), state);
});

test("all arrangement methods share identity-preserving swap and retained-tray semantics", () => {
  const first = loaded();
  assert.deepEqual(serial(move(first, 2, 0).slots), ["C", "B", "A", "D"]);
  const selected = reduce(first, { type: "move-start", source: { kind: "slot", index: 2 } });
  assert.equal(canContinue(selected), false);
  assert.deepEqual(serial(reduce(selected, { type: "move-cancel" }).slots), ["A", "B", "C", "D"]);
  let state = reduce(first, { type: "layout", id: "pair-v1" });
  state = reduce(reduce(state, { type: "move-start", source: { kind: "tray", id: "C" } }), { type: "move-to", index: 1 });
  assert.deepEqual(serial(state.slots), ["A", "C"]); assert.deepEqual(serial(state.tray), ["B", "D"]);
  const withEmpty = { ...state, slots: ["A", null], tray: ["C", "B", "D"] };
  state = reduce(reduce(withEmpty, { type: "move-start", source: { kind: "tray", id: "C" } }), { type: "move-to", index: 1 });
  assert.deepEqual(serial(state.slots), ["A", "C"]); assert.deepEqual(serial(state.tray), ["B", "D"]);
  assert.equal(new Set([...state.slots, ...state.tray]).size, Object.keys(state.photos).length);
});

test("looks and frames preserve original pixels and returning to Original uses the same sources", () => {
  const first = move(loaded(), 2, 0);
  let state = first;
  for (const look of presets.looks) state = reduce(state, { type: "look", id: look.id });
  for (const frame of presets.frames) state = reduce(state, { type: "frame", id: frame.id });
  state = reduce(state, { type: "look", id: "naturale-v1" });
  assert.equal(state.photos, first.photos); assert.equal(state.slots, first.slots);
  assert.ok(state.revision > first.revision);
});

test("layout geometry, frame margins and the shallow Contact Sheet footer stay within the PNG", () => {
  for (const layout of presets.layouts) {
    assert.ok(layout.width > 0 && layout.height > 0);
    for (const rect of layout.slots) {
      assert.equal(rect.width / rect.height, 4 / 3);
      assert.ok(rect.x >= 13 && rect.y >= 13);
      assert.ok(rect.x + rect.width + 13 <= layout.width);
      assert.ok(rect.y + rect.height + 13 <= layout.height);
    }
    const footer = presets.footerRegion(layout);
    assert.ok(footer.height >= 95); assert.ok(footer.y + footer.height <= layout.height - 45);
  }
  assert.equal(presets.footerRegion(presets.getLayout("contact-sheet-v1")).height, 95);
  assert.deepEqual(serial(presets.coverCrop(1200, 1600)), { x: 0, y: 350, width: 1200, height: 900 });
  assert.deepEqual(serial(presets.coverCrop(1600, 900)), { x: 200, y: 0, width: 1200, height: 900 });
  assert.throws(() => presets.coverCrop(0, 0));
});

test("photographic kernels are deterministic, clamped, alpha-preserving, and have an exact Original baseline", () => {
  const original = new Uint8ClampedArray([0, 0, 0, 255, 127, 127, 127, 201, 255, 255, 255, 255, 210, 110, 65, 255]);
  for (const look of presets.looks) {
    const first = original.slice(), second = original.slice();
    presets.applyLook(first, look.id); presets.applyLook(second, look.id);
    assert.deepEqual(first, second);
    for (let i = 3; i < first.length; i += 4) assert.equal(first[i], original[i]);
    if (look.id === "naturale-v1") assert.deepEqual(first, original);
    if (look.id === "monochrome-v1") for (let i = 0; i < first.length; i += 4) { assert.equal(first[i], first[i + 1]); assert.equal(first[i + 1], first[i + 2]); }
    if (look.id === "classic-v1") assert.ok(first[4] > first[6], "warm neutral has a modest red-over-blue response");
  }
});

test("every authored frame/layout renders the correct photo order and exports only in-bounds artwork", async () => {
  let lastCanvas;
  const canvases = [];
  const media = { checkAbort: (signal) => { if (signal?.aborted) throw new DOMException("Cancelled", "AbortError"); }, decodeImage: async (url) => ({ url, naturalWidth: 1640, naturalHeight: 1230 }), canvasBlob: async (canvas) => canvas };
  const document = { createElement: () => {
    const calls = [];
    const ctx = { fillStyle: "", font: "", textAlign: "center", textBaseline: "middle",
      fillRect(...args) { calls.push({ type: "fill", args, fill: this.fillStyle }); },
      strokeRect(...args) { calls.push({ type: "rect", args }); },
      drawImage(image, ...args) { calls.push({ type: "photo", image: image.url, args }); },
      beginPath() {}, moveTo() {}, lineTo() {}, stroke() {},
      fillText(text, x, y, maxWidth) { calls.push({ type: "text", text, x, y, maxWidth, font: this.font }); },
    };
    lastCanvas = { width: 0, height: 0, getContext: () => ctx, calls }; canvases.push(lastCanvas); return lastCanvas;
  } };
  const compositor = compile("compositor", { "./presets": presets, "./media": media, "../../../site.config": { withSiteBasePath: (path) => path } }, { document });
  for (const layout of presets.layouts) for (const frame of presets.frames) {
    const photos = layout.slots.map((_, i) => photo(String(i)));
    const output = await compositor.renderComposition({ layoutId: layout.id, frameId: frame.id, lookId: "naturale-v1", photos }, { display: "Cormorant Garamond", sans: "Manrope" }, new AbortController().signal);
    assert.equal(output.width, layout.width); assert.equal(output.height, layout.height);
    assert.deepEqual(output.calls[0], { type: "fill", args: [0, 0, layout.width, layout.height], fill: frame.paper });
    assert.deepEqual(serial(output.calls.filter((call) => call.type === "photo").map((call) => call.image)), serial(photos.map((p) => p.url)));
    for (const call of output.calls.filter((call) => call.type === "text")) {
      const footer = presets.footerRegion(layout), size = Number(call.font.match(/([\d.]+)px/)[1]);
      assert.ok(call.y - size / 2 >= footer.y); assert.ok(call.y + size / 2 <= footer.y + footer.height);
      assert.ok(call.x >= 0 && call.x <= layout.width); assert.ok(call.maxWidth <= footer.width);
    }
    assert.ok(output.calls.some((call) => call.text === "FOTOHAVN"));
    assert.equal(output.calls.some((call) => /photograph|selected/i.test(call.text ?? "")), false);
  }
  assert.equal(canvases.length, 16);
  const abort = new AbortController(); abort.abort();
  await assert.rejects(compositor.renderComposition({ layoutId: "pair-v1", frameId: "ivory-v1", lookId: "naturale-v1", photos: [] }, {}, abort.signal), { name: "AbortError" });
});

test("countdown cancellation clears its timer and does not resume after a background interruption", async () => {
  let callback, cleared = false;
  const media = compile("media", { "./presets": presets }, {
    setTimeout: (fn) => { callback = fn; return 1; }, clearTimeout: () => { cleared = true; },
  });
  const abort = new AbortController();
  const countdown = media.waitFor(1000, abort.signal);
  abort.abort(); await assert.rejects(countdown, { name: "AbortError" }); assert.equal(cleared, true);
  callback();
  await assert.rejects(media.waitFor(1000, abort.signal), { name: "AbortError" });
});

test("the compositor confines each look to photo windows and leaves authored paper and lettering unchanged", async () => {
  let canvas;
  const calls = [];
  const context = {
    fillStyle: "", font: "", fillRect(...args) { calls.push({ type: "fill", args, color: this.fillStyle }); },
    drawImage() {}, beginPath() {}, moveTo() {}, lineTo() {}, stroke() {}, strokeRect() {},
    getImageData(x, y, width, height) { calls.push({ type: "read", x, y, width, height }); return { width, height, data: new Uint8ClampedArray([150, 100, 50, 255]) }; },
    putImageData(pixels, x, y) { calls.push({ type: "write", x, y, pixels: Array.from(pixels.data) }); },
    fillText(text) { calls.push({ type: "text", text, color: this.fillStyle }); },
  };
  const compositor = compile("compositor", { "./presets": presets, "./media": {
    checkAbort() {}, decodeImage: async () => ({ naturalWidth: 1640, naturalHeight: 1230 }), canvasBlob: async (value) => value,
  } }, { document: { createElement: () => (canvas = { getContext: () => context }) } });
  const layout = presets.getLayout("pair-v1");
  await compositor.renderComposition({ layoutId: layout.id, frameId: "walnut-v1", lookId: "monochrome-v1", photos: [photo("A"), photo("B")] }, { display: "Cormorant", sans: "Manrope" }, new AbortController().signal);
  assert.equal(canvas.width, 900); assert.equal(canvas.height, 1500);
  assert.deepEqual(calls[0], { type: "fill", args: [0, 0, 900, 1500], color: "#2D211B" });
  assert.deepEqual(calls.filter((item) => item.type === "read").map(({ x, y, width, height }) => ({ x, y, width, height })), serial(layout.slots));
  for (const call of calls.filter((item) => item.type === "write")) assert.equal(new Set(call.pixels.slice(0, 3)).size, 1);
  assert.ok(calls.some((item) => item.type === "text" && item.color === "#FBF8F2"));
});

test("camera requests are explicit, video-only, and late permission grants stop every returned track", async () => {
  let resolveRequest, requestArguments, state, interrupted = 0, cell = 0;
  const cleanups = [];
  const react = {
    useState: (initial) => { const index = cell++; if (index === 0) state = initial; return [initial, (value) => { if (index === 0) state = value; }]; },
    useRef: (value) => ({ current: value }), useCallback: (fn) => fn,
    useEffect: (effect) => { cleanups.push(effect()); },
  };
  let stopped = 0;
  const track = { stop() { stopped += 1; }, getSettings: () => ({ deviceId: "front", facingMode: "user" }), onended: null };
  const stream = { getTracks: () => [track], getVideoTracks: () => [track] };
  const video = { srcObject: null, readyState: 2, videoWidth: 1280, videoHeight: 960, play: async () => {}, addEventListener() {}, removeEventListener() {} };
  const useCamera = compile("useCamera", { react }, {
    navigator: { mediaDevices: { enumerateDevices: async () => [], getUserMedia: (args) => { requestArguments = args; return new Promise((resolve) => { resolveRequest = resolve; }); } } },
    window: { isSecureContext: true },
  }).useCamera;
  const camera = useCamera({ current: video }, () => { interrupted += 1; });
  assert.equal(requestArguments, undefined, "mounting never requests camera permission");
  const request = camera.request();
  assert.equal(state.status, "requesting"); assert.equal(requestArguments.audio, false);
  camera.stop(); resolveRequest(stream); await request;
  assert.equal(stopped, 1); assert.equal(state.status, "idle"); assert.equal(video.srcObject, null);
  const next = camera.request(); resolveRequest(stream); await next;
  assert.equal(state.status, "ready"); assert.equal(video.srcObject, stream);
  track.onended(); assert.equal(interrupted, 1); assert.equal(state.status, "error"); assert.equal(video.srcObject, null);
  const leaving = camera.request(); cleanups.forEach((cleanup) => cleanup?.()); resolveRequest(stream); await leaving;
  assert.equal(stopped, 3, "unmount also rejects a delayed camera grant");
});

test("switching cameras releases the lens, verifies facing, and retries the last working source after failure", async () => {
  let state, cell = 0;
  const events = [], cleanups = [];
  const react = {
    useState: (initial) => { const index = cell++; return [initial, (value) => { if (index === 0) state = value; }]; },
    useRef: (value) => ({ current: value }), useCallback: (fn) => fn,
    useEffect: (effect) => { cleanups.push(effect()); },
  };
  let facing = "user", rejectNext = false, wrongLens = false;
  const mediaDevices = {
    enumerateDevices: async () => { throw new Error("enumeration unavailable"); },
    getUserMedia: async ({ audio, video }) => {
      assert.equal(audio, false);
      events.push(["open", serial(video)]);
      if (rejectNext) { rejectNext = false; throw new DOMException("Busy", "NotReadableError"); }
      facing = wrongLens ? "user" : video.facingMode?.exact ?? "user";
      const track = { onended: null, getSettings: () => ({ facingMode: facing, deviceId: facing }), stop: () => events.push(["stop"]) };
      return { getTracks: () => [track], getVideoTracks: () => [track] };
    },
  };
  const video = { srcObject: null, readyState: 2, videoWidth: 1280, videoHeight: 960, play: async () => {}, addEventListener() {}, removeEventListener() {} };
  const { useCamera } = compile("useCamera", { react }, { navigator: { mediaDevices }, window: { isSecureContext: true } });
  const camera = useCamera({ current: video }, () => {});
  await camera.request(); assert.equal(state.status, "ready");
  const rear = await camera.request({ facingMode: "environment" });
  assert.equal(rear.facingMode, "environment"); assert.equal(state.status, "ready");
  assert.equal(events[1][0], "stop"); assert.equal(events[2][1].facingMode.exact, "environment");
  rejectNext = true; await camera.request({ deviceId: "missing" });
  assert.equal(state.status, "error"); assert.equal(video.srcObject, null);
  const recovered = await camera.request(); assert.equal(recovered.facingMode, "environment");
  assert.equal(events.at(-1)[1].facingMode.exact, "environment", "retry remembers only successful selection");
  wrongLens = true; await camera.request({ facingMode: "environment" });
  assert.equal(state.status, "error"); assert.equal(video.srcObject, null, "a false rear-camera result must be released");
  cleanups.forEach((cleanup) => cleanup?.());
});

test("exact camera preview maps template crops back onto portrait and landscape sensor pixels", () => {
  const { cameraPreviewGeometry } = compile("cameraGeometry", { "./presets": presets });
  for (const [width, height] of [[720, 1280], [1920, 1080], [1280, 960]]) {
    for (const template of presets.templates) {
      const ratio = template.slots[0].width / template.slots[0].height;
      const geometry = cameraPreviewGeometry(width, height, ratio);
      const normalized = presets.coverCrop(width, height);
      const crop = presets.coverCrop(normalized.width, normalized.height, ratio);
      const previewCropWidth = width / (parseFloat(geometry.width) / 100);
      const previewCropHeight = height / (parseFloat(geometry.height) / 100);
      assert.ok(Math.abs(previewCropWidth - crop.width) < .00001);
      assert.ok(Math.abs(previewCropHeight - crop.height) < .00001);
      assert.ok(Math.abs(-parseFloat(geometry.left) / 100 * crop.width - normalized.x - crop.x) < .00001);
      assert.ok(Math.abs(-parseFloat(geometry.top) / 100 * crop.height - normalized.y - crop.y) < .00001);
    }
  }
  assert.equal(cameraPreviewGeometry(0, 1280, .75), null);
  assert.equal(cameraPreviewGeometry(720, 1280, NaN), null);
});

test("mirrored video capture uses the same center crop and commits one bounded local PNG", async () => {
  const calls = [];
  const context = { fillStyle: "", fillRect() {}, translate: (...args) => calls.push(["translate", ...args]), scale: (...args) => calls.push(["scale", ...args]), drawImage: (...args) => calls.push(["draw", ...args]) };
  const media = compile("media", { "./presets": presets }, {
    document: { createElement: () => ({ width: 0, height: 0, getContext: () => context, toBlob: (done) => done(new Blob(["PNG"], { type: "image/png" })) }) },
    crypto: { randomUUID: () => "capture-a" }, URL: { createObjectURL: () => "blob:local-capture" },
  });
  const video = { readyState: 2, videoWidth: 1920, videoHeight: 1080 };
  const result = await media.photoFromVideo(video, true, new AbortController().signal);
  assert.equal(result.width, 1440); assert.equal(result.height, 1080); assert.equal(result.kind, "camera"); assert.equal(result.blob.type, "image/png");
  assert.deepEqual(calls[0], ["translate", 1440, 0]); assert.deepEqual(calls[1], ["scale", -1, 1]);
  assert.deepEqual(calls[2].slice(2), [240, 0, 1440, 1080, 0, 0, 1440, 1080]);
  await assert.rejects(media.photoFromVideo({ readyState: 1, videoWidth: 0, videoHeight: 0 }, true, new AbortController().signal), /not ready/);
});


test("combined templates preserve captured originals through three and four photo selections", () => {
  const original = loaded();
  const three = reduce(original, { type: "template", id: "butter-gingham" });
  assert.deepEqual(serial(three.slots), ["A", "B", "C"]);
  assert.deepEqual(serial(three.tray), ["D"]);
  for (const template of presets.templates) {
    const changed = reduce(three, { type: "template", id: template.id });
    assert.equal(changed.slots.length, template.slots.length);
    assert.equal(changed.frameId, "ivory-v1");
    for (const id of ["A", "B", "C", "D"]) assert.equal(changed.photos[id], original.photos[id]);
    assert.equal(canContinue(changed), true);
  }
  const panorama = reduce(three, { type: "template", id: "panorama-four" });
  assert.deepEqual(serial(panorama.slots), ["A", "B", "C", "D"]);
  const capturing = reduce(original, { type: "begin", id: 55, mode: "retake", target: 0 });
  assert.equal(reduce(capturing, { type: "template", id: "butter-gingham" }), capturing);
});

test("published template windows fit their prints and preserve native orientations", () => {
  for (const template of presets.templates) for (const slot of template.slots) {
    assert.ok(slot.x >= 0 && slot.y >= 0);
    assert.ok(slot.x + slot.width <= template.width && slot.y + slot.height <= template.height);
  }
  assert.equal(presets.getTemplate("panorama-four").width / presets.getTemplate("panorama-four").height, 3);
  assert.equal(presets.getTemplate("butter-gingham").slots.length, 3);
});


test("all combined templates compose blank and captured windows with their native geometry", async () => {
  const document = { createElement: () => {
    const calls = [];
    const context = {
      fillRect(...args) { calls.push({ type: "fill", color: this.fillStyle, args }); },
      drawImage(image, ...args) { calls.push({ type: "image", url: image.url, args }); },
      fillText(text) { calls.push({ type: "text", text }); },
      beginPath() {}, moveTo() {}, lineTo() {}, stroke() {},
    };
    return { calls, getContext: () => context };
  } };
  const media = { checkAbort: () => {}, decodeImage: async (url) => ({ url, naturalWidth: 1600, naturalHeight: 1200 }), canvasBlob: async (canvas) => canvas };
  const compositor = compile("compositor", { "./presets": presets, "./media": media }, { document });
  for (const template of presets.templates) {
    const blank = await compositor.renderComposition({ layoutId: template.id, frameId: "ivory-v1", lookId: "naturale-v1", photos: [] }, { display: "Cormorant" }, new AbortController().signal);
    assert.deepEqual(serial(blank.calls.filter(c => c.type === "fill" && c.color === presets.blankPhotoColor).map(c => c.args)), serial(template.slots.map(r => [r.x, r.y, r.width, r.height])));
    const photos = template.slots.map((_, i) => photo(String(i)));
    const rendered = await compositor.renderComposition({ layoutId: template.id, frameId: "ivory-v1", lookId: "naturale-v1", photos }, { display: "Cormorant" }, new AbortController().signal);
    const placed = rendered.calls.filter(c => c.type === "image" && c.url.startsWith("blob:"));
    assert.deepEqual(serial(placed.map(c => c.url)), serial(photos.map(p => p.url)));
    assert.deepEqual(serial(placed.map(c => c.args.slice(-4))), serial(template.slots.map(r => [r.x, r.y, r.width, r.height])));
    if (template.artwork) assert.ok(rendered.calls.some(c => c.url === template.artwork));
    else assert.ok(rendered.calls.some(c => c.text === "FOTOHAVN"));
  }
});



test("Sepia removes source hue and adds a subtle brown tone while Naturale preserves every byte", () => {
  const pixels = new Uint8ClampedArray([90, 150, 220, 67, 128, 128, 128, 255]);
  const unchanged = new Uint8ClampedArray(pixels);
  presets.applyLook(unchanged, "naturale-v1");
  assert.deepEqual(unchanged, pixels);
  presets.applyLook(pixels, "sepia-v1");
  for (let i = 0; i < pixels.length; i += 4) {
    assert.ok(pixels[i] > pixels[i + 1] && pixels[i + 1] > pixels[i + 2]);
    assert.ok(pixels[i] - pixels[i + 2] <= 19, "sepia tint remains subtle");
  }
  assert.equal(pixels[3], 67);
  assert.equal(pixels[7], 255);
});


test("all seven output references have distinct treatments with softer and warmer monochrome variants", () => {
  assert.deepEqual(serial(presets.looks.map(f => f.sourceOutput)), [1, 2, 3, 4, 5, 6, 7]);
  const source = new Uint8ClampedArray([30, 30, 30, 255, 128, 128, 128, 255, 220, 220, 220, 255, 180, 90, 40, 255]);
  const outputs = Object.fromEntries(presets.looks.map(f => {
    const pixels = new Uint8ClampedArray(source); presets.applyLook(pixels, f.id); return [f.id, pixels];
  }));
  assert.equal(new Set(Object.values(outputs).map(p => [...p].join(','))).size, 7);
  const strong = outputs['monochrome-v1'], soft = outputs['soft-monochrome-v1'];
  assert.ok(soft[0] > strong[0] && soft[8] < strong[8]);
  for (let i = 0; i < soft.length; i += 4) assert.ok(soft[i] === soft[i + 1] && soft[i + 1] === soft[i + 2]);
  const warm = outputs['warm-monochrome-v1'], sepia = outputs['sepia-v1'];
  assert.ok(warm[4] > warm[6]);
  assert.ok(warm[4] - warm[6] < sepia[4] - sepia[6]);
});

test("preview rotation leaves native template geometry intact and maps asymmetric frames", () => {
  const original = presets.getLayout("panorama-four");
  const display = presets.previewLayout(original);
  assert.equal(display.width, 600); assert.equal(display.height, 1800);
  assert.equal(original.width, 1800); assert.equal(original.height, 600);
  assert.equal(original.slots[0].x, 0);
  assert.equal(display.slots[0].y, 1350);
  const asymmetric = presets.previewLayout({ width: 1000, height: 600, slots: [{ x: 100, y: 50, width: 200, height: 300 }] });
  assert.deepEqual(serial(asymmetric.slots[0]), { x: 50, y: 700, width: 300, height: 200 });
  const vertical = presets.getLayout("signature-strip");
  assert.equal(presets.previewLayout(vertical), vertical);
  assert.equal(presets.previewArtworkStyle(original).transform, "rotate(-90deg) translateX(-100%)");
});
